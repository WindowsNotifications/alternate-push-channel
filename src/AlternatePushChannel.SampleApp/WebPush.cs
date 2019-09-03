using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebPush;

namespace AlternatePushChannel.SampleApp
{
    public static class WebPush
    {
        // Keys generated from step #3 (don't store private in public source code)
        // Note that this is the same public key you include in your app in step #4
        public const string PublicKey = "BGg3UxXo3J_rH6VrJB2er_F8o7m2ZTGb2jiNm3tmlK4ORxsskX1HIVys5TA8lGkCYC-ur8GwrZMy-v0LZOwazvk";
        private const string PrivateKey = "_RwmE-l--jTgxtb8IQcL3cUiKRcjc5-a7SFdDgFL5nU";

        private static WebPushClient _webPushClient = new WebPushClient();

        public class Subscription
        {
            public string Endpoint { get; set; }
            public SubscriptionKeys Keys { get; set; }
        }

        public class SubscriptionKeys
        {
            public string P256DH { get; set; }
            public string Auth { get; set; }
        }

        public static async Task SendAsync(string subscriptionJson, string payload)
        {
            var subscription = JsonConvert.DeserializeObject<Subscription>(subscriptionJson);

            try
            {
                await _webPushClient.SendNotificationAsync(
                    subscription: new PushSubscription(
                        endpoint: subscription.Endpoint,
                        p256dh: subscription.Keys.P256DH,
                        auth: subscription.Keys.Auth),
                    payload: payload,
                    vapidDetails: new VapidDetails(
                        subject: "mailto:nothanks@microsoft.com",
                        publicKey: PublicKey,
                        privateKey: PrivateKey));
            }
            catch (Exception ex)
            {
                Debugger.Break();
                throw ex;
            }
            return;
        }
    }
}
