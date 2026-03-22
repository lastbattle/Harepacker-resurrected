using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class CharacterBuildStatFormulaTests
{
    [Fact]
    public void IncreaseMaxHp_UsesThiefGrowthForPhantomJobs()
    {
        CharacterBuild build = new CharacterBuild
        {
            Job = 2412,
            AP = 1,
            MaxHP = 100,
            HP = 100
        };

        int observedMin = -1;
        int observedMax = -1;

        bool increased = build.IncreaseMaxHp((min, max) =>
        {
            observedMin = min;
            observedMax = max;
            return max;
        });

        Assert.True(increased);
        Assert.Equal(16, observedMin);
        Assert.Equal(20, observedMax);
        Assert.Equal(120, build.MaxHP);
        Assert.Equal(120, build.HP);
        Assert.Equal(0, build.AP);
    }

    [Fact]
    public void TotalAttack_UsesCaneFormulaForPhantomJobs()
    {
        CharacterBuild build = new CharacterBuild
        {
            Job = 2412,
            STR = 4,
            DEX = 50,
            LUK = 200
        };

        build.Equip(CreateWeapon(1362000, 100));

        Assert.Equal(442, build.TotalAttack);
    }

    [Fact]
    public void TotalAttack_UsesDualBowgunFormulaForMercedesJobs()
    {
        CharacterBuild build = new CharacterBuild
        {
            Job = 2312,
            STR = 50,
            DEX = 200,
            LUK = 4
        };

        build.Equip(CreateWeapon(1522000, 100));

        Assert.Equal(442, build.TotalAttack);
    }

    private static WeaponPart CreateWeapon(int itemId, int weaponAttack)
    {
        return new WeaponPart
        {
            ItemId = itemId,
            Slot = EquipSlot.Weapon,
            BonusWeaponAttack = weaponAttack
        };
    }
}
