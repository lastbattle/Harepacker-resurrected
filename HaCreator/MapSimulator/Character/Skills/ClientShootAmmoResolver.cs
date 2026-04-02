using System.Collections.Generic;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator.Character.Skills;

public sealed class ShootAmmoSelection
{
    public int UseSlotIndex { get; init; } = -1;
    public int UseItemId { get; init; }
    public int CashSlotIndex { get; init; } = -1;
    public int CashItemId { get; init; }

    public bool HasUseAmmo => UseSlotIndex >= 0 && UseItemId > 0;
    public bool HasCashAmmo => CashSlotIndex >= 0 && CashItemId > 0;

    public ShootAmmoSelection Snapshot()
    {
        return new ShootAmmoSelection
        {
            UseSlotIndex = UseSlotIndex,
            UseItemId = UseItemId,
            CashSlotIndex = CashSlotIndex,
            CashItemId = CashItemId
        };
    }
}

public static class ClientShootAmmoResolver
{
    private const int ArrowWeaponType = 45;
    private const int CrossbowWeaponType = 46;
    private const int ThrowingStarWeaponType = 47;
    private const int BulletWeaponType = 49;
    private const int SpecialArrowWeaponItemId = 1472063;

    public static bool TryResolveSelection(
        IReadOnlyList<InventorySlotData> useSlots,
        IReadOnlyList<InventorySlotData> cashSlots,
        int weaponCode,
        int weaponItemId,
        int requiredAmmoCount,
        int requiredSkillAmmoItemId,
        out ShootAmmoSelection selection)
    {
        selection = new ShootAmmoSelection();
        int normalizedRequiredAmmoCount = requiredAmmoCount > 0 ? requiredAmmoCount : 1;

        TryResolveCashAmmoSlot(cashSlots, weaponCode, out int cashSlotIndex, out int cashItemId);

        if (TryResolveActiveBulletSelection(
                useSlots,
                normalizedRequiredAmmoCount,
                out int activeSlotIndex,
                out int activeBulletItemId))
        {
            selection = new ShootAmmoSelection
            {
                UseSlotIndex = activeSlotIndex,
                UseItemId = activeBulletItemId,
                CashSlotIndex = cashSlotIndex,
                CashItemId = cashItemId
            };
            return true;
        }

        int fallbackActiveBulletItemId = ResolveActiveBulletItemId(useSlots);
        if (fallbackActiveBulletItemId > 0)
        {
            if (TryResolveUseAmmoSlotByItemId(useSlots, fallbackActiveBulletItemId, normalizedRequiredAmmoCount, out int fallbackSlotIndex))
            {
                selection = new ShootAmmoSelection
                {
                    UseSlotIndex = fallbackSlotIndex,
                    UseItemId = fallbackActiveBulletItemId,
                    CashSlotIndex = cashSlotIndex,
                    CashItemId = cashItemId
                };
                return true;
            }

            selection = new ShootAmmoSelection
            {
                CashSlotIndex = cashSlotIndex,
                CashItemId = cashItemId
            };
            return false;
        }

        if (!TryResolveCompatibleUseAmmoSlot(
                useSlots,
                weaponCode,
                weaponItemId,
                normalizedRequiredAmmoCount,
                requiredSkillAmmoItemId,
                out int useSlotIndex,
                out int useItemId))
        {
            selection = new ShootAmmoSelection
            {
                CashSlotIndex = cashSlotIndex,
                CashItemId = cashItemId
            };
            return false;
        }

        selection = new ShootAmmoSelection
        {
            UseSlotIndex = useSlotIndex,
            UseItemId = useItemId,
            CashSlotIndex = cashSlotIndex,
            CashItemId = cashItemId
        };
        return true;
    }

    private static int ResolveActiveBulletItemId(IReadOnlyList<InventorySlotData> useSlots)
    {
        if (useSlots == null)
        {
            return 0;
        }

        for (int i = 0; i < useSlots.Count; i++)
        {
            InventorySlotData slot = useSlots[i];
            if (slot?.IsActiveBullet == true && !slot.IsDisabled && slot.ItemId > 0)
            {
                return slot.ItemId;
            }
        }

        return 0;
    }

