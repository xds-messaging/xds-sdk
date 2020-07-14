using System;
using System.Collections.Concurrent;

namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
    public static class FStoreTables
    {
        public const string TablePrefix = "tbl_";
        public static readonly ConcurrentDictionary<Type, FSTable> TableConfig = new ConcurrentDictionary<Type, FSTable>();
        public static readonly ConcurrentDictionary<string, uint> IdCache = new ConcurrentDictionary<string, uint>();
    }
}
