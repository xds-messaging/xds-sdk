namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
    public static class FStoreExtensions
    {
        public static string TableFolderName(this FSTable table)
        {
            return FStoreTables.TablePrefix + table.Name;
        }
    }
}
