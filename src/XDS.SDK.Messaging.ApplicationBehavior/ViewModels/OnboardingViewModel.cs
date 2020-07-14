using System;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.ECC;
using XDS.SDK.Messaging.Principal;

namespace XDS.Messaging.SDK.ApplicationBehavior.ViewModels
{
	public class OnboardingViewModel : NotifyPropertyChanged
	{
		readonly IXDSSecService ixdsCryptoService;
		readonly IMessageBoxService _messageBoxService;

		readonly AppRepository _repo;
		readonly DeviceVaultService _deviceVaultService;

		byte[] _privateKey;
		byte[] _collectedRandomBytes;

		bool _isCommitted;

		public OnboardingViewModel(IXDSSecService ixdsCryptoService, AppRepository appRepository, DeviceVaultService deviceVaultService, IMessageBoxService messageBoxService)
		{
			this.ixdsCryptoService = ixdsCryptoService;
			this._repo = appRepository;
			this._deviceVaultService = deviceVaultService;
			this._messageBoxService = messageBoxService;
			this.Name = ProfileViewModel.DefaultUsername;
		}

		public string Name
		{
			get => this._name;
			set
			{
				if (string.IsNullOrWhiteSpace(value))
					value = ProfileViewModel.DefaultUsername;
				Set(ref this._name, value.Trim());
			}
		}

		string _name;

		public byte[] PublicKey
		{
			get => this._publicKey;
			private set => Set(ref this._publicKey, value);
		}
		byte[] _publicKey;

		public string ChatId
		{
			get => this._chatId;
			private set => Set(ref this._chatId, value);
		}
		string _chatId;

        public string DefaultAddress
        {
            get => this.defaultAddress;
            private set => Set(ref this.defaultAddress, value);
        }
        string defaultAddress;

		public bool IsIdentityPublished
		{
			get => this._isIdentityPublished;
			private set => Set(ref this._isIdentityPublished, value);
		}
		bool _isIdentityPublished;

		public bool ShouldSaveBackup
		{
			get => this._shouldSaveBackup;
			set => Set(ref this._shouldSaveBackup, value);
		}
		bool _shouldSaveBackup;

		public byte[] PictureBytes
		{
			get => this._pictureBytes;
			set => Set(ref this._pictureBytes, value);
		}
		byte[] _pictureBytes;

		public string ValidatedMasterPassphrase
		{
			get => this._validatedMasterPassphrase;
			set => Set(ref this._validatedMasterPassphrase, value);
		}
		string _validatedMasterPassphrase;

		public void OnboardingGenerateIdentity(byte[] collectedRandom)
		{
			this._collectedRandomBytes = collectedRandom; // safe this for additional use to create the random master key

			ECKeyPair identityKeypair = this.ixdsCryptoService.GenerateCurve25519KeyPairExact(collectedRandom).Result;
			this._privateKey = identityKeypair.PrivateKey;
			this.PublicKey = identityKeypair.PublicKey;
			this.ChatId = XDS.SDK.Messaging.CrossTierTypes.ChatId.GenerateChatId(this.PublicKey);
		}

        XDSPrincipal xdsPrincipal;
        public void OnboardingGenerateIdentity(XDSPrincipal principal)
        {
            this.xdsPrincipal = principal;
			this._privateKey = this.xdsPrincipal.IdentityKeyPair.PrivateKey;
			this.PublicKey = this.xdsPrincipal.IdentityKeyPair.PublicKey;
            this.ChatId = this.xdsPrincipal.ChatId;
            this.DefaultAddress = this.xdsPrincipal.DefaultAddress.Address;
           
        }


		public async Task<bool?> CommitAll(LongRunningOperationContext longRunningOperationContext)
		{
			if (this._isCommitted)
				return null;

			try
			{
				if (string.IsNullOrWhiteSpace(this._chatId))
					throw new ArgumentNullException(nameof(this._chatId));
				if (string.IsNullOrWhiteSpace(this._name))
					throw new ArgumentNullException(nameof(this._name));
				if (string.IsNullOrWhiteSpace(this._validatedMasterPassphrase))
					throw new ArgumentNullException(nameof(this._validatedMasterPassphrase));

				if (this._privateKey == null || ByteArrays.AreAllBytesZero(this._privateKey))
					throw new ArgumentNullException(nameof(this._privateKey));
				if (this._publicKey == null || ByteArrays.AreAllBytesZero(this._publicKey))
					throw new ArgumentNullException(nameof(this._publicKey));
				
				if (this._pictureBytes == null || ByteArrays.AreAllBytesZero(this._pictureBytes))
					throw new ArgumentNullException(nameof(this._pictureBytes));


				await this._deviceVaultService.InitDeviceVaultKey(this._validatedMasterPassphrase, this.xdsPrincipal.MasterKey, longRunningOperationContext);

				await this._repo.AddProfile(new Profile
				{
					Id = ProfileViewModel.ProfileFStoreId,
					Name = this._name,
					PrivateKey = this._privateKey,
					PublicKey = this._publicKey,
					IsIdentityPublished = false,
					PictureBytes = this._pictureBytes,
					ChatId = this.xdsPrincipal.ChatId,
					DefaultAddress = this.xdsPrincipal.DefaultAddress.Address,
					DefaultAddressKeyPath = this.xdsPrincipal.DefaultAddress.KeyPath,
					DefaultAddressScriptPubKey = this.xdsPrincipal.DefaultAddress.ScriptPubKey,
					DefaultAddressHash = this.xdsPrincipal.DefaultAddress.Hash,
					DefaultAddressPrivateKey = this.xdsPrincipal.DefaultAddress.PrivateKey,
					DefaultAddressPublicKey = this.xdsPrincipal.DefaultAddress.PublicKey,
					MasterKey = this.xdsPrincipal.MasterKey
				});

				this._isCommitted = true;
				return true;
			}
			catch (Exception e)
			{
				await this._messageBoxService.ShowError("Error committing the profile - deleting all data and exiting. Exception: " + e.Message);
				await this._deviceVaultService.DeleteAllData();
				return false;
			}
		}

		
	}
}
