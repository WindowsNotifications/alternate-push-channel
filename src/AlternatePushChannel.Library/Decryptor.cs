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
            string serverPublicKey = cryptoKey.Substring("dh=".Length);

            // Note that using UrlBase64.Decode fails later on, but our WebEncoder seems to decode it so that it works later on
            byte[] serverPublicKeyBytes = WebEncoder.Base64UrlDecode(serverPublicKey);

            string saltStr = encryption.Substring("salt=".Length);

            // UTF16 seems correct, In Edge's code they decrypt as either UTF16 or Base64, but Base64 throws on me https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushMessageContent.cpp&version=GBofficial%2Frs_edge_spartan
            return Decrypt(Encoding.Unicode.GetBytes(encryptedPayload), serverPublicKeyBytes, contentEncoding, WebEncoder.Base64UrlDecode(saltStr), p256dh, WebEncoder.Base64UrlDecode(authKey));
        }
        private static string Decrypt(byte[] encryptedBytes, byte[] serverPublicKeyBytes, string contentEncoding, byte[] salt, AsymmetricCipherKeyPair p256dh, byte[] auth)
        {
            // Edge's decrypt method: https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushCryptoProvider.cpp&version=GBofficial%2Frs_edge_spartan&line=125&lineStyle=plain&lineEnd=125&lineStartColumn=29&lineEndColumn=43
            // Same as the Encrypt method except user and server are swapped

            var userKeyPair = p256dh;

            // This IS correct (passing server public key here throws exception, needs to be private key)
            var ecdhAgreement = AgreementUtilities.GetBasicAgreement("ECDH");
            ecdhAgreement.Init(userKeyPair.Private); // We use our private key

            // This is correct
            var serverPublicKey = ECKeyHelper.GetPublicKey(serverPublicKeyBytes);

            // This seems correct
            var key = ecdhAgreement.CalculateAgreement(serverPublicKey).ToByteArrayUnsigned();

            // This seems correct (but maybe we could try our different way of getting the bytes?)
            var userPublicKey = ((ECPublicKeyParameters)userKeyPair.Public).Q.GetEncoded(false);

            // This seems correct
            var prk = HKDF(auth, key, Encoding.UTF8.GetBytes("Content-Encoding: " + contentEncoding + "\0"), 32);

            // Generate the content encryption key (CEK) from the content encoding info.

            // Maybe the user and server should be flipped? But flipping them didn't change anything
            // Edge's DeriveEncryptionKeys: https://microsoft.visualstudio.com/OS/_git/os?path=%2Fonecoreuap%2Finetcore%2FEdgeManager%2FServiceWorkerManager%2FPushCryptoProvider.cpp&version=GBofficial%2Frs_edge_spartan&line=186&lineStyle=plain&lineEnd=186&lineStartColumn=29&lineEndColumn=49
            var cek = HKDF(salt, prk, CreateInfoChunk(contentEncoding, userPublicKey, serverPublicKeyBytes), 16);
            var nonce = HKDF(salt, prk, CreateInfoChunk("nonce", userPublicKey, serverPublicKeyBytes), 12);

            var decryptedMessage = DecryptAes(nonce, cek, encryptedBytes);

            // TODO: Remove padding?
            return Encoding.UTF8.GetString(decryptedMessage);
        }

        //private static void GenerateMessageKeyFromContentEncodingInfo()

        private static byte[] DecryptAes(byte[] nonce, byte[] cek, byte[] encryptedBytes)
        {
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
