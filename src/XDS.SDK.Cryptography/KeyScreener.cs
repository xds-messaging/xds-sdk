using System;
using System.Linq;

namespace XDS.SDK.Cryptography
{
    public static class KeyScreener
    {
        public static void Check(this byte[] cryptographicBytes, int expectedLength)
        {
            if (cryptographicBytes == null || cryptographicBytes.Length != expectedLength || cryptographicBytes.All(b => b == cryptographicBytes[0]))
            {
                var display = cryptographicBytes == null ? "null" : cryptographicBytes.ToHexString();
                throw new ArgumentException($"Unlikely cryptographic material of expected length {expectedLength}: {display}", nameof(cryptographicBytes));
            }
        }
    }
}
