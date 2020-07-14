using System;
using System.IO;

namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
    public class FStoreConfig
    {
		public string DefaultStoreName = "FStore";
	    public DirectoryInfo StoreLocation;
		public Action Initializer;

    }
}
