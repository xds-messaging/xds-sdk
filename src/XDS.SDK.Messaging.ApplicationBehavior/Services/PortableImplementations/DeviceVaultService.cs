using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public class DeviceVaultService
    {
        const string MasterKeyFilename = "key";

        readonly AppRepository repo;
        readonly ChatWorker chatWorker;
        readonly IXDSSecService ixdsCryptoService;

        public DeviceVaultService(AppRepository appRepository,ChatWorker chatWorker, IXDSSecService ixdsCryptoService)
        {
            this.repo = appRepository;
            this.chatWorker = chatWorker;
            this.ixdsCryptoService = ixdsCryptoService;
        }

        /// <summary>
        /// Checks if onboarding is required or if the app is already setup.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CheckIfOnboardingRequired()
        {
            var encryptedMasterRandomKey = await this.repo.LoadSpecialFile(MasterKeyFilename);
            return encryptedMasterRandomKey == null;
        }

        /// <summary>
        /// Loads the key file and tries to decrypt the master random key. Only if successful, it also sets the decrypted master random key in SymmetricKeyRepository.
        /// Then, Response.Success is true. Therefore, this method can also be used to check if the master password is correct. If it is incorrect, the method does nothing and 
        /// Response.Success is false.
        /// </summary>
        /// <param name="unprunedUtf16LeMasterPassword">the master password</param>
        /// <param name="context">Progress object</param>
        public async Task TryLoadDecryptAndSetMasterRandomKey(string unprunedUtf16LeMasterPassword, LongRunningOperationContext context)
        {
            KeyMaterial64 masterPasswordHash = CreateKeyMaterialFromPassphrase(unprunedUtf16LeMasterPassword);

            byte[] encryptedRandomMasterKey = await this.repo.LoadSpecialFile(MasterKeyFilename);
            byte[] decryptedMasterRandomKey = this.ixdsCryptoService.DefaultDecrypt(encryptedRandomMasterKey, masterPasswordHash, context);

            this.ixdsCryptoService.SymmetricKeyRepository.SetDeviceVaultRandomKey(new KeyMaterial64(decryptedMasterRandomKey));
        }

        /// <summary>
        /// Clears the decrypted master random key from memory.
        /// </summary>
        public void ClearMasterRandomKey()
        {
            this.ixdsCryptoService.SymmetricKeyRepository.ClearMasterRandomKey();
        }

        /// <summary>
        /// Creates, encrypts and saves the master random key during the onboarding process.
        /// </summary>
        /// <param name="newMasterPassphrase">The newly chosen master passphrase.</param>
        /// <param name="masterKey">The master random key material, including collected user random.</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InitDeviceVaultKey(string newMasterPassphrase, byte[] masterKey, LongRunningOperationContext context)
        {
            await DeleteAllData();
            KeyMaterial64 masterPassphraseKeyMaterial = CreateKeyMaterialFromPassphrase(newMasterPassphrase);

            byte[] deviceVaultKey = new byte[64];
            Buffer.BlockCopy(masterKey, 64, deviceVaultKey, 0, 64); // use the second half, so that we do not use the wallet seed material.
            deviceVaultKey = this.ixdsCryptoService.ComputeSHA512(deviceVaultKey); // hash it, because we have a new specific purpose and do not want key reuse.

            KeyMaterial64 deviceVaultKeyMaterial = new KeyMaterial64(deviceVaultKey);
            this.ixdsCryptoService.SymmetricKeyRepository.SetDeviceVaultRandomKey(deviceVaultKeyMaterial);
            await Task.Run(async () =>
            {
                await EncryptAndSaveDeviceVaultKey(deviceVaultKeyMaterial, masterPassphraseKeyMaterial, context);
            });

        }

        /// <summary>
        /// Changes the master password by encrypting the master random key with a new password and updating the key file.
        /// </summary>
        /// <param name="currentMasterPassword">The current master password.</param>
        /// <param name="futureMasterPassword">The future master password.</param>
        /// <returns></returns>
        public async Task ChangeMasterPassword(string currentMasterPassword, string futureMasterPassword)
        {
            // Verify the user knows the current master password
            await TryLoadDecryptAndSetMasterRandomKey(currentMasterPassword, null);

            KeyMaterial64 futureMasterPasswordHash = CreateKeyMaterialFromPassphrase(futureMasterPassword);

            // get the current master random key in plaintext
            KeyMaterial64 currentPlaintextMasterRandomKey = this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey();

            // encrypt the current master random key for saving in the key file and write the file.
            CipherV2 encryptedMasterRandomKey = this.ixdsCryptoService.BinaryEncrypt(new Clearbytes(currentPlaintextMasterRandomKey.GetBytes()), futureMasterPasswordHash, new RoundsExponent(10), null).Result;
            byte[] encryptedMasterRandomKeyBytes = this.ixdsCryptoService.BinaryEncodeXDSSec(encryptedMasterRandomKey, null).Result;
            await this.repo.WriteSpecialFile(MasterKeyFilename, encryptedMasterRandomKeyBytes);
        }

        /// <summary>
        /// Creates a backup of the the Profile and contact Identities and encrypts it with a supplied passphrase.
        /// </summary>
        /// <param name="backupPassphrase">Password for the backup</param>
        /// <param name="context">Object for progress</param>
        /// <returns>CreateBackupResult</returns>
        public async Task<CreateBackupResult> CreateBackup(string backupPassphrase, bool stopStartChatWorker, LongRunningOperationContext context = null)
        {
            if (stopStartChatWorker)
                await this.chatWorker.StopRunLoopAndDisconnectAllAsync();

            var backup = new Backup
            {
                Version = 1,
                PlaintextMasterRandomKey = this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey().GetBytes(),
                Profile = await this.repo.GetProfile()
            };

            var identities = await this.repo.GetAllContacts();
            backup.ContactIdentities = identities.Where(c => c.ContactState == ContactState.Valid).ToList();
            foreach (var identity in identities)
            {
                // TODO: Don't export unneeded contact state information!
            }

            var backupBytes = RepositorySerializer.Serialize(backup);
            Clearbytes clearbytes = new Clearbytes(backupBytes);

            Backup test = RepositorySerializer.Deserialize<Backup>(backupBytes);

            string xdsSecText = null;
            string error = null;

            await Task.Run(() =>
            {
                KeyMaterial64 backupKeyMaterial = CreateKeyMaterialFromPassphrase(backupPassphrase);
                var encryptResponse = this.ixdsCryptoService.BinaryEncrypt(clearbytes, backupKeyMaterial, new RoundsExponent(10), context);
                if (encryptResponse.IsSuccess)
                {
                    var encodeResponse = this.ixdsCryptoService.EncodeXDSSec(encryptResponse.Result);
                    if (encodeResponse.IsSuccess)
                        xdsSecText = encodeResponse.Result.Text;
                    else
                        error = encodeResponse.Error;
                }
                else error = encryptResponse.Error;
            });

            if (error != null)
                throw new Exception(error);

            var result = new CreateBackupResult
            {
                BackupFileContentsInXDSSecFormat = xdsSecText,
                OwnProfileId = ChatId.GenerateChatId(backup.Profile.PublicKey)
            };
            if (stopStartChatWorker)
                this.chatWorker.StartRunning();
            return result;
        }

        /// <summary>
        /// Called by a view to import, decrypt and set a Profile.
        /// </summary>
        /// <param name="xdsSecFormat">The file contents.</param>
        /// <param name="backupPassphrase">The file password.</param>
        /// <param name="newMasterPassphrase">The new master, which will be used to encrypt the master random key in from the backup</param>
        /// <param name="context">Object providing progress reporting (can be null).</param>
        /// <returns>The profile / Error message (or null)</returns>
        public async Task RestoreBackup(string xdsSecFormat, string backupPassphrase, string newMasterPassphrase, LongRunningOperationContext context)
        {

            KeyMaterial64 backupKeyMaterial = CreateKeyMaterialFromPassphrase(backupPassphrase);

            byte[] decryptedBytes = null;
            var error = String.Empty;
            await Task.Run(async () =>
            {
                var decodeResponse = this.ixdsCryptoService.DecodeXDSSec(xdsSecFormat, context);
                if (decodeResponse.IsSuccess)
                {
                    var decryptResponse = this.ixdsCryptoService.BinaryDecrypt(decodeResponse.Result, backupKeyMaterial, context);
                    if (decryptResponse.IsSuccess)
                    {
                        Clearbytes clearbytes = decryptResponse.Result;
                        decryptedBytes = clearbytes.GetBytes();
                    }
                    else
                    {
                        error = decryptResponse.Error;
                    }
                }
                else
                {
                    error = decodeResponse.Error;

                }
                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }

                Backup backup = RepositorySerializer.Deserialize<Backup>(decryptedBytes);
                if (backup?.Profile == null)
                    throw new Exception("Deserialization error");

                await DeleteAllData(); // Recycles the store -  now the rest better work!

                KeyMaterial64 masterRandomKey = new KeyMaterial64(backup.PlaintextMasterRandomKey);
                this.ixdsCryptoService.SymmetricKeyRepository.SetDeviceVaultRandomKey(masterRandomKey); // we can't write to the FStore before doing this

                KeyMaterial64 masterPassphraseKeyMaterial = CreateKeyMaterialFromPassphrase(newMasterPassphrase);
                await EncryptAndSaveDeviceVaultKey(masterRandomKey, masterPassphraseKeyMaterial, context);
                context.EncryptionProgress.Message = "Success";
                context.EncryptionProgress.Report(context.EncryptionProgress);

                await this.repo.AddProfile(backup.Profile);
                foreach (var c in backup.ContactIdentities)
                    await this.repo.AddContact(c);
            });

        }

        /// <summary>
        /// Stops the Chatworker and deletes/recreates the whole FStore.
        /// </summary>
        /// <returns></returns>
        public async Task DeleteAllData()
        {
            await this.chatWorker.StopRunLoopAndDisconnectAllAsync();
            await this.repo.DropRecreateStoreWithAllTables();
        }

        /// <summary>
        /// Gets the PassphraseQuality. 
        /// </summary>
        /// <param name="text">Passphrase</param>
        /// <returns>PassphraseQuality</returns>
        public PassphraseQuality GetPassphraseQuality(string text)
        {
            switch (text)
            {
                case var s when String.IsNullOrWhiteSpace(s):
                    return PassphraseQuality.None;
                case var s when s.Length < 9:
                    return PassphraseQuality.Low;
                case var s when s.Length < 43:
                    return PassphraseQuality.Medium;
                default:
                    return PassphraseQuality.High;
            }
        }

        /// <summary>
        /// Gets the PassphraseQuality string.
        /// </summary>
        /// <param name="q">PassphraseQuality</param>
        /// <returns>Display text.</returns>
        public string GetPassphraseQualityText(PassphraseQuality q)
        {
            switch (q)
            {
                case PassphraseQuality.Low:
                    return "Very weak passphrase!";
                case PassphraseQuality.Medium:
                    return "Weak passphrase.";
                case PassphraseQuality.High:
                    return "Congrats, good passphrase!";
                default:
                    return String.Empty;
            }
        }

        #region private & models

        KeyMaterial64 CreateKeyMaterialFromPassphrase(string passphrase)
        {
            NormalizedPassword normalizedPassword = this.ixdsCryptoService.NormalizePassword(passphrase).Result;
            KeyMaterial64 hashedPassword = this.ixdsCryptoService.HashPassword(normalizedPassword).Result;
            return hashedPassword;
        }

        async Task EncryptAndSaveDeviceVaultKey(KeyMaterial64 masterRandomKey, KeyMaterial64 masterPassphraseKeyMaterial, LongRunningOperationContext context)
        {
            context.EncryptionProgress.Message = "Encrypting master random key";
            context.EncryptionProgress.Report(context.EncryptionProgress);
            CipherV2 encryptedMasterRandomKey = this.ixdsCryptoService.BinaryEncrypt(new Clearbytes(masterRandomKey.GetBytes()), masterPassphraseKeyMaterial, new RoundsExponent(10), context).Result;
            byte[] encryptedMasterRandomKeyBytes = this.ixdsCryptoService.BinaryEncodeXDSSec(encryptedMasterRandomKey, context).Result;
            await this.repo.WriteSpecialFile(MasterKeyFilename, encryptedMasterRandomKeyBytes);
        }

        public class CreateBackupResult
        {
            public string BackupFileContentsInXDSSecFormat { get; set; }
            public string OwnProfileId { get; set; }
        }

        public class Backup
        {
            public int Version;
            public byte[] PlaintextMasterRandomKey;
            public Profile Profile;
            public List<Identity> ContactIdentities;
        }

        public class RestoreBackupModel
        {
            public string BackupInXDSSecFormat;
            public string BackupPassphrase;
            public string NewMasterPassphrase;
        }

        public enum PassphraseQuality
        {
            None = 0,
            Low = 1,
            Medium = 2,
            High = 3
        }

        #endregion
    }
}
