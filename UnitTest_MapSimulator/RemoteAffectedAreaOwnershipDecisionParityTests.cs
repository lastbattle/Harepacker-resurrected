using HaCreator.MapSimulator;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class RemoteAffectedAreaOwnershipDecisionParityTests
    {
        [Fact]
        public void ResolveOwnerEnemyDecision_UsesResolvedTeamOutcome_First()
        {
            bool isEnemy = MapSimulator.ResolveRemoteAffectedAreaOwnerEnemyDecision(
                hasResolvedOwnerTeam: true,
                resolvedOwnerIsEnemy: false,
                hasAreaOwnerEnemySnapshot: true,
                areaOwnerIsEnemySnapshot: true,
                hasExplicitHostileMetadata: true,
                ownerIsPartyMember: false);

            Assert.False(isEnemy);
        }

        [Fact]
        public void ResolveOwnerEnemyDecision_UsesAreaSnapshot_WhenTeamUnavailable()
        {
            bool isEnemy = MapSimulator.ResolveRemoteAffectedAreaOwnerEnemyDecision(
                hasResolvedOwnerTeam: false,
                resolvedOwnerIsEnemy: false,
                hasAreaOwnerEnemySnapshot: true,
                areaOwnerIsEnemySnapshot: false,
                hasExplicitHostileMetadata: true,
                ownerIsPartyMember: false);

            Assert.False(isEnemy);
        }

        [Fact]
        public void ResolveOwnerEnemyDecision_FallsBackToHostileMetadata_WhenNoTeamOrSnapshot()
        {
            bool isEnemy = MapSimulator.ResolveRemoteAffectedAreaOwnerEnemyDecision(
                hasResolvedOwnerTeam: false,
                resolvedOwnerIsEnemy: false,
                hasAreaOwnerEnemySnapshot: false,
                areaOwnerIsEnemySnapshot: false,
                hasExplicitHostileMetadata: true,
                ownerIsPartyMember: false);

            Assert.True(isEnemy);
        }

        [Fact]
        public void ResolveOwnerEnemyDecision_DoesNotAssumeEnemy_ForPartyMemberFallback()
        {
            bool isEnemy = MapSimulator.ResolveRemoteAffectedAreaOwnerEnemyDecision(
                hasResolvedOwnerTeam: false,
                resolvedOwnerIsEnemy: false,
                hasAreaOwnerEnemySnapshot: false,
                areaOwnerIsEnemySnapshot: false,
                hasExplicitHostileMetadata: true,
                ownerIsPartyMember: true);

            Assert.False(isEnemy);
        }
    }
}
