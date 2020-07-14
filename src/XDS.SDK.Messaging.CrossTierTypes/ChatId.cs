using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class ChatId
    {
        public const string Prefix = "xds1";
        public const int IdBytesLength = 6;

        public static string GenerateChatId(byte[] publicKey)
        {
            if (publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));

            var sha256Hash = Singletons.Sha256.Value.ComputeHash(publicKey);

            var checksumLength = 1;
            var chatIdDataBytes = new byte[IdBytesLength + checksumLength]; // we can have 4,294,967,295 different IDs - that is not a lot but about the same as phone numbers with country code
            Buffer.BlockCopy(sha256Hash, 0, chatIdDataBytes, 0, chatIdDataBytes.Length - checksumLength);

            var checksum = CalculateCheckSum(chatIdDataBytes, IdBytesLength); // we can do this only because like so, because the checksum byte is still 0 and irrelevant for the checksum calculation!

            chatIdDataBytes[chatIdDataBytes.Length - 1] = checksum;
            var chatIdDataPart = Base58Encoding.Encode(chatIdDataBytes);
            var chatId = $"{Prefix}{chatIdDataPart}";
            Debug.Assert(chatId.Length == 14 || chatId.Length == 13);
            var test = DecodeChatId(chatId);

            for (var i = 0; i < chatIdDataBytes.Length; i++)
            {
                if (chatIdDataBytes[i] != test[i])
                    throw new InvalidDataException();
            }

            return chatId;

        }

        public static byte[] DecodeChatId(string chatId)
        {
            try
            {
                if (chatId == null)
                    throw new ArgumentNullException(nameof(chatId));

                if (!chatId.StartsWith(Prefix))
                    throw new ArgumentException($"The chatId must start with {Prefix}.", nameof(chatId));

                var base58Part = chatId.Substring(Prefix.Length);
                byte[] bytes = Base58Encoding.Decode(base58Part);
                var bytesForChecksum = bytes.Take(bytes.Length - 1).ToArray();
                var actualChecksum = CalculateCheckSum(bytesForChecksum, IdBytesLength);
                var expectedChecksum = bytes[bytes.Length - 1];
                if (expectedChecksum != actualChecksum)
                    throw new InvalidDataException("Invalid checksum!");
                return bytes;
            }
            catch (Exception e)
            {
                throw new ArgumentException("not valid, please check spelling.", "chatId", e);
            }

        }

        static byte CalculateCheckSum(byte[] dataPart, int numberOfBytesForChecksum)
        {
            byte checksum = 0;
            for (int i = 0; i < numberOfBytesForChecksum; i++)
            {
                checksum += dataPart[i];
            }

            return checksum;
        }
    }
}
