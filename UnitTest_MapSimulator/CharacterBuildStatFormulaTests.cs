using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzProperties;
using System.Reflection;

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

    [Fact]
    public void TotalMaxHp_UsesSkillStatProviderBonus()
    {
        CharacterBuild build = new CharacterBuild
        {
            MaxHP = 5000,
            HP = 5000,
            SkillStatBonusProvider = stat => stat == BuffStatType.MaxHP ? 600 : 0
        };

        Assert.Equal(5600, build.TotalMaxHP);
        Assert.Equal(5000, build.TotalHP);
    }

    [Fact]
    public void SkillLoader_CreateLevelData_MapsPassiveAliasStatsForCaneExpert()
    {
        SkillData skill = new SkillData
        {
            Name = "Cane Expert",
            Description = "Increases Cane Mastery, Weapon Attack, and Minimum Critical Damage."
        };

        WzSubProperty node = new WzSubProperty("common");
        node.AddProperty(new WzStringProperty("padX", "x"));

        SkillLevelData levelData = InvokeCreateLevelData(skill, node, 30);

        Assert.Equal(30, levelData.PAD);
    }

    [Fact]
    public void SkillLoader_CreateLevelData_MapsPassiveAliasStatsForDualBowgunMastery()
    {
        SkillData skill = new SkillData
        {
            Name = "Dual Bowguns Mastery",
            Description = "Increases the weapon mastery and accuracy of Dual Bowguns."
        };

        WzSubProperty node = new WzSubProperty("common");
        node.AddProperty(new WzStringProperty("accX", "6*x"));

        SkillLevelData levelData = InvokeCreateLevelData(skill, node, 20);

        Assert.Equal(120, levelData.ACC);
    }

    [Fact]
    public void SkillLoader_CreateLevelData_MapsMechanicVehicleEnhancedStats()
    {
        SkillData skill = new SkillData
        {
            Name = "Extreme Mech",
            Description = "Further enhances the ATT, DEF, Max HP, Max MP, and Weapon Mastery of your Mech."
        };

        WzSubProperty node = new WzSubProperty("common");
        node.AddProperty(new WzStringProperty("epad", "25+u(x/2)"));
        node.AddProperty(new WzStringProperty("epdd", "400+20*x"));
        node.AddProperty(new WzStringProperty("emdd", "400+20*x"));
        node.AddProperty(new WzStringProperty("emhp", "600+20*x"));
        node.AddProperty(new WzStringProperty("emmp", "600+20*x"));

        SkillLevelData levelData = InvokeCreateLevelData(skill, node, 30);

        Assert.Equal(40, levelData.EnhancedPAD);
        Assert.Equal(1000, levelData.EnhancedPDD);
        Assert.Equal(1000, levelData.EnhancedMDD);
        Assert.Equal(1200, levelData.EnhancedMaxHP);
        Assert.Equal(1200, levelData.EnhancedMaxMP);
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

    private static SkillLevelData InvokeCreateLevelData(SkillData skill, WzImageProperty node, int level)
    {
        MethodInfo method = typeof(SkillLoader).GetMethod(
            "CreateLevelData",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        return (SkillLevelData)method.Invoke(null, [skill, node, level])!;
    }
}
