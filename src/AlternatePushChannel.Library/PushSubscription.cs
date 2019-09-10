using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using Windows.Networking.PushNotifications;

namespace AlternatePushChannel.Library
{
    public sealed class PushSubscription
    {
        /// <summary>
        /// A string containing the endpoint associated with the push subscription.
        /// </summary>
        [JsonProperty("endpoint")]
        public string Endpoint { get; internal set; }

        [JsonProperty("keys")]
        public PushSubscriptionKeys Keys { get; internal set; }

        [JsonProperty("expirationTime")]
        internal DateTimeOffset ExpirationTime { get; set; }

        /// <summary>
        /// The native WNS push channel object.
        /// </summary>
        [JsonIgnore]
        public PushNotificationChannel Channel { get; internal set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    internal class StoredPushSubscription
    {
        public string AppServerKey { get; set; }

        public PushSubscriptionKeys Keys { get; set; }

        public string P265Private { get; set; }

        public string ChannelUri { get; set; }
    }
}
