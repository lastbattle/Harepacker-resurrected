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

        [Fact]
        public void Update_CancelsQueuedFollowUpAttackWhenWeaponChangesBeforeExecution()
        {
            var triggerSkill = CreateTriggerSkill();
            var followUpSkill = CreateFollowUpSkill();
            var manager = CreateSkillManager(1302000, triggerSkill, followUpSkill);
            manager.SetSkillLevel(triggerSkill.SkillId, 1);
            manager.SetSkillLevel(followUpSkill.SkillId, 1);

            int? castSkillId = null;
            manager.OnSkillCast = cast => castSkillId = cast?.SkillId;

            InvokeQueueFollowUpAttack(manager, triggerSkill, 1000, 77, true);
            EquipWeapon(manager, 1312000);

            manager.Update(1090, 0.016f);

            Assert.Null(castSkillId);
            Assert.Equal(0, GetQueuedFollowUpAttackCount(manager));
        }

        [Fact]
        public void Update_QueuedProjectileFollowUpKeepsOriginalFacingWhenPlayerTurnsBeforeExecution()
        {
            var triggerSkill = CreateTriggerSkill();
            var followUpSkill = CreateProjectileFollowUpSkill();
            var manager = CreateSkillManager(1302000, triggerSkill, followUpSkill);
            manager.SetSkillLevel(triggerSkill.SkillId, 1);
            manager.SetSkillLevel(followUpSkill.SkillId, 1);

            InvokeQueueFollowUpAttack(manager, triggerSkill, 1000, 77, true);
            SetFacingRight(manager, false);

            manager.Update(1090, 0.016f);

            var projectile = GetSingleProjectile(manager);
            Assert.True(projectile.FacingRight);
            Assert.True(projectile.VelocityX > 0);
        }

        [Fact]
        public void TryQueueFollowUpAttack_ReplacesPendingFollowUpInsteadOfStacking()
        {
            var triggerSkill = CreateTriggerSkill();
            var followUpSkill = CreateFollowUpSkill();
            var manager = CreateSkillManager(1302000, triggerSkill, followUpSkill);
            manager.SetSkillLevel(triggerSkill.SkillId, 1);
            manager.SetSkillLevel(followUpSkill.SkillId, 1);

            InvokeQueueFollowUpAttack(manager, triggerSkill, 1000, 77, true);
            InvokeQueueFollowUpAttack(manager, triggerSkill, 1040, 88, false);

            Assert.Equal(1, GetQueuedFollowUpAttackCount(manager));

            object queuedAttack = GetSingleQueuedFollowUpAttack(manager);
            Assert.Equal(1040 + 90, GetQueuedFollowUpAttackIntProperty(queuedAttack, "ExecuteTime"));
            Assert.Equal(88, GetQueuedFollowUpAttackNullableIntProperty(queuedAttack, "TargetMobId"));
            Assert.False(GetQueuedFollowUpAttackBoolProperty(queuedAttack, "FacingRight"));
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

        private static SkillData CreateProjectileFollowUpSkill()
        {
            return new SkillData
            {
                SkillId = 1000001,
                MaxLevel = 1,
                IsAttack = true,
                IsPassive = true,
                Projectile = new ProjectileData
                {
                    Speed = 300f,
                    LifeTime = 2000f,
                    MaxHits = 1
                },
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 150,
                        AttackCount = 1,
                        MobCount = 1,
                        Prop = 100,
                        BulletCount = 1
                    }
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

        private static object GetSingleQueuedFollowUpAttack(SkillManager manager)
        {
            FieldInfo field = typeof(SkillManager).GetField("_queuedFollowUpAttacks", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var queue = Assert.IsAssignableFrom<System.Collections.IEnumerable>(field!.GetValue(manager));
            var enumerator = queue.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            object queuedAttack = enumerator.Current;
            Assert.False(enumerator.MoveNext());
            return queuedAttack;
        }

        private static int GetQueuedFollowUpAttackIntProperty(object queuedAttack, string propertyName)
        {
            PropertyInfo property = queuedAttack.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            return Assert.IsType<int>(property!.GetValue(queuedAttack));
        }

        private static int? GetQueuedFollowUpAttackNullableIntProperty(object queuedAttack, string propertyName)
        {
            PropertyInfo property = queuedAttack.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            return Assert.IsType<int?>(property!.GetValue(queuedAttack));
        }

        private static bool GetQueuedFollowUpAttackBoolProperty(object queuedAttack, string propertyName)
        {
            PropertyInfo property = queuedAttack.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            return Assert.IsType<bool>(property!.GetValue(queuedAttack));
        }

        private static ActiveProjectile GetSingleProjectile(SkillManager manager)
        {
            FieldInfo field = typeof(SkillManager).GetField("_projectiles", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var projectiles = Assert.IsType<List<ActiveProjectile>>(field!.GetValue(manager));
            var projectile = Assert.Single(projectiles);
            return projectile;
        }

        private static void EquipWeapon(SkillManager manager, int weaponItemId)
        {
            FieldInfo field = typeof(SkillManager).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var player = field!.GetValue(manager) as PlayerCharacter;
            Assert.NotNull(player);

            player!.Build.Equip(new WeaponPart
            {
                ItemId = weaponItemId,
                Slot = EquipSlot.Weapon,
                Type = CharacterPartType.Weapon
            });
        }

        private static void SetFacingRight(SkillManager manager, bool facingRight)
        {
            FieldInfo field = typeof(SkillManager).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var player = field!.GetValue(manager) as PlayerCharacter;
            Assert.NotNull(player);

            player!.FacingRight = facingRight;
        }
    }
}
