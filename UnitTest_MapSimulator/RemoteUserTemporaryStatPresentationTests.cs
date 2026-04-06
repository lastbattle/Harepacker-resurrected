using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteUserTemporaryStatPresentationTests
    {
        [Theory]
        [InlineData(412, new[] { 4121006, 33101003, 13101003, 3201004, 3101004 })]
        [InlineData(3311, new[] { 33101003, 4121006, 13101003, 3201004, 3101004 })]
        [InlineData(1311, new[] { 13101003, 4121006, 33101003, 3201004, 3101004 })]
        [InlineData(321, new[] { 3201004, 4121006, 33101003, 13101003, 3101004 })]
        [InlineData(311, new[] { 3101004, 4121006, 33101003, 13101003, 3201004 })]
        public void EnumerateRemoteSoulArrowSkillIds_PrefersJobOwnedSkillFirst(int jobId, int[] expected)
        {
            IReadOnlyList<int> ordered = RemoteUserActorPool.EnumerateRemoteSoulArrowSkillIds(jobId);

            Assert.Equal(expected, ordered);
        }

        [Theory]
        [InlineData(2112, new[] { 21120007, 23111005, 20011010, 20001010, 10001010 })]
        [InlineData(2311, new[] { 23111005, 21120007, 20011010, 20001010, 10001010 })]
        [InlineData(2218, new[] { 20011010, 21120007, 23111005, 20001010, 10001010 })]
        [InlineData(2108, new[] { 20001010, 21120007, 23111005, 20011010, 10001010 })]
        [InlineData(1311, new[] { 10001010, 21120007, 23111005, 20011010, 20001010 })]
        [InlineData(2001, new[] { 20011010, 21120007, 23111005, 20001010, 10001010 })]
        [InlineData(2000, new[] { 20001010, 21120007, 23111005, 20011010, 10001010 })]
        public void EnumerateRemoteBarrierSkillIds_PrefersCurrentJobFamilyFirst(int jobId, int[] expected)
        {
            IReadOnlyList<int> ordered = RemoteUserActorPool.EnumerateRemoteBarrierSkillIds(jobId);

            Assert.Equal(expected, ordered);
        }

        [Fact]
        public void EnumerateRemoteShadowPartnerSkillIds_PrefersNightWalkerSkillFirst()
        {
            IReadOnlyList<int> ordered = RemoteUserActorPool.EnumerateRemoteShadowPartnerSkillIds(1411);

            Assert.Equal(new[] { 14111000, 4111002, 4211008 }, ordered);
        }

        [Fact]
        public void TryResolveMechanicTamingMobOverrideItemId_UsesMechanicTankMount()
        {
            RemoteUserTemporaryStatKnownState knownState = CreateKnownState(mechanicMode: 35121005);

            bool resolved = RemoteUserActorPool.TryResolveMechanicTamingMobOverrideItemId(knownState, out int itemId);

            Assert.True(resolved);
            Assert.Equal(1932016, itemId);
        }

        [Theory]
        [InlineData("stand1", "ghoststand")]
        [InlineData("walk1", "ghostwalk")]
        [InlineData("jump", "ghostjump")]
        [InlineData("ladder", "ghostladder")]
        [InlineData("rope", "ghostrope")]
        [InlineData("fly", "ghostfly")]
        [InlineData("prone", "ghostproneStab")]
        [InlineData("shoot1", "ghoststand")]
        public void ResolveClientVisibleActionName_HiddenLikeActionsUseGhostFamily(string baseActionName, string expected)
        {
            RemoteUserTemporaryStatKnownState knownState = CreateKnownState(hasDarkSight: true);

            string resolved = RemoteUserActorPool.ResolveClientVisibleActionName(baseActionName, knownState);

            Assert.Equal(expected, resolved);
        }

        [Theory]
        [InlineData("stand1", 35121005, "tank_stand")]
        [InlineData("walk1", 35121005, "tank_walk")]
        [InlineData("shoot1", 35121005, "tank")]
        [InlineData("prone", 35121005, "tank_prone")]
        [InlineData("stand1", 35111004, "siege_stand")]
        [InlineData("shoot1", 35111004, "siege")]
        public void TryResolveMechanicVisibleActionName_UsesKnownMechanicPresentation(
            string baseActionName,
            int mechanicMode,
            string expected)
        {
            bool resolved = RemoteUserActorPool.TryResolveMechanicVisibleActionName(baseActionName, mechanicMode, out string actionName);

            Assert.True(resolved);
            Assert.Equal(expected, actionName);
        }

        private static RemoteUserTemporaryStatKnownState CreateKnownState(
            int? speed = null,
            bool hasShadowPartner = false,
            bool hasDarkSight = false,
            bool hasSoulArrow = false,
            int? chargeSkillId = null,
            int? morphId = null,
            int? ghostId = null,
            bool hasBarrier = false,
            bool hasWindWalk = false,
            int? mechanicMode = null,
            bool hasDarkAura = false,
            bool hasBlueAura = false,
            bool hasYellowAura = false)
        {
            return new RemoteUserTemporaryStatKnownState(
                speed,
                hasShadowPartner,
                hasDarkSight,
                hasSoulArrow,
                chargeSkillId,
                morphId,
                ghostId,
                hasBarrier,
                hasWindWalk,
                mechanicMode,
                hasDarkAura,
                hasBlueAura,
                hasYellowAura);
        }
    }
}
