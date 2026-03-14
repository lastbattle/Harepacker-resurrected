using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SkillManagerQueuedFollowUpAttackTests
    {
        [Fact]
        public void TryQueueFollowUpAttack_RequiresMatchingWeaponAndLearnedFollowUpSkill()
        {
            var triggerSkill = CreateTriggerSkill();
            var followUpSkill = CreateFollowUpSkill();

            var matchingManager = CreateSkillManager(1302000, triggerSkill, followUpSkill);
            matchingManager.SetSkillLevel(triggerSkill.SkillId, 1);
            matchingManager.SetSkillLevel(followUpSkill.SkillId, 1);

            InvokeQueueFollowUpAttack(matchingManager, triggerSkill, 1000, 77, true);
            Assert.Equal(1, GetQueuedFollowUpAttackCount(matchingManager));

            var wrongWeaponManager = CreateSkillManager(1312000, triggerSkill, followUpSkill);
            wrongWeaponManager.SetSkillLevel(triggerSkill.SkillId, 1);
            wrongWeaponManager.SetSkillLevel(followUpSkill.SkillId, 1);

            InvokeQueueFollowUpAttack(wrongWeaponManager, triggerSkill, 1000, 77, true);
            Assert.Equal(0, GetQueuedFollowUpAttackCount(wrongWeaponManager));

            var unlearnedFollowUpManager = CreateSkillManager(1302000, triggerSkill, followUpSkill);
            unlearnedFollowUpManager.SetSkillLevel(triggerSkill.SkillId, 1);

            InvokeQueueFollowUpAttack(unlearnedFollowUpManager, triggerSkill, 1000, 77, true);
            Assert.Equal(0, GetQueuedFollowUpAttackCount(unlearnedFollowUpManager));
        }

        [Fact]
        public void Update_ExecutesQueuedFollowUpAttackAfterDelay()
        {
            var triggerSkill = CreateTriggerSkill();
            var followUpSkill = CreateFollowUpSkill();
            var manager = CreateSkillManager(1302000, triggerSkill, followUpSkill);
            manager.SetSkillLevel(triggerSkill.SkillId, 1);
            manager.SetSkillLevel(followUpSkill.SkillId, 1);

            int? castSkillId = null;
            manager.OnSkillCast = cast => castSkillId = cast?.SkillId;

            InvokeQueueFollowUpAttack(manager, triggerSkill, 1000, 77, true);

            manager.Update(1089, 0.016f);
            Assert.Null(castSkillId);

            manager.Update(1090, 0.016f);
            Assert.Equal(followUpSkill.SkillId, castSkillId);
            Assert.Equal(0, GetQueuedFollowUpAttackCount(manager));
        }

        private static SkillData CreateTriggerSkill()
        {
            return new SkillData
            {
                SkillId = 1001001,
                MaxLevel = 1,
                IsAttack = true,
                FinalAttackTriggers = new Dictionary<int, HashSet<int>>
                {
                    [1000001] = new HashSet<int> { 30 }
                },
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData { Level = 1, Damage = 100, MobCount = 1, AttackCount = 1 }
                }
            };
        }

        private static SkillData CreateFollowUpSkill()
        {
            return new SkillData
            {
                SkillId = 1000001,
                MaxLevel = 1,
                IsAttack = true,
                IsPassive = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData { Level = 1, Damage = 150, AttackCount = 1, MobCount = 1, Prop = 100 }
                }
            };
        }

        private static SkillManager CreateSkillManager(int weaponItemId, params SkillData[] availableSkills)
        {
            var build = new CharacterBuild();
            build.Equip(new WeaponPart
            {
                ItemId = weaponItemId,
                Slot = EquipSlot.Weapon,
                Type = CharacterPartType.Weapon
            });

            var manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                new PlayerCharacter(device: null, texturePool: null, build: build));

            var availableSkillsField = typeof(SkillManager).GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(availableSkillsField);
            availableSkillsField!.SetValue(manager, new List<SkillData>(availableSkills));
            return manager;
        }

        private static void InvokeQueueFollowUpAttack(SkillManager manager, SkillData triggerSkill, int currentTime, int? targetMobId, bool facingRight)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("TryQueueFollowUpAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(manager, new object[] { triggerSkill, currentTime, targetMobId, facingRight });
        }

        private static int GetQueuedFollowUpAttackCount(SkillManager manager)
        {
            FieldInfo field = typeof(SkillManager).GetField("_queuedFollowUpAttacks", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var queue = field!.GetValue(manager) as System.Collections.ICollection;
            Assert.NotNull(queue);
            return queue!.Count;
        }
    }
}