    private static bool TryResolveActiveBulletSelection(
        IReadOnlyList<InventorySlotData> useSlots,
        int requiredAmmoCount,
        out int slotIndex,
        out int itemId)
    {
        slotIndex = -1;
        itemId = 0;
        if (useSlots == null)
        {
            return false;
        }

        for (int i = 0; i < useSlots.Count; i++)
        {
            InventorySlotData slot = useSlots[i];
            if (slot?.IsActiveBullet != true
                || slot.IsDisabled
                || slot.ItemId <= 0
                || slot.Quantity < requiredAmmoCount)
            {
                continue;
            }

            slotIndex = i;
            itemId = slot.ItemId;
            return true;
        }

        return false;
    }

    private static bool TryResolveUseAmmoSlotByItemId(
        IReadOnlyList<InventorySlotData> useSlots,
        int itemId,
        int requiredAmmoCount,
        out int slotIndex)
    {
        slotIndex = -1;
        if (useSlots == null || itemId <= 0)
        {
            return false;
        }

        for (int i = 0; i < useSlots.Count; i++)
        {
            InventorySlotData slot = useSlots[i];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId != itemId
                || slot.Quantity < requiredAmmoCount)
            {
                continue;
            }

            slotIndex = i;
            return true;
        }

        return false;
    }

    private static bool TryResolveCompatibleUseAmmoSlot(
        IReadOnlyList<InventorySlotData> useSlots,
        int weaponCode,
        int weaponItemId,
        int requiredAmmoCount,
        int requiredSkillAmmoItemId,
        out int slotIndex,
        out int itemId)
    {
        slotIndex = -1;
        itemId = 0;
        if (useSlots == null)
        {
            return false;
        }

        for (int i = 0; i < useSlots.Count; i++)
        {
            InventorySlotData slot = useSlots[i];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId <= 0
                || slot.Quantity < requiredAmmoCount
                || !IsCompatibleBulletItem(weaponCode, weaponItemId, slot.ItemId)
                || !MatchesRequiredSkillAmmoItem(requiredSkillAmmoItemId, slot.ItemId))
            {
                continue;
            }

            slotIndex = i;
            itemId = slot.ItemId;
            return true;
        }

        return false;
    }

    private static void TryResolveCashAmmoSlot(
        IReadOnlyList<InventorySlotData> cashSlots,
        int weaponCode,
        out int slotIndex,
        out int itemId)
    {
        slotIndex = -1;
        itemId = 0;
        if (cashSlots == null)
        {
            return;
        }

        for (int i = 0; i < cashSlots.Count; i++)
        {
            InventorySlotData slot = cashSlots[i];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId <= 0
                || slot.Quantity <= 0
                || !IsCompatibleCashBulletItem(weaponCode, slot.ItemId))
            {
                continue;
            }

            slotIndex = i;
            itemId = slot.ItemId;
            return;
        }
    }

    private static bool MatchesRequiredSkillAmmoItem(int requiredSkillAmmoItemId, int itemId)
    {
        if (requiredSkillAmmoItemId <= 0)
        {
            return true;
        }

        int requiredThousandsFamily = requiredSkillAmmoItemId / 1000;
        return requiredThousandsFamily is 2331 or 2332
            ? itemId / 1000 == requiredThousandsFamily
            : itemId == requiredSkillAmmoItemId;
    }

    private static bool IsCompatibleBulletItem(int weaponCode, int weaponItemId, int itemId)
    {
        return weaponCode switch
        {
            ArrowWeaponType => itemId / 1000 == 2060,
            CrossbowWeaponType => itemId / 1000 == 2061,
            ThrowingStarWeaponType => itemId / 10000 == 207,
            BulletWeaponType => itemId / 10000 == 233,
            _ when weaponItemId == SpecialArrowWeaponItemId => itemId / 1000 == 2060,
            _ => false
        };
    }

    private static bool IsCompatibleCashBulletItem(int weaponCode, int itemId)
    {
        return weaponCode switch
        {
            ArrowWeaponType or CrossbowWeaponType => itemId / 1000 == 5020,
            ThrowingStarWeaponType => itemId / 1000 == 5021,
            BulletWeaponType => itemId / 1000 == 5022,
            _ => false
        };
    }
}
