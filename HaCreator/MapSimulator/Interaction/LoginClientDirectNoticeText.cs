namespace HaCreator.MapSimulator.Interaction
{
    internal static class LoginClientDirectNoticeText
    {
        internal const int DeleteCharacterTransferStringPoolId = 0xFD4;
        internal const int CreateCharacterTransferStringPoolId = 0xFD9;

        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }
    }
}
