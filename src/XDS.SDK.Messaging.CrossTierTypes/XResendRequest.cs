namespace XDS.SDK.Messaging.CrossTierTypes
{
    public sealed class XResendRequest : IId
    {
		/// <summary>
		/// Id is the 16 byte NetworkPayloadHash in new Guid(bytes[] hash).ToString() format.
		/// </summary>
	    public string Id { get; set; }

		/// <summary>
		/// If a client posts a ResendRequest, RecipientId should be null (and not its own Id, because its unnecessary).
		/// In a CheckForResendRequest query, the querying client must supply the RecipientId, so that he node can also look if the message that was originally sent is still on the relay.
		/// </summary>
		public string RecipientId { get; set; }
    }

	public static class XResendRequestExtensions
	{
		public static byte[] Serialize(this XResendRequest r)
		{
			byte[] serialized = PocoSerializer.Begin()
				.Append(r.Id)
				.Append(r.RecipientId)
				.Finish();
			return serialized;
		}



		public static XResendRequest DeserializeResendRequest(this byte[] resendRequest)
		{
			var r = new XResendRequest();

			var ser = PocoSerializer.GetDeserializer(resendRequest);

			r.Id = ser.MakeString(0);
			r.RecipientId = ser.MakeString(1);
			return r;
		}
	}
}
