using System;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public class DeviceVaultService
    {
        const string MasterKeyFilename = "key";

        readonly AppRepository repo;
        readonly IXDSSecService xdsCryptoService;

        public DeviceVaultService(AppRepository appRepository,IXDSSecService xdsCryptoService)
        {
            this.repo = appRepository;
            this.xdsCryptoService = xdsCryptoService;
        }

        /// <summary>
        /// Checks if onboarding is required or if the app is already setup.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CheckIsOnboardingRequiredAsync()
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
        public async Task TryLoadDecryptAndSetMasterRandomKeyAsync(string unprunedUtf16LeMasterPassword, LongRunningOperationContext context)
        {
            KeyMaterial64 masterPasswordHash = CreateKeyMaterialFromPassphrase(unprunedUtf16LeMasterPassword);

            byte[] encryptedRandomMasterKey = await this.repo.LoadSpecialFile(MasterKeyFilename);
            byte[] decryptedMasterRandomKey = this.xdsCryptoService.DefaultDecrypt(encryptedRandomMasterKey, masterPasswordHash, context);

            this.xdsCryptoService.SymmetricKeyRepository.SetDeviceVaultRandomKey(new KeyMaterial64(decryptedMasterRandomKey));
        }

        /// <summary>
        /// Clears the decrypted master random key from memory.
        /// </summary>
        public void ClearMasterRandomKey()
        {
            this.xdsCryptoService.SymmetricKeyRepository.ClearMasterRandomKey();
        }

        /// <summary>
        /// Creates, encrypts and saves the master random key during the onboarding process.
        /// </summary>
        /// <param name="newMasterPassphrase">The newly chosen master passphrase.</param>
        /// <param name="masterKey">The master random key material, including collected user random.</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InitDeviceVaultKeyAsync(string newMasterPassphrase, byte[] masterKey, LongRunningOperationContext context)
        {
            KeyMaterial64 masterPassphraseKeyMaterial = CreateKeyMaterialFromPassphrase(newMasterPassphrase);

            byte[] deviceVaultKey = new byte[64];
            Buffer.BlockCopy(masterKey, 64, deviceVaultKey, 0, 64); // use the second part, so that we do not use the wallet seed material.
            deviceVaultKey = this.xdsCryptoService.ComputeSHA512(deviceVaultKey); // hash it, because we have a new specific purpose and do not want key reuse.

            KeyMaterial64 deviceVaultKeyMaterial = new KeyMaterial64(deviceVaultKey);
            this.xdsCryptoService.SymmetricKeyRepository.SetDeviceVaultRandomKey(deviceVaultKeyMaterial);
            await Task.Run(async () =>
            {
                await EncryptAndSaveDeviceVaultKeyAsync(deviceVaultKeyMaterial, masterPassphraseKeyMaterial, context);
            });

        }

        /// <summary>
        /// Changes the master password by encrypting the master random key with a new password and updating the key file.
        /// </summary>
        /// <param name="currentMasterPassword">The current master password.</param>
        /// <param name="futureMasterPassword">The future master password.</param>
        /// <returns></returns>
        public async Task ChangeMasterPasswordAsync(string currentMasterPassword, string futureMasterPassword)
        {
            // Verify the user knows the current master password
            await TryLoadDecryptAndSetMasterRandomKeyAsync(currentMasterPassword, null);

            KeyMaterial64 futureMasterPasswordHash = CreateKeyMaterialFromPassphrase(futureMasterPassword);

            // get the current master random key in plaintext
            KeyMaterial64 currentPlaintextMasterRandomKey = this.xdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey();

            // encrypt the current master random key for saving in the key file and write the file.
            CipherV2 encryptedMasterRandomKey = this.xdsCryptoService.BinaryEncrypt(new Clearbytes(currentPlaintextMasterRandomKey.GetBytes()), futureMasterPasswordHash, new RoundsExponent(10), null).Result;
            byte[] encryptedMasterRandomKeyBytes = this.xdsCryptoService.BinaryEncodeXDSSec(encryptedMasterRandomKey, null).Result;
            await this.repo.WriteSpecialFile(MasterKeyFilename, encryptedMasterRandomKeyBytes);
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
                case var s when string.IsNullOrWhiteSpace(s):
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
                    return string.Empty;
            }
        }

        #region private & models

        KeyMaterial64 CreateKeyMaterialFromPassphrase(string passphrase)
        {
            NormalizedPassword normalizedPassword = this.xdsCryptoService.NormalizePassword(passphrase).Result;
            KeyMaterial64 hashedPassword = this.xdsCryptoService.HashPassword(normalizedPassword).Result;
            return hashedPassword;
        }

        async Task EncryptAndSaveDeviceVaultKeyAsync(KeyMaterial64 masterRandomKey, KeyMaterial64 masterPassphraseKeyMaterial, LongRunningOperationContext context)
        {
            context.EncryptionProgress.Message = "Encrypting master random key";
            context.EncryptionProgress.Report(context.EncryptionProgress);
            CipherV2 encryptedMasterRandomKey = this.xdsCryptoService.BinaryEncrypt(new Clearbytes(masterRandomKey.GetBytes()), masterPassphraseKeyMaterial, new RoundsExponent(10), context).Result;
            byte[] encryptedMasterRandomKeyBytes = this.xdsCryptoService.BinaryEncodeXDSSec(encryptedMasterRandomKey, context).Result;
            await this.repo.WriteSpecialFile(MasterKeyFilename, encryptedMasterRandomKeyBytes);
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
