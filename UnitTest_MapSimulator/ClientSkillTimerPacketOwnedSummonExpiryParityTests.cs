using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class ClientSkillTimerPacketOwnedSummonExpiryParityTests
{
    [Fact]
    public void ResolvePacketOwnedExpiryTargetOrder_UsesBranchSpecificRangeForSelfDestructAttack()
    {
        var summon = new ActiveSummon
        {
            PositionX = 100,
            PositionY = 100,
            FacingRight = true,
            AssistType = SummonAssistType.PeriodicAttack,
            CurrentAnimationBranchName = "attackTriangle",
            SkillData = new SkillData
            {
                SummonAttackRangeLeft = 120,
                SummonAttackRangeRight = 0,
                SummonAttackRangeTop = -30,
                SummonAttackRangeBottom = 30,
                SummonNamedRangeMetadata = new Dictionary<string, SkillData.SummonRangeMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["attackTriangle"] = new(0, 120, -30, 30)
                }
            }
        };

        int[] result = SummonedPool.ResolvePacketOwnedExpiryTargetOrder(
            summon,
            new[]
            {
                new SummonedPool.PacketOwnedExpiryTargetCandidate(1, new Rectangle(130, 80, 20, 40)),
                new SummonedPool.PacketOwnedExpiryTargetCandidate(2, new Rectangle(10, 80, 20, 40))
            },
            maxTargets: 2);

        Assert.Equal(new[] { 1 }, result);
    }

    [Fact]
    public void ResolvePacketOwnedExpiryTargetOrder_MatchesLocalSummonDistanceForwardAndVerticalTieBreaks()
    {
        var summon = new ActiveSummon
        {
            PositionX = 100,
            PositionY = 100,
            FacingRight = true,
            AssistType = SummonAssistType.PeriodicAttack,
            SkillData = new SkillData
            {
                SummonAttackRangeLeft = 120,
                SummonAttackRangeRight = 120,
                SummonAttackRangeTop = -60,
                SummonAttackRangeBottom = 60
            }
        };

        int[] result = SummonedPool.ResolvePacketOwnedExpiryTargetOrder(
            summon,
            new[]
            {
                new SummonedPool.PacketOwnedExpiryTargetCandidate(1, new Rectangle(120, 90, 20, 20)),
                new SummonedPool.PacketOwnedExpiryTargetCandidate(2, new Rectangle(60, 90, 20, 20)),
                new SummonedPool.PacketOwnedExpiryTargetCandidate(3, new Rectangle(90, 120, 20, 20))
            },
            maxTargets: 3);

        Assert.Equal(new[] { 1, 3, 2 }, result);
    }
}
