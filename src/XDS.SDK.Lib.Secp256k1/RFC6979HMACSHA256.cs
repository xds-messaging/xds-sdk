#if HAS_SPAN
#nullable enable
using System;

namespace XDS.SDK.Lib.Secp256k1
{
#if SECP256K1_LIB
	public
#else
	internal
#endif
	class RFC6979HMACSHA256 : IDisposable
	{
		byte[]? v;
		byte[]? k;
		bool retry;

		public void Initialize(ReadOnlySpan<byte> key)
		{
			Span<byte> one = stackalloc byte[1];
			one[0] = 1;
			Span<byte> zero = stackalloc byte[1];
			zero[0] = 0;
			this.v = new byte[32];
			this.k = new byte[32];

			this.v.AsSpan().Fill(1); /* RFC6979 3.2.b. */
			this.k.AsSpan().Fill(0); /* RFC6979 3.2.c. */

			using var hmac = new HMACSHA256();
			/* RFC6979 3.2.d. */
			hmac.Initialize(this.k);
			hmac.Write32(this.v);
			hmac.Write32(zero);
			hmac.Write32(key);
			hmac.Finalize(this.k);
			hmac.Initialize(this.k);
			hmac.Write32(this.v);
			hmac.Finalize(this.v);

			/* RFC6979 3.2.f. */
			hmac.Initialize(this.k);
			hmac.Write32(this.v);
			hmac.Write32(one);
			hmac.Write32(key);
			hmac.Finalize(this.k);
			hmac.Initialize(this.k);
			hmac.Write32(this.v);
			hmac.Finalize(this.v);
			this.retry = false;
		}

		public void Generate(Span<byte> output)
		{
			/* RFC6979 3.2.h. */
			Span<byte> zero = stackalloc byte[1];
			zero[0] = 0;
			var outlen = output.Length;
			using var hmac = new HMACSHA256();
			if (this.retry)
			{
				hmac.Initialize(this.k);
				hmac.Write32(this.v);
				hmac.Write32(zero);
				hmac.Finalize(this.k);
				hmac.Initialize(this.k);
				hmac.Write32(this.v);
				hmac.Finalize(this.v);
			}

			while (outlen > 0)
			{
				int now = outlen;
				hmac.Initialize(this.k);
				hmac.Write32(this.v);
				hmac.Finalize(this.v);
				if (now > 32)
				{
					now = 32;
				}
				this.v.AsSpan().Slice(0, now).CopyTo(output);
				output = output.Slice(now);
				outlen -= now;
			}
			this.retry = true;
		}
		public void Dispose()
		{
			this.k?.AsSpan().Fill(0);
			this.v?.AsSpan().Fill(0);
			this.retry = false;
		}
	}
}
#nullable restore
#endif
