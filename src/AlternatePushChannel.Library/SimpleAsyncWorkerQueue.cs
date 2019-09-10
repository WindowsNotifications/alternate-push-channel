using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlternatePushChannel.Library
{
    /// <summary>
    /// This worker queue is a more simple implementation of a worker queue.
    /// https://github.com/barebonesdev/portablelibraries/blob/master/ToolsPortable/ToolsPortable/AsyncWorkerQueue.cs
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class SimpleAsyncWorkerQueue<T>
    {
        internal event EventHandler OnAllCompleted;

        private class QueuedItem
        {
            public object MergeIdentifier { get; set; }

            public TaskCompletionSource<T> TaskCompletionSource { get; set; }

            public Func<Task<T>> WorkerFunction { get; set; }
        }

        private Queue<QueuedItem> _queue = new Queue<QueuedItem>();

        internal bool IsRunning
        {
            get
            {
                lock (this)
                {
                    return _queue.Count > 0;
                }
            }
        }

        public Task<T> QueueAsync(Func<Task<T>> workerFunction)
        {
            QueuedItem queuedItem = new QueuedItem()
            {
                TaskCompletionSource = new TaskCompletionSource<T>(),
                WorkerFunction = workerFunction
            };

            bool shouldStart = false;

            lock (this)
            {
                // Enqueue it
                _queue.Enqueue(queuedItem);

                // If this was the first, we have to start everything
                shouldStart = _queue.Count == 1;
            }

            if (shouldStart)
            {
                Start();
            }

            return queuedItem.TaskCompletionSource.Task;
        }

        /// <summary>
        /// Will merge with any pending tasks that haven't started yet and have the same mergeIdentifier.
        /// </summary>
        /// <param name="mergeIdentifier">Compared via object.Equals to determine if same instance and should be merged</param>
        /// <param name="workerFunction"></param>
        /// <returns></returns>
        public Task<T> QueueOrMergeAsync(object mergeIdentifier, Func<Task<T>> workerFunction, bool allowMergeWithAlreadyStarted = false)
        {
            if (mergeIdentifier == null)
            {
                throw new ArgumentNullException(nameof(mergeIdentifier));
            }

            QueuedItem queuedItem;

            bool shouldStart = false;

            lock (this)
            {
                if (allowMergeWithAlreadyStarted && _queue.Count > 0)
                {
                    var alreadyRunning = _queue.First();
                    if (alreadyRunning.MergeIdentifier != null && object.Equals(alreadyRunning.MergeIdentifier, mergeIdentifier))
                    {
                        // Return that task without modifying worker function, since it already started
                        return alreadyRunning.TaskCompletionSource.Task;
                    }
                }

                if (_queue.Count > 1)
                {
                    QueuedItem matching = _queue.Skip(1).FirstOrDefault(i => i.MergeIdentifier != null && object.Equals(i.MergeIdentifier, mergeIdentifier));

                    if (matching != null && matching != _queue.Peek())
                    {
                        // Update the worker function to the latest
                        matching.WorkerFunction = workerFunction;

                        // And return that task
                        return matching.TaskCompletionSource.Task;
                    }
                }

                // Otherwise need to schedule it
                queuedItem = new QueuedItem()
                {
                    TaskCompletionSource = new TaskCompletionSource<T>(),
                    WorkerFunction = workerFunction,
                    MergeIdentifier = mergeIdentifier
                };

                // Enqueue it
                _queue.Enqueue(queuedItem);

                // If this was the first, we have to start everything
                if (_queue.Count == 1)
                {
                    shouldStart = true;
                }
            }

            if (shouldStart)
            {
                Start();
            }

            return queuedItem.TaskCompletionSource.Task;
        }

        private async void Start()
        {
            bool shouldStartNext = false;
            do
            {
                QueuedItem queuedItem = null;
                lock (this)
                {
                    queuedItem = _queue.Peek();
                }

                try
                {
                    T answer = await queuedItem.WorkerFunction();
                    queuedItem.TaskCompletionSource.TrySetResult(answer);
                }
                catch (Exception ex)
                {
                    try
                    {
                        queuedItem.TaskCompletionSource.TrySetException(ex);
                    }
                    catch { }
                }

                lock (this)
                {
                    _queue.Dequeue();
                    shouldStartNext = _queue.Count > 0;
                }
            }
            while (shouldStartNext);

            OnAllCompleted?.Invoke(this, new EventArgs());
        }
    }

    internal class SimpleAsyncWorkerQueueReturnless : SimpleAsyncWorkerQueue<bool>
    {
        public Task QueueAsync(Func<Task> workerFunction)
        {
            return base.QueueAsync(async delegate
            {
                await workerFunction();
                return true;
            });
        }

        public Task QueueOrMergeAsync(object mergeIdentifier, Func<Task> workerFunction)
        {
            return base.QueueOrMergeAsync(mergeIdentifier, async delegate
            {
                await workerFunction();
                return true;
            });
        }
    }
}
