namespace XDS.SDK.Messaging.CrossTierTypes
{
    public enum CommandId : byte
    {
        Zero = 0,
        AnyNews = 10,

	    CheckForResendRequest = 15,
	    CheckForResendRequest_Response = 16,

		DownloadMessageParts = 20,
        UploadMessageParts = 30,
        AnyNews_Response = 31,
        DownloadMessageParts_Response = 32,
        UploadMessageParts_Response = 33,
        UploadMessage = 34,
        UploadMessage_Response = 35,

		UploadResendRequest = 36,
	    UploadResendRequest_Response = 37,

		PublishIdentity = 50,
        PublishIdentity_Response = 51,
        GetIdentity = 52,
        GetIdentity_Response = 53,
        Headerless = 54,
        LostDynamicKey_Response = 55,
        NoSuchUser_Response = 56,
        DownloadMessages = 57,
        DownloadMessage_Response = 58,

        PhotonBalance = 70,
        PhotonBalance_Response = 71,
        PhotonOutputs = 72,
        PhotonOutputs_Response = 73,

        ServerException = 254,
       
    }

    public enum CommandHeader
    {
        Yes,
        No
    }
}
