namespace XDS.SDK.Messaging.CrossTierTypes
{
    public enum  ServerCommand :byte
    {
        None = 0,
        ACK = 200,
        AnyNewsReply = 201,
        GetMessagesReply = 202,
        GetMessagesReply_Empty = 203,
    }
}
