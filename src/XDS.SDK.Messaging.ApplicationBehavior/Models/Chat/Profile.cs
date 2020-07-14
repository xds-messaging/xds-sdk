using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Chat
{
	public class Profile : IId
	{
		#region IId

		public string Id
		{
			get => "1";
			set { }
		}

		#endregion

		public string Name { get; set; }

		public byte[] PictureBytes { get; set; }

		public byte[] PublicKey { get; set; }

		public byte[] PrivateKey { get; set; }

		public bool IsIdentityPublished { get; set; }

		public string ChatId { get; set; }

		public byte[] MasterKey { get; set; }

        public byte[] DefaultAddressPrivateKey { get; set; }

        public byte[] DefaultAddressPublicKey { get; set; }

        public byte[] DefaultAddressHash { get; set; }

        public string DefaultAddress { get; set; }

        public byte[] DefaultAddressScriptPubKey { get; set; }

        public string DefaultAddressKeyPath { get; set; }
	}
}
