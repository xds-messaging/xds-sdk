#if HAS_SPAN
#nullable enable
using System;

namespace XDS.SDK.Lib.Secp256k1
{
	public class HMACSHA256 : IDisposable
	{
		Secp256k1.SHA256? inner, outer;
		public HMACSHA256()
		{

		}
		public HMACSHA256(ReadOnlySpan<byte> key)
		{
			Initialize(key);
		}

		public void Initialize(ReadOnlySpan<byte> key)
		{
			int n;
			Span<byte> rkey = stackalloc byte[64];
			rkey.Clear();
			if (key.Length <= 64)
			{
				key.CopyTo(rkey);
			}
			else
			{
				using var sha = new SHA256();
				sha.Write(key);
				sha.GetHash(rkey);
			}
			this.outer = new Secp256k1.SHA256();
			for (n = 0; n < 64; n++)
			{
				rkey[n] ^= 0x5c;
			}
			this.outer.Write(rkey);

			this.inner = new Secp256k1.SHA256();
			for (n = 0; n < 64; n++)
			{
				rkey[n] ^= 0x5c ^ 0x36;
			}
			this.inner.Write(rkey);
			rkey.Clear();
		}

		public void Write32(ReadOnlySpan<byte> data)
		{
			if (this.inner is null)
				throw new InvalidOperationException("You need to call HMACSHA256.Initialize first");
			this.inner.Write(data);
		}

		public void Finalize(Span<byte> output)
		{
			if (this.inner is null || this.outer is null)
				throw new InvalidOperationException("You need to call HMACSHA256.Initialize first");
			Span<byte> temp = stackalloc byte[32];
			this.inner.GetHash(temp);
			this.outer.Write(temp);
			temp.Clear();
			this.outer.GetHash(output);
		}

		public void Dispose()
		{
			this.inner?.Dispose();
			this.outer?.Dispose();
		}
	}
}
#nullable restore
#endif
