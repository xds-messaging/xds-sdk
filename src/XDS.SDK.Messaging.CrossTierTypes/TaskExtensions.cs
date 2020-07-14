
using System.Threading.Tasks;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            task.ConfigureAwait(false); // Do also not sync back Exceptions to the calling thread.
        }
    }
}
