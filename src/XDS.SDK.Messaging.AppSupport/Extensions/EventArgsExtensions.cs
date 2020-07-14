using System;
using System.Threading;

namespace XDS.Messaging.SDK.AppSupport.NetStandard.Extensions
{
    public static class EventArgsExtensions
    {
        public static void Raise<TEventArgs>(this TEventArgs e,
                                             object sender,
                                             ref EventHandler<TEventArgs> eventDelegate)
            where TEventArgs : EventArgs
        {
            Volatile.Read(ref eventDelegate)?.Invoke(sender, e);
        }
    }
}
