using System;
using System.Linq;
using System.Text;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Cryptography.Api.Implementations
{
    public static class XDSSecFormatter
    {
        public const int HeaderLenght = 67;

        const string XDSSecSlashText = "XDSSecurity/";

        static readonly char[] WhiteList =
        {
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
            'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r',
            's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '+', '/', '$', '='
        };

        public static XDSSecText CreateXDSSecText(CipherV2 cipherV2)
        {
            Guard.NotNull(cipherV2);

            var xdsSecTextV2Bytes = ByteArrays.Concatenate(
                // len			Sum(len)		Start Index
                new[] { CipherV2.Version },               // 1			1				0
                new[] { cipherV2.RoundsExponent.Value },  // 1			2				1
                new[] { cipherV2.PlaintextPadding.Value },     // 1			3				2
                cipherV2.IV16.GetBytes(),               // 16			19				3
                cipherV2.MACCipher16.GetBytes(),        // 16			35				19
                cipherV2.RandomKeyCipher32.GetBytes(),  // 32			67				35
                cipherV2.MessageCipher.GetBytes()       // len			67 + len		67
                );

            if (xdsSecTextV2Bytes.Length < HeaderLenght)
                throw new Exception("Data cannot be shorter than the required header.");

            var xdsSecTextV2Base64 = Base64Encoder.EncodeDataToBase64CharArray(xdsSecTextV2Bytes);

            var sb = new StringBuilder();
            const int breakAfter = 74;
            var charsInLine = 0;

            foreach (var c in XDSSecSlashText)
            {
                sb.Append(c);
                if (++charsInLine != breakAfter)
                    continue;
                sb.Append(new[] { '\r', '\n' });
                charsInLine = 0;
            }

            foreach (var c in xdsSecTextV2Base64)
            {
                sb.Append(c == '/' ? '$' : c);
                if (++charsInLine != breakAfter)
                    continue;
                sb.Append(new[] { '\r', '\n' });
                charsInLine = 0;
            }

            return new XDSSecText(sb.ToString());
        }

        public static XDSSecText CreateXDSSecText(byte[] cipherV2, int length, int breakAfter = 74)
        {
            Guard.NotNull(cipherV2);
            int maxBytesNeeded;
            if (length == -1)
            {
                length = Base64Encoder.CalculateBase64EncodedLengthInChars(cipherV2.Length);
                maxBytesNeeded = cipherV2.Length;
            }
            else
            {
                maxBytesNeeded = Base64Encoder.SafeEstimateBytesNeededForNBase64Chars(length);
            }
            char[] charsToProduce = new char[length + 4]; // safety margin
            Convert.ToBase64CharArray(cipherV2, 0, Math.Min(cipherV2.Length, maxBytesNeeded), charsToProduce, 0);


            var sb = new StringBuilder();
            var ellipsis = " ...";
            length = length - ellipsis.Length;
            var charsInLine = 0;
            var charactersProcessed = 0;
            foreach (var c in XDSSecSlashText)
            {
                sb.Append(c);
                if (++charsInLine != breakAfter)
                    continue;
                sb.Append(new[] { '\r', '\n' });
                charsInLine = 0;
            }

            foreach (var c in charsToProduce)
            {
                if (length == charactersProcessed++)
                {
                    sb.Append(ellipsis);
                    break;
                }


                sb.Append(c == '/' ? '$' : c);
                if (++charsInLine != breakAfter)
                    continue;
                sb.Append(new[] { '\r', '\n' });
                charsInLine = 0;
            }

            return new XDSSecText(sb.ToString());
        }

        public static byte[] CreateBinary(CipherV2 cipherV2)
        {
            Guard.NotNull(cipherV2);

            var xdsSec2Bytes = ByteArrays.Concatenate(
                // len			Sum(len)		Start Index
                new[] { CipherV2.Version },               // 1			1				0
                new[] { cipherV2.RoundsExponent.Value },  // 1			2				1
                new[] { cipherV2.PlaintextPadding.Value },     // 1			3				2
                cipherV2.IV16.GetBytes(),               // 16			19				3
                cipherV2.MACCipher16.GetBytes(),        // 16			35				19
                cipherV2.RandomKeyCipher32.GetBytes(),  // 32			67				35
                cipherV2.MessageCipher.GetBytes()       // len			67 + len		67
                );

            return xdsSec2Bytes;

        }

        public static byte[] CreateBinaryDH(CipherV2 cipherV2)
        {
            Guard.NotNull(cipherV2);
            var randomKeyCipher32Placeholder = new byte[32];
            randomKeyCipher32Placeholder[0] = 1; // insert a bit so that the SecureBytes validation does not throw.
            cipherV2.RandomKeyCipher32 = new RandomKeyCipher32(randomKeyCipher32Placeholder);

            var xdsSec2Bytes = ByteArrays.Concatenate(
                // len			Sum(len)		Start Index
                new[] { CipherV2.Version },               // 1			1				0
                new[] { cipherV2.RoundsExponent.Value },  // 1			2				1
                new[] { cipherV2.PlaintextPadding.Value },     // 1			3				2
                cipherV2.IV16.GetBytes(),               // 16			19				3
                cipherV2.MACCipher16.GetBytes(),        // 16			35				19
                cipherV2.RandomKeyCipher32.GetBytes(),  // 32			67				35
                cipherV2.MessageCipher.GetBytes()       // len			67 + len		67
                );

            return xdsSec2Bytes;

        }

        static CipherV2 DissectBytesToCipherV2(byte[] xdsSecBytes)
        {
            var version = xdsSecBytes[0];
            var exponent = xdsSecBytes[1];
            var padding = xdsSecBytes[2];

            if (version != CipherV2.Version)
                throw CommonFormatException("Expected a version byte at index 0 of value '2'.");

            if ((exponent > 31 || exponent < 4) && exponent != 0xff)
                throw CommonFormatException("The value for the rounds exponent at index 1 is invalid.");

            if (padding > 15)
                throw CommonFormatException("The value at the padding byte at index 1 is invalid.");


            var cipher = new CipherV2
            {
                PlaintextPadding = new PlaintextPadding(padding),
                RoundsExponent = new RoundsExponent(exponent)
            };


            var iv16 = new byte[16];
            Buffer.BlockCopy(xdsSecBytes, 3, iv16, 0, 16);
            cipher.IV16 = new IV16(iv16);

            var macCipher = new byte[16];
            Buffer.BlockCopy(xdsSecBytes, 19, macCipher, 0, 16);
            cipher.MACCipher16 = new MACCipher16(macCipher);

            var randomKeyCipher = new byte[32];
            Buffer.BlockCopy(xdsSecBytes, 35, randomKeyCipher, 0, 32);
            cipher.RandomKeyCipher32 = new RandomKeyCipher32(randomKeyCipher);

            var cipherBytes = new byte[xdsSecBytes.Length - 67];
            Buffer.BlockCopy(xdsSecBytes, 67, cipherBytes, 0, cipherBytes.Length);
            cipher.MessageCipher = new MessageCipher(cipherBytes);

            return cipher;
        }

        public static CipherV2 DissectXDSSecText(string xdsSecText, LongRunningOperationContext context)
        {
            try
            {
                var xdsSec = WhiteListXDSSecCharacters(xdsSecText, context);

                if (!xdsSec.StartsWith(XDSSecSlashText, StringComparison.OrdinalIgnoreCase))
                    throw CommonFormatException("The prefix '{0}' is missing.".FormatInvariant(XDSSecSlashText));

                var xdsSecTextV2Base64 = xdsSec.Remove(0, XDSSecSlashText.Length).Replace('$', '/');

                var xdsSecTextV2Bytes = Base64Encoder.DecodeBase64StringToBinary(xdsSecTextV2Base64);
                return DissectBytesToCipherV2(xdsSecTextV2Bytes);
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith(LocalizableStrings.MsgFormatError))
                    throw;
                throw CommonFormatException(e.Message);
            }
        }

        public static CipherV2 DissectXDSSecBytes(byte[] xdsSecBytes, LongRunningOperationContext context)
        {
            if (xdsSecBytes == null)
                throw new ArgumentNullException(nameof(xdsSecBytes));
            return DissectBytesToCipherV2(xdsSecBytes);
        }

        static string WhiteListXDSSecCharacters(string xdsSecBase64, LongRunningOperationContext context)
        {
            var sb = new StringBuilder();

            foreach (var c in xdsSecBase64)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (WhiteList.Contains(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        static FormatException CommonFormatException(string errorDetails)
        {
            return new FormatException(LocalizableStrings.MsgFormatError + "\r\n\r\n" + errorDetails);
        }


    }
}