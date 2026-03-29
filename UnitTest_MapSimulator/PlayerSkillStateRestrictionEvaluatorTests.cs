using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using System.Runtime.CompilerServices;

namespace UnitTest_MapSimulator;

public sealed class PlayerSkillStateRestrictionEvaluatorTests
{
    [Fact]
    public void BoundJumpActionProfileRequiresAirborneChainEvenWithoutType40()
    {
        var player = CreatePlayer();
        player.Physics.CurrentFoothold = CreateFoothold();

        var skill = new SkillData
        {
            SkillId = 9999999,
            Name = "Test Assaulter",
            ClientInfoType = 1,
            CasterMove = true,
            AvailableInJumpingState = true,
            PrepareActionName = "dash",
            ActionName = "assaulter"
        };

        string restriction = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, skill, currentTime: 0);

        Assert.Equal("Bound-jump skills must be chained while airborne.", restriction);
    }

    [Fact]
    public void BoundJumpActionProfileAllowsAirborneChainWhenJumpIsActive()
    {
        var player = CreatePlayer();
        player.Physics.CurrentFoothold = null;
        player.Physics.IsOnLadderOrRope = false;
        player.Physics.SetVelocity(0f, -120f);

        var skill = new SkillData
        {
            SkillId = 9999999,
            Name = "Test Assaulter",
            ClientInfoType = 1,
            CasterMove = true,
            AvailableInJumpingState = true,
            PrepareActionName = "dash",
            ActionName = "assaulter"
        };

        string restriction = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, skill, currentTime: 0);

        Assert.Null(restriction);
    }

    [Fact]
    public void HighestJumpSkillsRequireNearApexWindow()
    {
        var player = CreatePlayer();
        player.Physics.CurrentFoothold = null;
        player.Physics.IsOnLadderOrRope = false;
        player.Physics.SetVelocity(0f, -120f);

        var skill = new SkillData
        {
            SkillId = 23111001,
            Name = "Monsoon",
            AvailableInJumpingState = true,
            RequireHighestJump = true,
            CasterMove = true,
            ActionName = "elfTornado"
        };

        string restriction = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, skill, currentTime: 0);

        Assert.Equal("This skill must be used near the top of a jump.", restriction);

        player.Physics.SetVelocity(0f, -40f);
        restriction = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, skill, currentTime: 0);

        Assert.Null(restriction);
    }

    private static PlayerCharacter CreatePlayer()
    {
        return new PlayerCharacter(new CharacterBuild());
    }

    private static FootholdLine CreateFoothold()
    {
        return (FootholdLine)RuntimeHelpers.GetUninitializedObject(typeof(FootholdLine));
    }
}
