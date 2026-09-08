namespace HaCreator.MapSimulator.UI
{
    internal enum StorageExpansionAccessFailure
    {
        None = 0,
        RuntimeUnavailable,
        UnauthorizedCharacter,
        SessionLocked,
        MissingAccountAuthority,
        MissingStoragePasscode
    }

    internal static class StorageExpansionAccessGate
    {
        public static StorageExpansionAccessFailure Evaluate(IStorageRuntime storageRuntime)
        {
            if (storageRuntime == null)
            {
                return StorageExpansionAccessFailure.RuntimeUnavailable;
            }

            if (!storageRuntime.CanCurrentCharacterAccess)
            {
                return StorageExpansionAccessFailure.UnauthorizedCharacter;
            }

            if (!storageRuntime.IsAccessSessionActive)
            {
                return StorageExpansionAccessFailure.SessionLocked;
            }

            if (!storageRuntime.IsClientAccountAuthorityVerified)
            {
                return StorageExpansionAccessFailure.MissingAccountAuthority;
            }

            if (!storageRuntime.IsSecondaryPasswordVerified)
            {
                return StorageExpansionAccessFailure.MissingStoragePasscode;
            }

            return StorageExpansionAccessFailure.None;
        }
    }
}
