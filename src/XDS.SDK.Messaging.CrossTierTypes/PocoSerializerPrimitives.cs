using System;
using System.Collections.Generic;
using System.Text;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static partial class PocoSerializer
    {
        // Create Append(XX xx)-/MakeXX Methods to support more primitive types
        // in your custom PocoSerializer-based serializers.

        public static bool MakeBoolean(this List<byte[]> ser, int index)
        {
            byte value = ser[index][0];
            if (value == 1)
                return true;
            if (value == 2)
                return false;
            throw new ArgumentOutOfRangeException();
        }

        public static int MakeInt32(this List<byte[]> ser, int index)
        {
            return BitConverter.ToInt32(ser[index], 0);
        }

        public static long MakeInt64(this List<byte[]> ser, int index)
        {
            return BitConverter.ToInt64(ser[index], 0);
        }

        public static ulong MakeUInt64(this List<byte[]> ser, int index)
        {
	        return BitConverter.ToUInt64(ser[index], 0);
        }

		public static ushort MakeUInt16(this List<byte[]> ser, int index)
        {
            return BitConverter.ToUInt16(ser[index], 0);
        }

        public static byte[] MakeByteArray(this List<byte[]> ser, int index)
        {
            return ser[index];
        }

        public static byte MakeByte(this List<byte[]> ser, int index)
        {
            return ser[index][0];
        }

        public static byte DeserializeByteCore(this byte[] b)
        {
            return b[0];
        }

        public static string DeserializeStringCore(this byte[] strg)
        {
            return Encoding.UTF8.GetString(strg, 0, strg.Length);
        }
        public static string MakeString(this List<byte[]> ser, int index)
        {
            if (ser[index] == null)
                return null;
            return DeserializeStringCore(ser[index]);
        }
        public static DateTime MakeDateTime(this List<byte[]> ser, int index)
        {
            return new DateTime(BitConverter.ToInt64(ser[index], 0), DateTimeKind.Utc);
        }

        public static byte[] SerializeCore(this string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        public static List<byte[]> Append(this List<byte[]> ser, string s)
        {

            ser.Add(s == null ? null : SerializeCore(s));
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, DateTime dateTime)
        {

            long ticks = dateTime.Ticks;
            ser.Add(BitConverter.GetBytes(ticks));
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, byte b)
        {
            ser.Add(new[] { b });
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, bool boolean)
        {
            byte value = boolean ? (byte)1 : (byte)2; // 1: true, 2: false
            ser.Add(new[] { value });
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, byte[] byteArray)
        {
            ser.Add(byteArray); // do this even when it is null!
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, ushort uint16)
        {
            ser.Add(BitConverter.GetBytes(uint16));
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, int int32)
        {
            ser.Add(BitConverter.GetBytes(int32));
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, long int64)
        {
            ser.Add(BitConverter.GetBytes(int64));
            return ser;
        }

        public static List<byte[]> Append(this List<byte[]> ser, ulong uint64)
        {
	        ser.Add(BitConverter.GetBytes(uint64));
	        return ser;
        }

		public static List<byte[]> Append(this List<byte[]> ser, Dictionary<long, byte[]> dictLongByteArray)
        {
            if (dictLongByteArray == null)
            {
                ser.Add(new byte[] { 0xff });
                return ser;
            }
            List<byte> serializedDictionary = new List<byte>();

            foreach (long key in dictLongByteArray.Keys)
            {
                byte[] array = dictLongByteArray[key];
                int arrLen = array?.Length ?? -1;

                byte[] keyBytes = BitConverter.GetBytes(key);
                byte[] arrLenBytes = BitConverter.GetBytes(arrLen);
                byte[] arrayBytes = array ?? new byte[0];
                var serializedKvp = Concatenate(keyBytes, arrLenBytes, arrayBytes);
                serializedDictionary.AddRange(serializedKvp);
            }
            ser.Add(serializedDictionary.ToArray());
            return ser;
        }

        public static Dictionary<long, byte[]> MakeDictLongByteArray(this List<byte[]> ser, int index)
        {
            byte[] data = ser[index];
            if (data.Length == 1 && data[0] == 0xff) // case Dictionay is null
                return null;
            var ret = new Dictionary<long, byte[]>();
            if (data.Length == 0) // case Dictionary was empty
                return ret;
            var cursor = 0;
        makeKvp:
            long dictKey = BitConverter.ToInt64(data, cursor);
            cursor += sizeof(long);
            int arrLen = BitConverter.ToInt32(data, cursor);
            cursor += sizeof(int);
            byte[] arr = arrLen == -1 ? null : new byte[arrLen];
            if (arr != null)
                Buffer.BlockCopy(data, cursor, arr, 0, arrLen);
            ret.Add(dictKey, arr);
            cursor += arrLen == -1 ? 0 : arrLen;
            if (cursor < data.Length)
                goto makeKvp;
            return ret;
        }
    }
}
