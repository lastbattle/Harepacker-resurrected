namespace MapleLib.WzLib.WzStructure.Enums
{
    /// <summary>
    /// Wz Directory type (these are guesses fyi, it needs to be verified in the future)
    /// </summary>
    public enum WzDirectoryType : byte
    {
        UnknownType_1 = 1,
        RetrieveStringFromOffset_2 = 2,
        WzDirectory_3 = 3,
        WzImage_4 = 4,
    }
}
