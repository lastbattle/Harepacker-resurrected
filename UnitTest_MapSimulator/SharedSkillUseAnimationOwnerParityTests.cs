using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class SharedSkillUseAnimationOwnerParityTests
    {
        [Fact]
        public void NonMeleeDelayRateCast_RoutesThroughRequestSeam()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.DelayRateOverride = 625;

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.True(shouldRoute);
        }

        [Fact]
        public void NonMeleeRequestedBranchCast_RoutesThroughRequestSeamWithoutDelayOverride()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.DelayRateOverride = null;
            castInfo.RequestedBranchNames = new[] { "effect2" };

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.True(shouldRoute);
        }

        [Fact]
        public void NonMeleeOffsetCast_RoutesThroughRequestSeamWithoutDelayOverride()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.DelayRateOverride = null;
            castInfo.OriginOffset = new Point(12, -8);

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.True(shouldRoute);
        }

        [Fact]
        public void NonMeleeFacingOverrideCast_RoutesThroughRequestSeamWithoutDelayOverride()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.DelayRateOverride = null;
            castInfo.FollowOwnerFacing = false;

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.True(shouldRoute);
        }

        [Fact]
        public void NonMeleeBLeftOverrideCast_RoutesThroughRequestSeamWithoutDelayOverride()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.DelayRateOverride = null;
            castInfo.FacingRightOverride = true;

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.True(shouldRoute);
        }

        [Fact]
        public void NonMeleeUnshapedCast_DoesNotRouteThroughRequestSeamWithoutDelayOverride()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.DelayRateOverride = null;

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.False(shouldRoute);
        }

        [Fact]
        public void AttackCast_DoesNotRouteThroughRequestSeamEvenWhenRequestShaped()
        {
            SkillCastInfo castInfo = CreateNonMeleeCast();
            castInfo.SkillData.IsAttack = true;
            castInfo.DelayRateOverride = null;
            castInfo.RequestedBranchNames = new[] { "effect2" };

            bool shouldRoute = MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(castInfo);

            Assert.False(shouldRoute);
        }

        [Fact]
        public void RequestShapingClassifier_RecognizesBranchOffsetAndFollowOverrides()
        {
            Assert.True(MapSimulator.HasClientSkillEffectRequestShapingForTesting(
                new[] { "effect" },
                Point.Zero,
                followOwnerFacing: true,
                followOwnerPosition: true));
            Assert.True(MapSimulator.HasClientSkillEffectRequestShapingForTesting(
                requestedBranchNames: null,
                new Point(3, 0),
                followOwnerFacing: true,
                followOwnerPosition: true));
            Assert.True(MapSimulator.HasClientSkillEffectRequestShapingForTesting(
                requestedBranchNames: null,
                Point.Zero,
                followOwnerFacing: false,
                followOwnerPosition: true));
            Assert.True(MapSimulator.HasClientSkillEffectRequestShapingForTesting(
                requestedBranchNames: null,
                Point.Zero,
                followOwnerFacing: true,
                followOwnerPosition: false));
            Assert.True(MapSimulator.HasClientSkillEffectRequestShapingForTesting(
                requestedBranchNames: null,
                Point.Zero,
                followOwnerFacing: true,
                followOwnerPosition: true,
                facingRightOverride: true));
            Assert.False(MapSimulator.HasClientSkillEffectRequestShapingForTesting(
                requestedBranchNames: null,
                Point.Zero,
                followOwnerFacing: true,
                followOwnerPosition: true));
        }

        [Fact]
        public void EffectBranchLastIndexFilter_RespectsClientNLastOwnership()
        {
            Assert.True(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("effect", 0));
            Assert.True(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("effect0", 0));
            Assert.False(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("effect1", 0));
            Assert.True(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("effect1", 1));
            Assert.False(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("effect2", 1));
            Assert.True(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("special", 1));
            Assert.False(MapSimulator.ShouldIncludeAnimationDisplayerEffectBranchForTesting("effect", -1));
        }

        [Fact]
        public void NonMeleeShowSkillEffectCast_UsesClientNLastOverride()
        {
            var skill = new SkillData
            {
                SkillId = 2301002,
                IsAttack = false,
                IsPrepareSkill = false,
                IsKeydownSkill = false
            };

            int? lastIndex = SkillManager.ResolveClientLocalShowSkillEffectEffectBranchLastIndexOverride(skill);

            Assert.Equal(int.MaxValue, lastIndex);
        }

        [Fact]
        public void NonMeleeShowSkillEffectCast_UsesClientBLeftOverride()
        {
            var skill = new SkillData
            {
                SkillId = 2301002,
                IsAttack = false,
                IsPrepareSkill = false,
                IsKeydownSkill = false
            };

            bool? facingRightOverride = SkillManager.ResolveClientLocalShowSkillEffectFacingRightOverride(skill);

            Assert.True(facingRightOverride);
        }

        [Fact]
        public void AttackShowSkillEffectCast_DoesNotApplyClientBLeftOverride()
        {
            var skill = new SkillData
            {
                SkillId = 1001004,
                IsAttack = true,
                IsPrepareSkill = false,
                IsKeydownSkill = false
            };

            bool? facingRightOverride = SkillManager.ResolveClientLocalShowSkillEffectFacingRightOverride(skill);

            Assert.Null(facingRightOverride);
        }

        [Fact]
        public void LocalShowSkillEffectRequest_DefaultBLeftUsesRightFacingOverride()
        {
            SkillUseEffectRequest request = SkillManager.CreateClientLocalShowSkillEffectRequest(
                effectSkillId: 35100004,
                sourceSkillId: 35101004,
                currentTime: 1000);

            Assert.True(request.FacingRightOverride);
        }

        [Fact]
        public void LocalShowSkillEffectRequest_BLeftOneUsesLeftFacingOverride()
        {
            SkillUseEffectRequest request = SkillManager.CreateClientLocalShowSkillEffectRequest(
                effectSkillId: 35100004,
                sourceSkillId: 35101004,
                currentTime: 1000,
                showSkillEffectBLeft: 1);

            Assert.False(request.FacingRightOverride);
        }

        private static SkillCastInfo CreateNonMeleeCast()
        {
            return new SkillCastInfo
            {
                SkillId = 2301002,
                SkillData = new SkillData
                {
                    SkillId = 2301002,
                    IsAttack = false,
                    IsPrepareSkill = false,
                    IsKeydownSkill = false
                },
                OriginOffset = Point.Zero,
                FollowOwnerFacing = true,
                FollowOwnerPosition = true
            };
        }
    }
}
