using System.Threading;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface ICancellation
    {
        CancellationTokenSource ApplicationStopping { get; }
        bool CanExit { get; set; }
    }
}
