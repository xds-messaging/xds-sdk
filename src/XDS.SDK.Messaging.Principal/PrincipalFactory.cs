using System;
using System.Linq;
using System.Security.Cryptography;
using XDS.SDK.Cryptography;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.ECC;
using XDS.SDK.Lib.Bech32;
using XDS.SDK.Lib.HDKeys;
using XDS.SDK.Lib.HDKeys.BIP32;
using XDS.SDK.Lib.HDKeys.BIP39;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.SDK.Messaging.Principal
{
    public sealed class PrincipalFactory
    {
        readonly IXDSSecService xdsCryptoService;

        /// <summary>
        /// A word list of 24 words is needed to encode 256 bits of entropy.
        /// </summary>
        public const int WordListLength = 24;

        public PrincipalFactory(IXDSSecService xdsCryptoService)
        {
            this.xdsCryptoService = xdsCryptoService;
        }

        public byte[] WalletSeed { get; private set; }
        public string WalletSentence { get; private set; }

        public byte[] SecondSeed { get; private set; }
        public string SecondSentence { get; private set; }

        public byte[] ThirdSeed { get; private set; }
        public string ThirdSentence { get; private set; }

        public byte[] MasterKey { get; private set; }
        public string MasterSentence { get; private set; }

        public void CreateMnemonics(byte[] preliminaryMasterRandomKeyMaterial)
        {
            preliminaryMasterRandomKeyMaterial.Check(3 * 32);

            (this.WalletSeed, this.WalletSentence) = CreateHdSecrets(preliminaryMasterRandomKeyMaterial, 0 * 32);
            (this.SecondSeed, this.SecondSentence) = CreateHdSecrets(preliminaryMasterRandomKeyMaterial, 1 * 32);
            (this.ThirdSeed, this.ThirdSentence) = CreateHdSecrets(preliminaryMasterRandomKeyMaterial, 2 * 32);

            if (this.WalletSentence == this.SecondSentence)
                throw new InvalidOperationException("All three sentences must be different.");
            if (this.WalletSentence == this.ThirdSentence || this.SecondSentence == this.ThirdSentence)
                throw new InvalidOperationException("All three sentences must be different.");

            this.MasterKey = new byte[3 + 64];
            Buffer.BlockCopy(this.WalletSeed, 0, this.MasterKey, 0 * 64, 64);
            Buffer.BlockCopy(this.SecondSeed, 0, this.MasterKey, 1 * 64, 64);
            Buffer.BlockCopy(this.ThirdSeed, 0, this.MasterKey, 2 * 64, 64);
            this.MasterKey.Check(3 * 64);
            this.MasterSentence = $"{this.WalletSentence} {this.SecondSentence} {this.ThirdSentence}";
            CheckNumberOfWords(this.MasterSentence, 3 * WordListLength);
        }

        static (byte[] seed, string sentence) CreateHdSecrets(byte[] sourceKeyMaterial, int startIndex)
        {
            WordList wordList = WordList.English;

            var usedKeyMaterial = new byte[32];
            Buffer.BlockCopy(sourceKeyMaterial, startIndex, usedKeyMaterial, 0, 32);

            var mnemonic = new Mnemonic(wordList, usedKeyMaterial);
            if (!mnemonic.IsValidChecksum)
                throw new InvalidOperationException("Invalid checksum.");

            var seed = mnemonic.DeriveSeed();
            seed.Check(64);

            var sentence = mnemonic.ToString();
            CheckNumberOfWords(sentence, WordListLength);

            var testRecovery = new Mnemonic(sentence, WordList.English);
            if (testRecovery.ToString() != sentence ||
                !ByteArrays.AreAllBytesEqual(testRecovery.DeriveSeed(), seed))
            {
                throw new InvalidOperationException("This seed cannot be recovered.");
            }

            return (seed, sentence);
        }


        public void CreateMnemonics(string masterSentence)
        {
            CheckNumberOfWords(masterSentence, 3 * WordListLength);

            this.WalletSentence = string.Join(" ", masterSentence.Split(" ").ToArray().Take(WordListLength));

            var firstMnemonic = new Mnemonic(this.WalletSentence, WordList.English);

            this.WalletSeed = firstMnemonic.DeriveSeed();

            this.WalletSentence = firstMnemonic.ToString();

            this.SecondSentence = string.Join(" ", masterSentence.Split(" ").ToArray().Skip(WordListLength).Take(WordListLength));

            var secondMnemonic = new Mnemonic(this.SecondSentence, WordList.English);

            this.SecondSeed = secondMnemonic.DeriveSeed();

            this.SecondSentence = secondMnemonic.ToString();



            this.ThirdSentence = string.Join(" ", masterSentence.Split(" ").ToArray().Skip(WordListLength * 2).Take(WordListLength));

            var thirdMnemonic = new Mnemonic(this.ThirdSentence, WordList.English);

            this.ThirdSeed = thirdMnemonic.DeriveSeed();

            this.ThirdSentence = secondMnemonic.ToString();

            if (masterSentence != $"{this.WalletSentence} {this.SecondSentence} {this.ThirdSentence}")
                throw new InvalidOperationException("Error in recovery.");

            this.MasterSentence = masterSentence;
            this.MasterKey = new byte[3 * 64];
            Buffer.BlockCopy(this.WalletSeed, 0, this.MasterKey, 0 * 64, 64);
            Buffer.BlockCopy(this.SecondSeed, 0, this.MasterKey, 1 * 64, 64);
            Buffer.BlockCopy(this.ThirdSeed, 0, this.MasterKey, 2 * 64, 64);
            this.MasterKey.Check(3 * 64);
        }

        public XDSPrincipal GetXDSPrincipal()
        {
            var identityKeyPair = CreateIdentityKeyPair();
            var chatId = ChatId.GenerateChatId(identityKeyPair.PublicKey);
            var address = CreateHdAddress(this.WalletSeed, 0, 0, 0);
            return new XDSPrincipal(this.MasterKey, this.WalletSeed, identityKeyPair, chatId, address);
        }

        public XDSPubKeyAddress CreateHdAddress(byte[] walletSeed, int accountIndex, int changePath, int addressIndex)
        {
            var keyPath = new KeyPath($"m/44'/{XDSPrincipal.XDSCoinType}'/{accountIndex}'/{changePath}/{addressIndex}");
            var seedExtKey = new ExtKey(walletSeed);
            var derivedKey = seedExtKey.Derive(keyPath);

            CompressedPubKey compressedPubKey = derivedKey.PrivateKey.CompressedPubKey;
            var hash = compressedPubKey.GetHash160();
            var bech = new Bech32Encoder("xds");
            var address = bech.Encode(0, hash);
            return new XDSPubKeyAddress
            {
                PrivateKey = derivedKey.PrivateKey.RawBytes,
                PublicKey = derivedKey.PrivateKey.CompressedPubKey.ToBytes(),
                Hash = hash,
                KeyPath = keyPath.ToString(),
                Address = address,
                ScriptPubKey = ByteArrays.Concatenate(new byte[1], hash)
            };
        }

        ECKeyPair CreateIdentityKeyPair()
        {
            byte[] identityKeyPairKeyMaterial = new byte[128]; // 128 bytes = 64 byte entropy (512 bits). Required for Curve E520.
            Buffer.BlockCopy(this.SecondSeed, 0, identityKeyPairKeyMaterial, 0, 64);
            Buffer.BlockCopy(this.ThirdSeed, 0, identityKeyPairKeyMaterial, 64, 64);

            byte[] privateKeySeed;
            using (var sha256 = SHA256.Create())
            {
                privateKeySeed = sha256.ComputeHash(identityKeyPairKeyMaterial); // but currently used is Curve 25519, so we compress this to 256 bits.
            }

            var response = this.xdsCryptoService.GenerateCurve25519KeyPairExact(privateKeySeed);
            if (response.IsSuccess)
                return response.Result;
            throw new InvalidOperationException(response.Error);
        }

        static void CheckNumberOfWords(string sentence, int expectedWords)
        {
            if (sentence.Split(" ").Length != expectedWords)
                throw new ArgumentException("Unexpected number of words.", nameof(sentence));
        }


    }
}
