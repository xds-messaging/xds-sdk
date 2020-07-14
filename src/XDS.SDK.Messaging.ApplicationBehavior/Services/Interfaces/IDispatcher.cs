using System;
using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface IDispatcher
    {
        void Run(Action action);
        Task RunAsync(Func<Task> action);
    }
}
