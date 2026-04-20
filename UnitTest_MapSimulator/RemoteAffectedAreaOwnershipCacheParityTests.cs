using System.Collections.Generic;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public class RemoteAffectedAreaOwnershipCacheParityTests
{
    [Fact]
    public void BattlefieldOwnerTeamResolution_PrefersAreaSnapshotBeforeOwnerCache()
    {
        bool resolved = MapSimulator.TryResolveBattlefieldAffectedAreaOwnerTeamWithAreaSnapshot(
            ownerId: 101,
            areaObjectId: 5001,
            localTeamId: 1,
            cachedOwnerTeams: new Dictionary<int, int> { [101] = 2 },
            cachedAreaOwnerTeams: new Dictionary<int, int> { [5001] = 1 },
            resolveRuntimeOwnerTeamId: _ => null,
            out int ownerTeamId);

        Assert.True(resolved);
        Assert.Equal(1, ownerTeamId);
    }

    [Fact]
    public void MonsterCarnivalOwnerTeamResolution_PrefersAreaSnapshotBeforeOwnerCache()
    {
        bool resolved = MapSimulator.TryResolveMonsterCarnivalAffectedAreaOwnerTeamWithAreaSnapshot(
            ownerId: 202,
            areaObjectId: 6002,
            cachedOwnerTeams: new Dictionary<int, MonsterCarnivalTeam> { [202] = MonsterCarnivalTeam.Team1 },
            cachedAreaOwnerTeams: new Dictionary<int, MonsterCarnivalTeam> { [6002] = MonsterCarnivalTeam.Team0 },
            resolveOwnerName: _ => null,
            resolveCharacterTeam: _ => null,
            out MonsterCarnivalTeam team);

        Assert.True(resolved);
        Assert.Equal(MonsterCarnivalTeam.Team0, team);
    }

    [Fact]
    public void ResolveOwnerNameWithAreaSnapshot_PrefersAreaSnapshotBeforeOwnerCache()
    {
        string ownerName = MapSimulator.ResolveRemoteAffectedAreaOwnerName(
            areaObjectId: 7003,
            ownerId: 303,
            cachedOwnerNamesByAreaObjectId: new Dictionary<int, string> { [7003] = "AreaSnapshotName" },
            cachedOwnerNames: new Dictionary<int, string> { [303] = "OwnerCacheName" },
            resolveLiveOwnerName: _ => null);

        Assert.Equal("AreaSnapshotName", ownerName);
    }

    [Fact]
    public void ResolveOwnerNameWithAreaSnapshot_PrefersLiveOwnerNameFirst()
    {
        string ownerName = MapSimulator.ResolveRemoteAffectedAreaOwnerName(
            areaObjectId: 8004,
            ownerId: 404,
            cachedOwnerNamesByAreaObjectId: new Dictionary<int, string> { [8004] = "AreaSnapshotName" },
            cachedOwnerNames: new Dictionary<int, string> { [404] = "OwnerCacheName" },
            resolveLiveOwnerName: _ => "LiveOwner");

        Assert.Equal("LiveOwner", ownerName);
    }

    [Fact]
    public void HostileOwnerFallback_AssumptionRejectedForPartyMember()
    {
        bool assumeEnemy = MapSimulator.ShouldAssumeRemoteAffectedAreaOwnerIsEnemyFromHostileMetadata(
            hasResolvedOwnerTeam: false,
            hasExplicitHostileMetadata: true,
            ownerIsPartyMember: true);

        Assert.False(assumeEnemy);
    }
}
