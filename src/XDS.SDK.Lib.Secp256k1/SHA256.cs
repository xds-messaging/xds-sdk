#if HAS_SPAN
#nullable enable
using System;
using System.Security.Cryptography;

namespace XDS.SDK.Lib.Secp256k1
{
#if SECP256K1_LIB
	public
#endif
	class SHA256 : IDisposable
	{
		SHA256Managed sha = new SHA256Managed();
		int _Pos;
		byte[] _Buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64);
		public void Write(ReadOnlySpan<byte> buffer)
		{
			int copied = 0;
			int toCopy = 0;
			var innerSpan = new Span<byte>(this._Buffer, this._Pos, this._Buffer.Length - this._Pos);
			while (!buffer.IsEmpty)
			{
				toCopy = Math.Min(innerSpan.Length, buffer.Length);
				buffer.Slice(0, toCopy).CopyTo(innerSpan.Slice(0, toCopy));
				buffer = buffer.Slice(toCopy);
				innerSpan = innerSpan.Slice(toCopy);
				copied += toCopy;
				this._Pos += toCopy;
				if (ProcessBlockIfNeeded())
					innerSpan = this._Buffer.AsSpan();
			}
		}
		private bool ProcessBlockIfNeeded()
		{
			if (this._Pos == this._Buffer.Length)
			{
				ProcessBlock();
				return true;
			}
			return false;
		}
		private void ProcessBlock()
		{
			this.sha.TransformBlock(this._Buffer, 0, this._Pos, null, -1);
			this._Pos = 0;
		}
		public void GetHash(Span<byte> output)
		{
			ProcessBlock();
			this.sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			var hash1 = this.sha.Hash;
			hash1.AsSpan().CopyTo(output);
		}

		public void Dispose()
		{
			System.Buffers.ArrayPool<byte>.Shared.Return(this._Buffer, true);
			this.sha.Dispose();
		}
	}
}
#endif
