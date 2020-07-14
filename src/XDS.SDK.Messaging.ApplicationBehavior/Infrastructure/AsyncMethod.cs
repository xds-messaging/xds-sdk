using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Infrastructure
{
    public static class AsyncMethod
    {
        static readonly TaskFactory TaskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        /// <summary>
        /// Run async method as sync.
        /// </summary>
        /// <see cref="https://cpratt.co/async-tips-tricks/"/>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="callerMemberName"></param>
        /// <returns></returns>
        public static TResult RunSync<TResult>(Func<Task<TResult>> func, [CallerMemberName] string callerMemberName = null)
        {
            return TaskFactory
                       .StartNew(func)
                       .Unwrap()
                       .GetAwaiter()
                       .GetResult();

        }

        /// <summary>
        /// Run async method as sync.
        /// <see cref="https://cpratt.co/async-tips-tricks/"/>
        /// </summary>
        /// <param name="func"></param>
        /// <param name="callerMemberName"></param>
        public static void RunSync(Func<Task> func, [CallerMemberName] string callerMemberName = null)
        {
            TaskFactory
            .StartNew(func)
            .Unwrap()
            .GetAwaiter()
            .GetResult();
        }
    }
}
