using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class EquipmentWindowParityTests
{
    [Fact]
    public void PlaceEquipment_BaseEquipUnderCash_ReplacesHiddenBaseOnly()
    {
        CharacterBuild build = new();
        CharacterPart concealedBase = new()
        {
            ItemId = 1040000,
            Name = "Old Coat",
            Slot = EquipSlot.Coat
        };
        CharacterPart visibleCash = new()
        {
            ItemId = 1703000,
            Name = "Cash Coat",
            Slot = EquipSlot.Coat,
            IsCash = true
        };
        CharacterPart incomingBase = new()
        {
            ItemId = 1042000,
            Name = "New Coat",
            Slot = EquipSlot.Coat
        };

        build.Equip(visibleCash);
        build.EquipHidden(concealedBase);

        IReadOnlyList<CharacterPart> displaced = build.PlaceEquipment(incomingBase, EquipSlot.Coat);

        CharacterPart visiblePart = Assert.IsType<CharacterPart>(build.Equipment[EquipSlot.Coat]);
        CharacterPart hiddenPart = Assert.IsType<CharacterPart>(build.HiddenEquipment[EquipSlot.Coat]);
        Assert.Same(visibleCash, visiblePart);
        Assert.Same(incomingBase, hiddenPart);
        CharacterPart displacedPart = Assert.Single(displaced);
        Assert.Same(concealedBase, displacedPart);
    }

    [Fact]
    public void Unequip_CashEquip_RestoresHiddenBase()
    {
        CharacterBuild build = new();
        CharacterPart concealedBase = new()
        {
            ItemId = 1072000,
            Name = "Battle Shoes",
            Slot = EquipSlot.Shoes
        };
        CharacterPart visibleCash = new()
        {
            ItemId = 1704000,
            Name = "Cash Shoes",
            Slot = EquipSlot.Shoes,
            IsCash = true
        };

        build.Equip(visibleCash);
        build.EquipHidden(concealedBase);

        CharacterPart removed = build.Unequip(EquipSlot.Shoes);

        Assert.Same(visibleCash, removed);
        CharacterPart visiblePart = Assert.IsType<CharacterPart>(build.Equipment[EquipSlot.Shoes]);
        Assert.Same(concealedBase, visiblePart);
        Assert.False(build.HiddenEquipment.ContainsKey(EquipSlot.Shoes));
    }
}
