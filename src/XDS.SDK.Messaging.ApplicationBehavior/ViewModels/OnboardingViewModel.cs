using System;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.ECC;
using XDS.SDK.Messaging.Principal;

namespace XDS.Messaging.SDK.ApplicationBehavior.ViewModels
{
    public class OnboardingViewModel : NotifyPropertyChanged
    {
        readonly IXDSSecService xdsCryptoService;
        readonly AppRepository appRepository;
        readonly DeviceVaultService deviceVaultService;

        byte[] privateKey;


        public OnboardingViewModel(IXDSSecService xdsCryptoService, AppRepository appRepository, DeviceVaultService deviceVaultService)
        {
            this.xdsCryptoService = xdsCryptoService;
            this.appRepository = appRepository;
            this.deviceVaultService = deviceVaultService;
            this.Name = ProfileViewModel.DefaultUsername;
        }

        public string Name
        {
            get => this.name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    value = ProfileViewModel.DefaultUsername;
                Set(ref this.name, value.Trim());
            }
        }

        string name;

        public byte[] PublicKey
        {
            get => this.publicKey;
            private set => Set(ref this.publicKey, value);
        }
        byte[] publicKey;

        public string ChatId
        {
            get => this.chatId;
            private set => Set(ref this.chatId, value);
        }
        string chatId;

        public string DefaultAddress
        {
            get => this.defaultAddress;
            private set => Set(ref this.defaultAddress, value);
        }
        string defaultAddress;

        public bool IsIdentityPublished
        {
            get => this.isIdentityPublished;
            private set => Set(ref this.isIdentityPublished, value);
        }
        bool isIdentityPublished;

        public bool ShouldSaveBackup
        {
            get => this.shouldSaveBackup;
            set => Set(ref this.shouldSaveBackup, value);
        }
        bool shouldSaveBackup;

        public byte[] PictureBytes
        {
            get => this.pictureBytes;
            set => Set(ref this.pictureBytes, value);
        }
        byte[] pictureBytes;

        public string ValidatedMasterPassphrase
        {
            get => this.validatedMasterPassphrase;
            set => Set(ref this.validatedMasterPassphrase, value);
        }
        string validatedMasterPassphrase;

        public void OnboardingGenerateIdentity(byte[] collectedRandom)
        {
            ECKeyPair ecKeyPair = this.xdsCryptoService.GenerateCurve25519KeyPairExact(collectedRandom).Result;
            this.privateKey = ecKeyPair.PrivateKey;
            this.PublicKey = ecKeyPair.PublicKey;
            this.ChatId = XDS.SDK.Messaging.CrossTierTypes.ChatId.GenerateChatId(this.PublicKey);
        }

        XDSPrincipal xdsPrincipal;
        public void OnboardingGenerateIdentity(XDSPrincipal principal)
        {
            this.xdsPrincipal = principal;
            this.privateKey = this.xdsPrincipal.IdentityKeyPair.PrivateKey;
            this.PublicKey = this.xdsPrincipal.IdentityKeyPair.PublicKey;
            this.ChatId = this.xdsPrincipal.ChatId;
            this.DefaultAddress = this.xdsPrincipal.DefaultAddress.Address;

        }


        public async Task CommitAllAsync(LongRunningOperationContext longRunningOperationContext)
        {
            if (string.IsNullOrWhiteSpace(this.chatId))
                throw new ArgumentNullException(nameof(this.chatId));
            if (string.IsNullOrWhiteSpace(this.name))
                throw new ArgumentNullException(nameof(this.name));
            if (string.IsNullOrWhiteSpace(this.validatedMasterPassphrase))
                throw new ArgumentNullException(nameof(this.validatedMasterPassphrase));

            if (this.privateKey == null || ByteArrays.AreAllBytesZero(this.privateKey))
                throw new ArgumentNullException(nameof(this.privateKey));
            if (this.publicKey == null || ByteArrays.AreAllBytesZero(this.publicKey))
                throw new ArgumentNullException(nameof(this.publicKey));

            if (this.pictureBytes == null || ByteArrays.AreAllBytesZero(this.pictureBytes))
                throw new ArgumentNullException(nameof(this.pictureBytes));

            await this.deviceVaultService.InitDeviceVaultKeyAsync(this.validatedMasterPassphrase, this.xdsPrincipal.MasterKey, longRunningOperationContext);

            await this.appRepository.AddProfile(new Profile
            {
                Id = ProfileViewModel.ProfileFStoreId,
                Name = this.name,
                PrivateKey = this.privateKey,
                PublicKey = this.publicKey,
                IsIdentityPublished = false,
                PictureBytes = this.pictureBytes,
                ChatId = this.xdsPrincipal.ChatId,
                DefaultAddress = this.xdsPrincipal.DefaultAddress.Address,
                DefaultAddressKeyPath = this.xdsPrincipal.DefaultAddress.KeyPath,
                DefaultAddressScriptPubKey = this.xdsPrincipal.DefaultAddress.ScriptPubKey,
                DefaultAddressHash = this.xdsPrincipal.DefaultAddress.Hash,
                DefaultAddressPrivateKey = this.xdsPrincipal.DefaultAddress.PrivateKey,
                DefaultAddressPublicKey = this.xdsPrincipal.DefaultAddress.PublicKey,
                MasterKey = this.xdsPrincipal.MasterKey
            });
        }
    }
}
