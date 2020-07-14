using System;
using System.Security.Cryptography;

namespace XDS.SDK.Messaging.CrossTierTypes
{
	public static class NetworkPayloadHash
	{
		const int Lenght = 16;

		static readonly SHA512 Sha512 = SHA512.Create();

		static byte[] Compute(byte[] networkPayload)
		{
			var networkPayloadHash = new byte[Lenght];
			var sha521Hash = Sha512.ComputeHash(networkPayload);
			Buffer.BlockCopy(sha521Hash, 0, networkPayloadHash, 0, Lenght);
			return networkPayloadHash;
		}

		public static string ComputeAsGuidString(byte[] networkPayload)
		{
			var hashBytes = Compute(networkPayload);
			var guidString = new Guid(hashBytes).ToString();
			return guidString;
		}
	}
}
