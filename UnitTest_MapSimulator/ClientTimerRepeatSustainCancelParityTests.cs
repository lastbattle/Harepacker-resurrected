using HaCreator.MapSimulator.Character.Skills;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class ClientTimerRepeatSustainCancelParityTests
    {
        [Fact]
        public void RouteExpiredRepeatSustainTimerBatchToClientCancel_GatesBySourceAndDedupesCancelFamily()
        {
            var expirations = new[]
            {
                new SkillManager.ClientSkillTimerExpiration(35111004, "repeat-sustain-end", 1000),
                new SkillManager.ClientSkillTimerExpiration(35121013, "repeat-sustain-end", 1001),
                new SkillManager.ClientSkillTimerExpiration(35111004, "buff-expire", 1002),
                new SkillManager.ClientSkillTimerExpiration(0, "repeat-sustain-end", 1003)
            };

            List<int> requestedSkillIds = new();
            int routed = SkillManager.RouteExpiredRepeatSustainTimerBatchToClientCancel(
                expirations,
                ResolveConnectedRepeatCancelFamily,
                (skillId, _) =>
                {
                    requestedSkillIds.Add(skillId);
                    return true;
                });

            Assert.Equal(1, routed);
            Assert.Single(requestedSkillIds);
            Assert.Equal(35111004, requestedSkillIds[0]);
        }

        [Fact]
        public void TryRegisterClientTimerBatchCancelFamily_DedupesBattleMageAliasFamily()
        {
            HashSet<int> activeFamilyKeys = new();

            bool firstAdded = SkillManager.TryRegisterClientTimerBatchCancelFamily(
                activeFamilyKeys,
                32120000,
                ResolveBattleMageAliasCancelFamily);
            bool secondAdded = SkillManager.TryRegisterClientTimerBatchCancelFamily(
                activeFamilyKeys,
                32001003,
                ResolveBattleMageAliasCancelFamily);

            Assert.True(firstAdded);
            Assert.False(secondAdded);
            Assert.Single(activeFamilyKeys);
        }

        [Fact]
        public void TryRegisterClientTimerBatchCancelFamily_RejectsInvalidInputs()
        {
            HashSet<int> activeFamilyKeys = new();

            Assert.False(SkillManager.TryRegisterClientTimerBatchCancelFamily(
                activeFamilyKeys,
                0,
                ResolveConnectedRepeatCancelFamily));
            Assert.False(SkillManager.TryRegisterClientTimerBatchCancelFamily(
                null,
                35111004,
                ResolveConnectedRepeatCancelFamily));
            Assert.Empty(activeFamilyKeys);
        }

        [Fact]
        public void ShouldSuppressClientCancelBatchRequest_DedupesAliasFamilyWithinActiveBatch()
        {
            HashSet<int> activeFamilyKeys = new();

            bool firstSuppressed = SkillManager.ShouldSuppressClientCancelBatchRequest(
                isDispatchingBatch: true,
                activeFamilyKeys,
                32120000,
                ResolveBattleMageAliasCancelFamily);
            bool secondSuppressed = SkillManager.ShouldSuppressClientCancelBatchRequest(
                isDispatchingBatch: true,
                activeFamilyKeys,
                32001003,
                ResolveBattleMageAliasCancelFamily);

            Assert.False(firstSuppressed);
            Assert.True(secondSuppressed);
            Assert.Single(activeFamilyKeys);
        }

        [Fact]
        public void ShouldSuppressClientCancelBatchRequest_DoesNotSuppressWhenInactiveOrInvalid()
        {
            HashSet<int> activeFamilyKeys = new();

            Assert.False(SkillManager.ShouldSuppressClientCancelBatchRequest(
                isDispatchingBatch: false,
                activeFamilyKeys,
                35111004,
                ResolveConnectedRepeatCancelFamily));
            Assert.False(SkillManager.ShouldSuppressClientCancelBatchRequest(
                isDispatchingBatch: true,
                activeFamilyKeys,
                0,
                ResolveConnectedRepeatCancelFamily));
            Assert.False(SkillManager.ShouldSuppressClientCancelBatchRequest(
                isDispatchingBatch: true,
                null,
                35111004,
                ResolveConnectedRepeatCancelFamily));
            Assert.Empty(activeFamilyKeys);
        }

        [Fact]
        public void BeginClientCancelBatchScope_ClearsOnlyOnOuterScopeEntry()
        {
            int scopeDepth = 0;
            HashSet<int> activeFamilyKeys = new() { 32120000 };

            SkillManager.BeginClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);

            Assert.Equal(1, scopeDepth);
            Assert.Empty(activeFamilyKeys);

            activeFamilyKeys.Add(32001003);
            SkillManager.BeginClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);

            Assert.Equal(2, scopeDepth);
            Assert.Single(activeFamilyKeys);
        }

        [Fact]
        public void EndClientCancelBatchScope_ClearsOnFinalScopeExitAndClampsInvalidDepth()
        {
            int scopeDepth = 0;
            HashSet<int> activeFamilyKeys = new();

            SkillManager.BeginClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);
            SkillManager.BeginClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);
            activeFamilyKeys.Add(32120001);

            SkillManager.EndClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);
            Assert.Equal(1, scopeDepth);
            Assert.Single(activeFamilyKeys);

            SkillManager.EndClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);
            Assert.Equal(0, scopeDepth);
            Assert.Empty(activeFamilyKeys);

            scopeDepth = -5;
            activeFamilyKeys.Add(32101003);
            SkillManager.EndClientCancelBatchScope(ref scopeDepth, activeFamilyKeys);
            Assert.Equal(0, scopeDepth);
            Assert.Single(activeFamilyKeys);
        }

        private static IReadOnlyList<int> ResolveConnectedRepeatCancelFamily(int skillId)
        {
            if (skillId == 35111004 || skillId == 35121013)
            {
                return new[] { 35111004, 35121013 };
            }

            return new[] { skillId };
        }

        private static IReadOnlyList<int> ResolveBattleMageAliasCancelFamily(int skillId)
        {
            return skillId switch
            {
                32120000 or 32001003 => new[] { 32120000, 32001003 },
                32110000 or 32101002 => new[] { 32110000, 32101002 },
                32120001 or 32101003 => new[] { 32120001, 32101003 },
                _ => new[] { skillId }
            };
        }
    }
}
