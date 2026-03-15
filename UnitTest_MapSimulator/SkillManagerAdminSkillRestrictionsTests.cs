using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SkillManagerAdminSkillRestrictionsTests
    {
        [Fact]
        public void CanCastSkill_RejectsGmSkillForNonAdminJobs()
        {
            var gmSkill = CreateSkill(9001000, 900);
            var manager = CreateSkillManager(jobId: 112, gmSkill);
            manager.SetSkillLevel(gmSkill.SkillId, 1);

            Assert.False(manager.CanCastSkill(gmSkill.SkillId, currentTime: 1000));
        }

        [Fact]
        public void CanCastSkill_AllowsGmSkillsForGmAndSuperGmJobs()
        {
            var gmSkill = CreateSkill(9001000, 900);

            var gmManager = CreateSkillManager(jobId: 900, gmSkill);
            gmManager.SetSkillLevel(gmSkill.SkillId, 1);
            Assert.True(gmManager.CanCastSkill(gmSkill.SkillId, currentTime: 1000));

            var superGmManager = CreateSkillManager(jobId: 910, gmSkill);
            superGmManager.SetSkillLevel(gmSkill.SkillId, 1);
            Assert.True(superGmManager.CanCastSkill(gmSkill.SkillId, currentTime: 1000));
        }

        [Fact]
        public void CanCastSkill_RestrictsSuperGmOnlySkillsToSuperGmJob()
        {
            var superGmSkill = CreateSkill(9101000, 910);

            var gmManager = CreateSkillManager(jobId: 900, superGmSkill);
            gmManager.SetSkillLevel(superGmSkill.SkillId, 1);
            Assert.False(gmManager.CanCastSkill(superGmSkill.SkillId, currentTime: 1000));

            var superGmManager = CreateSkillManager(jobId: 910, superGmSkill);
            superGmManager.SetSkillLevel(superGmSkill.SkillId, 1);
            Assert.True(superGmManager.CanCastSkill(superGmSkill.SkillId, currentTime: 1000));
        }

        private static SkillData CreateSkill(int skillId, int jobId)
        {
            return new SkillData
            {
                SkillId = skillId,
                Job = jobId,
                MaxLevel = 1,
                Type = SkillType.Buff,
                IsBuff = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData { Level = 1, Time = 30 }
                }
            };
        }

        private static SkillManager CreateSkillManager(int jobId, params SkillData[] availableSkills)
        {
            var build = new CharacterBuild
            {
                Job = jobId
            };

            var manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                new PlayerCharacter(device: null, texturePool: null, build: build));

            var availableSkillsField = typeof(SkillManager).GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(availableSkillsField);
            availableSkillsField!.SetValue(manager, new List<SkillData>(availableSkills));
            return manager;
        }
    }
}
