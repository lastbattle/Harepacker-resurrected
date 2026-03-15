using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SkillManagerQuickSlotValidationTests
    {
        [Fact]
        public void TrySetHotkey_RejectsUnlearnedPassiveAndInvisibleSkills()
        {
            var activeSkill = new SkillData { SkillId = 1001001, MaxLevel = 1 };
            var passiveSkill = new SkillData { SkillId = 1000000, MaxLevel = 1, IsPassive = true };
            var invisibleSkill = new SkillData { SkillId = 1001002, MaxLevel = 1, Invisible = true };
            var manager = CreateSkillManager(activeSkill, passiveSkill, invisibleSkill);

            manager.SetSkillLevel(activeSkill.SkillId, 1);
            manager.SetSkillLevel(passiveSkill.SkillId, 1);
            manager.SetSkillLevel(invisibleSkill.SkillId, 1);

            Assert.True(manager.TrySetHotkey(0, activeSkill.SkillId));
            Assert.Equal(activeSkill.SkillId, manager.GetHotkeySkill(0));

            Assert.False(manager.TrySetHotkey(1, passiveSkill.SkillId));
            Assert.Equal(0, manager.GetHotkeySkill(1));

            Assert.False(manager.TrySetHotkey(2, invisibleSkill.SkillId));
            Assert.Equal(0, manager.GetHotkeySkill(2));

            Assert.False(manager.TrySetHotkey(3, 9999999));
            Assert.Equal(0, manager.GetHotkeySkill(3));
        }

        [Fact]
        public void RevalidateHotkeys_ClearsSlotsWhenSkillStopsBeingLearned()
        {
            var activeSkill = new SkillData { SkillId = 2001001, MaxLevel = 1 };
            var manager = CreateSkillManager(activeSkill);
            manager.SetSkillLevel(activeSkill.SkillId, 1);

            Assert.True(manager.TrySetHotkey(0, activeSkill.SkillId));
            Assert.Equal(activeSkill.SkillId, manager.GetHotkeySkill(0));

            manager.SetSkillLevel(activeSkill.SkillId, 0);

            Assert.Equal(1, manager.RevalidateHotkeys());
            Assert.Equal(0, manager.GetHotkeySkill(0));
            Assert.Empty(manager.GetAllHotkeys());
        }

        [Fact]
        public void SetSkillLevel_ClearsHotkeyImmediatelyWhenSkillBecomesUnlearned()
        {
            var activeSkill = new SkillData { SkillId = 2001002, MaxLevel = 1 };
            var manager = CreateSkillManager(activeSkill);
            manager.SetSkillLevel(activeSkill.SkillId, 1);

            Assert.True(manager.TrySetHotkey(0, activeSkill.SkillId));
            Assert.Equal(activeSkill.SkillId, manager.GetHotkeySkill(0));

            manager.SetSkillLevel(activeSkill.SkillId, 0);

            Assert.Equal(0, manager.GetHotkeySkill(0));
            Assert.Empty(manager.GetAllHotkeys());
        }

        [Fact]
        public void CharacterPresetApplySkillsTo_RevalidatesHotkeysAfterLoadingLevels()
        {
            var activeSkill = new SkillData { SkillId = 3001001, MaxLevel = 1 };
            var manager = CreateSkillManager(activeSkill);
            var preset = new CharacterPreset
            {
                SkillHotkeys = new Dictionary<int, int> { [0] = activeSkill.SkillId },
                SkillLevels = new Dictionary<int, int> { [activeSkill.SkillId] = 1 }
            };

            preset.ApplySkillsTo(manager);

            Assert.Equal(activeSkill.SkillId, manager.GetHotkeySkill(0));
        }

        [Fact]
        public void LoadHotkeys_UsesAssignmentValidationForMixedEntries()
        {
            var activeSkill = new SkillData { SkillId = 3001002, MaxLevel = 1 };
            var passiveSkill = new SkillData { SkillId = 3000000, MaxLevel = 1, IsPassive = true };
            var manager = CreateSkillManager(activeSkill, passiveSkill);

            manager.SetSkillLevel(activeSkill.SkillId, 1);
            manager.SetSkillLevel(passiveSkill.SkillId, 1);

            manager.LoadHotkeys(new Dictionary<int, int>
            {
                [0] = activeSkill.SkillId,
                [1] = passiveSkill.SkillId,
                [2] = 9999999
            });

            Assert.Equal(activeSkill.SkillId, manager.GetHotkeySkill(0));
            Assert.Equal(0, manager.GetHotkeySkill(1));
            Assert.Equal(0, manager.GetHotkeySkill(2));
            Assert.Equal(new Dictionary<int, int> { [0] = activeSkill.SkillId }, manager.GetAllHotkeys());
        }

        private static SkillManager CreateSkillManager(params SkillData[] availableSkills)
        {
            var manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                new PlayerCharacter(device: null, texturePool: null, build: null));

            var availableSkillsField = typeof(SkillManager).GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(availableSkillsField);

            availableSkillsField!.SetValue(manager, new List<SkillData>(availableSkills));
            return manager;
        }
    }
}
