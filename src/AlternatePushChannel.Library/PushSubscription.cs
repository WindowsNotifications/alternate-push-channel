﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
}
