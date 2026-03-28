using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class AbilityWindowParityTests
{
    [Fact]
    public void TotalHands_IncludesEquippedCraftBonus()
    {
        CharacterBuild build = new()
        {
            DEX = 25,
            INT = 15,
            LUK = 20,
            Hands = 7
        };

        build.Equip(new CharacterPart
        {
            Slot = EquipSlot.Glove,
            BonusDEX = 5,
            BonusINT = 3,
            BonusLUK = 4,
            BonusHands = 11
        });

        Assert.Equal(90, build.TotalHands);
    }

    [Fact]
    public void TotalMaxHpAndMp_ApplyFlatAndPercentSkillBonuses()
    {
        CharacterBuild build = new()
        {
            MaxHP = 1000,
            MaxMP = 800,
            SkillStatBonusProvider = stat => stat switch
            {
                BuffStatType.MaxHP => 50,
                BuffStatType.MaxMP => 40,
                BuffStatType.MaxHPPercent => 20,
                BuffStatType.MaxMPPercent => 25,
                _ => 0
            }
        };

        build.Equip(new CharacterPart
        {
            Slot = EquipSlot.Coat,
            BonusHP = 100,
            BonusMP = 60
        });

        Assert.Equal(1380, build.TotalMaxHP);
        Assert.Equal(1125, build.TotalMaxMP);
    }
}
