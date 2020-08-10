using System;
using System.Diagnostics;
using XDS.SDK.Cryptography.Api;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Implementations;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.ECC;

namespace XDS.SDK.Cryptography.Simple
{
    public class VCL
    {
        static IXDSSecService _visualCrypt2Service;
        static object lockObject = new object();
        public static IXDSSecService Instance()
        {
            if (_visualCrypt2Service == null)
            {
                lock (lockObject)
                {
                    if (_visualCrypt2Service == null)
                    {
                        _visualCrypt2Service = new XDSSecService();
                        _visualCrypt2Service.Init(new Platform_NetStandard());
                    }
                }

            }
            return _visualCrypt2Service;
        }

        static ECKeyPair _ecKeyPair;
        public static ECKeyPair ECKeyPair
        {
            get
            {
                if (_ecKeyPair == null)
                {
                    lock (lockObject)
                    {
                        if (_ecKeyPair == null)
                            _ecKeyPair = Instance().GenerateCurve25519KeyPairExact(Instance().GetRandom(32).Result.X).Result;
                    }
                }
                return _ecKeyPair;
            }
        }

        public static byte[] DecryptClient(byte[] cipherV2Bytes, byte[] serverAuthPubKey, byte[] serverSessionPublicKey, byte[] singleClientPrivateKey)
        {
            var hashedSharedSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(singleClientPrivateKey, serverSessionPublicKey);
            var authSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(singleClientPrivateKey, serverAuthPubKey);

            var keyMaterial64 = ToKeyMaterial64(hashedSharedSecretBytes, authSecretBytes);

            CipherV2 cipherV2FromClient = VCL.Instance().BinaryDecodeXDSSec(cipherV2Bytes, VCL.GetContext()).Result;
            var binaryDecryptResponse = VCL.Instance().BinaryDecrypt(cipherV2FromClient, keyMaterial64, VCL.GetContext());
            if (!binaryDecryptResponse.IsSuccess && binaryDecryptResponse.Error == LocalizableStrings.MsgPasswordError)
                return null;
            return binaryDecryptResponse.Result.GetBytes();
        }

        public static byte[] Decrypt(byte[] cipherV2Bytes, byte[] publicKey, byte[] privateKey, byte[] privateAuthKey)
        {
            var hashedSharedSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateKey, publicKey);
            var authSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateAuthKey, publicKey);

            var keyMaterial64 = ToKeyMaterial64(hashedSharedSecretBytes, authSecretBytes);

            CipherV2 cipherV2FromClient = VCL.Instance().BinaryDecodeXDSSec(cipherV2Bytes, VCL.GetContext()).Result;
            var binaryDecryptResponse = VCL.Instance().BinaryDecrypt(cipherV2FromClient, keyMaterial64, VCL.GetContext());
            if (!binaryDecryptResponse.IsSuccess && binaryDecryptResponse.Error == LocalizableStrings.MsgPasswordError)
                return null;
            return binaryDecryptResponse.Result.GetBytes();
        }

        public static byte[] EncryptClient(byte[] plaintextBytes, byte[] serverAuthPubKey, byte[] serverSessionPublicKey, byte[] singleClientPrivateKey)
        {
            if (plaintextBytes == null)
                throw new ArgumentNullException(nameof(plaintextBytes));
            if (serverAuthPubKey == null)
                throw new ArgumentNullException(nameof(serverAuthPubKey));
            if (serverSessionPublicKey == null)
                throw new ArgumentNullException(nameof(serverSessionPublicKey));
            if (singleClientPrivateKey == null)
                throw new ArgumentNullException(nameof(singleClientPrivateKey));

            var hashedSharedSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(singleClientPrivateKey, serverSessionPublicKey);
            var authSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(singleClientPrivateKey, serverAuthPubKey);
            var keyMaterial64 = ToKeyMaterial64(hashedSharedSecretBytes, authSecretBytes);

            CipherV2 cipher = VCL.Instance().BinaryEncrypt(new Clearbytes(plaintextBytes), keyMaterial64, new RoundsExponent(RoundsExponent.DontMakeRounds), VCL.GetContext()).Result;
            return VCL.Instance().BinaryEncodeXDSSec(cipher, VCL.GetContext()).Result;
        }

        public static byte[] Encrypt(byte[] plaintextBytes, byte[] publicKey, byte[] privateKey, byte[] privateAuthKey)
        {
            if (plaintextBytes == null)
                throw new ArgumentNullException(nameof(plaintextBytes));
            if (publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));
            if (privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));

            var hashedSharedSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateKey, publicKey);
            var authSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateAuthKey, publicKey);
            var keyMaterial64 = ToKeyMaterial64(hashedSharedSecretBytes, authSecretBytes);

            CipherV2 cipher = VCL.Instance().BinaryEncrypt(new Clearbytes(plaintextBytes), keyMaterial64, new RoundsExponent(RoundsExponent.DontMakeRounds), VCL.GetContext()).Result;
            return VCL.Instance().BinaryEncodeXDSSec(cipher, VCL.GetContext()).Result;
        }


        public static byte[] EncryptWithPassphrase(string passphrase, byte[] bytesToEncryt)
        {
            var context = GetContext();
            NormalizedPassword normalizedPassword = Instance().NormalizePassword(passphrase).Result;
            KeyMaterial64 passwordDerivedkeyMaterial64 = Instance().HashPassword(normalizedPassword).Result;
            CipherV2 cipherV2 = Instance().BinaryEncrypt(new Clearbytes(bytesToEncryt), passwordDerivedkeyMaterial64, new RoundsExponent(RoundsExponent.DontMakeRounds), context).Result;
            var cipherV2Bytes = Instance().BinaryEncodeXDSSec(cipherV2, context).Result;
            return cipherV2Bytes;
        }

        public static byte[] DecryptWithPassphrase(string passphrase, byte[] bytesToDecrypt)
        {
            var context = GetContext();
            NormalizedPassword normalizedPassword = Instance().NormalizePassword(passphrase).Result;
            KeyMaterial64 passwordDerivedkeyMaterial64 = Instance().HashPassword(normalizedPassword).Result;
            CipherV2 cipherV2 = Instance().BinaryDecodeXDSSec(bytesToDecrypt, context).Result;
            var response = Instance().BinaryDecrypt(cipherV2, passwordDerivedkeyMaterial64, context);
            if (response.IsSuccess)
                return response.Result.GetBytes();
            return null;
        }


        static KeyMaterial64 ToKeyMaterial64(byte[] hashedSharedSecretBytes, byte[] authSecretBytes)
        {
            byte[] dest = new byte[64];
            Buffer.BlockCopy(hashedSharedSecretBytes, 0, dest, 0, 32);
            Buffer.BlockCopy(authSecretBytes, 0, dest, 32, 32);
            return new KeyMaterial64(dest);
        }

        static LongRunningOperationContext GetContext()
        {
            Action<EncryptionProgress> action = progress =>
            {
                Debug.WriteLine(progress.Message);
            };
            return new LongRunningOperation(action, () => { Debug.WriteLine("Done!"); }).Context;
        }


    }
}
