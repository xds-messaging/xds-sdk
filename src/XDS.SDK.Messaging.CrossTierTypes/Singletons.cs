using System;
using System.Security.Cryptography;
using System.Threading;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class Singletons
    {
        public static readonly Lazy<SHA256> Sha256 = new Lazy<SHA256>(SHA256.Create, LazyThreadSafetyMode.ExecutionAndPublication);

    }
}
