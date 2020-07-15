using System;
using System.IO;

namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
    public class FStoreConfig
    {
        /// <summary>
        /// The name is part of the StoreLocation path.
        /// </summary>
		public string DefaultStoreName = "FStore";

        /// <summary>
        /// The location where the store should be created.
        /// </summary>
	    public DirectoryInfo StoreLocation;

        /// <summary>
        /// The store has a function to delete and recreate itself by executing this delegate.
        /// </summary>
		public Action<FStoreConfig> Initializer;

    }
}
