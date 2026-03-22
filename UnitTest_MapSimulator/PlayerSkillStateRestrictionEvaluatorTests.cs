using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapEditor.Instance.Shapes;
using System.Runtime.CompilerServices;

namespace UnitTest_MapSimulator;

public sealed class PlayerSkillStateRestrictionEvaluatorTests
{
    private static readonly SkillData TestSkill = new()
    {
        SkillId = 1001000,
        Name = "Test Skill"
    };

    [Fact]
    public void GetRestrictionMessage_BlocksStunSealAndAttractStatuses()
    {
        PlayerCharacter player = CreatePlayer();

        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Stun, 30_000, 1000);
        Assert.Equal("Skills cannot be used while stunned.", PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, TestSkill, 1000));

        player.ClearSkillBlockingStatuses();
        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Seal, 30_000, 1000);
        Assert.Equal("Skills cannot be used while sealed.", PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, TestSkill, 1000));

        player.ClearSkillBlockingStatuses();
        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Attract, 10_000, 1000);
        Assert.Equal("Skills cannot be used while seduced.", PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, TestSkill, 1000));
    }

    [Fact]
    public void GetRestrictionMessage_ExpiresBlockingStatusesAtTheirEndTime()
    {
        PlayerCharacter player = CreatePlayer();
        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Seal, 5000, 1000);

        Assert.False(PlayerSkillStateRestrictionEvaluator.CanUseSkill(player, TestSkill, 5999));
        Assert.True(PlayerSkillStateRestrictionEvaluator.CanUseSkill(player, TestSkill, 6000));
    }

    [Fact]
    public void GetRestrictionMessage_PrioritizesClientSkillAvailableOrdering()
    {
        PlayerCharacter player = CreatePlayer();
        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Attract, 10_000, 1000);
        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Seal, 30_000, 1000);
        player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Stun, 30_000, 1000);

        Assert.Equal("Skills cannot be used while stunned.", PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, TestSkill, 1000));
    }

    [Fact]
    public void GetRestrictionMessage_BlocksFlashJumpSkillsWhileGrounded()
    {
        PlayerCharacter player = CreatePlayer();
        SetOnFoothold(player, onFoothold: true);

        SkillData flashJump = new()
        {
            SkillId = 4111006,
            ClientInfoType = 40,
            CasterMove = true,
            AvailableInJumpingState = true
        };

        Assert.Equal(
            "Bound-jump skills must be chained while airborne.",
            PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, flashJump, 1000));
    }

    [Fact]
    public void GetRestrictionMessage_BlocksFlashJumpSkillsAfterJumpTurnsIntoFall()
    {
        PlayerCharacter player = CreatePlayer();
        SetOnFoothold(player, onFoothold: false);
        player.Physics.VelocityY = 120;

        SkillData flashJump = new()
        {
            SkillId = 4211009,
            ClientInfoType = 40,
            CasterMove = true,
            AvailableInJumpingState = true
        };

        Assert.Equal(
            "Bound-jump skills cannot be used after the jump has already turned into a fall.",
            PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, flashJump, 1000));
    }

    [Fact]
    public void GetRestrictionMessage_BlocksGroundStartedBoundJumpSkillsInAir()
    {
        PlayerCharacter player = CreatePlayer();
        SetOnFoothold(player, onFoothold: false);

        SkillData windWalk = new()
        {
            SkillId = 11101005,
            ClientInfoType = 41,
            CasterMove = true
        };

        Assert.Equal(
            "This movement skill must start from the ground.",
            PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, windWalk, 1000));
    }

    [Fact]
    public void GetRestrictionMessage_BlocksJaguarJumpWhileFlyingMapMovementIsActive()
    {
        PlayerCharacter player = CreatePlayer();
        SetOnFoothold(player, onFoothold: true);
        player.Physics.IsFlyingMap = true;
        player.Physics.IsFlying = true;

        SkillData jaguarJump = new()
        {
            SkillId = 33001002,
            ClientInfoType = 40,
            CasterMove = true,
            AvailableInJumpingState = true
        };

        Assert.Equal(
            "This movement skill cannot be used while swimming or flying.",
            PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, jaguarJump, 1000));
    }

    private static PlayerCharacter CreatePlayer()
    {
        return new PlayerCharacter(new CharacterBuild());
    }

    private static void SetOnFoothold(PlayerCharacter player, bool onFoothold)
    {
        player.Physics.CurrentFoothold = onFoothold
            ? (FootholdLine)RuntimeHelpers.GetUninitializedObject(typeof(FootholdLine))
            : null;
        player.Physics.VelocityY = 0;
        player.Physics.IsOnLadderOrRope = false;
        player.Physics.IsFlying = false;
        player.Physics.IsFlyingMap = false;
        player.Physics.IsInSwimArea = false;
    }
}
