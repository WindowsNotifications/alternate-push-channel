﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AlternatePushChannel.Library;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace WebPush.Util
{
    // @LogicSoftware
    // https://github.com/web-push-libs/web-push-csharp/tree/master/WebPush
    // Originally from https://github.com/LogicSoftware/WebPushEncryption/blob/master/src/Encryptor.cs
    internal static class Encryptor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="p256dhPublic">The P256DH from subscription</param>
        /// <param name="auth">The Auth from subscription</param>
        /// <param name="payload">Plain-text payload</param>
        /// <returns></returns>
        public static EncryptionResult Encrypt(string p256dhPublic, string auth, string payload)
        {
            var p256dhPublicBytes = WebEncoder.Base64UrlDecode(p256dhPublic);
            var authBytes = WebEncoder.Base64UrlDecode(auth);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            return Encrypt(p256dhPublicBytes, authBytes, payloadBytes);
        }

        public static EncryptionResult Encrypt(byte[] p256dhPublic, byte[] auth, byte[] payload)
        {
            var salt = GenerateSalt(16);
            var serverKeyPair = ECKeyHelper.GenerateKeys();

            var ecdhAgreement = AgreementUtilities.GetBasicAgreement("ECDH");
            ecdhAgreement.Init(serverKeyPair.Private);

            var userPublicKey = ECKeyHelper.GetPublicKey(p256dhPublic);

            var key = ecdhAgreement.CalculateAgreement(userPublicKey).ToByteArrayUnsigned();
            var serverPublicKey = ((ECPublicKeyParameters)serverKeyPair.Public).Q.GetEncoded(false);

            var prk = HKDF(auth, key, Encoding.UTF8.GetBytes("Content-Encoding: auth\0"), 32);
            var cek = HKDF(salt, prk, CreateInfoChunk("aesgcm", p256dhPublic, serverPublicKey), 16);
            var nonce = HKDF(salt, prk, CreateInfoChunk("nonce", p256dhPublic, serverPublicKey), 12);

            var input = AddPaddingToInput(payload);
            var encryptedMessage = EncryptAes(nonce, cek, input);

            return new EncryptionResult
            {
                Salt = salt,
                Payload = encryptedMessage,
                PublicKey = serverPublicKey
            };
        }

        private static byte[] GenerateSalt(int length)
        {
            var salt = new byte[length];
            var random = new Random();
            random.NextBytes(salt);
            return salt;
        }

        private static byte[] AddPaddingToInput(byte[] data)
        {
            var input = new byte[0 + 2 + data.Length];
            Buffer.BlockCopy(ConvertInt(0), 0, input, 0, 2);
            Buffer.BlockCopy(data, 0, input, 0 + 2, data.Length);
            return input;
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

        public static byte[] HKDFSecondStep(byte[] key, byte[] info, int length)
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

        public static byte[] HKDF(byte[] salt, byte[] prk, byte[] info, int length)
        {
            var hmac = new HmacSha256(salt);
            var key = hmac.ComputeHash(prk);

            return HKDFSecondStep(key, info, length);
        }

        public static byte[] ConvertInt(int number)
        {
            var output = BitConverter.GetBytes(Convert.ToUInt16(number));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(output);
            }

            return output;
        }

        public static byte[] CreateInfoChunk(string type, byte[] recipientPublicKey, byte[] senderPublicKey)
        {
            var output = new List<byte>();
            output.AddRange(Encoding.UTF8.GetBytes($"Content-Encoding: {type}\0P-256\0"));
            output.AddRange(ConvertInt(recipientPublicKey.Length));
            output.AddRange(recipientPublicKey);
            output.AddRange(ConvertInt(senderPublicKey.Length));
            output.AddRange(senderPublicKey);
            return output.ToArray();
        }
    }

    public class HmacSha256
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