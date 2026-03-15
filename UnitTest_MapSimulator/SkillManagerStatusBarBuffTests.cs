using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SkillManagerStatusBarBuffTests
    {
        [Fact]
        public void GetStatusBarBuffEntries_ExposesTemporaryStatLabelsAndPrimaryIconForMultiStatBuffs()
        {
            var skill = new SkillData
            {
                SkillId = 2301003,
                MaxLevel = 1,
                IsBuff = true,
                Name = "Bless",
                Description = "Raises attack and accuracy."
            };
            skill.Levels[1] = new SkillLevelData
            {
                Level = 1,
                Time = 30,
                PAD = 20,
                ACC = 15
            };

            var manager = CreateSkillManager(skill);
            InvokeApplyBuff(manager, skill, 1, 1000);

            IReadOnlyList<StatusBarBuffEntry> entries = manager.GetStatusBarBuffEntries(1500);
            StatusBarBuffEntry entry = Assert.Single(entries);

            Assert.Equal("buff/incPAD", entry.IconKey);
            Assert.Equal(new[] { "PAD", "ACC" }, entry.TemporaryStatLabels);
            Assert.Equal(new[] { "Physical Attack", "Accuracy" }, entry.TemporaryStatDisplayNames);
            Assert.Equal(29500, entry.RemainingMs);
        }

        private static SkillManager CreateSkillManager(params SkillData[] availableSkills)
        {
            var manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                new PlayerCharacter(device: null, texturePool: null, build: null));

            FieldInfo availableSkillsField = typeof(SkillManager).GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(availableSkillsField);
            availableSkillsField!.SetValue(manager, new List<SkillData>(availableSkills));
            return manager;
        }

        private static void InvokeApplyBuff(SkillManager manager, SkillData skill, int level, int currentTime)
        {
            MethodInfo applyBuff = typeof(SkillManager).GetMethod("ApplyBuff", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(applyBuff);
            applyBuff!.Invoke(manager, new object[] { skill, level, currentTime });
        }
    }
}
