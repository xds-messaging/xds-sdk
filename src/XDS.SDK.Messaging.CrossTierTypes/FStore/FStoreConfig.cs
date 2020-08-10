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
        /// A delegate that ensures all FStore directories if they do not exist and
        /// configures all FStore repositories. This must be called both when it's a clean
        /// install and on every normal run. IFStore.DeleteAndRecreateStore also
        /// uses this.
        /// </summary>
		public Action<FStoreConfig> Initializer;

    }
}
