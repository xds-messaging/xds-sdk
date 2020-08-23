using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface ICancellation
    {
        /// <summary>
        /// To shut down the application, call cancel on this.
        /// </summary>
        CancellationToken Token { get; }

        DirectoryInfo DataDirRoot { get; }

        string GetTempDir(bool createIfNotExists);

        Task<bool> PrepareLaunch(DirectoryInfo dataDirRoot);

        void RegisterWorker(IWorker worker);

        Task StartWorkers();

        /// <summary>
        /// Set this to true to self-destruct at the end of the shutdown.
        /// </summary>
        bool IsSelfDestructRequested { get; set; }

        /// <summary>
        /// Cancel shuts down the application by calling Cancel on the
        /// wrapped CancellationTokenSource.
        /// </summary>
        void Cancel();
    }
}
