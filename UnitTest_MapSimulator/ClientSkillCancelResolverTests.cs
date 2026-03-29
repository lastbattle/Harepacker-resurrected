using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class ClientSkillCancelResolverTests
{
    [Fact]
    public void ResolveCancelRequestSkillIds_PreservesAllAffectedSkillTargets()
    {
        SkillData parentSkill = new()
        {
            SkillId = 21120011,
            ClientInfoType = 50,
            AffectedSkillIds = new[] { 21100002, 21110003 }
        };

        IReadOnlyList<int> resolved = ClientSkillCancelResolver.ResolveCancelRequestSkillIds(
            parentSkill.SkillId,
            skillId => skillId == parentSkill.SkillId ? parentSkill : null,
            new[] { parentSkill });

        Assert.Equal(new[] { 21100002, 21110003 }, resolved);
    }

    [Fact]
    public void DoesClientCancelMatchSkillId_MatchesEveryAffectedSkillTarget()
    {
        SkillData parentSkill = new()
        {
            SkillId = 21120011,
            ClientInfoType = 50,
            AffectedSkillIds = new[] { 21100002, 21110003 }
        };

        Assert.True(ClientSkillCancelResolver.DoesClientCancelMatchSkillId(
            parentSkill.SkillId,
            21100002,
            skillId => skillId == parentSkill.SkillId ? parentSkill : null,
            new[] { parentSkill }));

        Assert.True(ClientSkillCancelResolver.DoesClientCancelMatchSkillId(
            parentSkill.SkillId,
            21110003,
            skillId => skillId == parentSkill.SkillId ? parentSkill : null,
            new[] { parentSkill }));
    }

    [Fact]
    public void ResolveCancelRequestSkillIds_MapsDummyChildrenBackToParent()
    {
        SkillData parentSkill = new()
        {
            SkillId = 35100008,
            DummySkillParents = new[] { 35101009, 35101010 }
        };

        IReadOnlyList<int> resolved = ClientSkillCancelResolver.ResolveCancelRequestSkillIds(
            35101010,
            _ => null,
            new[] { parentSkill });

        Assert.Equal(new[] { 35100008 }, resolved);
    }
}
