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
        AccountInfoResult = 7,
        SetAccountResult = 8,
        ConfirmEulaResult = 9,
        CheckPinCodeResultCreate = 10,
        CheckPinCodeResultVerify = 11,
        CheckPinCodeResultNotice = 12,
        UpdatePinCodeResult = 13,
        EnableSpwResult = 14,
        CheckSpwResult = 15,
    }

    internal static class LoginPacketResultDialogParity
    {
        public static LoginPacketResultDialogOwner ResolveViewAllCharOwner() =>
            LoginPacketResultDialogOwner.ViewAllCharResult;

        public static LoginPacketResultDialogOwner ResolveSelectCharacterOwner() =>
            LoginPacketResultDialogOwner.SelectCharacterResult;

        public static LoginPacketResultDialogOwner ResolveSelectCharacterByVacOwner() =>
            LoginPacketResultDialogOwner.SelectCharacterByVacResult;

        public static LoginPacketResultDialogOwner ResolveAccountInfoOwner() =>
            LoginPacketResultDialogOwner.AccountInfoResult;

        public static LoginPacketResultDialogOwner ResolveSetAccountOwner() =>
            LoginPacketResultDialogOwner.SetAccountResult;

        public static LoginPacketResultDialogOwner ResolveConfirmEulaOwner() =>
            LoginPacketResultDialogOwner.ConfirmEulaResult;

        public static LoginPacketResultDialogOwner ResolveCheckPinCodeOwner(byte? resultCode)
        {
            return resultCode switch
            {
                1 => LoginPacketResultDialogOwner.CheckPinCodeResultCreate,
                2 or 4 => LoginPacketResultDialogOwner.CheckPinCodeResultVerify,
                _ => LoginPacketResultDialogOwner.CheckPinCodeResultNotice,
            };
        }

        public static LoginPacketResultDialogOwner ResolveUpdatePinCodeOwner() =>
            LoginPacketResultDialogOwner.UpdatePinCodeResult;

        public static LoginPacketResultDialogOwner ResolveEnableSpwOwner() =>
            LoginPacketResultDialogOwner.EnableSpwResult;

        public static LoginPacketResultDialogOwner ResolveCheckSpwOwner() =>
            LoginPacketResultDialogOwner.CheckSpwResult;

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
