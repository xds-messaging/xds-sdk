namespace XDS.SDK.Messaging.CrossTierTypes
{
    public sealed class Command
    {
        public readonly CommandId CommandId;
        public readonly byte[] CommandData;

        public Command(CommandId commandId, byte[] commandData)
        {
            this.CommandId = commandId;
            this.CommandData = commandData;
        }
    }
}
