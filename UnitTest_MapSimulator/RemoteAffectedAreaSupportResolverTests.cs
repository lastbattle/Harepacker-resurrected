using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public class RemoteAffectedAreaSupportResolverTests
{
    [Fact]
    public void HasProjectableSupportBuffMetadata_IgnoresPureRecoveryOnlyData()
    {
        var levelData = new SkillLevelData
        {
            HP = 400,
            MP = 200
        };

        Assert.False(RemoteAffectedAreaSupportResolver.HasProjectableSupportBuffMetadata(levelData));
        Assert.Null(RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(levelData));
    }

    [Fact]
    public void CreateProjectedSupportBuffLevelData_RetainsSupportedStatsAndDropsRecoveryOnlyFields()
    {
        var levelData = new SkillLevelData
        {
            Level = 12,
            PAD = 22,
            Speed = 15,
            STR = 8,
            AllStat = 5,
            CriticalRate = 12,
            HP = 300,
            MP = 150
        };

        SkillLevelData projected = RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(levelData);

        Assert.NotNull(projected);
        Assert.Equal(12, projected.Level);
        Assert.Equal(22, projected.PAD);
        Assert.Equal(15, projected.Speed);
        Assert.Equal(8, projected.STR);
        Assert.Equal(5, projected.AllStat);
        Assert.Equal(12, projected.CriticalRate);
        Assert.Equal(0, projected.HP);
        Assert.Equal(0, projected.MP);
    }

    [Fact]
    public void CharacterData_TotalPrimaryStats_IncludeSkillStatBonuses()
    {
        var character = new CharacterData
        {
            STR = 10,
            DEX = 20,
            INT = 30,
            LUK = 40,
            SkillStatBonusProvider = stat => stat switch
            {
                BuffStatType.Strength => 3,
                BuffStatType.Dexterity => 4,
                BuffStatType.Intelligence => 5,
                BuffStatType.Luck => 6,
                _ => 0
            }
        };

        Assert.Equal(13, character.TotalSTR);
        Assert.Equal(24, character.TotalDEX);
        Assert.Equal(35, character.TotalINT);
        Assert.Equal(46, character.TotalLUK);
    }
}
