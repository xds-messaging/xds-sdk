using System;
using System.Collections.Generic;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization
{
	public static class BackupSerializer
	{
		public static byte[] SerializeCore(DeviceVaultService.Backup b)
		{
			if (b?.ContactIdentities == null)
				return null;

			var profileBytes = ProfileSerializer.SerializeCore(b.Profile);

			byte[] serialized = PocoSerializer.Begin()
				.Append(b.Version)
				.Append(b.PlaintextMasterRandomKey)
				.Append(profileBytes)
				.Append(b.ContactIdentities.SerializeCollection(IdentitySerializer.SerializeCore))
				.Finish();
			return serialized;
		}

		public static DeviceVaultService.Backup Deserialize(byte[] serializedBackup)
		{
			try
			{
				var b = new DeviceVaultService.Backup();
				List<byte[]> ser = PocoSerializer.GetDeserializer(serializedBackup);

				b.Version = ser.MakeInt32(0);
				switch (b.Version)
				{
					case 1:
						b.PlaintextMasterRandomKey = ser.MakeByteArray(1);

						var profileBytes = ser.MakeByteArray(2);
						b.Profile = ProfileSerializer.Deserialize(profileBytes);

						var contactsCollection = ser.MakeByteArray(3);
						b.ContactIdentities = contactsCollection.DeserializeCollection(IdentitySerializer.Deserialize);
						break;
					default:
						throw new InvalidOperationException("Invalid backup format.");
				}
				return b;
			}
			catch (Exception e)
			{
				throw new Exception("Error deserializing backup.", e);
			}
		}
	}
}
