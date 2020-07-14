using System;
using System.Diagnostics;
using XDS.SDK.Lib.Secp256k1;

namespace XDS.SDK.Lib.HDKeys
{
    public class CompressedPubKey
    {
        public readonly ECPubKey EcPubKey;

        /// <summary>
        /// Create a new public key from a byte array, that contains a public key.
        /// </summary>
        /// <param name="bytes">public key</param>
        public CompressedPubKey(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length != 33)
                throw new ArgumentException("A compressed public key must have a length of 33 bytes");

            var success = Context.Instance.TryCreatePubKey(bytes, out var compressed, out this.EcPubKey);

            if (!success || this.EcPubKey is null || !compressed)
                throw new FormatException("Invalid public key");

        }


        public CompressedPubKey(ECPubKey ecPubKey)
        {
            if (ecPubKey == null)
                throw new ArgumentNullException(nameof(ecPubKey));
            this.EcPubKey = ecPubKey;
        }

        public byte[] GetHash160()
        {
            Span<byte> tmp = stackalloc byte[33];
            this.EcPubKey.WriteToSpan(true, tmp, out int len);
            Debug.Assert(tmp.Length == len);
            return Hashes.Hash160(tmp.ToArray(), 0, tmp.Length);
        }

        public byte[] ToBytes()
        {
            return this.EcPubKey.ToBytes(true);
        }
       
    }
}
