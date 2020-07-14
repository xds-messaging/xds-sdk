namespace XDS.SDK.Messaging.CrossTierTypes
{
    public enum  MessageType : byte
    {
        None = 0,
        Text = 1,
        Media = 2,
        TextAndMedia = 3,
	    File = 4,
		ReadReceipt = 20,
        DeliveryReceipt = 21,
	}

	public static class MessageTypeExtensions
	{
		public static bool IsReceipt(this MessageType messageType)
		{
			return messageType == MessageType.DeliveryReceipt || messageType == MessageType.ReadReceipt;
		}

		public static bool IsContent(this MessageType messageType)
		{
			return messageType == MessageType.Text || messageType == MessageType.TextAndMedia ||
			       messageType == MessageType.Media || messageType == MessageType.File;
		}
	}
}
