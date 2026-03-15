using System;
using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class EquipSlotStateResolverTests
{
    [Fact]
    public void ResolveDisplayedPart_PrefersLongcoatOverCoat()
    {
        var build = new CharacterBuild();
        var coat = new CharacterPart { ItemId = 1040000, Slot = EquipSlot.Coat, Name = "Coat" };
        var longcoat = new CharacterPart { ItemId = 1050000, Slot = EquipSlot.Longcoat, Name = "Overall" };
        build.Equip(coat);
        build.Equip(longcoat);

        CharacterPart displayed = EquipSlotStateResolver.ResolveDisplayedPart(build, EquipSlot.Coat);

        Assert.Same(longcoat, displayed);
    }

    [Fact]
    public void ResolveVisualState_DisablesPantsWhenOverallEquipped()
    {
        var build = new CharacterBuild();
        build.Equip(new CharacterPart { ItemId = 1050000, Slot = EquipSlot.Longcoat, Name = "Overall" });

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Pants);

        Assert.True(state.IsDisabled);
        Assert.Equal(EquipSlotDisableReason.OverallOccupiesPantsSlot, state.Reason);
    }

    [Fact]
    public void ResolveVisualState_DisablesShieldForBeginnerSubJobRestriction()
    {
        var build = new CharacterBuild
        {
            Job = 0,
            SubJob = 1
        };

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Shield);

        Assert.True(state.IsDisabled);
        Assert.Equal(EquipSlotDisableReason.BeginnerSubJobShieldRestriction, state.Reason);
        Assert.Equal("Disabled for this subjob", state.Message);
    }

    [Fact]
    public void ResolveVisualState_DisablesShieldForTwoHandedWeapon()
    {
        var build = new CharacterBuild();
        build.Equip(new WeaponPart
        {
            ItemId = 1402000,
            Slot = EquipSlot.Weapon,
            Name = "Two-Handed Sword",
            IsTwoHanded = true
        });

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Shield);

        Assert.True(state.IsDisabled);
        Assert.Equal(EquipSlotDisableReason.TwoHandedWeapon, state.Reason);
        Assert.Equal("Two-handed weapon equipped", state.Message);
    }

    [Fact]
    public void ResolveVisualState_DisablesExpiredEquipment()
    {
        var build = new CharacterBuild();
        build.Equip(new CharacterPart
        {
            ItemId = 1000000,
            Slot = EquipSlot.Cap,
            Name = "Expired Cap",
            ExpirationDateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        });

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(
            build,
            EquipSlot.Cap,
            new DateTime(2026, 3, 15, 0, 0, 1, DateTimeKind.Utc));

        Assert.True(state.IsDisabled);
        Assert.True(state.IsExpired);
        Assert.Equal(EquipSlotDisableReason.ItemExpired, state.Reason);
    }

    [Fact]
    public void ResolveVisualState_DisablesBrokenEquipment()
    {
        var build = new CharacterBuild();
        build.Equip(new CharacterPart
        {
            ItemId = 1070000,
            Slot = EquipSlot.Shoes,
            Name = "Broken Shoes",
            Durability = 0,
            MaxDurability = 10
        });

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Shoes);

        Assert.True(state.IsDisabled);
        Assert.True(state.IsBroken);
        Assert.Equal(EquipSlotDisableReason.ItemBroken, state.Reason);
        Assert.Equal("Durability depleted", state.Message);
    }

    [Fact]
    public void ResolveVisualState_DisablesMountSlotsWithoutMonsterRiding()
    {
        var build = new CharacterBuild
        {
            HasMonsterRiding = false
        };

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.TamingMob);

        Assert.True(state.IsDisabled);
        Assert.Equal(EquipSlotDisableReason.MonsterRidingRequired, state.Reason);
    }
}
