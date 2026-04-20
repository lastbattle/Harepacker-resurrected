using HaCreator.MapSimulator.Character.Skills;
using System.Collections.Generic;
using System.Linq;

namespace UnitTest_MapSimulator;

public sealed class ClientTimerSkillCancelResolverParityTests
{
    [Theory]
    [InlineData(32120000, 32001003)]
    [InlineData(32110000, 32101002)]
    [InlineData(32120001, 32101003)]
    public void NormalizeClientCancelRequestSkillId_MapsBattleMageAliases(int aliasSkillId, int expectedSkillId)
    {
        int normalizedSkillId = ClientSkillCancelResolver.NormalizeClientCancelRequestSkillId(aliasSkillId);
        Assert.Equal(expectedSkillId, normalizedSkillId);
    }

    [Theory]
    [InlineData(32001003, 32120000)]
    [InlineData(32101002, 32110000)]
    [InlineData(32101003, 32120001)]
    public void DoesClientCancelMatchSkillId_TreatsBattleMageAliasesAsEquivalent(int activeSkillId, int requestSkillId)
    {
        bool matched = ClientSkillCancelResolver.DoesClientCancelMatchSkillId(
            activeSkillId,
            requestSkillId,
            _ => null,
            null);

        Assert.True(matched);
    }

    [Fact]
    public void ResolveConnectedCancelFamilySkillIds_FollowsAffectedSkillAliasLinks()
    {
        SkillData linkedSkill = new()
        {
            SkillId = 90000001,
            ClientInfoType = 1,
            AffectedSkillIds = new[] { 32120000 }
        };
        SkillData[] catalog = { linkedSkill };
        Dictionary<int, SkillData> skillsById = new()
        {
            [linkedSkill.SkillId] = linkedSkill
        };

        IReadOnlyList<int> connectedFamily = ClientSkillCancelResolver.ResolveConnectedCancelFamilySkillIds(
            32001003,
            skillId => skillsById.TryGetValue(skillId, out SkillData skill) ? skill : null,
            catalog);

        Assert.Contains(32001003, connectedFamily);
        Assert.Contains(32120000, connectedFamily);
        Assert.Contains(90000001, connectedFamily);
    }

    [Fact]
    public void TryRegisterClientTimerBatchCancelFamily_DedupesSharedFamilyKey()
    {
        HashSet<int> activeCancelFamilyKeys = new();

        bool firstAdded = SkillManager.TryRegisterClientTimerBatchCancelFamily(
            activeCancelFamilyKeys,
            32001003,
            skillId => skillId == 32001003 || skillId == 32120000
                ? new[] { 32001003, 32120000 }
                : new[] { skillId });
        bool secondAdded = SkillManager.TryRegisterClientTimerBatchCancelFamily(
            activeCancelFamilyKeys,
            32120000,
            skillId => skillId == 32001003 || skillId == 32120000
                ? new[] { 32001003, 32120000 }
                : new[] { skillId });

        Assert.True(firstAdded);
        Assert.False(secondAdded);
        Assert.Single(activeCancelFamilyKeys);
        Assert.Equal(32001003, activeCancelFamilyKeys.Single());
    }
}
