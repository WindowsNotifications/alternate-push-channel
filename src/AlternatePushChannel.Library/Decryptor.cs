﻿using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlternatePushChannel.Library
{
    internal static class Decryptor
    {
        private const int ContentEncryptionKeyLength = 16;
        private const int ApplicationServerKeyLength = 65;
        private const int NonceLength = 12;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="encryptedPayload"></param>
        /// <param name="cryptoKey">EncryptionResult.PublicKey (server public key) "dh=BNSSQjo..."</param>
        /// <param name="contentEncoding">aesgcm</param>
        /// <param name="encryption">EncryptionResult.Salt? "salt=WASwg7..."</param>
        /// <param name="p256dh">Client-generated</param>
        /// <param name="authKey">Client-generated</param>
        /// <returns></returns>
        public static string Decrypt(string encryptedPayload, string cryptoKey, string contentEncoding, string encryption, AsymmetricCipherKeyPair p256dh, string authKey)
        {
            encryptedPayload = encryptedPayload.Substring(0, encryptedPayload.Length - 1);
            string serverPublicKey = cryptoKey.Substring("dh=".Length);

            // Note that using UrlBase64.Decode fails later on, but our WebEncoder seems to decode it so that it works later on
            byte[] serverPublicKeyBytes = WebEncoder.Base64UrlDecode(serverPublicKey);

            string saltStr = encryption.Substring("salt=".Length);

            // UTF16 seems correct, In Edge's code they decrypt as either UTF16 or Base64, but Base64 throws on me https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushMessageContent.cpp&version=GBofficial%2Frs_edge_spartan
            return Decrypt(Convert.FromBase64String(encryptedPayload), serverPublicKeyBytes, contentEncoding, WebEncoder.Base64UrlDecode(saltStr), p256dh, WebEncoder.Base64UrlDecode(authKey));
        }
        private static string Decrypt(byte[] encryptedBytes, byte[] serverPublicKeyBytes, string contentEncoding, byte[] salt, AsymmetricCipherKeyPair p256dh, byte[] auth)
        {
            //while (encryptedBytes[encryptedBytes.Length - 1] == 0)
            //{
            //    encryptedBytes = encryptedBytes.Take(encryptedBytes.Length - 1).ToArray();
            //}
            // Edge's decrypt method: https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushCryptoProvider.cpp&version=GBofficial%2Frs_edge_spartan&line=125&lineStyle=plain&lineEnd=125&lineStartColumn=29&lineEndColumn=43
            // Same as the Encrypt method except user and server are swapped

            var userKeyPair = p256dh;

            // This IS correct (passing server public key here throws exception, needs to be private key)
            var ecdhAgreement = AgreementUtilities.GetBasicAgreement("ECDH");
            ecdhAgreement.Init(userKeyPair.Private); // We use our private key

            // This is correct
            var serverPublicKey = ECKeyHelper.GetPublicKey(serverPublicKeyBytes);

            // This seems correct (definitely should be server public key)
            var key = ecdhAgreement.CalculateAgreement(serverPublicKey).ToByteArrayUnsigned();

            // This seems correct (but maybe we could try our different way of getting the bytes?)
            byte[] userPublicKey = ((ECPublicKeyParameters)userKeyPair.Public).Q.GetEncoded(false);




            // First, Get the content encoding specific data used to generate the keys for the message.
            // Note right now we're always assuming aesgcm encoding
            // Code from AesGcmPushMessageContent.cpp: https://microsoft.visualstudio.com/OS/_git/os#path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FAesGcmPushMessageContent.cpp&version=GBofficial%2Frsmaster&_a=contents
            //byte[] pseudoRandomKeyInfo = GeneratePseudoRandomKeyInfo();

            //byte[] contentEncodingContext = GenerateContentEncodingContext(userPublicKey, serverPublicKeyBytes);

            //// Second, generate the keys used to encrypt the message.
            //byte[] contentEncryptionKey;
            //byte[] nonce;

            //DeriveEncryptionKeys(
            //    contentEncoding,
            //    contentEncodingContext,
            //    userPublicKey,
            //    serverPublicKeyBytes,
            //    userKeyPair.Private,
            //    auth,
            //    salt,
            //    pseudoRandomKeyInfo,
            //    out contentEncryptionKey,
            //    out nonce);






            // This seems correct
            var prk = HKDF(auth, key, Encoding.UTF8.GetBytes("Content-Encoding: auth\0"), 32);

            // Maybe the user and server should be flipped? But flipping them didn't change anything
            // Edge's DeriveEncryptionKeys: https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushCryptoProvider.cpp&version=GBofficial%2Frs_edge_spartan&line=186&lineStyle=plain&lineEnd=186&lineStartColumn=29&lineEndColumn=49

            // CreateMessageKeyGeneratorFromSharedSecret (is that the prk?)
            /*
             *     HRESULT hr = CreateMessageKeyGeneratorFromSharedSecret(
        messagePublicKey, // That's the server's public key (serverPublicKeyBytes)
        messagePrivateKey, // That's the client's private key (userKeyPair.Private)
        authSecret, // That's the client's generated auth bytes (auth)
        salt, // That's the salt from server (salt)
        pseudoRandomKeyInfo
             */

            // Generate the content encryption key (CEK) from the content encoding info.
            var cek = HKDF(salt, prk, CreateInfoChunk(contentEncoding, userPublicKey, serverPublicKeyBytes), 16);
            var nonce = HKDF(salt, prk, CreateInfoChunk("nonce", userPublicKey, serverPublicKeyBytes), 12);

            var decryptedMessage = DecryptAes(nonce, cek, encryptedBytes);

            // Remove the padding
            decryptedMessage = RemovePaddingFromPayload(decryptedMessage);

            return Encoding.UTF8.GetString(decryptedMessage);
        }

        private static byte[] RemovePaddingFromPayload(byte[] data)
        {
            return data.Skip(2).ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentEncoding">From push payload</param>
        /// <param name="contentEncodingContext"></param>
        /// <param name="subscriptionPublicKey"></param>
        /// <param name="messagePublicKey">From push payload, the server public key (messagePublicKey)</param>
        /// <param name="messagePrivateKey">Client private key (subscriptionPrivateKey)</param>
        /// <param name="authSecret"></param>
        /// <param name="salt"></param>
        /// <param name="pseudoRandomKeyInfo"></param>
        /// <param name="contentEncryptionKey"></param>
        /// <param name="nonce"></param>
        private static void DeriveEncryptionKeys(
            string contentEncoding,
            byte[] contentEncodingContext,
            byte[] subscriptionPublicKey,
            byte[] messagePublicKey,
            byte[] messagePrivateKey,
            byte[] authSecret,
            byte[] salt,
            byte[] pseudoRandomKeyInfo,
            out byte[] contentEncryptionKey,
            out byte[] nonce)
        {
            contentEncryptionKey = null;
            nonce = null;
        }

        private static void CreateMessageKeyGeneratorFromSharedSecret(
            byte[] publicKey,
            byte[] privateKey,
            byte[] authSecret,
            byte[] salt,
            byte[] pseudoRandomKeyInfo)
        {
            // Creates the HmacKeyDerivationFunction (HKDF) used to generate the pseudo-random keys (PRK)
            // for push message encryption.
            //
            // First, create a different HKDF to create a PRK from the secrets shared between the client and sender.
            // The PRK produced by the first HKDF is the input key material for the message HKDF created by this function.
            //
            // The first HKDF generates a PRK by:
            //
            //   1) Creating a 'shared secret' using the public key and private key to produce the input key material (IKM) for the HKDF.
            //   2) Using the 'auth secret' from the client's push subscription as the salt for the HKDF.
        }

        private static byte[] GeneratePseudoRandomKeyInfo()
        {
            return Encoding.UTF8.GetBytes("Content-Encoding: auth\0");
        }

        // The content encoding context produces a BYTE[] with the following format where:
        //
        //  - <client/sender public key length> is a big endian uint8_t with the length of the public key, which is: 0x0 0x41 (65).
        //  - <client/sender public key> is the big endian key data, which is 0x4 <32-byte X coordinate> <32-byte Y coordinate>.
        //
        // The format:
        //
        //      'P-2560\0'
        //      '<client public key length>'
        //      '<client public key data>'
        //      '<sender public key length>'
        //      '<sender public key data>'
        /// <summary>
        /// 
        /// </summary>
        /// <param name="subscriptionPublicKeyBytes">Client public key</param>
        /// <param name="serverPublicKeyBytes">Server public key (from push payload)</param>
        /// <returns></returns>
        private static byte[] GenerateContentEncodingContext(byte[] subscriptionPublicKeyBytes, byte[] serverPublicKeyBytes)
        {
            var output = new List<byte>();
            output.AddRange(Encoding.UTF8.GetBytes("P-256\0"));
            output.AddRange(ConvertInt(subscriptionPublicKeyBytes.Length));
            output.AddRange(subscriptionPublicKeyBytes);
            output.AddRange(ConvertInt(serverPublicKeyBytes.Length));
            output.AddRange(serverPublicKeyBytes);
            return output.ToArray();
        }

        private static void GenerateMessageKeyFromContentEncodingInfo()
        {

        }

        private static byte[] DecryptAes(byte[] nonce, byte[] cek, byte[] encryptedBytes)
        {
            // The 'cipherText' (encryptedBytes) uses following format: <encrypted message data><AES GCM tag>.
            //
            // Decryption must follow these steps:
            //
            // First, configure the decryption algorithm to use the tag from 'cipherText' as the expected 'AES GCM tag'.
            // The tag is the last 16 bytes of the 'cipherText'.
            //
            // Second, decrypt the encrypted message data without the tag from the 'cipherText'.

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(cek), 128, nonce);
            cipher.Init(false, parameters);

            var cipherText = new byte[cipher.GetOutputSize(encryptedBytes.Length)];
            var len = cipher.ProcessBytes(encryptedBytes, 0, encryptedBytes.Length, cipherText, 0);
            cipher.DoFinal(cipherText, len);

            return cipherText;
        }

        internal static byte[] CreateInfoChunk(string type, byte[] recipientPublicKey, byte[] senderPublicKey)
        {
            var output = new List<byte>();
            output.AddRange(Encoding.UTF8.GetBytes($"Content-Encoding: {type}\0P-256\0"));
            output.AddRange(ConvertInt(recipientPublicKey.Length));
            output.AddRange(recipientPublicKey);
            output.AddRange(ConvertInt(senderPublicKey.Length));
            output.AddRange(senderPublicKey);
            return output.ToArray();
        }

        private static byte[] ConvertInt(int number)
        {
            var output = BitConverter.GetBytes(Convert.ToUInt16(number));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(output);
            }

            return output;
        }

        private static byte[] HKDFSecondStep(byte[] key, byte[] info, int length)
        {
            var hmac = new HmacSha256(key);
            var infoAndOne = info.Concat(new byte[] { 0x01 }).ToArray();
            var result = hmac.ComputeHash(infoAndOne);

            if (result.Length > length)
            {
                Array.Resize(ref result, length);
            }

            return result;
        }

        /// <summary>
        /// Equivalent of GenerateMessageKeyFromContentEncodingInfo: https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushCryptoProvider.cpp&version=GBofficial%2Frs_edge_spartan&line=280&lineStyle=plain&lineEnd=280&lineStartColumn=29&lineEndColumn=70
        /// </summary>
        /// <param name="salt"></param>
        /// <param name="prk"></param>
        /// <param name="info"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static byte[] HKDF(byte[] salt, byte[] prk, byte[] info, int length)
        {
            var hmac = new HmacSha256(salt);
            var key = hmac.ComputeHash(prk);

            return HKDFSecondStep(key, info, length);
        }

        internal class HmacSha256
        {
            private readonly HMac _hmac;

            public HmacSha256(byte[] key)
            {
                _hmac = new HMac(new Sha256Digest());
                _hmac.Init(new KeyParameter(key));
            }

            public byte[] ComputeHash(byte[] value)
            {
                var resBuf = new byte[_hmac.GetMacSize()];
                _hmac.BlockUpdate(value, 0, value.Length);
                _hmac.DoFinal(resBuf, 0);

                return resBuf;
            }
        }


        private static byte[] EncryptAes(byte[] nonce, byte[] cek, byte[] message)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(cek), 128, nonce);
            cipher.Init(true, parameters);

            //Generate Cipher Text With Auth Tag
            var cipherText = new byte[cipher.GetOutputSize(message.Length)];
            var len = cipher.ProcessBytes(message, 0, message.Length, cipherText, 0);
            cipher.DoFinal(cipherText, len);

            //byte[] tag = cipher.GetMac();
            return cipherText;
        }
    }
}
