using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator.UI
{
    public sealed class PreparedSkillHudParityTests
    {
        [Fact]
        public void ResolvePreparedGaugeDuration_UsesClientDefaultForGenericKeydown()
        {
            int duration = PreparedSkillHudRules.ResolvePreparedGaugeDuration(9999999);

            Assert.Equal(2000, duration);
        }

        [Fact]
        public void ResolvePreparedGaugeDuration_UsesMonkeyWaveFallbackWhenWzIsUnavailable()
        {
            int duration = PreparedSkillHudRules.ResolvePreparedGaugeDuration(5311002);

            Assert.Equal(1080, duration);
        }

        [Fact]
        public void ResolveProfile_UsesDragonHudDefaults()
        {
            var profile2215 = PreparedSkillHudRules.ResolveProfile(22151001);
            var profile2212 = PreparedSkillHudRules.ResolveProfile(22121000);

            Assert.True(profile2215.Visible);
            Assert.Equal("KeyDownBar2", profile2215.SkinKey);
            Assert.Equal(500, profile2215.GaugeDurationMs);
            Assert.Equal(PreparedSkillHudSurface.World, profile2215.Surface);
            Assert.False(profile2215.ShowText);

            Assert.True(profile2212.Visible);
            Assert.Equal("KeyDownBar3", profile2212.SkinKey);
            Assert.Equal(500, profile2212.GaugeDurationMs);
            Assert.Equal(PreparedSkillHudSurface.World, profile2212.Surface);
            Assert.False(profile2212.ShowText);
        }

        [Fact]
        public void ResolveRemotePreparedSkillPhases_SplitsPrepareAndHoldWindows()
        {
            PreparedSkillHudRules.ResolveRemotePreparedSkillPhases(
                isKeydownSkill: true,
                isHolding: false,
                durationMs: 1200,
                maxHoldDurationMs: 4000,
                explicitAutoEnterHold: false,
                out int activeDurationMs,
                out int prepareDurationMs,
                out bool autoEnterHold);

            Assert.True(autoEnterHold);
            Assert.Equal(4000, activeDurationMs);
            Assert.Equal(1200, prepareDurationMs);
        }

        [Fact]
        public void BuildStatusText_UsesPreparingCaptionAfterGaugeFillBeforeRelease()
        {
            var prepared = new StatusBarPreparedSkillRenderData
            {
                DurationMs = 5000,
                RemainingMs = 2800,
                TextVariant = PreparedSkillHudTextVariant.Default
            };

            string text = PreparedSkillHudTextResolver.BuildStatusText(prepared, gaugeDurationMs: 2000, progress: 1f);

            Assert.Equal("Preparing 3 sec", text);
        }

        [Fact]
        public void BuildStatusText_UsesReleaseCaptionForReleaseArmedSkills()
        {
            var prepared = new StatusBarPreparedSkillRenderData
            {
                DurationMs = 1200,
                RemainingMs = 0,
                TextVariant = PreparedSkillHudTextVariant.ReleaseArmed
            };

            string text = PreparedSkillHudTextResolver.BuildStatusText(prepared, gaugeDurationMs: 1200, progress: 1f);

            Assert.Equal("Release", text);
        }

        [Fact]
        public void BuildStatusText_UsesAmplifyCaptionsForSg88()
        {
            var preparing = new StatusBarPreparedSkillRenderData
            {
                TextVariant = PreparedSkillHudTextVariant.Amplify
            };
            var prepared = new StatusBarPreparedSkillRenderData
            {
                TextVariant = PreparedSkillHudTextVariant.Amplify
            };

            Assert.Equal(
                "Amplifying 75%",
                PreparedSkillHudTextResolver.BuildStatusText(preparing, gaugeDurationMs: 2000, progress: 0.75f));
            Assert.Equal(
                "Amplified",
                PreparedSkillHudTextResolver.BuildStatusText(prepared, gaugeDurationMs: 2000, progress: 1f));
        }

        [Fact]
        public void BuildStatusText_UsesMaintainingCaptionWhileHoldWindowRemains()
        {
            var prepared = new StatusBarPreparedSkillRenderData
            {
                IsHolding = true,
                IsKeydownSkill = true,
                MaxHoldDurationMs = 2500,
                HoldElapsedMs = 1100,
                TextVariant = PreparedSkillHudTextVariant.Default
            };

            string text = PreparedSkillHudTextResolver.BuildStatusText(prepared, gaugeDurationMs: 2000, progress: 0.5f);
            float progress = PreparedSkillHudTextResolver.ResolveProgress(prepared, gaugeDurationMs: 2000);

            Assert.Equal("Maintaining 2 sec", text);
            Assert.Equal(0.56f, progress, 2);
        }

        [Fact]
        public void ResolveRemoteDragonActionHelpers_UseModeledHoldPhaseInsteadOfRawPacketState()
        {
            var prepared = new RemoteUserActorPool.RemotePreparedSkillState
            {
                SkillId = 22151001,
                StartTime = 1000,
                PrepareDurationMs = 500,
                IsHolding = false
            };

            string actionName = RemoteUserActorPool.ResolveRemoteDragonActionName(prepared, isHolding: true);
            int elapsed = RemoteUserActorPool.ResolveRemoteDragonActionElapsedMs(prepared, 1850, actionName, isHolding: true);

            Assert.Equal("stand", actionName);
            Assert.Equal(350, elapsed);
        }
    }
}
