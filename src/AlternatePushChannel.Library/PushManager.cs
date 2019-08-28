using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        private static async Task<PushSubscription> SubscribeHelper(string applicationServerKey, string channelId)
        {
            IBuffer appServerKeyBuffer = UrlB64ToUint8Array(applicationServerKey).AsBuffer();

            var channel = await PushNotificationChannelManager.GetDefault().CreateRawPushNotificationChannelWithAlternateKeyForApplicationAsync(appServerKeyBuffer, channelId);

            return new PushSubscription()
            {
                Endpoint = channel.Uri,
                Keys = new PushSubscriptionKeys()
                {
                    Auth = "6N_NTiV11SvELvTCa1wU0w", // Dummy value
                    P256DH = "BBmeyTF6FttmODOTLXZsUlgd-TcNrNYRccGHq87PKbO0AZSRAIO75ck6AOK55xypFtbFyqN9LCmj4h-cT6cVc1s" // Dummy value
                },
                Channel = channel
            };
        }

        private static byte[] UrlB64ToUint8Array(string base64String)
        {
            var paddingLength = (4 - base64String.Length % 4) % 4;
            var padding = string.Join("", new int[paddingLength].Select(i => "="));
            var base64 = (base64String + padding);

            base64 = Regex.Replace(base64, "\\-", "+");

            base64 = Regex.Replace(base64, "_", "/");

            var rawData = Convert.FromBase64String(base64);
            return rawData;
            //const outputArray = new Uint8Array(rawData.length);

            //for (let i = 0; i < rawData.length; ++i)
            //{
            //    outputArray[i] = rawData.charCodeAt(i);
            //}
            //return outputArray;
        }
    }
}
