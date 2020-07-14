using System;
using System.Threading;
using System.Threading.Tasks;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    /// <summary>
    /// http://www.hanselman.com/blog/ComparingTwoTechniquesInNETAsynchronousCoordinationPrimitives.aspx
    /// </summary>
    public sealed class AsyncLock
    {
        readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        readonly Task<IDisposable> _releaser;

        public AsyncLock()
        {
            this._releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync()
        {
            var waitTask = this._semaphoreSlim.WaitAsync();
            return waitTask.IsCompleted ?
                        this._releaser :
                        waitTask.ContinueWith((t, state) => (IDisposable)state,
                            this._releaser.Result, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        sealed class Releaser : IDisposable
        {
            readonly AsyncLock _asyncLock;

            internal Releaser(AsyncLock asyncLock)
            {
                this._asyncLock = asyncLock;
            }

            public void Dispose()
            {
                this._asyncLock._semaphoreSlim.Release();
            }
        }
    }
}
