using Org.BouncyCastle.Crypto;
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
        /// <param name="cryptoKey">EncryptionResult.PublicKey? "dh=BNSSQjo..."</param>
        /// <param name="contentEncoding">aesgcm</param>
        /// <param name="encryption">EncryptionResult.Salt? "salt=WASwg7..."</param>
        /// <param name="keyPair">Client-generated</param>
        /// <param name="authKey">Client-generated</param>
        /// <returns></returns>
        public static string Decrypt(string encryptedPayload, string cryptoKey, string contentEncoding, string encryption, AsymmetricCipherKeyPair keyPair, string authKey)
        {
            return Decrypt(Encoding.UTF8.GetBytes(encryptedPayload), cryptoKey, contentEncoding, encryption, keyPair, authKey);
        }
        private static string Decrypt(byte[] encryptedBytes, string cryptoKey, string contentEncoding, string encryption, AsymmetricCipherKeyPair keyPair, string authKey)
        {

        }
    }
}
