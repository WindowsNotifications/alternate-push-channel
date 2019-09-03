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
    /// <summary>
    /// Web-like implementation of push notifications for UWP.
    /// </summary>
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

        public static string GetDecryptedContent(RawNotification notification)
        {
            return Decrypt(notification.Content, notification.Headers.GetValueOrDefault("Crypto-Key"), notification.Headers.GetValueOrDefault("Content-Encoding"), notification.Headers.GetValueOrDefault("Encryption"));
        }

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
            return Decryptor.Decrypt(encryptedPayload, cryptoKey, contentEncoding, encryption, _keyPair, _authKey);
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
                        P256DH = p256dh
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
