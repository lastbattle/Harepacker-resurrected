using System;
using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class PreparedSkillRemoteHudParityTests
    {
        private static readonly MethodInfo TryResolvePreparedSkillPhaseMethod = typeof(RemoteUserActorPool).GetMethod(
            "TryResolvePreparedSkillPhase",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RemoteUserActorPool.TryResolvePreparedSkillPhase was not found.");

        [Fact]
        public void ReleaseTriggeredRemotePreparedSkill_AutoEntersHoldWithoutExplicitHoldDuration()
        {
            PreparedSkillHudRules.ResolveRemotePreparedSkillPhases(
                5311002,
                isKeydownSkill: true,
                isHolding: false,
                durationMs: 2000,
                maxHoldDurationMs: 0,
                explicitAutoEnterHold: false,
                out int activeDurationMs,
                out int prepareDurationMs,
                out bool autoEnterHold);

            Assert.True(autoEnterHold);
            Assert.Equal(2000, prepareDurationMs);
            Assert.Equal(0, activeDurationMs);
        }

        [Fact]
        public void RepeatRemotePreparedSkill_DoesNotAutoEnterHoldWithoutHoldWindow()
        {
            PreparedSkillHudRules.ResolveRemotePreparedSkillPhases(
                5101004,
                isKeydownSkill: true,
                isHolding: false,
                durationMs: 1000,
                maxHoldDurationMs: 0,
                explicitAutoEnterHold: false,
                out int activeDurationMs,
                out int prepareDurationMs,
                out bool autoEnterHold);

            Assert.False(autoEnterHold);
            Assert.Equal(0, prepareDurationMs);
            Assert.Equal(1000, activeDurationMs);
        }

        [Fact]
        public void RemotePreparedSkillPhase_KeepsReleaseTriggeredHoldAliveAfterChargeCompletes()
        {
            RemotePreparedSkillState prepared = new()
            {
                SkillId = 5311002,
                SkillName = "Monkey Wave",
                DurationMs = 0,
                PrepareDurationMs = 2000,
                GaugeDurationMs = 2000,
                StartTime = 100,
                IsKeydownSkill = true,
                IsHolding = false,
                AutoEnterHold = true,
                MaxHoldDurationMs = 0,
                TextVariant = PreparedSkillHudTextVariant.ReleaseArmed
            };

            object[] arguments =
            {
                prepared,
                2400,
                0,
                0,
                0f,
                false,
                0
            };

            bool resolved = (bool)TryResolvePreparedSkillPhaseMethod.Invoke(null, arguments)!;

            Assert.True(resolved);
            Assert.Equal(0, (int)arguments[2]);
            Assert.Equal(0, (int)arguments[3]);
            Assert.Equal(1f, (float)arguments[4]);
            Assert.True((bool)arguments[5]);
            Assert.Equal(300, (int)arguments[6]);
        }
    }
}
