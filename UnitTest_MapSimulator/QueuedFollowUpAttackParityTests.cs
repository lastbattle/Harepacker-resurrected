using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class QueuedFollowUpAttackParityTests
{
    [Fact]
    public void RefreshQueuedProjectileFireTimeSelection_UsesStoredFireTimeWeaponContext()
    {
        ActiveProjectile projectile = new()
        {
            IsQueuedFinalAttack = true,
            ResolvedShootWeaponCode = 45,
            ResolvedShootWeaponItemId = 1452000,
            ResolvedShootAmmoSelection = new ShootAmmoSelection
            {
                UseSlotIndex = 2,
                UseItemId = 2060000,
                CashSlotIndex = 3,
                CashItemId = 5020000
            }
        };

        ShootAmmoSelection refreshedSelection = SkillManager.RefreshQueuedProjectileFireTimeSelection(
            projectile,
            new[]
            {
                new InventorySlotData { ItemId = 2060000, Quantity = 1 }
            },
            new[]
            {
                new InventorySlotData { ItemId = 5020000, Quantity = 1 }
            },
            fallbackWeaponCode: 49,
            fallbackWeaponItemId: 1492000);

        Assert.Equal(0, refreshedSelection.UseSlotIndex);
        Assert.Equal(2060000, refreshedSelection.UseItemId);
        Assert.Equal(0, refreshedSelection.CashSlotIndex);
        Assert.Equal(5020000, refreshedSelection.CashItemId);
    }

    [Fact]
    public void RefreshQueuedProjectileFireTimeSelection_FallsBackToCurrentWeaponContextWhenStoredContextMissing()
    {
        ActiveProjectile projectile = new()
        {
            IsQueuedSparkAttack = true,
            ResolvedShootAmmoSelection = new ShootAmmoSelection
            {
                UseSlotIndex = 4,
                UseItemId = 2061000,
                CashSlotIndex = 5,
                CashItemId = 5020000
            }
        };

        ShootAmmoSelection refreshedSelection = SkillManager.RefreshQueuedProjectileFireTimeSelection(
            projectile,
            new[]
            {
                new InventorySlotData { ItemId = 2061000, Quantity = 1 }
            },
            new[]
            {
                new InventorySlotData { ItemId = 5020000, Quantity = 1 }
            },
            fallbackWeaponCode: 46,
            fallbackWeaponItemId: 1462000);

        Assert.Equal(0, refreshedSelection.UseSlotIndex);
        Assert.Equal(2061000, refreshedSelection.UseItemId);
        Assert.Equal(0, refreshedSelection.CashSlotIndex);
        Assert.Equal(5020000, refreshedSelection.CashItemId);
    }

    [Fact]
    public void ResolveQueuedProjectileVisualItemId_FallsBackToUseAmmoWhenCashLaneWasInvalidated()
    {
        ShootAmmoSelection selection = new()
        {
            UseSlotIndex = 0,
            UseItemId = 2060000,
            CashSlotIndex = -1,
            CashItemId = 5020000
        };

        int visualItemId = SkillManager.ResolveQueuedProjectileVisualItemId(selection);

        Assert.Equal(2060000, visualItemId);
    }
}
