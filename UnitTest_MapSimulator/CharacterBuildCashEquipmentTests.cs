using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class CharacterBuildCashEquipmentTests
{
    [Fact]
    public void Unequip_RemovingCashItemRestoresHiddenBaseItem()
    {
        CharacterBuild build = new CharacterBuild();
        CharacterPart basePendant = new CharacterPart { ItemId = 1122000, Slot = EquipSlot.Pendant, Name = "Base Pendant" };
        CharacterPart cashPendant = new CharacterPart { ItemId = 1702000, Slot = EquipSlot.Pendant, Name = "Cash Pendant", IsCash = true };

        build.Equip(basePendant);
        build.Equip(cashPendant);
        build.EquipHidden(basePendant);

        CharacterPart removed = build.Unequip(EquipSlot.Pendant);

        Assert.Same(cashPendant, removed);
        Assert.Same(basePendant, build.Equipment[EquipSlot.Pendant]);
        Assert.Empty(build.HiddenEquipment);
    }

    [Fact]
    public void ResolveDisplayedPart_PrefersVisibleCashItemButKeepsUnderlyingBaseItem()
    {
        CharacterBuild build = new CharacterBuild();
        CharacterPart basePendant = new CharacterPart { ItemId = 1122000, Slot = EquipSlot.Pendant, Name = "Base Pendant" };
        CharacterPart cashPendant = new CharacterPart { ItemId = 1702000, Slot = EquipSlot.Pendant, Name = "Cash Pendant", IsCash = true };

        build.Equip(cashPendant);
        build.EquipHidden(basePendant);

        Assert.Same(cashPendant, EquipSlotStateResolver.ResolveDisplayedPart(build, EquipSlot.Pendant));
        Assert.Same(basePendant, EquipSlotStateResolver.ResolveUnderlyingPart(build, EquipSlot.Pendant));
    }

    [Fact]
    public void TotalAttack_UsesHiddenWeaponStatsWhenVisibleWeaponIsCash()
    {
        CharacterBuild build = new CharacterBuild
        {
            STR = 4,
            DEX = 50,
            LUK = 200,
            Job = 2412
        };

        WeaponPart baseWeapon = new WeaponPart
        {
            ItemId = 1362000,
            Slot = EquipSlot.Weapon,
            BonusWeaponAttack = 100
        };
        WeaponPart cashWeapon = new WeaponPart
        {
            ItemId = 1362001,
            Slot = EquipSlot.Weapon,
            BonusWeaponAttack = 0,
            IsCash = true
        };

        build.Equip(cashWeapon);
        build.EquipHidden(baseWeapon);

        Assert.Equal(442, build.TotalAttack);
    }

    [Fact]
    public void Encode_WritesHiddenEquipmentMapFromBuild()
    {
        CharacterBuild build = new CharacterBuild
        {
            Gender = CharacterGender.Female,
            Skin = SkinColor.Light,
            Face = new FacePart { ItemId = 21000 },
            Hair = new HairPart { ItemId = 31000 }
        };

        build.Equip(new CharacterPart { ItemId = 1002140, Slot = EquipSlot.Cap, IsCash = true });
        build.EquipHidden(new CharacterPart { ItemId = 1002001, Slot = EquipSlot.Cap });

        byte[] payload = LoginAvatarLookCodec.Encode(build);

        Assert.True(LoginAvatarLookCodec.TryDecode(payload, out LoginAvatarLook look, out string error), error);
        Assert.Equal(1002140, look.VisibleEquipmentByBodyPart[1]);
        Assert.Equal(1002001, look.HiddenEquipmentByBodyPart[1]);
    }
}
