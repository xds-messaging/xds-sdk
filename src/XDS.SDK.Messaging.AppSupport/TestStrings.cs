namespace XDS.Messaging.SDK.AppSupport.NetStandard
{
	public static class TestStrings
	{
		public const string LoremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis vulputate feugiat " +
			"turpis, in tristique urna consequat sed. In dolor augue, tincidunt vitae dignissim a, volutpat sit amet turpis. Sed placerat " +
			"eu arcu ac volutpat. Pellentesque non elit neque. In nisi diam, ullamcorper eget ipsum ut, mollis aliquam est.";

		public const string OnboardingNicknameAsId = "Do you want to use your XDS-ID as nickname?";

		public const string OnboardingBackupIdentityExplanation = "You can use the backup to move your ID between different devices or if you are using multiple IDs. If you lose your phone and did not make a backup of your Obsdian identity, you will never be able to send or receive messages with your ID again. There is no such thing as a user account you could recover and we are not keeping your private key on your behalf. This backup does not include your contact list, your messages and settings.";

		public const string StrongPasswordExplanation = "You need to set the passphrase!";

		public const string BackupFile = "Backup file isn't selected.";

		public const string EncryptPassword = "The XDS app is designed as a secure encrypted vault on your device. All your messages, pictures, files, and customisations are encryted so that no data is leaked when you lose your device or use a cloud backup service. This passphrase will be used to encrypt the root encrytion key, which protects other data and keys.";

		public const string NickAndProfilePhoto = "You can assign yourself a name and a picture, and also assign names and pictures to all of your contacts. This feature is only for user interface clarity and design. All these names and pictures are encrypted on your phone and your contacts will never see it. Therefore you can be very creative with this kind of personalisation, it's private!";

		public const string GenerationRandomData = "By moving your finger, you create additional random data, which is combined with the data from the system's random number generator. The combined data is used to generate your elliptic curve private/public identity keypair. Your XDS ID is derived from the public key of this keypair. Your friends will encrypt messages for you with your public key and an ad-hoc generated ephemeral key. Only the owner of the private key and nobody else is able to decrypt these messages. By signing your messages with your private key, your friends can also be sure that a message they receive is indeed from you and not from a 'man in the middle'.";

		public const string OnboardingIdGeneratedText = "Please be always aware that you cannot know for sure which real person is behind an XDS ID until you have compared the public key of your communication partner - as he can see it on his device - with his public key as you can see it on your device. Comparing the XDS ID alone is not enough!";

		public const string Version = "Version: {0}";

		public const string IdNotCopiedToClipboard = "ID could not be copied to clipboard.";

		public const string IdCopiedToClipboard = "ID copied to clipboard.";

		public const string DecryptingMessage = "decrypting...";

		public const string OpenLinkConfirmation = "Opening a link discloses your IP address to the website - proceed anyway?";

		public const string SendImageTitle = "Image preview";
	}
}