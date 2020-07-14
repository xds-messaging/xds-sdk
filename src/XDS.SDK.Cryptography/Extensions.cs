using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XDS.SDK.Cryptography
{
	public static class Extensions
	{
		static readonly Dictionary<byte, byte[]> Utf8HexTable = new Dictionary<byte, byte[]>();
		public static readonly Dictionary<ushort, byte> Utf8HexTableReverse = new Dictionary<ushort, byte>();

		static readonly Dictionary<byte, string> HexTable = new Dictionary<byte, string>();
		static readonly Dictionary<string, byte> HexTable2 = new Dictionary<string, byte>();

		static readonly object lockObject = new object();

		public static string ToBase64(this byte[] bytes)
		{
			return Convert.ToBase64String(bytes);
		}

		public static byte[] FromBase64(this string base64)
		{
			return Convert.FromBase64String(base64);
		}

		public static byte[] ToUTF8Bytes(this string text)
		{
			return Encoding.UTF8.GetBytes(text);
		}

		public static string FromUTF8Bytes(this byte[] utf8Bytes)
		{
			return Encoding.UTF8.GetString(utf8Bytes);
		}

		public static string ToHexString(this byte[] bytes)
		{
			EnsureHexTable();

			var hexString = "";
			foreach (byte b in bytes)
				hexString += HexTable[b];
			return hexString;
		}




		private static string ByteArrayToHex(byte[] bytes)
		{
			return BitConverter.ToString(bytes).Replace("-", "");
		}
		private static byte[] HexToByteArray(string hex)
		{
			return System.Linq.Enumerable.Range(0, hex.Length)
				.Where(x => x % 2 == 0)
				.Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
				.ToArray();
		}


		public static byte[] ToUtf8Hex(this byte[] bytes)
		{
			EnsureHexTable();

			var utf8Length = bytes.Length * 2;
			var utf8MaxIndex = utf8Length - 1;
			var utf8MaxIndexPrev = utf8Length - 2;

			var utf8 = new byte[utf8Length];

			for (var i = 0; i < bytes.Length; i++)
			{
				byte[] utf8Bytes = Utf8HexTable[bytes[i]];
				var i2 = 2 * i;
				utf8[utf8MaxIndex - i2] = utf8Bytes[1];
				utf8[utf8MaxIndexPrev - i2] = utf8Bytes[0];
			}

			return utf8;
		}

		public static byte[] FromUtf8Hex(this byte[] bytes)
		{
			EnsureHexTable();

			var dest = new byte[bytes.Length / 2];

			for (var i = 0; i < bytes.Length; i += 2)
			{
				var item = BitConverter.ToUInt16(bytes, i);
				var destByte = Utf8HexTableReverse[item];
				dest[dest.Length - 1 - i / 2] = destByte;
			}
			return dest;
		}

		public static byte[] FromHexString(this string hexString)
		{
			EnsureHexTable();

			var bytes = new byte[hexString.Length / 2];
			var byteIndex = 0;
			for (var i = 0; i < hexString.Length; i += 2)
			{
				bytes[byteIndex] = HexTable2[hexString.Substring(i, 2)];
				byteIndex++;
			}
			return bytes;
		}

		static void EnsureHexTable()
		{
			if (HexTable.Count == 0)
			{
				lock (lockObject)
				{
					if (HexTable.Count == 0)
					{
						for (byte i = 0; i <= 255; i++)
						{
							var hexString = i.ToString("x2");
							HexTable.Add(i, hexString);
							HexTable2.Add(hexString, i);
							var twoBytes = Encoding.ASCII.GetBytes(hexString);
							Utf8HexTable.Add(i, twoBytes);
							Utf8HexTableReverse.Add(BitConverter.ToUInt16(twoBytes, 0), i);
							if (i == 255)  // overflow!
								return;
						}
					}
				}
			}
		}
	}
}
