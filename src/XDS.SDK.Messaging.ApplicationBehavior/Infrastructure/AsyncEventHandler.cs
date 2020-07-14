using System;
using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Infrastructure
{
    public delegate Task AsyncEventHandler(object sender, EventArgs args);
    public delegate Task AsyncEventHandler<in T>(object sender, T args);
}
