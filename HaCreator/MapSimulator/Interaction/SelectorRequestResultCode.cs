namespace HaCreator.MapSimulator
{
    internal enum SelectorRequestResultCode
    {
        None = 0,
        Success = 1,
        LoginStepBlocked = 2,
        WorldUnavailable = 3,
        AdultWorldRestricted = 4,
        ChannelUnavailable = 5,
        ChannelFull = 6,
        AdultChannelRestricted = 7,
        ServerRejected = 8,
    }
}
