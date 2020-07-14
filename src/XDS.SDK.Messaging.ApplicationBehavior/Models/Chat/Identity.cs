using System;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Chat
{
    public class Identity : IId
    {
		public Identity() { }

        #region IId

        public string Id { get; set; }

		#endregion

		/// <summary>
		/// Field used when adding a contact. Needed to download the public key.
		/// </summary>
		public string UnverifiedId;

		public string ChatId => XDS.SDK.Messaging.CrossTierTypes.ChatId.GenerateChatId(this.StaticPublicKey);

		public string Name;
	    public byte[] Image;
	    public DateTime LastSeenUtc;
	    public DateTime FirstSeenUtc;
	    public byte[] StaticPublicKey;
	    public ContactState ContactState;
	    public byte[] CryptographicInformation;
	}
}

