using System;
using System.Linq;
using System.Threading.Tasks;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;

namespace XDS.SDK.Cryptography.E2E
{
	public enum E2EDecryptionKeyType
	{
		MyStaticPrivateKey = 1,
		DynamicPrivateKey = 2,
		UnavailableDynamicPrivateKey =3
	}
	public class GetE2EDecryptionKeyResult
	{
		public KeyMaterial64 E2EDecryptionKeyMaterial;
		public E2EDecryptionKeyType E2EDecryptionKeyType;
	}

    public interface IE2ERatchetParameters
    {
		string OwnId { get; set; }
		byte[] OwnStaticPrivateKey { get; set; }
		Func<string, Task<E2EUser>> GetUser { get; set; }
		Func<E2EUser, Task> UpdateUser { get; set; }
	}

    public class E2ERatchetParameters : IE2ERatchetParameters
    {
        public string OwnId { get; set; }
        public byte[] OwnStaticPrivateKey { get; set; }
		public Func<string, Task<E2EUser>> GetUser { get; set; }
		public Func<E2EUser, Task> UpdateUser { get; set; }
	}

    public class E2ERatchet
	{

		const int KeepLatestDynamicPrivateKeys = 50;

		readonly IXDSSecService ixdsCryptoService;
		
		readonly RatchetTimer ratchetTimer = new RatchetTimer();

        IE2ERatchetParameters parameters;
        bool isInitialized;

		public E2ERatchet(IXDSSecService ixdsCryptoService)
        {
            
			this.ixdsCryptoService = ixdsCryptoService;
		}

       

        public void InitialiseFromRatchetParameters(IE2ERatchetParameters ratchetParameters)
        {
			// it's possible we should recycle the ratchetTime each time we initialize the ratchet, review this.

            this.parameters = ratchetParameters ?? throw new ArgumentNullException(nameof(ratchetParameters));

			this.isInitialized = true;
        }

        

		public async Task<GetE2EDecryptionKeyResult> GetEndToEndDecryptionKeyAsync(string senderId, byte[] dynamicPublicKey, long privateKeyHint)
		{
			EnsureInitialized();

			var user = await GetCheckedUserAsync(senderId);

			var result = new GetE2EDecryptionKeyResult();
			

			byte[] dynamicPrivateKeyOrStaticPrivateKey;

			if (privateKeyHint == 0)
			{
				dynamicPrivateKeyOrStaticPrivateKey = this.parameters.OwnStaticPrivateKey;
				result.E2EDecryptionKeyType = E2EDecryptionKeyType.MyStaticPrivateKey;
			}
			else
			{
				if (user.DynamicPrivateDecryptionKeys.TryGetValue(privateKeyHint, out dynamicPrivateKeyOrStaticPrivateKey))
				{
					result.E2EDecryptionKeyType = E2EDecryptionKeyType.DynamicPrivateKey;
				}
				else
				{
					// Possible reasons:
					// - we did not look into the right user's ratchet while trying to determine the sender
					// - The ratchets are not in sync, resend/new dynamic key exchange required
					result.E2EDecryptionKeyType = E2EDecryptionKeyType.UnavailableDynamicPrivateKey;
					return result;
				}
			}

			var dynamicSharedSecret = this.ixdsCryptoService.CalculateAndHashSharedSecret(dynamicPrivateKeyOrStaticPrivateKey, dynamicPublicKey);

			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, user.AuthSecret);
			result.E2EDecryptionKeyMaterial = new KeyMaterial64(symmetricKeyMaterial);
			return result;
		}


