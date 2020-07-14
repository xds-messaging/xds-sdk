#nullable enable
using System;
using System.Diagnostics;
using XDS.SDK.Lib.Secp256k1;

namespace XDS.SDK.Lib.HDKeys
{
    public class PrivateKey : IDisposable
    {
        private const int KEY_SIZE = 32;





        internal ECPrivKey _ECKey;

        public bool IsCompressed
        {
            get;
            internal set;
        }

        
        internal PrivateKey(ECPrivKey ecKey, bool compressed)
        {
            if (ecKey == null)
                throw new ArgumentNullException(nameof(ecKey));
            this.IsCompressed = compressed;
            this._ECKey = ecKey;
        }

      
        public PrivateKey(byte[] data, int count = -1, bool fCompressedIn = true)
        {
            if (count == -1)
                count = data.Length;
            if (count != KEY_SIZE)
            {
                throw new ArgumentException(paramName: "data", message: $"The size of an EC key should be {KEY_SIZE}");
            }
            if (Context.Instance.TryCreateECPrivKey(data.AsSpan().Slice(0, KEY_SIZE), out var key) && key is ECPrivKey)
            {
                this.IsCompressed = fCompressedIn;
                this._ECKey = key;
            }
            else
                throw new ArgumentException(paramName: "data", message: "Invalid EC key");

        }

        public byte[] RawBytes
        {
            get { return this._ECKey.sec.ToBytes(); }
        }

        CompressedPubKey? _PubKey;

        public CompressedPubKey CompressedPubKey
        {
            get
            {
                AssertNotDiposed();
                if (this._PubKey is CompressedPubKey pubkey)
                    return pubkey;
                pubkey = new CompressedPubKey(this._ECKey.CreatePubKey());
                this._PubKey = pubkey;
                return pubkey;

            }
        }







        public PrivateKey Derivate(byte[] cc, uint nChild, out byte[] ccChild)
        {
            AssertNotDiposed();
            if (!this.IsCompressed)
                throw new InvalidOperationException("The key must be compressed");
            Span<byte> vout = stackalloc byte[64];
            vout.Clear();
            if ((nChild >> 31) == 0)
            {
                Span<byte> pubkey = this.CompressedPubKey.ToBytes().AsSpan();
                Debug.Assert(pubkey.Length == 33);
               
                Hashes.BIP32Hash(cc, nChild, pubkey[0], pubkey.Slice(1), vout);
            }
            else
            {
                Span<byte> privkey = stackalloc byte[32];
                this._ECKey.WriteToSpan(privkey);
                Hashes.BIP32Hash(cc, nChild, 0, privkey, vout);
                privkey.Fill(0);
            }
            ccChild = new byte[32];
            vout.Slice(32, 32).CopyTo(ccChild);
            ECPrivKey keyChild = this._ECKey.TweakAdd(vout.Slice(0, 32));
            vout.Clear();
            return new PrivateKey(keyChild, true);

        }

        public PrivateKey Uncover(PrivateKey scan, CompressedPubKey ephem)
        {
            AssertNotDiposed();
#if HAS_SPAN
            Span<byte> tmp = stackalloc byte[33];
            ephem.EcPubKey.GetSharedPubkey(scan._ECKey).WriteToSpan(true, tmp, out _);
            var c = Context.Instance.CreateECPrivKey(Hashes.Sha256(tmp.ToArray(),0,tmp.ToArray().Length));
            var priv = c.sec + this._ECKey.sec;
            return new PrivateKey(this._ECKey.ctx.CreateECPrivKey(priv), this.IsCompressed);
#else
			var curve = ECKey.Secp256k1;
			var priv = new BigInteger(1, CompressedPubKey.GetStealthSharedSecret(scan, ephem))
							.Add(new BigInteger(1, this.ToBytes()))
							.Mod(curve.N)
							.ToByteArrayUnsigned();

			if (priv.Length < 32)
				priv = new byte[32 - priv.Length].Concat(priv).ToArray();

			var key = new Key(priv, fCompressedIn: this.IsCompressed);
			return key;
#endif
        }




        void AssertNotDiposed()
        {
            if (this._ECKey.cleared)
                throw new ObjectDisposedException(nameof(PrivateKey));
        }
        public void Dispose()
        {
            this._ECKey.Clear();
        }

    }
}
#nullable disable
