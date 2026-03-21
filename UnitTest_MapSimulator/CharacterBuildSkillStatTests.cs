using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class CharacterBuildSkillStatTests
{
    [Fact]
    public void TotalsIncludeSkillRuntimeBonuses()
    {
        var build = new CharacterBuild
        {
            Attack = 18,
            Defense = 11,
            MagicAttack = 12,
            MagicDefense = 10,
            Accuracy = 3,
            Avoidability = 4,
            CriticalRate = 5,
            Speed = 100,
            JumpPower = 100,
            SkillStatBonusProvider = stat => stat switch
            {
                BuffStatType.Attack => 7,
                BuffStatType.Defense => 6,
                BuffStatType.MagicAttack => 5,
                BuffStatType.MagicDefense => 4,
                BuffStatType.Accuracy => 3,
                BuffStatType.Avoidability => 2,
                BuffStatType.CriticalRate => 1,
                BuffStatType.Speed => 8,
                BuffStatType.Jump => 9,
                _ => 0
            }
        };

        Assert.Equal(15, build.TotalAttack);
        Assert.Equal(12, build.TotalDefense);
        Assert.Equal(12, build.TotalMagicAttack);
        Assert.Equal(9, build.TotalMagicDefense);
        Assert.Equal(11, build.TotalAccuracy);
        Assert.Equal(9, build.TotalAvoidability);
        Assert.Equal(6, build.TotalCriticalRate);
        Assert.Equal(108f, build.TotalSpeed);
        Assert.Equal(109f, build.TotalJumpPower);
    }

    [Fact]
    public void TotalMasteryUsesSkillProviderWithBaseFallback()
    {
        var build = new CharacterBuild();
        Assert.Equal(10, build.TotalMastery);

        build.SkillMasteryProvider = () => 60;
        Assert.Equal(60, build.TotalMastery);
    }
}
