using AlternatePushChannel.Library.Encryption;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.PushNotifications;
using Windows.Storage.Streams;

namespace AlternatePushChannel.Library
{
    public static class PushManager
    {
        /// <summary>
        /// Creates a push channel for the given channel ID. Note that if you change your application server key, you have to delete the previous push channel.
        /// </summary>
        /// <param name="applicationServerKey"></param>
        /// <param name="channelId"></param>
        public static IAsyncOperation<PushSubscription> Subscribe(string applicationServerKey, string channelId)
        {
            return SubscribeHelper(applicationServerKey, channelId).AsAsyncOperation();
        }

        private static ECDsaCng _dsa;
        private static AsymmetricCipherKeyPair _keyPair;
        private static string _authKey;
        //private static ECDiffieHellmanCng _encrypt;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="encryptedPayload">r/JwZaorThJfqkpZjV6umbDXQy9G</param>
        /// <param name="cryptoKey">dh=BNSSQjo...</param>
        /// <param name="contentEncoding">aesgcm</param>
        /// <param name="encryption">salt=WASwg7...</param>
        /// <returns></returns>
        public static string Decrypt(string encryptedPayload, string cryptoKey, string contentEncoding, string encryption)
        {
            // Trip starting dh=
            cryptoKey = cryptoKey.Substring("dh=".Length);

            // https://www.codeproject.com/Tips/1071190/Encryption-and-Decryption-of-Data-using-Elliptic-C

            // Maybe this for decoding HTTP? https://www.tpeczek.com/2017/03/supporting-encrypted-content-encoding.html

            // This? https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=netframework-4.8

            using (Aes aesAlg = Aes.Create(contentEncoding))
            {
            }

            // This? https://gist.github.com/jbtule/4336842#file-aesgcm-cs

            byte[] encryptedBytes = Encoding.Unicode.GetBytes(encryptedPayload);

            byte[] decryptedBytes = AESThenHMAC.SimpleDecrypt(encryptedBytes, UrlB64ToUint8Array(cryptoKey), UrlB64ToUint8Array(_authKey));
            string decrypted = Encoding.UTF8.GetString(decryptedBytes);
            return decrypted;

            //KeyParameter keyparam = ParameterUtilities.CreateKeyParameter("DES", )
            IBufferedCipher cipher = CipherUtilities.GetCipher("DES/ECB/ISO7816_4PADDING");
            cipher.Init(false, _keyPair.Private);

            string decryptedPayload = Encoding.UTF8.GetString(cipher.DoFinal(Encoding.UTF8.GetBytes(encryptedPayload)));
            return decryptedPayload;
        }

        private static async Task<PushSubscription> SubscribeHelper(string applicationServerKey, string channelId)
        {
            IBuffer appServerKeyBuffer = UrlB64ToUint8Array(applicationServerKey).AsBuffer();

            var channel = await PushNotificationChannelManager.GetDefault().CreateRawPushNotificationChannelWithAlternateKeyForApplicationAsync(appServerKeyBuffer, channelId);

            // https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.ecdsacng?view=netframework-4.8
            //_dsa = new ECDsaCng();
            //_dsa.HashAlgorithm = CngAlgorithm.Sha256;
            //var p256dh = Uint8ArrayToB64String(_dsa.Key.Export(CngKeyBlobFormat.EccPublicBlob)); // This needs some work

            string p256dh;

            var keyPair = GenerateKeyPair();
            _keyPair = keyPair;

            //p256dh = Uint8ArrayToB64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).GetEncoded());
            p256dh = Uint8ArrayToB64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).PublicKeyData.GetBytes());


            string auth;

            using (RNGCryptoServiceProvider randomProvider = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[16];
                randomProvider.GetBytes(bytes);
                auth = Uint8ArrayToB64String(bytes);
                _authKey = auth;
            }


            return new PushSubscription()
                {
                    Endpoint = channel.Uri,
                    Keys = new PushSubscriptionKeys()
                    {
                        Auth = auth,
                        //Auth = "6N_NTiV11SvELvTCa1wU0w", // Dummy value
                        P256DH = p256dh
                        //P256DH = "BBmeyTF6FttmODOTLXZsUlgd-TcNrNYRccGHq87PKbO0AZSRAIO75ck6AOK55xypFtbFyqN9LCmj4h-cT6cVc1s" // Dummy value
                    },
                    Channel = channel
                };
        }

        private static AsymmetricCipherKeyPair GenerateKeyPair()
        {
            // https://davidtavarez.github.io/2019/implementing-elliptic-curve-diffie-hellman-c-sharp/
            X9ECParameters x9EC = NistNamedCurves.GetByName("P-256");
            ECDomainParameters ecDomain = new ECDomainParameters(x9EC.Curve, x9EC.G, x9EC.N, x9EC.H, x9EC.GetSeed());
            ECKeyPairGenerator g = (ECKeyPairGenerator)GeneratorUtilities.GetKeyPairGenerator("ECDH");
            g.Init(new ECKeyGenerationParameters(ecDomain, new SecureRandom()));

            AsymmetricCipherKeyPair aliceKeyPair = g.GenerateKeyPair();
            return aliceKeyPair;
        }

        private static byte[] UrlB64ToUint8Array(string base64String)
        {
            var paddingLength = (4 - base64String.Length % 4) % 4;
            var padding = string.Join("", new int[paddingLength].Select(i => "="));
            var base64 = (base64String + padding);

            base64 = base64.Replace("-", "+");

            base64 = base64.Replace("_", "/");

            var rawData = Convert.FromBase64String(base64);
            return rawData;
            //const outputArray = new Uint8Array(rawData.length);

            //for (let i = 0; i < rawData.length; ++i)
            //{
            //    outputArray[i] = rawData.charCodeAt(i);
            //}
            //return outputArray;
        }

        private static string Uint8ArrayToB64String(byte[] uint8Array)
        {
            var base64 = Convert.ToBase64String(uint8Array);

            base64 = base64.Replace("/", "_");
            base64 = base64.Replace("+", "-");

            base64 = base64.TrimEnd('=');

            return base64;
        }
    }
}
