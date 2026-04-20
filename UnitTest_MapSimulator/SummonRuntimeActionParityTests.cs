using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator
{
    public class SummonRuntimeActionParityTests
    {
        [Fact]
        public void Action14_StrictSubsummonBranch_DoesNotAllowOneTimeFallback_WhenAuthoredBranchMissing()
        {
            Assert.True(SummonRuntimeRules.IsStrictPacketSkillBranchAction(14));
            Assert.True(SummonRuntimeRules.IsStrictPacketSkillBranchAction(0x8E));
            Assert.False(SummonedPool.ShouldAllowPacketOwnedSkillOneTimeFallback(14, null));
            Assert.False(SummonedPool.ShouldAllowPacketOwnedSkillOneTimeFallback(0x8E, string.Empty));
        }

        [Fact]
        public void Action14_StrictSubsummonBranch_AllowsPlaybackOnlyWhenAuthoredBranchResolves()
        {
            Assert.True(SummonedPool.ShouldAllowPacketOwnedSkillOneTimeFallback(14, "subsummon"));
        }

        [Fact]
        public void NonStrictPacketSkillAction_StillAllowsFallbackOwnership()
        {
            Assert.False(SummonRuntimeRules.IsStrictPacketSkillBranchAction(13));
            Assert.True(SummonedPool.ShouldAllowPacketOwnedSkillOneTimeFallback(13, null));
        }

        [Fact]
        public void Action14_RuntimeAssistOwnershipFlip_RequiresAuthoredSubsummonBranch()
        {
            SkillData noSubsummonSkill = CreateSkill(
                minionAbility: "summon",
                "skill1");
            SkillData withSubsummonSkill = CreateSkill(
                minionAbility: "summon",
                "subsummon",
                "skill1");

            Assert.False(SummonRuntimeRules.HasAuthoredPacketSkillAssistOwnershipBranch(
                noSubsummonSkill,
                packetAction: 14,
                assistType: SummonAssistType.SummonAction));
            Assert.True(SummonRuntimeRules.HasAuthoredPacketSkillAssistOwnershipBranch(
                withSubsummonSkill,
                packetAction: 14,
                assistType: SummonAssistType.SummonAction));
        }

        [Fact]
        public void Action14_BranchRouting_StaysStrictlyOnAuthoredSubsummon()
        {
            SkillData noSubsummonSkill = CreateSkill(
                minionAbility: "summon",
                "skill1",
                "skill2");
            SkillData withSubsummonSkill = CreateSkill(
                minionAbility: "summon",
                "subsummon",
                "skill1");

            Assert.Null(SummonRuntimeRules.ResolvePacketSkillBranch(
                noSubsummonSkill,
                packetAction: 14,
                assistType: SummonAssistType.SummonAction));
            Assert.Equal(
                "subsummon",
                SummonRuntimeRules.ResolvePacketSkillBranch(
                    withSubsummonSkill,
                    packetAction: 14,
                    assistType: SummonAssistType.SummonAction));
        }

        private static SkillData CreateSkill(string minionAbility, params string[] branchNames)
        {
            Dictionary<string, SkillAnimation> namedAnimations = new(System.StringComparer.OrdinalIgnoreCase);
            foreach (string branchName in branchNames)
            {
                namedAnimations[branchName] = CreateAnimation(branchName);
            }

            return new SkillData
            {
                MinionAbility = minionAbility,
                SummonNamedAnimations = namedAnimations,
                SummonActionAnimations = new Dictionary<string, SkillAnimation>(namedAnimations, System.StringComparer.OrdinalIgnoreCase)
            };
        }

        private static SkillAnimation CreateAnimation(string name)
        {
            return new SkillAnimation
            {
                Name = name,
                Frames = new List<SkillFrame>
                {
                    new SkillFrame
                    {
                        Delay = 120
                    }
                }
            };
        }
    }
}