		public KeyMaterial64 GetInitialE2EDecryptionKey(byte[] dynamicPublicKey)
		{
            EnsureInitialized();

			var dynamicSharedSecret = this.ixdsCryptoService.CalculateAndHashSharedSecret(this.parameters.OwnStaticPrivateKey, dynamicPublicKey);
			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, new byte[32]);
			return new KeyMaterial64(symmetricKeyMaterial);
		}

		public async Task SaveIncomingDynamicPublicKeyOnSuccessfulDecryptionAsync(string senderId, byte[] dynamicPublicKey, long dynamicPublicKeyId)
		{
            EnsureInitialized();

			Guard.NotNull(senderId, dynamicPublicKey);
			if (dynamicPublicKeyId == 0)
				throw new ArgumentException("A dynamic public key must never have an ID of 0.");
			var user = await GetCheckedUserAsync(senderId);
			user.LatestDynamicPublicKey = dynamicPublicKey;
			user.LatestDynamicPublicKeyId = dynamicPublicKeyId;
			await this.parameters.UpdateUser(user);
		}

		public async Task<Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>> GetE2EEncryptionKeyCommonAsync(string recipientId, bool? isInitial)
		{
            EnsureInitialized();

			E2EUser user = await GetCheckedUserAsync(recipientId);
			if (user.IsJustInitialized)
				isInitial = true;

			if (isInitial == true) // When the contact was just added (the ratchet was just initialized, AuthSecret was null before), or we are answering for a resent request, we use this 'initial' method.
				return await GetInitialEndToEndEncryptionKeyAsync(recipientId);
			return await GetEndToEndEncryptionKeyAsync(recipientId);
		}
        void EnsureInitialized()
        {
            if (!this.isInitialized)
                throw new InvalidOperationException($"You need to update the {nameof(IE2ERatchetParameters)} members and call {nameof(InitialiseFromRatchetParameters)} before using the ratchet.");
        }

		async Task<Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>> GetEndToEndEncryptionKeyAsync(string recipientId)
		{
			E2EUser user = await GetCheckedUserAsync(recipientId);
			long existingMaxKeyId = 0;
			if (user.DynamicPrivateDecryptionKeys.Keys.Count > 0) // count might be 0 initially...might be a bug or not
			{
				existingMaxKeyId = user.DynamicPrivateDecryptionKeys.Keys.Max();
			}

			long nextDynamicPublicKeyId = this.ratchetTimer.GetNextTicks(existingMaxKeyId);

            var random = this.ixdsCryptoService.GetRandom(32).Result.X;
			var ecdhKeypair = this.ixdsCryptoService.GenerateCurve25519KeyPairExact(random).Result;
			byte[] dynamicPublicKey = ecdhKeypair.PublicKey;

			long privateKeyHint;

			user.DynamicPrivateDecryptionKeys[nextDynamicPublicKeyId] = ecdhKeypair.PrivateKey;
			RemoveExcessKeys(user);
			await this.parameters.UpdateUser(user);

			byte[] dynamicOrStaticPublicKey;
			if (user.LatestDynamicPublicKey != null)
			{
				dynamicOrStaticPublicKey = user.LatestDynamicPublicKey;
				privateKeyHint = user.LatestDynamicPublicKeyId;
			}
			else
			{
				dynamicOrStaticPublicKey = user.StaticPublicKey;
				privateKeyHint = 0;
			}

			var dynamicSharedSecret = this.ixdsCryptoService.CalculateAndHashSharedSecret(ecdhKeypair.PrivateKey, dynamicOrStaticPublicKey);

			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, user.AuthSecret);
			return new Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>(new KeyMaterial64(symmetricKeyMaterial), dynamicPublicKey, nextDynamicPublicKeyId, privateKeyHint, null);
		}

		async Task<Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>> GetInitialEndToEndEncryptionKeyAsync(string recipientId)
		{
			E2EUser user = await GetCheckedUserAsync(recipientId);

			// user.DynamicPrivateDecryptionKeys = new Dictionary<long, byte[]>(); // don't do this. Or only the last receipt of a resent message can be decrypted
			user.LatestDynamicPublicKey = null;
			user.LatestDynamicPublicKeyId = 0;


			long nextDynamicPublicKeyId = this.ratchetTimer.GetNextTicks(0);
            var random = this.ixdsCryptoService.GetRandom(32).Result.X;
			var ecdhKeyPair = this.ixdsCryptoService.GenerateCurve25519KeyPairExact(random).Result;
			byte[] dynamicPublicKey = ecdhKeyPair.PublicKey;

			user.DynamicPrivateDecryptionKeys[nextDynamicPublicKeyId] = ecdhKeyPair.PrivateKey;
			RemoveExcessKeys(user);

			await this.parameters.UpdateUser(user);

			long privateKeyHint = 0;

			var dynamicSharedSecret = this.ixdsCryptoService.CalculateAndHashSharedSecret(ecdhKeyPair.PrivateKey, user.StaticPublicKey);
			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, user.AuthSecret);
			var symmetricKeyMaterialMetaData = ByteArrays.Concatenate(dynamicSharedSecret, new byte[32]); // note we are not using user.AuthSecret fro the metadata

			return new Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>(new KeyMaterial64(symmetricKeyMaterial), dynamicPublicKey, nextDynamicPublicKeyId, privateKeyHint, new KeyMaterial64(symmetricKeyMaterialMetaData));
		}


		// TODO: Review this, compare it with TLSCLient.RemovePreviousKeys and when key cleanup is done
		// This may not work correctly.
		void RemoveExcessKeys(E2EUser user)
		{
			var excess = user.DynamicPrivateDecryptionKeys.Keys.OrderByDescending(k => k).Skip(KeepLatestDynamicPrivateKeys);
			foreach (var keyId in excess)
				user.DynamicPrivateDecryptionKeys.Remove(keyId);
		}

		async Task<E2EUser> GetCheckedUserAsync(string userId)
		{
			E2EUser user = await this.parameters.GetUser(userId);
			if (user.DynamicPrivateDecryptionKeys == null)
				throw new InvalidOperationException("Must be guaranteed on object creation, atm in AppRepository, line 315.");
			if (user.AuthSecret == null)
			{
				user.AuthSecret = this.ixdsCryptoService.CalculateAndHashSharedSecret(this.parameters.OwnStaticPrivateKey,
					user.StaticPublicKey);
				user.IsJustInitialized = true;
			}

			if (user.IsJustInitialized)
				await this.parameters.UpdateUser(user);
			return user;
		}

	}
}