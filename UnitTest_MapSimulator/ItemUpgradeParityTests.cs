using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class ItemUpgradeParityTests
{
    [Fact]
    public void ResolveAuthoredStatDeltaProfile_ParsesGroupedStatPhrases()
    {
        var profile = ItemUpgradeUI.ResolveAuthoredStatDeltaProfileForTesting(
            "All stats +3, Weapon/Magic ATT+ 2, Weapon/Magic DEF+ 4, HP/MP +50, Accuracy/Aviodability+1, Speed/Jump +2");

        Assert.Equal(2, profile.WeaponAttack);
        Assert.Equal(2, profile.MagicAttack);
        Assert.Equal(4, profile.WeaponDefense);
        Assert.Equal(4, profile.MagicDefense);
        Assert.Equal(3, profile.Strength);
        Assert.Equal(3, profile.Dexterity);
        Assert.Equal(3, profile.Intelligence);
        Assert.Equal(3, profile.Luck);
        Assert.Equal(50, profile.MaxHp);
        Assert.Equal(50, profile.MaxMp);
        Assert.Equal(1, profile.Accuracy);
        Assert.Equal(1, profile.Avoidability);
        Assert.Equal(2, profile.Speed);
        Assert.Equal(2, profile.Jump);
    }

    [Fact]
    public void ResolveSuccessRateFromDescription_ParsesNumberFirstWording()
    {
        float successRate = ItemUpgradeUI.ResolveSuccessRateFromDescriptionForTesting(
            "Improves Diligence on Armor with durability.\n70% success rate.\nDiligence +2");

        Assert.Equal(0.7f, successRate, 3);
    }

    [Fact]
    public void ResolveTargetSlots_PrefersVegaScrollNameWhenDescriptionConflicts()
    {
        IReadOnlyCollection<EquipSlot> slots = ItemUpgradeUI.ResolveTargetSlotsForTesting(
            "Scroll for Shoes for ATT 60%",
            "Improves attack on gloves.\nSuccess rate: 60%. Weapon Attack +2. The success rate of this scroll can be enhanced by Vega's Spell.");

        Assert.Contains(EquipSlot.Shoes, slots);
        Assert.DoesNotContain(EquipSlot.Glove, slots);
    }

    [Fact]
    public void ResolveTargetSlots_RecognizesLegacyTargetLabels()
    {
        IReadOnlyCollection<EquipSlot> slots = ItemUpgradeUI.ResolveTargetSlotsForTesting(
            "Scroll for Face Eqp. for Avoidability 30%",
            "Improves Avoidability on Face Eqp.\nSuccess Rate:30%, Avoidability +2, Dex +2");

        Assert.Contains(EquipSlot.FaceAccessory, slots);
    }

    [Fact]
    public void ResolveDestroyChanceFromDescription_ParsesHyphenatedDestroyText()
    {
        float destroyChance = ItemUpgradeUI.ResolveDestroyChanceFromDescriptionForTesting(
            "If it fails, the item has a 50%-chance of being destroyed.");

        Assert.Equal(0.5f, destroyChance, 3);
    }

    [Fact]
    public void ResolveDestroyChanceFromDescription_ParsesCompleteDestructionText()
    {
        float destroyChance = ItemUpgradeUI.ResolveDestroyChanceFromDescriptionForTesting(
            "Upon failure, the item will be destroyed completely.");

        Assert.Equal(1.0f, destroyChance, 3);
    }
}
