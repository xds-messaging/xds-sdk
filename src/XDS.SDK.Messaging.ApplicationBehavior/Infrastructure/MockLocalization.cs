using XDS.SDK.Cryptography.Api;

namespace XDS.Messaging.SDK.ApplicationBehavior.Infrastructure
{
	public static class MockLocalization
	{
		public static string ReplaceKey(string key)
		{
			if (string.IsNullOrEmpty(key))
				return key;
			switch (key)
			{
				case LocalizableStrings.MsgPasswordError:
					return "The passphrase is incorrect.";
				case LocalizableStrings.MsgFormatError:
					return "Format error.";

				case LocalizableStrings.MsgEncryptingRandomKey:
					return "Encrypting random key...";
				case LocalizableStrings.MsgCalculatingMAC:
					return "Calculating MAC...";
				case LocalizableStrings.MsgEncryptingMessage:
					return "Encrypting payload...";
				case LocalizableStrings.MsgEncryptingMAC:
					return "Encrypting MAC...";

				case LocalizableStrings.MsgDecryptingMAC:
					return "Decrypting MAC...";
				case LocalizableStrings.MsgDecryptingRandomKey:
					return "Decrypting random key...";
				case LocalizableStrings.MsgDecryptingMessage:
					return "Decrypting payload...";
				case LocalizableStrings.MsgProcessingKey:
					return "Processing key...";

			}
			return key;
		}
	}
}
