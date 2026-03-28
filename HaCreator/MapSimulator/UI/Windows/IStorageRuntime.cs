using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public interface IStorageRuntime
    {
        string AccountLabel { get; }
        string CurrentCharacterName { get; }
        IReadOnlyList<string> SharedCharacterNames { get; }
        IReadOnlyList<string> AuthorizedCharacterNames { get; }
        bool CanCurrentCharacterAccess { get; }
        bool IsAccessSessionActive { get; }
        bool HasAccountPic { get; }
        bool IsAccountPicVerified { get; }
        bool HasAccountSecondaryPassword { get; }
        bool IsAccountSecondaryPasswordVerified { get; }
        bool RequiresClientAccountAuthority { get; }
        bool IsClientAccountAuthorityVerified { get; }
        bool HasSecondaryPassword { get; }
        bool IsSecondaryPasswordVerified { get; }

        IReadOnlyList<InventorySlotData> GetSlots(InventoryType type);
        int GetSlotLimit();
        void SetSlotLimit(int slotLimit);
        int GetUsedSlotCount();
        bool CanExpandSlotLimit(int amount = 4);
        bool TryExpandSlotLimit(int amount = 4);
        long GetMesoCount();
        void SetMeso(long amount);
        void AddItem(InventoryType type, InventorySlotData slotData);
        bool CanAcceptItem(InventoryType type, InventorySlotData slotData);
        bool TryRemoveSlotAt(InventoryType type, int slotIndex, out InventorySlotData slotData);
        void SortSlots(InventoryType type);
        void BeginAccessSession();
        void EndAccessSession();
        void ConfigureAccess(string accountLabel, string accountKey, string currentCharacterName, IEnumerable<string> sharedCharacterNames);
        void ConfigureLoginAccountSecurity(string picCode, bool secondaryPasswordEnabled, string secondaryPassword);
        bool TryVerifyAccountPic(string password);
        bool TryVerifyAccountSecondaryPassword(string password);
        bool TrySetSecondaryPassword(string password);
        bool TryVerifySecondaryPassword(string password);
        void ClearSecondaryPasswordVerification();
    }
}
