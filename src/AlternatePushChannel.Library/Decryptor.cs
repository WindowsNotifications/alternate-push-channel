using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            // Trim the null terminator
            if (encryptedPayload.EndsWith("\0"))
            {
                encryptedPayload = encryptedPayload.Substring(0, encryptedPayload.Length - 1);
            }

            byte[] encryptedBytes;

            // WNS either gives us Base64 or UTF16, try both
            try
            {
                encryptedBytes = Convert.FromBase64String(encryptedPayload);
            }
            catch
            {
                encryptedBytes = Encoding.Unicode.GetBytes(encryptedPayload);
            }


            string serverPublicKey = cryptoKey.Substring("dh=".Length);

            // Note that using UrlBase64.Decode fails later on, but our WebEncoder seems to decode it so that it works later on
            byte[] serverPublicKeyBytes = WebEncoder.Base64UrlDecode(serverPublicKey);

            string saltStr = encryption.Substring("salt=".Length);

            return Decrypt(encryptedBytes, serverPublicKeyBytes, contentEncoding, WebEncoder.Base64UrlDecode(saltStr), p256dh, WebEncoder.Base64UrlDecode(authKey));
        }
        private static string Decrypt(byte[] encryptedBytes, byte[] serverPublicKeyBytes, string contentEncoding, byte[] salt, AsymmetricCipherKeyPair p256dh, byte[] auth)
        {
            var userKeyPair = p256dh;

            // This code is basically the reverse of https://github.com/web-push-libs/web-push-csharp/blob/master/WebPush/Util/Encryptor.cs
            var ecdhAgreement = AgreementUtilities.GetBasicAgreement("ECDH");
            ecdhAgreement.Init(userKeyPair.Private); // We use our private key

            var serverPublicKey = ECKeyHelper.GetPublicKey(serverPublicKeyBytes);

            var key = ecdhAgreement.CalculateAgreement(serverPublicKey).ToByteArrayUnsigned();

            byte[] userPublicKey = ((ECPublicKeyParameters)userKeyPair.Public).Q.GetEncoded(false);

            var prk = HKDF(auth, key, Encoding.UTF8.GetBytes("Content-Encoding: auth\0"), 32);

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
            // Apparently it's always just padded by two leading empty bytes
            return data.Skip(2).ToArray();
        }

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
    }
}
