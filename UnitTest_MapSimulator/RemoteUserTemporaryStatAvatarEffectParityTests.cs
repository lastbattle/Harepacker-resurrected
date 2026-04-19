using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteUserTemporaryStatAvatarEffectParityTests
    {
        [Fact]
        public void SameSkillNonPacketRefresh_PreservesExistingStateObjectAndTimeline()
        {
            SkillData skill = CreateAffectedSkill(32101002, "Blue Aura");
            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectState(
                    skill.SkillId,
                    skill,
                    animationStartTime: 1400,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState existingState));

            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState updatedState =
                RemoteUserActorPool.UpdateRemoteTemporaryStatAvatarEffectStateForTesting(
                    existingState,
                    skill.SkillId,
                    skill,
                    currentTime: 1900,
                    reseedTimelineFromPacket: false);

            Assert.Same(existingState, updatedState);
            Assert.Equal(1400, updatedState.AnimationStartTime);
        }

        [Fact]
        public void SameSkillPacketReapply_PreservesStateObjectAndReseedsTimelineStart()
        {
            SkillData originalSkill = CreateAffectedSkill(32120000, "Advanced Dark Aura");
            SkillData refreshedSkill = CreateAffectedSkill(32120000, "Advanced Dark Aura Refreshed");
            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectState(
                    originalSkill.SkillId,
                    originalSkill,
                    animationStartTime: 3000,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState existingState));

            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState updatedState =
                RemoteUserActorPool.UpdateRemoteTemporaryStatAvatarEffectStateForTesting(
                    existingState,
                    refreshedSkill.SkillId,
                    refreshedSkill,
                    currentTime: 3600,
                    reseedTimelineFromPacket: true);

            Assert.Same(existingState, updatedState);
            Assert.Equal(3600, updatedState.AnimationStartTime);
            Assert.Same(refreshedSkill, updatedState.Skill);
        }

        [Fact]
        public void SkillChange_RebuildsStateAndSeedsFromCurrentTime()
        {
            SkillData skillA = CreateAffectedSkill(32101003, "Yellow Aura");
            SkillData skillB = CreateAffectedSkill(32110000, "Advanced Blue Aura");
            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillA.SkillId,
                    skillA,
                    animationStartTime: 4200,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState existingState));

            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState updatedState =
                RemoteUserActorPool.UpdateRemoteTemporaryStatAvatarEffectStateForTesting(
                    existingState,
                    skillB.SkillId,
                    skillB,
                    currentTime: 4700,
                    reseedTimelineFromPacket: true);

            Assert.NotSame(existingState, updatedState);
            Assert.Equal(skillB.SkillId, updatedState.SkillId);
            Assert.Equal(4700, updatedState.AnimationStartTime);
            Assert.Equal(4700, updatedState.TransitionStartTime);
            Assert.Equal(0f, updatedState.TransitionStartAlpha);
            Assert.Equal(1f, updatedState.TransitionEndAlpha);
            Assert.True(updatedState.TransitionDurationMs > 0);
        }

        [Fact]
        public void SkillChange_CreatesTailStateWithFadeOutFromPreviousEffect()
        {
            SkillData skillA = CreateAffectedSkill(32001003, "Dark Aura");
            SkillData skillB = CreateAffectedSkill(32120001, "Advanced Yellow Aura");
            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillA.SkillId,
                    skillA,
                    animationStartTime: 2000,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState previousState));
            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillB.SkillId,
                    skillB,
                    animationStartTime: 2400,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState nextState));

            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectTailStateForTesting(
                    previousState,
                    nextState,
                    currentTime: 2600,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState tailState));

            Assert.NotNull(tailState);
            Assert.NotSame(previousState, tailState);
            Assert.Equal(previousState.SkillId, tailState.SkillId);
            Assert.Equal(1f, tailState.TransitionStartAlpha);
            Assert.Equal(0f, tailState.TransitionEndAlpha);
            Assert.Equal(2600, tailState.TransitionStartTime);
            Assert.True(tailState.TransitionDurationMs > 0);
        }

        [Fact]
        public void TransitionAlpha_InterpolatesForSkillChangeFadeInAndTailFadeOut()
        {
            SkillData skillA = CreateAffectedSkill(32101002, "Blue Aura");
            SkillData skillB = CreateAffectedSkill(32110000, "Advanced Blue Aura");
            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillA.SkillId,
                    skillA,
                    animationStartTime: 1000,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState previousState));

            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState nextState =
                RemoteUserActorPool.UpdateRemoteTemporaryStatAvatarEffectStateForTesting(
                    previousState,
                    skillB.SkillId,
                    skillB,
                    currentTime: 1300,
                    reseedTimelineFromPacket: true);
            Assert.NotSame(previousState, nextState);

            Assert.True(
                RemoteUserActorPool.TryCreateRemoteTemporaryStatAvatarEffectTailStateForTesting(
                    previousState,
                    nextState,
                    currentTime: 1300,
                    out RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState tailState));

            float nextStartAlpha = RemoteUserActorPool.ResolveRemoteTemporaryStatAvatarEffectTransitionAlphaForTesting(nextState, 1300);
            float nextMidAlpha = RemoteUserActorPool.ResolveRemoteTemporaryStatAvatarEffectTransitionAlphaForTesting(nextState, 1390);
            float tailStartAlpha = RemoteUserActorPool.ResolveRemoteTemporaryStatAvatarEffectTransitionAlphaForTesting(tailState, 1300);
            float tailMidAlpha = RemoteUserActorPool.ResolveRemoteTemporaryStatAvatarEffectTransitionAlphaForTesting(tailState, 1390);

            Assert.True(nextStartAlpha <= 0.01f);
            Assert.True(nextMidAlpha > nextStartAlpha);
            Assert.True(tailStartAlpha >= 0.99f);
            Assert.True(tailMidAlpha < tailStartAlpha);
        }

        private static SkillData CreateAffectedSkill(int skillId, string name)
        {
            return new SkillData
            {
                SkillId = skillId,
                Name = name,
                AffectedEffect = new SkillAnimation
                {
                    Name = $"{name}_affected",
                    Frames = new List<SkillFrame>
                    {
                        new SkillFrame { Delay = 90 }
                    },
                    ZOrder = 0
                }
            };
        }
    }
}
