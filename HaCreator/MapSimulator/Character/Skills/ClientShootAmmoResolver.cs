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

        TryResolveCashAmmoSlot(cashSlots, weaponCode, weaponItemId, out int cashSlotIndex, out int cashItemId);

        if (TryResolveActiveBulletSelection(
                useSlots,
                weaponCode,
                weaponItemId,
                normalizedRequiredAmmoCount,
                requiredSkillAmmoItemId,
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
            if (TryResolveUseAmmoSlotByItemId(
                    useSlots,
                    weaponCode,
                    weaponItemId,
                    fallbackActiveBulletItemId,
                    normalizedRequiredAmmoCount,
                    requiredSkillAmmoItemId,
                    out int fallbackSlotIndex))
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

    public static ShootAmmoSelection RefreshQueuedSelectionSlotMetadata(
        ShootAmmoSelection queuedSelection,
        IReadOnlyList<InventorySlotData> useSlots,
        IReadOnlyList<InventorySlotData> cashSlots,
        int weaponCode,
        int weaponItemId)
    {
        if (queuedSelection == null)
        {
            return null;
        }

        int refreshedUseSlotIndex = -1;
        if (queuedSelection.UseItemId > 0
            && IsCompatibleBulletItem(weaponCode, weaponItemId, queuedSelection.UseItemId))
        {
            int resolvedUseSlotIndex = FindSlotIndexByItemId(useSlots, queuedSelection.UseItemId);
            if (resolvedUseSlotIndex >= 0)
            {
                refreshedUseSlotIndex = resolvedUseSlotIndex;
            }
        }

        int refreshedCashSlotIndex = -1;
        if (queuedSelection.CashItemId > 0
            && IsCompatibleCashBulletItem(weaponCode, weaponItemId, queuedSelection.CashItemId))
        {
            int resolvedCashSlotIndex = FindSlotIndexByItemId(cashSlots, queuedSelection.CashItemId);
            if (resolvedCashSlotIndex >= 0)
            {
                refreshedCashSlotIndex = resolvedCashSlotIndex;
            }
        }

        return new ShootAmmoSelection
        {
            UseSlotIndex = refreshedUseSlotIndex,
            UseItemId = queuedSelection.UseItemId,
            CashSlotIndex = refreshedCashSlotIndex,
            CashItemId = queuedSelection.CashItemId
        };
    }

    public static bool TryRefreshQueuedSelectionForExecution(
        ShootAmmoSelection queuedSelection,
        IReadOnlyList<InventorySlotData> useSlots,
        IReadOnlyList<InventorySlotData> cashSlots,
        int weaponCode,
        int weaponItemId,
        int requiredAmmoCount,
        int requiredSkillAmmoItemId,
        bool requiresUseAmmo,
        out ShootAmmoSelection refreshedSelection)
    {
        refreshedSelection = RefreshQueuedSelectionSlotMetadata(
            queuedSelection,
            useSlots,
            cashSlots,
            weaponCode,
            weaponItemId);
        if (queuedSelection == null)
        {
            return !requiresUseAmmo;
        }

        int normalizedRequiredAmmoCount = requiredAmmoCount > 0 ? requiredAmmoCount : 1;
        if (requiresUseAmmo)
        {
            if (!TryResolveUseAmmoSlotByItemId(
                    useSlots,
                    weaponCode,
                    weaponItemId,
                    queuedSelection.UseItemId,
                    normalizedRequiredAmmoCount,
                    requiredSkillAmmoItemId,
                    out int refreshedUseSlotIndex))
            {
                refreshedSelection = new ShootAmmoSelection
                {
                    UseSlotIndex = -1,
                    UseItemId = queuedSelection.UseItemId,
                    CashSlotIndex = refreshedSelection?.CashSlotIndex ?? -1,
                    CashItemId = refreshedSelection?.CashItemId ?? queuedSelection.CashItemId
                };
                return false;
            }

            refreshedSelection = new ShootAmmoSelection
            {
                UseSlotIndex = refreshedUseSlotIndex,
                UseItemId = queuedSelection.UseItemId,
                CashSlotIndex = refreshedSelection?.CashSlotIndex ?? -1,
                CashItemId = refreshedSelection?.CashItemId ?? queuedSelection.CashItemId
            };
        }

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

    private static int FindSlotIndexByItemId(IReadOnlyList<InventorySlotData> slots, int itemId)
    {
        if (slots == null || itemId <= 0)
        {
            return -1;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData slot = slots[i];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId != itemId
                || slot.Quantity <= 0)
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private static bool TryResolveActiveBulletSelection(
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
            if (slot?.IsActiveBullet != true
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

    private static bool TryResolveUseAmmoSlotByItemId(
        IReadOnlyList<InventorySlotData> useSlots,
        int weaponCode,
        int weaponItemId,
        int itemId,
        int requiredAmmoCount,
        int requiredSkillAmmoItemId,
        out int slotIndex)
    {
        slotIndex = -1;
        if (useSlots == null
            || itemId <= 0
            || !IsCompatibleBulletItem(weaponCode, weaponItemId, itemId)
            || !MatchesRequiredSkillAmmoItem(requiredSkillAmmoItemId, itemId))
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
        int weaponItemId,
        out int slotIndex,
        out int itemId)
    {
        slotIndex = -1;
        itemId = 0;
        if (cashSlots == null)
        {
            return;
        }

        if (TryResolveActiveCashAmmoSelection(
                cashSlots,
                weaponCode,
                weaponItemId,
                out int activeSlotIndex,
                out int activeCashItemId))
        {
            slotIndex = activeSlotIndex;
            itemId = activeCashItemId;
            return;
        }

        int fallbackActiveCashItemId = ResolveActiveBulletItemId(cashSlots);
        if (fallbackActiveCashItemId > 0)
        {
            if (TryResolveCashAmmoSlotByItemId(
                    cashSlots,
                    weaponCode,
                    weaponItemId,
                    fallbackActiveCashItemId,
                    out int fallbackSlotIndex))
            {
                slotIndex = fallbackSlotIndex;
                itemId = fallbackActiveCashItemId;
            }

            return;
        }

        for (int i = 0; i < cashSlots.Count; i++)
        {
            InventorySlotData slot = cashSlots[i];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId <= 0
                || slot.Quantity <= 0
                || !IsCompatibleCashBulletItem(weaponCode, weaponItemId, slot.ItemId))
            {
                continue;
            }

            slotIndex = i;
            itemId = slot.ItemId;
            return;
        }
    }

    private static bool TryResolveActiveCashAmmoSelection(
        IReadOnlyList<InventorySlotData> cashSlots,
        int weaponCode,
        int weaponItemId,
        out int slotIndex,
        out int itemId)
    {
        slotIndex = -1;
        itemId = 0;
        if (cashSlots == null)
        {
            return false;
        }

        for (int i = 0; i < cashSlots.Count; i++)
        {
            InventorySlotData slot = cashSlots[i];
            if (slot?.IsActiveBullet != true
                || slot.IsDisabled
                || slot.ItemId <= 0
                || slot.Quantity <= 0
                || !IsCompatibleCashBulletItem(weaponCode, weaponItemId, slot.ItemId))
            {
                continue;
            }

            slotIndex = i;
            itemId = slot.ItemId;
            return true;
        }

        return false;
    }

    private static bool TryResolveCashAmmoSlotByItemId(
        IReadOnlyList<InventorySlotData> cashSlots,
        int weaponCode,
        int weaponItemId,
        int itemId,
        out int slotIndex)
    {
        slotIndex = -1;
        if (cashSlots == null
            || itemId <= 0
            || !IsCompatibleCashBulletItem(weaponCode, weaponItemId, itemId))
        {
            return false;
        }

        for (int i = 0; i < cashSlots.Count; i++)
        {
            InventorySlotData slot = cashSlots[i];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId != itemId
                || slot.Quantity <= 0)
            {
                continue;
            }

            slotIndex = i;
            return true;
        }

        return false;
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
        if (weaponItemId == SpecialArrowWeaponItemId)
        {
            return itemId / 1000 == 2060;
        }

        return weaponCode switch
        {
            ArrowWeaponType => itemId / 1000 == 2060,
            CrossbowWeaponType => itemId / 1000 == 2061,
            ThrowingStarWeaponType => itemId / 10000 == 207,
            BulletWeaponType => itemId / 10000 == 233,
            _ => false
        };
    }

    private static bool IsCompatibleCashBulletItem(int weaponCode, int weaponItemId, int itemId)
    {
        if (weaponItemId == SpecialArrowWeaponItemId)
        {
            return itemId / 1000 == 5020;
        }

        return weaponCode switch
        {
            ArrowWeaponType or CrossbowWeaponType => itemId / 1000 == 5020,
            ThrowingStarWeaponType => itemId / 1000 == 5021,
            BulletWeaponType => itemId / 1000 == 5022,
            _ => false
        };
    }
}
