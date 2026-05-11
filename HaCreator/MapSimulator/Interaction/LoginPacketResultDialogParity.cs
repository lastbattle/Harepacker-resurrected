namespace HaCreator.MapSimulator
{
    public enum LoginPacketResultDialogOwner
    {
        None = 0,
        ViewAllCharResult = 1,
        SelectCharacterResult = 2,
        SelectCharacterByVacResult = 3,
        DeleteCharacterResult = 4,
        DeleteCharacterResultChildModal = 5,
        DeleteCharacterResultDirectNotice = 6,
    }

    internal static class LoginPacketResultDialogParity
    {
        public static LoginPacketResultDialogOwner ResolveViewAllCharOwner() =>
            LoginPacketResultDialogOwner.ViewAllCharResult;

        public static LoginPacketResultDialogOwner ResolveSelectCharacterOwner() =>
            LoginPacketResultDialogOwner.SelectCharacterResult;

        public static LoginPacketResultDialogOwner ResolveSelectCharacterByVacOwner() =>
            LoginPacketResultDialogOwner.SelectCharacterByVacResult;

        public static LoginPacketResultDialogOwner ResolveDeleteCharacterOwner(byte? resultCode)
        {
            return resultCode switch
            {
                6 or 9 or 20 or 22 or 24 or 29 or 35 or 36 => LoginPacketResultDialogOwner.DeleteCharacterResultChildModal,
                26 => LoginPacketResultDialogOwner.DeleteCharacterResultDirectNotice,
                _ => LoginPacketResultDialogOwner.DeleteCharacterResult,
            };
        }

        public static LoginUtilityDialogFrameVariant ResolveDeleteCharacterFrameVariant(
            byte? resultCode,
            bool hasNoticeText)
        {
            return ResolveDeleteCharacterOwner(resultCode) switch
            {
                LoginPacketResultDialogOwner.DeleteCharacterResultChildModal => LoginUtilityDialogFrameVariant.LoginNoticeCog,
                LoginPacketResultDialogOwner.DeleteCharacterResultDirectNotice => LoginUtilityDialogFrameVariant.Default,
                _ when hasNoticeText => LoginUtilityDialogFrameVariant.LoginNotice,
                _ => LoginUtilityDialogFrameVariant.Default,
            };
        }
    }
}
