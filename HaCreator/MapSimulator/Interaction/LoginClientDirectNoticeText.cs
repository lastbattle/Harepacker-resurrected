namespace HaCreator.MapSimulator.Interaction
{
    internal static class LoginClientDirectNoticeText
    {
        internal const int DeleteCharacterTransferStringPoolId = 0xFD4;
        internal const int CreateCharacterTransferStringPoolId = 0xFD9;

        private const string DeleteCharacterTransferNotice =
            "You cannot delete a character that\r\n is currently going through the transfer.";

        private const string CreateCharacterTransferNotice =
            "You cannot create a new character \r\nunder the account that \r\nhas requested for a transfer.";

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId switch
            {
                DeleteCharacterTransferStringPoolId => DeleteCharacterTransferNotice,
                CreateCharacterTransferStringPoolId => CreateCharacterTransferNotice,
                _ => null,
            };

            return text != null;
        }
    }
}
