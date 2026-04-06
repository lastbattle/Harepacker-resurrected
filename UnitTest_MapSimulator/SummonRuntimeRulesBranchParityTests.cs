using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class SummonRuntimeRulesBranchParityTests
    {
        [Fact]
        public void ResolvePacketSkillBranch_UsesIndexedSkillBranchBeforeGenericSupportFallback()
        {
            SkillData skill = CreateSkill(
                35111011,
                "skill1",
                "heal",
                "support");

            string? branch = SummonRuntimeRules.ResolvePacketSkillBranch(skill, 1, SummonAssistType.Support);

            Assert.Equal("skill1", branch);
        }

        [Fact]
        public void ResolvePacketSkillBranch_UsesAuthoredSkill2ForSupportFallbackBeforeSupportName()
        {
            SkillData skill = CreateSkill(
                35111011,
                "skill2",
                "support");
            skill.MinionAbility = "mes";

            string? branch = SummonRuntimeRules.ResolvePacketSkillBranch(skill, 0, SummonAssistType.Support);

            Assert.Equal("skill2", branch);
        }

        [Fact]
        public void ResolvePacketSkillBranch_UsesIndexedSkillBranchForSummonAssistWhenSubsummonIsMissing()
        {
            SkillData skill = CreateSkill(
                400000001,
                "skill1",
                "skill2");
            skill.MinionAbility = "summon";

            string? branch = SummonRuntimeRules.ResolvePacketSkillBranch(skill, 14, SummonAssistType.SummonAction);

            Assert.Equal("skill1", branch);
        }

        private static SkillData CreateSkill(int skillId, params string[] branchNames)
        {
            var skill = new SkillData
            {
                SkillId = skillId,
                SummonNamedAnimations = new Dictionary<string, SkillAnimation>()
            };

            foreach (string branchName in branchNames)
            {
                skill.SummonNamedAnimations[branchName] = CreateAnimation();
            }

            return skill;
        }

        private static SkillAnimation CreateAnimation()
        {
            var animation = new SkillAnimation();
            animation.Frames.Add(new SkillFrame { Delay = 90 });
            return animation;
        }
    }
}
