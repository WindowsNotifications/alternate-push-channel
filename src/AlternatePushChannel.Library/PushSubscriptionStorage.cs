using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.UI.Xaml;

namespace AlternatePushChannel.Library
{
    internal class PushSubscriptionStorageData
    {
        public Dictionary<string, PushSubscription> Subscriptions { get; set; }

        /// <summary>
        /// We'll also store previous subscriptions, since if an app created a new subscription but then 
        /// </summary>
        public Dictionary<string, PushSubscription> PreviousSubscriptions { get; set; }
    }

    internal class PushSubscriptionStorage
    {
        private static object _lock = new object();
        private static Task<PushSubscriptionStorage> _loadTask;

        /// <summary>
        /// Gets the storage instance
        /// </summary>
        /// <returns></returns>
        public static Task<PushSubscriptionStorage> GetAsync()
        {
            lock (_lock)
            {
                if (_loadTask == null || _loadTask.IsFaulted)
                {
                    _loadTask = LoadAsync();
                }

                return _loadTask;
            }
        }

        public StoredPushSubscription[] GetSubscriptions(string channelId)
        {
            lock (this)
            {
                if (_subscriptions.TryGetValue(channelId, out List<StoredPushSubscription> subscriptions))
                {
                    return subscriptions.ToArray();
                }
                else
                {
                    return new StoredPushSubscription[0];
                }
            }
        }

        public async Task DeleteSubscriptionsOlderThanAsync(string channelId, StoredPushSubscription newestSubscriptionToKeep)
        {
            bool needsSave = false;
            lock (this)
            {
                if (_subscriptions.TryGetValue(channelId, out List<StoredPushSubscription> subscriptions))
                {
                    for (int i = 0; i < subscriptions.Count; i++)
                    {
                        // Compare via reference works since we'll only ever have a single instance of each object
                        if (subscriptions[i] == newestSubscriptionToKeep)
                        {
                            // Delete any remaining
                            if (i + 1 < subscriptions.Count)
                            {
                                subscriptions.RemoveRange(i + 1, subscriptions.Count - (i + 1));
                            }
                        }
                    }
                }
            }

            if (needsSave)
            {
                await SaveAsync();
            }
        }

        private const string FileName = "AlternatePushChannel.Library.Data.json";

        private static async Task<PushSubscriptionStorage> LoadAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalCacheFolder.GetFileAsync(FileName);
                string json = await FileIO.ReadTextAsync(file);
                return new PushSubscriptionStorage(JsonConvert.DeserializeObject<Dictionary<string, List<StoredPushSubscription>>>(json));
            }
            catch
            {
                return new PushSubscriptionStorage(new Dictionary<string, List<StoredPushSubscription>>());
            }
        }

        /// <summary>
        /// Mapping of channel IDs to subscriptions
        /// </summary>
        private Dictionary<string, List<StoredPushSubscription>> _subscriptions;

        private PushSubscriptionStorage(Dictionary<string, List<StoredPushSubscription>> subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public async Task SavePushSubscriptionAsync(string channelId, StoredPushSubscription subscription)
        {
            lock (this)
            {
                if (_subscriptions.TryGetValue(channelId, out List<StoredPushSubscription> subscriptions))
                {
                    subscriptions.Insert(0, subscription);
                }
                else
                {
                    _subscriptions[channelId] = new List<StoredPushSubscription>() { subscription };
                }
            }

            await SaveAsync();
        }

        private SimpleAsyncWorkerQueueReturnless _saveQueue = new SimpleAsyncWorkerQueueReturnless();
        private async Task SaveAsync()
        {
            await _saveQueue.QueueOrMergeAsync("Save", async delegate
            {
                try
                {
                    string json = JsonConvert.SerializeObject(_subscriptions);

                    var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(file, json);
                }
                catch { }
            });
        }
    }
}
