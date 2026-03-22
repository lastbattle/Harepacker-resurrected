using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

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

    private static PlayerCharacter CreatePlayer()
    {
        return new PlayerCharacter(new CharacterBuild());
    }
}
