using System;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.SDK.Cryptography.Api.Interfaces;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization
{
	public static class RepositorySerializer
	{
		public static IXDSSecService XDSSecService { private get; set; }

		public static byte[] Serialize<T>(T item) where T : class
		{
			switch (item)
			{
				case null:
					return null;
				case Message m:
					return MessageSerializer.SerializeCore(m);
				case Identity i:
					return IdentitySerializer.SerializeCore(i).EncryptForStorage();
				case Profile p:
					return ProfileSerializer.SerializeCore(p).EncryptForStorage();
				case DeviceVaultService.Backup b:
					return BackupSerializer.SerializeCore(b);
				default:
					throw new ArgumentOutOfRangeException(nameof(item));
			}
		}

		//if (item == null)
		//    return null;
		//if (typeof(T) == typeof(Message))
		//	return MessageSerializer.SerializeCore(item as Message);

		//         if (typeof(T) == typeof(Identity))
		//             return IdentitySerializer.SerializeCore(item as Identity).EncryptForStorage();

		//         if (typeof(T) == typeof(Profile))
		//             return ProfileSerializer.SerializeCore(item as Profile).EncryptForStorage();

		//      if (typeof(T) == typeof(DeviceVaultService.Backup))
		//       return BackupSerializer.SerializeCore(item as DeviceVaultService.Backup);

		//throw new Exception();

		public static T Deserialize<T>(byte[] data) where T : class
		{
			if (typeof(T) == typeof(Message))
				return MessageSerializer.Deserialize(data) as T;

			if (typeof(T) == typeof(Profile))
				return ProfileSerializer.Deserialize(data.DecryptFromStorage()) as T;

			if (typeof(T) == typeof(Identity))
				return IdentitySerializer.Deserialize(data.DecryptFromStorage()) as T;

			if (typeof(T) == typeof(DeviceVaultService.Backup))
				return BackupSerializer.Deserialize(data) as T;

			throw new Exception();
		}

		static byte[] EncryptForStorage(this byte[] plaintextSerializedItem)
		{
			return XDSSecService.DefaultEncrypt(plaintextSerializedItem, XDSSecService.SymmetricKeyRepository.GetMasterRandomKey());
		}

		static byte[] DecryptFromStorage(this byte[] encryptedData)
		{
			return XDSSecService.DefaultDecrypt(encryptedData, XDSSecService.SymmetricKeyRepository.GetMasterRandomKey());
		}
	}
}
