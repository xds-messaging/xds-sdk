namespace XDS.SDK.Messaging.CrossTierTypes
{
    public enum ContactState : byte
    {
        None = 0,
        Added = 1,
        NonExistent = 2,
        Revoked = 9,
        Valid = 10,
    }
}
