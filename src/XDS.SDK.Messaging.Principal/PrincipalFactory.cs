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
        readonly IXDSSecService ixdsCryptoService;

        /// <summary>
        /// A word list of 24 words is needed to encode 256 bits of entropy.
        /// </summary>
        public const int WordListLength = 24;

        public PrincipalFactory(IXDSSecService ixdsCryptoService)
        {
            this.ixdsCryptoService = ixdsCryptoService;
        }

        public byte[] WalletSeed { get; private set; }
        public string FirstSentence { get; private set; }

        public byte[] SecondSeed { get; private set; }
        public string SecondSentence { get; private set; }

        public byte[] MasterKey { get; private set; }
        public string MasterSentence { get; private set; }

        public void CreateMnemonics(byte[] preliminaryMasterRandomKeyMaterial)
        {
            preliminaryMasterRandomKeyMaterial.Check(64);

            WordList wordList = WordList.English;

            var first32 = new byte[32];
            Buffer.BlockCopy(preliminaryMasterRandomKeyMaterial, 0, first32, 0, 32);

            var firstMnemonic = new Mnemonic(wordList, first32);
            if (!firstMnemonic.IsValidChecksum)
                throw new InvalidOperationException("Invalid checksum.");

            this.WalletSeed = firstMnemonic.DeriveSeed();
            this.WalletSeed.Check(64);

            this.FirstSentence = firstMnemonic.ToString();
            CheckNumberOfWords(this.FirstSentence, WordListLength);

            var testRecovery1 = new Mnemonic(this.FirstSentence, WordList.English);
            if (testRecovery1.ToString() != this.FirstSentence ||
                !ByteArrays.AreAllBytesEqual(testRecovery1.DeriveSeed(), this.WalletSeed))
            {
                throw new InvalidOperationException("This seed cannot be recovered.");
            }

            var second32 = new byte[32];
            Buffer.BlockCopy(preliminaryMasterRandomKeyMaterial, 32, second32, 0, 32);

            var secondMnemonic = new Mnemonic(wordList, second32);
            if (!secondMnemonic.IsValidChecksum)
                throw new InvalidOperationException("Invalid checksum.");

            this.SecondSeed = secondMnemonic.DeriveSeed();
            this.SecondSeed.Check(64);

            this.SecondSentence = secondMnemonic.ToString();
            CheckNumberOfWords(this.SecondSentence, WordListLength);

            var testRecovery2 = new Mnemonic(this.SecondSentence, WordList.English);
            if (testRecovery2.ToString() != this.SecondSentence ||
                !ByteArrays.AreAllBytesEqual(testRecovery2.DeriveSeed(), this.SecondSeed))
            {
                throw new InvalidOperationException("This seed cannot be recovered.");
            }

            if (this.FirstSentence == this.SecondSentence)
                throw new InvalidOperationException("The first mnemonic cannot be the same as the second mnemonic.");

            this.MasterKey = new byte[128];
            Buffer.BlockCopy(this.WalletSeed, 0, this.MasterKey, 0, 64);
            Buffer.BlockCopy(this.SecondSeed, 0, this.MasterKey, 64, 64);
            this.MasterKey.Check(128);
            this.MasterSentence = $"{this.FirstSentence} {this.SecondSentence}";
            CheckNumberOfWords(this.MasterSentence, 2 * WordListLength);
        }


        public void CreateMnemonics(string masterSentence)
        {
            CheckNumberOfWords(masterSentence, 2 * WordListLength);

            this.FirstSentence = string.Join(" ", masterSentence.Split(" ").ToArray().Take(WordListLength));

            var firstMnemonic = new Mnemonic(this.FirstSentence, WordList.English);

            this.WalletSeed = firstMnemonic.DeriveSeed();

            this.FirstSentence = firstMnemonic.ToString();

            this.SecondSentence = string.Join(" ", masterSentence.Split(" ").ToArray().Skip(WordListLength).Take(WordListLength));

            var secondMnemonic = new Mnemonic(this.SecondSentence, WordList.English);

            this.SecondSeed = secondMnemonic.DeriveSeed();

            this.SecondSentence = secondMnemonic.ToString();

            if (masterSentence != $"{this.FirstSentence} {this.SecondSentence}")
                throw new InvalidOperationException("Error in recovery.");

            this.MasterSentence = masterSentence;
            this.MasterKey = new byte[128];
            Buffer.BlockCopy(this.WalletSeed, 0, this.MasterKey, 0, 64);
            Buffer.BlockCopy(this.SecondSeed, 0, this.MasterKey, 64, 64);
            this.MasterKey.Check(128);
        }

        public XDSPrincipal GetXDSPrincipal()
        {
            var identityKeyPair = CreateIdentityKeyPair();
            var chatId = ChatId.GenerateChatId(identityKeyPair.PublicKey);
            var address = CreateHdAddress(this.WalletSeed, 0, 0, 0);
            return new XDSPrincipal(this.MasterKey, this.WalletSeed, identityKeyPair, chatId, address);
        }

        public XDSPubKeyAddress CreateHdAddress(byte[] walletSeed, int accountIndex, int changePath,int addressIndex)
        {
            var keyPath = new KeyPath($"m/44'/{XDSPrincipal.XDSCoinType}'/{accountIndex}'/{changePath}/{addressIndex}");
            var seedExtKey = new ExtKey(walletSeed);
           var derivedKey = seedExtKey.Derive(keyPath);
            
            CompressedPubKey compressedPubKey = derivedKey.PrivateKey.CompressedPubKey;
           var hash = compressedPubKey.GetHash160();
           var bech = new Bech32Encoder("xds1");
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
            byte[] privateKeySeed;
            using (var sha256 = SHA256.Create())
            {
                privateKeySeed = sha256.ComputeHash(this.MasterKey);
            }

            var response = this.ixdsCryptoService.GenerateCurve25519KeyPairExact(privateKeySeed);
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
