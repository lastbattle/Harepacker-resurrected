using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedSummonedTimerParityTests
    {
        private static readonly Type PacketOwnedSummonStateType = typeof(SummonedPool).GetNestedType(
            "PacketOwnedSummonState",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SummonedPool.PacketOwnedSummonState was not found.");

        private static readonly Type PacketOwnedSummonTimerType = typeof(SummonedPool).GetNestedType(
            "PacketOwnedSummonTimer",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SummonedPool.PacketOwnedSummonTimer was not found.");

        private static readonly MethodInfo UpdateSummonExpiryTimersMethod = typeof(SummonedPool).GetMethod(
            "UpdateSummonExpiryTimers",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SummonedPool.UpdateSummonExpiryTimers was not found.");

        private static readonly FieldInfo SummonsByObjectIdField = typeof(SummonedPool).GetField(
            "_summonsByObjectId",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SummonedPool._summonsByObjectId was not found.");

        private static readonly FieldInfo SummonExpiryTimersField = typeof(SummonedPool).GetField(
            "_summonExpiryTimers",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SummonedPool._summonExpiryTimers was not found.");

        [Fact]
        public void Type10ConnectedFamilyTimerInheritance_UsesParentFamilyTimer()
        {
            SkillData parent = CreateSkill(32001003, authoredDurationSeconds: 8);
            SkillData child = CreateSkill(32110007, clientInfoType: 10, affectedSkillIds: 32001003);
            IReadOnlyCollection<SkillData> catalog = new[] { child, parent };

            int durationMs = SummonedPool.ResolvePacketOwnedCreateDurationMs(
                child,
                levelData: null,
                skillLevel: 1,
                skillId: child.SkillId,
                ownerIsLocal: true,
                currentTime: 1000,
                localCancelFamilyRemainingDurationAccessor: (skillId, _) => skillId == parent.SkillId ? 7000 : 0,
                localSkillLevelAccessor: skillId => skillId == parent.SkillId ? 1 : 0,
                getSkillData: skillId => catalog.FirstOrDefault(skill => skill.SkillId == skillId),
                skillCatalog: catalog);

            Assert.Equal(7000, durationMs);
        }

        [Fact]
        public void MixedDummyOfAndAffectedSkillGraphTimerInheritance_ReachesConnectedParentTimer()
        {
            SkillData root = CreateSkill(35100008, dummySkillParents: new[] { 35101009, 35101010 });
            SkillData mid = CreateSkill(35101009, clientInfoType: 16, affectedSkillIds: 35001001);
            SkillData target = CreateSkill(35001001);
            IReadOnlyCollection<SkillData> catalog = new[] { root, mid, target };

            int durationMs = SummonedPool.ResolvePacketOwnedCreateDurationMs(
                target,
                levelData: null,
                skillLevel: 1,
                skillId: target.SkillId,
                ownerIsLocal: true,
                currentTime: 2500,
                localCancelFamilyRemainingDurationAccessor: (skillId, _) => skillId == root.SkillId ? 6200 : 0,
                localSkillLevelAccessor: skillId => skillId == root.SkillId ? 1 : 0,
                getSkillData: skillId => catalog.FirstOrDefault(skill => skill.SkillId == skillId),
                skillCatalog: catalog);

            Assert.Equal(6200, durationMs);
        }

        [Fact]
        public void InheritedLocalCancelFamilyDuration_PrefersLongestRunningFamilySummon()
        {
            SkillData parent = CreateSkill(32001003, authoredDurationSeconds: 8);
            SkillData child = CreateSkill(32110007, clientInfoType: 10, affectedSkillIds: 32001003);
            IReadOnlyCollection<SkillData> catalog = new[] { child, parent };
            ActiveSummon runningParentSummon = new()
            {
                SkillId = parent.SkillId,
                StartTime = 1000,
                Duration = 10000
            };

            int remainingMs = SummonedPool.ResolveInheritedLocalCancelFamilyDurationMs(
                child.SkillId,
                currentTime: 2000,
                localPacketOwnedSummons: new[] { runningParentSummon },
                localCancelFamilyRemainingDurationAccessor: (_, _) => 4000,
                getSkillData: skillId => catalog.FirstOrDefault(skill => skill.SkillId == skillId),
                skillCatalog: catalog);

            Assert.Equal(9000, remainingMs);
        }

        [Fact]
        public void UpdateSummonExpiryTimers_PublishesOwnerAwareOrderedExpiryBatch()
        {
            SummonedPool pool = new();
            List<PacketOwnedSummonTimerExpiration[]> batches = new();
            List<PacketOwnedSummonTimerExpiration> singles = new();
            pool.OnSummonExpiryTimersExpiredBatch = batch => batches.Add(batch);
            pool.OnSummonExpiryTimerExpired = expiration => singles.Add(expiration);

            AddTrackedSummon(pool, objectId: 41, skillId: 32110007, expireTime: 1000, ownerCharacterId: 101, ownerIsLocal: true);
            AddTrackedSummon(pool, objectId: 22, skillId: 35001001, expireTime: 900, ownerCharacterId: 202, ownerIsLocal: false);

            UpdateSummonExpiryTimersMethod.Invoke(pool, new object[] { 1200 });

            PacketOwnedSummonTimerExpiration[] batch = Assert.Single(batches);
            Assert.Equal(2, batch.Length);
            Assert.Equal(new[] { 22, 41 }, batch.Select(static expiration => expiration.SummonedObjectId).ToArray());
            Assert.Equal(new[] { 22, 41 }, singles.Select(static expiration => expiration.SummonedObjectId).ToArray());

            PacketOwnedSummonTimerExpiration remote = batch[0];
            Assert.Equal(202, remote.OwnerCharacterId);
            Assert.False(remote.OwnerIsLocal);
            Assert.Equal(1200, remote.CurrentTime);

            PacketOwnedSummonTimerExpiration local = batch[1];
            Assert.Equal(101, local.OwnerCharacterId);
            Assert.True(local.OwnerIsLocal);
            Assert.Equal(1200, local.CurrentTime);
        }

        [Fact]
        public void LocalPacketOwnedSummonExpiryCancelRouting_UsesCurrentTickAndSkipsRemoteOwners()
        {
            List<(int SkillId, int CurrentTime)> requests = new();

            bool localResult = MapSimulator.TryRouteLocalPacketOwnedSummonExpiryToClientCancel(
                new PacketOwnedSummonTimerExpiration(
                    SkillId: 32110007,
                    SummonedObjectId: 41,
                    ExpireTime: 1000,
                    CurrentTime: 1234,
                    OwnerCharacterId: 101,
                    OwnerIsLocal: true),
                (skillId, currentTime) =>
                {
                    requests.Add((skillId, currentTime));
                    return true;
                });

            bool remoteResult = MapSimulator.TryRouteLocalPacketOwnedSummonExpiryToClientCancel(
                new PacketOwnedSummonTimerExpiration(
                    SkillId: 35001001,
                    SummonedObjectId: 22,
                    ExpireTime: 900,
                    CurrentTime: 1234,
                    OwnerCharacterId: 202,
                    OwnerIsLocal: false),
                (skillId, currentTime) =>
                {
                    requests.Add((skillId, currentTime));
                    return true;
                });

            Assert.True(localResult);
            Assert.False(remoteResult);
            Assert.Equal(new[] { (32110007, 1234) }, requests);
        }

        private static SkillData CreateSkill(
            int skillId,
            int clientInfoType = 0,
            int[] affectedSkillIds = null,
            int[] dummySkillParents = null,
            int authoredDurationSeconds = 0)
        {
            Dictionary<int, SkillLevelData> levels = new();
            if (authoredDurationSeconds > 0)
            {
                levels[1] = new SkillLevelData
                {
                    Level = 1,
                    Time = authoredDurationSeconds
                };
            }

            return new SkillData
            {
                SkillId = skillId,
                ClientInfoType = clientInfoType,
                AffectedSkillIds = affectedSkillIds ?? Array.Empty<int>(),
                DummySkillParents = dummySkillParents ?? Array.Empty<int>(),
                Levels = levels
            };
        }

        private static void AddTrackedSummon(
            SummonedPool pool,
            int objectId,
            int skillId,
            int expireTime,
            int ownerCharacterId,
            bool ownerIsLocal)
        {
            ActiveSummon summon = new()
            {
                ObjectId = objectId,
                SkillId = skillId,
                StartTime = 100,
                Duration = expireTime - 100
            };

            object state = Activator.CreateInstance(PacketOwnedSummonStateType, nonPublic: true)
                ?? throw new InvalidOperationException("Failed to create PacketOwnedSummonState.");
            PacketOwnedSummonStateType.GetProperty("Summon", BindingFlags.Public | BindingFlags.Instance)?
                .SetValue(state, summon);
            PacketOwnedSummonStateType.GetProperty("OwnerCharacterId", BindingFlags.Public | BindingFlags.Instance)?
                .SetValue(state, ownerCharacterId);
            PacketOwnedSummonStateType.GetProperty("OwnerIsLocal", BindingFlags.Public | BindingFlags.Instance)?
                .SetValue(state, ownerIsLocal);

            object timer = Activator.CreateInstance(PacketOwnedSummonTimerType, nonPublic: true)
                ?? throw new InvalidOperationException("Failed to create PacketOwnedSummonTimer.");
            PacketOwnedSummonTimerType.GetProperty("SummonedObjectId", BindingFlags.Public | BindingFlags.Instance)?
                .SetValue(timer, objectId);
            PacketOwnedSummonTimerType.GetProperty("SkillId", BindingFlags.Public | BindingFlags.Instance)?
                .SetValue(timer, skillId);
            PacketOwnedSummonTimerType.GetProperty("ExpireTime", BindingFlags.Public | BindingFlags.Instance)?
                .SetValue(timer, expireTime);

            IDictionary summonsByObjectId = (IDictionary)(SummonsByObjectIdField.GetValue(pool)
                ?? throw new InvalidOperationException("SummonedPool._summonsByObjectId was null."));
            IList expiryTimers = (IList)(SummonExpiryTimersField.GetValue(pool)
                ?? throw new InvalidOperationException("SummonedPool._summonExpiryTimers was null."));

            summonsByObjectId[objectId] = state;
            expiryTimers.Add(timer);
        }
    }
}
