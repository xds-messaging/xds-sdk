using System;
using System.IO;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Messaging.SDK.AppSupport.NetStandard
{
	public static class FStoreInitializer
	{

		public static readonly FStoreConfig FStoreConfig = new FStoreConfig
		{
			DefaultStoreName = "FStore",
			StoreLocation = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Personal)),
			Initializer = InitFStore
		};

		public static void InitFStore()
		{
			var fStore = new FStoreMono(FStoreConfig);
			if (!fStore.StoreExists())
				fStore.CreateStore();

			var profilesTable = new FSTable(nameof(Profile), IdMode.UserGenerated); // Single item, Id does not matter but speeds things up
			if (!fStore.TableExists(profilesTable, null))
				fStore.CreateTable(profilesTable);
			FStoreTables.TableConfig[typeof(Profile)] = profilesTable;

			var contactsTable = new FSTable(nameof(Identity), IdMode.UserGenerated); // Id is necessary to retrieve an item
			if (!fStore.TableExists(contactsTable, null))
				fStore.CreateTable(contactsTable);
			FStoreTables.TableConfig[typeof(Identity)] = contactsTable;

			var messagesTable = new FSTable(nameof(Message), IdMode.Auto, true, true); // e.g. /tbl_Message/1234567890/999999
			if (!fStore.TableExists(messagesTable, null))                 //       /[page: recipientId]/[auto-id]
				fStore.CreateTable(messagesTable);
			FStoreTables.TableConfig[typeof(Message)] = messagesTable;
		}
	}
}
