using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace XDS.SDK.Lib.HDKeys.BIP39
{
	/// <summary>
	/// A .NET implementation of the Bitcoin Improvement Proposal - 39 (BIP39)
	/// BIP39 specification used as reference located here: https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki
	/// Made by thashiznets@yahoo.com.au
	/// v1.0.1.1
	/// I ♥ Bitcoin :)
	/// Bitcoin:1ETQjMkR1NNh4jwLuN5LxY7bMsHC9PUPSV
	/// </summary>
	public class Mnemonic
	{
		public Mnemonic(string mnemonic, WordList wordList = null)
		{
			if (mnemonic == null)
				throw new ArgumentNullException(nameof(mnemonic));
			this._Mnemonic = mnemonic.Trim();

			if (wordList == null)
				wordList = BIP39.WordList.AutoDetect(mnemonic) ?? BIP39.WordList.English;

			var words = mnemonic.Split(new char[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
			//if the sentence is not at least 12 characters or cleanly divisible by 3, it is bad!
			if (!CorrectWordCount(words.Length))
			{
				throw new FormatException("Word count should be 12,15,18,21 or 24");
			}
			this._Words = words;
			this._WordList = wordList;
			this._Indices = wordList.ToIndices(words);
		}

		/// <summary>
		/// Generate a mnemonic
		/// </summary>
		/// <param name="wordList"></param>
		/// <param name="entropy"></param>
		public Mnemonic(WordList wordList, byte[] entropy)
		{
			wordList = wordList ?? BIP39.WordList.English;
			this._WordList = wordList;
			if (entropy == null || entropy.Length != 32)
				throw new ArgumentException("32 bytes entropy are required.");

			var i = Array.IndexOf(entArray, entropy.Length * 8);
			if (i == -1)
				throw new ArgumentException("The length for entropy should be : " + String.Join(",", entArray), "entropy");

			int cs = csArray[i];
			byte[] checksum = Hashes.Sha256(entropy,0,entropy.Length);
			BitWriter entcsResult = new BitWriter();

			entcsResult.Write(entropy);
			entcsResult.Write(checksum, cs);
			this._Indices = entcsResult.ToIntegers();
			this._Words = this._WordList.GetWords(this._Indices);
			this._Mnemonic = this._WordList.GetSentence(this._Indices);
		}

		

	

		static readonly int[] msArray = new[] { 12, 15, 18, 21, 24 };
		static readonly int[] csArray = new[] { 4, 5, 6, 7, 8 };
		static readonly int[] entArray = new[] { 128, 160, 192, 224, 256 };

		bool? _IsValidChecksum;
		public bool IsValidChecksum
		{
			get
			{
				if (this._IsValidChecksum == null)
				{
					int i = Array.IndexOf(msArray, this._Indices.Length);
					int cs = csArray[i];
					int ent = entArray[i];

					BitWriter writer = new BitWriter();
					var bits = BIP39.WordList.ToBits(this._Indices);
					writer.Write(bits, ent);
					var entropy = writer.ToBytes();
					var checksum = Hashes.Sha256(entropy,0,entropy.Length);

					writer.Write(checksum, cs);
					var expectedIndices = writer.ToIntegers();
					this._IsValidChecksum = expectedIndices.SequenceEqual(this._Indices);
				}
				return this._IsValidChecksum.Value;
			}
		}

		private static bool CorrectWordCount(int ms)
		{
			return msArray.Any(_ => _ == ms);
		}


		// FIXME: this method is not used. Shouldn't we delete it?
		private int ToInt(BitArray bits)
		{
			if (bits.Length != 11)
			{
				throw new InvalidOperationException("should never happen, bug in nbitcoin");
			}

			int number = 0;
			int base2Divide = 1024; //it's all downhill from here...literally we halve this for each bit we move to.

			//literally picture this loop as going from the most significant bit across to the least in the 11 bits, dividing by 2 for each bit as per binary/base 2
			foreach (bool b in bits)
			{
				if (b)
				{
					number = number + base2Divide;
				}

				base2Divide = base2Divide / 2;
			}

			return number;
		}

		private readonly WordList _WordList;
		public WordList WordList
		{
			get
			{
				return this._WordList;
			}
		}

		private readonly int[] _Indices;
		public int[] Indices
		{
			get
			{
				return this._Indices;
			}
		}
		private readonly string[] _Words;
		public string[] Words
		{
			get
			{
				return this._Words;
			}
		}

		static Encoding NoBOMUTF8 = new UTF8Encoding(false);
		public byte[] DeriveSeed(string passphrase = null)
		{
			passphrase = passphrase ?? "";
			var salt = Concat(NoBOMUTF8.GetBytes("mnemonic"), Normalize(passphrase));
			var bytes = Normalize(this._Mnemonic);
#if NO_NATIVE_HMACSHA512
			var mac = new NBitcoin.BouncyCastle.Crypto.Macs.HMac(new NBitcoin.BouncyCastle.Crypto.Digests.Sha512Digest());
			mac.Init(new NBitcoin.BouncyCastle.Crypto.Parameters.KeyParameter(bytes));
			return Pbkdf2.ComputeDerivedKey(mac, salt, 2048, 64);
#elif NO_NATIVE_RFC2898_HMACSHA512
			return NBitcoin.Crypto.Pbkdf2.ComputeDerivedKey(new System.Security.Cryptography.HMACSHA512(bytes), salt, 2048, 64);
#else
			using System.Security.Cryptography.Rfc2898DeriveBytes derive = new System.Security.Cryptography.Rfc2898DeriveBytes(bytes, salt, 2048, System.Security.Cryptography.HashAlgorithmName.SHA512);
			return derive.GetBytes(64);
#endif

		}

		internal static byte[] Normalize(string str)
		{
			return NoBOMUTF8.GetBytes(NormalizeString(str));
		}

		internal static string NormalizeString(string word)
		{
#if !NOSTRNORMALIZE
			if (!SupportOsNormalization())
			{
				return KDTable.NormalizeKD(word);
			}
			else
			{
				return word.Normalize(NormalizationForm.FormKD);
			}
#else
			return KDTable.NormalizeKD(word);
#endif
		}

#if !NOSTRNORMALIZE
		static bool? _SupportOSNormalization;
		internal static bool SupportOsNormalization()
		{
			if (_SupportOSNormalization == null)
			{
				var notNormalized = "あおぞら";
				var normalized = "あおぞら";
				if (notNormalized.Equals(normalized, StringComparison.Ordinal))
				{
					_SupportOSNormalization = false;
				}
				else
				{
					try
					{
						_SupportOSNormalization = notNormalized.Normalize(NormalizationForm.FormKD).Equals(normalized, StringComparison.Ordinal);
					}
					catch { _SupportOSNormalization = false; }
				}
			}
			return _SupportOSNormalization.Value;
		}
#endif

		

		static Byte[] Concat(Byte[] source1, Byte[] source2)
		{
			//Most efficient way to merge two arrays this according to http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
			Byte[] buffer = new Byte[source1.Length + source2.Length];
			System.Buffer.BlockCopy(source1, 0, buffer, 0, source1.Length);
			System.Buffer.BlockCopy(source2, 0, buffer, source1.Length, source2.Length);

			return buffer;
		}


		string _Mnemonic;
		public override string ToString()
		{
			return this._Mnemonic;
		}


	}
}
#pragma warning restore CS0618 // Type or member is obsolete
