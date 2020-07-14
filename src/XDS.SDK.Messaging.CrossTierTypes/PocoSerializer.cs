using System;
using System.Collections.Generic;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static partial class PocoSerializer
    {
        // You need to write a small custom De-/Serializer for each POCO you want to support.
        // See XMessagePartExtentions for a sample POCO serialization.
        // Go to CustomSerializerUserExtensible.cs to create suppot for new primitive types.
        // Generic Collection De-/Serialization is already included!

        #region nouserservicablepartsinside

        const int SizeLenght = sizeof(int);

        public static List<byte[]> Begin()
        {
            return new List<byte[]>();
        }

        
        public static byte[] Finish(this List<byte[]> ser, int? itemCount = null)
        {
            int elements = 0;
            int totalContentSize = 0;
            for (int i = 0; i < ser.Count; i++)
            {
                var byteArray = ser[i];
                elements++;
                if (byteArray != null) // i.e. we add zero for null
                    totalContentSize += byteArray.Length;
            }
            var destOffset = 0;
            if (itemCount.HasValue)  // then it's a collection with itemCount elements
            {
                destOffset += 4; // leave one int room for itemCount
                totalContentSize += 4;
            }
            var dest = new byte[totalContentSize + elements * SizeLenght];
            if (itemCount.HasValue)
            {
                // copy itemCount to the beginning of the result
                var countBytes = BitConverter.GetBytes(itemCount.Value);
                Buffer.BlockCopy(countBytes, 0, dest, 0, 4);
            }


            for (int i = 0; i < ser.Count; i++)
            {
                var byteArray = ser[i];
                int currentLenght;
                if (byteArray != null)
                    currentLenght = byteArray.Length;
                else currentLenght = -1; // -1 marks null

                byte[] currentLenghtBytes = BitConverter.GetBytes(currentLenght);
                Buffer.BlockCopy(currentLenghtBytes, 0, dest, destOffset, SizeLenght);
                destOffset += SizeLenght;

                if (currentLenght > 0) // do nothing empty and null arrays.
                    Buffer.BlockCopy(byteArray, 0, dest, destOffset, currentLenght);

                if (currentLenght >= 0) // adds 0 for -1 marked arrays
                    destOffset += currentLenght;
            }
            return dest;
        }

        public static List<byte[]> GetDeserializer(byte[] source, int skip = 0)
        {
            var split = new List<byte[]>();
            var sourceIndex = 0 + skip;
            while (sourceIndex < source.Length - 1 - skip)
            {
                var currentLenght = BitConverter.ToInt32(source, sourceIndex);
                sourceIndex += SizeLenght;

                if (currentLenght >= 0)
                {
                    var dest = new byte[currentLenght];
                    if (currentLenght > 0)
                        Buffer.BlockCopy(source, sourceIndex, dest, 0, currentLenght);
                    sourceIndex += currentLenght;
                    split.Add(dest);
                }
                else if (currentLenght == -1) // marks null
                {
                    split.Add(null);
                }
                else throw new InvalidOperationException("Lenght must be -1, 0, or > 0");

            }
            return split;
        }

        public static byte[] SerializeCollection<T>(this IList<T> collection, Func<T, byte[]> itemSerializer)
        {
            var ser = Begin();
            for (var i = 0; i < collection.Count; i++)
                ser.Append(itemSerializer(collection[i]));
            return ser.Finish(collection.Count);
        }

        public static List<T> DeserializeCollection<T>(this byte[] serializedCollection, Func<byte[], T> itemDeserializer) where T : class
        {
            var collection = new List<T>();

            var itemCount = BitConverter.ToInt32(serializedCollection, 0);
            var ser = GetDeserializer(serializedCollection, 4);
           
            for (var i = 0; i < itemCount; i++)
            {
                byte[] serializedItem = ser.MakeByteArray(i);
                T item = itemDeserializer(serializedItem);
                collection.Add(item);
            }
            return collection;
        }

        public static byte[] Concatenate(params byte[][] byteArrays)
        {
            var retLenght = 0;
            for (var i=0; i<byteArrays.Length; i++)
                retLenght += byteArrays[i].Length;

            var ret = new byte[retLenght];

            var offset = 0;
            for (var i = 0; i < byteArrays.Length; i++)
            {
                Buffer.BlockCopy(byteArrays[i], 0, ret, offset, byteArrays[i].Length);
                offset += byteArrays[i].Length;
            }
            return ret;
        }

        #endregion
    }
}