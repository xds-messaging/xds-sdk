using System;
using System.Linq;
using System.Text;
using XDS.SDK.Lib.Secp256k1;

#if !NO_BC
#endif

namespace XDS.SDK.Lib.HDKeys.BIP32
{
    /// <summary>
	/// A private Hierarchical Deterministic key
	/// </summary>
	public class ExtKey
	{
		const int ChainCodeLength = 32;

        /// <summary>
        /// The hash is by convention keyed with the string "Bitcoin seed".
        /// https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki
        /// </summary>
        static readonly byte[] hashKey = Encoding.ASCII.GetBytes("Bitcoin seed");

		PrivateKey privateKey;
		byte[] chainCode = new byte[ChainCodeLength];
        uint nChild;
		byte nDepth;

		

		/// <summary>
		/// Gets the depth of this extended key from the root key.
		/// </summary>
		public byte Depth
		{
			get
			{
				return this.nDepth;
			}
		}

		/// <summary>
		/// Gets the child number of this key (in reference to the parent).
		/// </summary>
		public uint Child
		{
			get
			{
				return this.nChild;
			}
		}

		public byte[] ChainCode
		{
			get
			{
				byte[] chainCodeCopy = new byte[ChainCodeLength];
				Buffer.BlockCopy(this.chainCode, 0, chainCodeCopy, 0, ChainCodeLength);

				return chainCodeCopy;
			}
		}

		public ExtKey() { }

		
		

		

		/// <summary>
		/// Constructor. Creates an extended key from the private key, with the specified value
		/// for chain code. Depth, fingerprint, and child number, will have their default values.
		/// </summary>
		public ExtKey(PrivateKey masterKey, byte[] chainCode)
		{
            if (chainCode == null)
				throw new ArgumentNullException(nameof(chainCode));
			if (chainCode.Length != ChainCodeLength)
				throw new ArgumentException($"The chain code must be {ChainCodeLength} bytes.", nameof(chainCode));

			this.privateKey = masterKey ?? throw new ArgumentNullException(nameof(masterKey));
			Buffer.BlockCopy(chainCode, 0, this.chainCode, 0, ChainCodeLength);
		}

		/// <summary>
		/// Constructor. Creates a new extended key from the specified seed bytes.
		/// </summary>
		public ExtKey(byte[] seed)
		{
			SetMaster(seed.ToArray());
		}

		/// <summary>
		/// Constructor. Creates a new extended key from the specified seed bytes.
		/// </summary>
		public ExtKey(ReadOnlySpan<byte> seed)
		{
			SetMaster(seed);
		}

		void SetMaster(ReadOnlySpan<byte> seed)
		{
			Span<byte> hashMAC = stackalloc byte[64];
			if (Hashes.HMACSHA512(hashKey, seed, hashMAC, out int len) && len == 64 &&
				Context.Instance.TryCreateECPrivKey(hashMAC.Slice(0, 32), out ECPrivKey k) && !(k is null))
			{
				this.privateKey = new PrivateKey(k, true);
				hashMAC.Slice(32, ChainCodeLength).CopyTo(this.chainCode);
				hashMAC.Clear();
			}
			else
			{
				throw new InvalidOperationException("Invalid ExtKey (this should never happen)");
			}
		}

		/// <summary>
		/// Get the private key of this extended key.
		/// </summary>
		public PrivateKey PrivateKey
		{
			get
			{
				return this.privateKey;
			}
		}

		/// <summary>
        /// Derives a new extended key in the hierarchy at the given path below the current key,
        /// by deriving the specified child at each step.
        /// </summary>
        public ExtKey Derive(KeyPath derivation)
        {
            ExtKey result = this;
            return derivation.Indexes.Aggregate(result, (current, index) => current.Derive(index));
        }

        /// <summary>
        /// Derives a new extended key in the hierarchy as the given child number.
        /// </summary>
        public ExtKey Derive(uint index)
        {
            var result = new ExtKey
            {
                nDepth = (byte)(this.nDepth + 1),
                nChild = index
            };
            result.privateKey = this.privateKey.Derivate(this.chainCode, index, out result.chainCode);
            return result;
        }

		/// <summary>
		/// Gets whether or not this extended key is a hardened child.
		/// </summary>
		public bool IsHardened
		{
			get
			{
				return (this.nChild & 0x80000000u) != 0;
			}
		}
	}
}
