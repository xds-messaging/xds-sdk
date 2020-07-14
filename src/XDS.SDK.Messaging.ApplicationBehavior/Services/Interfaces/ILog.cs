using System;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface ILog
    {
        void Debug(string info);
        void Exception(Exception e, string message = null);
    }
}
