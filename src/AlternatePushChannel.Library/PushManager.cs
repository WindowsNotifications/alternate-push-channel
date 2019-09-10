using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Networking.PushNotifications;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace AlternatePushChannel.Library
{
    /// <summary>
    /// Web-like implementation of push notifications for UWP.
    /// </summary>
    public static class PushManager
    {
        private static bool? _isSupported;

        /// <summary>
        /// Gets a boolean that represents whether the PushManager is supported (only supported on 15063 and higher). If this returns false, avoid calling any other PushManager APIs. If your app's min version is set to 15063 or higher, there's no need to call this API.
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                if (_isSupported == null)
                {
                    _isSupported = ApiInformation.IsTypePresent("Windows.Networking.PushNotifications.PushNotificationChannelManagerForUser")
                        && ApiInformation.IsMethodPresent("Windows.Networking.PushNotifications.PushNotificationChannelManagerForUser", "CreateRawPushNotificationChannelWithAlternateKeyForApplicationAsync");
                }

                return _isSupported.Value;
            }
        }

        /// <summary>
        /// Creates a push channel for the given channel ID. Note that if you change your application server key, you have to delete the previous push channel.
        /// </summary>
        /// <param name="applicationServerKey"></param>
        /// <param name="channelId"></param>
        public static IAsyncOperation<PushSubscription> SubscribeAsync(string applicationServerKey, string channelId)
        {
            return SubscribeHelper(applicationServerKey, channelId).AsAsyncOperation();
        }

        public static IAsyncOperation<string> GetDecryptedContentAsync(RawNotification notification)
        {
            return GetDecryptedContentHelperAsync(notification).AsAsyncOperation();
        }

        private static async Task<string> GetDecryptedContentHelperAsync(RawNotification notification)
        {
            if (notification.Headers == null)
            {
                // It's not encrypted
                return notification.Content;
            }

            string encryptedPayload = notification.Content; // r/JwZaorThJfqkpZjV6umbDXQy9G
            string cryptoKey = notification.Headers["Crypto-Key"]; // dh=BNSSQjo...
            string contentEncoding = notification.Headers["Content-Encoding"]; // aesgcm
            string encryption = notification.Headers["Encryption"]; // salt=WASwg7...

            var storage = await PushSubscriptionStorage.GetAsync();

            StoredPushSubscription[] storedSubscriptions = storage.GetSubscriptions(notification.ChannelId);

            foreach (var sub in storedSubscriptions)
            {
                try
                {
                    byte[] userPrivateKey = WebEncoder.Base64UrlDecode(sub.P265Private);
                    byte[] userPublicKey = WebEncoder.Base64UrlDecode(sub.Keys.P256DH);

                    string decrypted = Decryptor.Decrypt(encryptedPayload, cryptoKey, contentEncoding, encryption, userPrivateKey, userPublicKey, sub.Keys.Auth);

                    // Make sure we've deleted any old channels now that we successfully received with this one
                    await storage.DeleteSubscriptionsOlderThanAsync(notification.ChannelId, sub);

                    return decrypted;
                }
                catch
                {

                }
            }

            throw new Exception("Failed to decrypt");
        }

        private static async Task<PushSubscription> SubscribeHelper(string applicationServerKey, string channelId)
        {
            IBuffer appServerKeyBuffer = UrlB64ToUint8Array(applicationServerKey).AsBuffer();

            // No matter what, we always get the channel (we don't do any caching and expiration logic, WNS caches the channel for 24 hours)
            var channel = await PushNotificationChannelManager.GetDefault().CreateRawPushNotificationChannelWithAlternateKeyForApplicationAsync(appServerKeyBuffer, channelId);

            var storage = await PushSubscriptionStorage.GetAsync();

            var existingSubscriptionInfo = storage.GetSubscriptions(channelId).FirstOrDefault(i => i.ChannelUri == channel.Uri);
            if (existingSubscriptionInfo != null)
            {
                // If the app server key has changed
                if (existingSubscriptionInfo.AppServerKey != applicationServerKey)
                {
                    // We need to destroy the WNS channel and create a new one

                    // Close the channel
                    channel.Close();

                    // Create a new one
                    channel = await PushNotificationChannelManager.GetDefault().CreateRawPushNotificationChannelWithAlternateKeyForApplicationAsync(appServerKeyBuffer, channelId);

                    // And set existing subscription info to null since there's no longer existing info
                    existingSubscriptionInfo = null;
                }

                else
                {
                    // Otherwise, return the existing info
                    return new PushSubscription()
                    {
                        Endpoint = channel.Uri,
                        Keys = new PushSubscriptionKeys()
                        {
                            Auth = existingSubscriptionInfo.Keys.Auth,
                            P256DH = existingSubscriptionInfo.Keys.P256DH
                        },
                        Channel = channel,
                        ExpirationTime = channel.ExpirationTime
                    };
                }
            }

            // Note that we have to store a series of these key pairs...
            // A developer with an existing channel might call Subscribe again, generating a new channel and key pair,
            // but they fail to upload the channel/key to their server. Therefore, their server still has the old channel/key,
            // and pushing to that channel still needs to work, so we need to hold onto the old keys.
            // We can't delete the old keys until either (1) the expiration time of 30 days for the channel occurs,
            // or (2) we've received and decrypted a push with the newer key pair, and can therefore throw away the old keys, since
            // we know at that point that the server successfully received the new channel/key.
            // Although in case (2), the app developer could still hold onto a previous channel and use it, so the only real truth is the
            // expiration time. Other than that, any channel that we return MUST keep working.
            // However, W3C spec states that once a message has been received for a newer subscription, the old ones MUST be deactivated,
            // so (2) is the correct pattern


            // Otherwise, we have to create new pairs for this new channel
            string p256dh;

            var keyPair = GenerateKeyPair();

            p256dh = Uint8ArrayToB64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).PublicKeyData.GetBytes());

            var authBytes = CryptographicBuffer.GenerateRandom(16).ToArray();
            string auth = Uint8ArrayToB64String(authBytes);

            await storage.SavePushSubscriptionAsync(channelId, new StoredPushSubscription()
            {
                ChannelUri = channel.Uri,
                Keys = new PushSubscriptionKeys()
                {
                    Auth = auth,
                    P256DH = p256dh
                },
                AppServerKey = applicationServerKey,
                P265Private = Uint8ArrayToB64String(PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private).ToAsn1Object().GetDerEncoded())
            });

            return new PushSubscription()
            {
                Endpoint = channel.Uri,
                Keys = new PushSubscriptionKeys()
                {
                    Auth = auth,
                    P256DH = p256dh
                },
                Channel = channel,
                ExpirationTime = channel.ExpirationTime
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
