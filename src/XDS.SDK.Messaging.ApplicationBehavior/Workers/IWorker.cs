using System;
using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Workers
{
    public interface IWorker
    {
        Task WorkerTask { get; }

        Task InitializeAsync();

        Exception FaultReason { get; }

        string GetInfo();

        void Pause();

        void Resume();
    }
}
