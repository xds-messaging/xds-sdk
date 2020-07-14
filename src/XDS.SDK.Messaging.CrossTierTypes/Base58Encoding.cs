using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Messaging.CrossTierTypes
{
	// Implements https://en.bitcoin.it/wiki/Base58Check_encoding
	// adapted from:
	// https://gist.githubusercontent.com/CodesInChaos/3175971/raw/f1f66726c46936cffe1cdf22f818e755ede9ea29/Base58Encoding.cs
	public static class Base58Encoding
	{
		public const int CheckSumSizeInBytes = 4;

		public static byte[] AddCheckSum(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			byte[] checkSum = GetCheckSum(data);
			byte[] dataWithCheckSum = ByteArrays.Concatenate(data, checkSum);
			Debug.Assert(data.Length + CheckSumSizeInBytes == dataWithCheckSum.Length);
			return dataWithCheckSum;
		}

		//Returns null if the checksum is invalid
		public static byte[] VerifyAndRemoveCheckSum(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			byte[] result = SubArray(data, 0, data.Length - CheckSumSizeInBytes);
			byte[] givenCheckSum = SubArray(data, data.Length - CheckSumSizeInBytes);
			byte[] correctCheckSum = GetCheckSum(result);
			if (givenCheckSum.SequenceEqual(correctCheckSum))
				return result;
			else
				return null;
		}

		private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

		public static string Encode(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			// Decode byte[] to BigInteger
			BigInteger intData = 0;
			for (int i = 0; i < data.Length; i++)
			{
				intData = intData * 256 + data[i];
			}

			// Encode BigInteger to Base58 string
			string result = "";
			while (intData > 0)
			{
				int remainder = (int)(intData % 58);
				intData /= 58;
				result = Digits[remainder] + result;
			}

			// Append `1` for each leading 0 byte
			for (int i = 0; i < data.Length && data[i] == 0; i++)
			{
				result = '1' + result;
			}
			Debug.Assert(result != null);
			return result;
		}

		public static string EncodeWithCheckSum(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));
			var encoded = Encode(AddCheckSum(data));
			Debug.Assert(encoded != null);
			return encoded;
		}

		public static byte[] Decode(string s)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));

			// Decode Base58 string to BigInteger 
			BigInteger intData = 0;
			for (int i = 0; i < s.Length; i++)
			{
				int digit = Digits.IndexOf(s[i]); //Slow
				if (digit < 0)
					throw new FormatException(string.Format("Invalid Base58 character `{0}` at position {1}", s[i], i));
				intData = intData * 58 + digit;
			}

			// Encode BigInteger to byte[]
			// Leading zero bytes get encoded as leading `1` characters
			int leadingZeroCount = s.TakeWhile(c => c == '1').Count();
			var leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
			var bytesWithoutLeadingZeros =
				intData.ToByteArray()
				.Reverse()// to big endian
				.SkipWhile(b => b == 0);//strip sign byte
			var result = leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();
			Debug.Assert(result != null);
			return result;
		}

		// Throws `FormatException` if s is not a valid Base58 string, or the checksum is invalid
		public static byte[] DecodeWithCheckSum(string s)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));

			var dataWithCheckSum = Decode(s);
			var dataWithoutCheckSum = VerifyAndRemoveCheckSum(dataWithCheckSum);
			if (dataWithoutCheckSum == null)
				throw new FormatException("Base58 checksum is invalid");
			return dataWithoutCheckSum;
		}

		private static byte[] GetCheckSum(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));


			SHA256 sha256 = SHA256.Create();
			byte[] hash1 = sha256.ComputeHash(data);
			byte[] hash2 = sha256.ComputeHash(hash1);

			var result = new byte[CheckSumSizeInBytes];
			Buffer.BlockCopy(hash2, 0, result, 0, result.Length);
			Debug.Assert(result != null);
			return result;
		}

		public static T[] SubArray<T>(T[] arr, int start, int length)
		{
			var result = new T[length];
			Buffer.BlockCopy(arr, start, result, 0, length);
			return result;
		}

		public static T[] SubArray<T>(T[] arr, int start)
		{
			return SubArray(arr, start, arr.Length - start);
		}
	}
}
