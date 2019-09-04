using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AlternatePushChannel.Library
{
    public sealed class PushSubscriptionKeys
    {
        [JsonProperty("p256dh")]
        public string P256DH { get; internal set; }

        [JsonProperty("auth")]
        public string Auth { get; internal set; }
    }
}
