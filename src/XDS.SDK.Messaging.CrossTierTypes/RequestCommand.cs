namespace XDS.SDK.Messaging.CrossTierTypes
{
    public sealed class RequestCommand
    {
        public readonly CommandId CommandId;
        public readonly object Contents;
        public RequestCommand(CommandId commandId, object serializableContents)
        {
            this.CommandId = commandId;
            this.Contents = serializableContents;
        }
	}
}
