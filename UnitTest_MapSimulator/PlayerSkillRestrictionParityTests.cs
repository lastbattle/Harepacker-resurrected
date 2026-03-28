using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator
{
    public class PlayerSkillRestrictionParityTests
    {
        [Fact]
        public void PolymorphMobSkill_BlocksSkillUsageAndClearsAvatarTransform()
        {
            PlayerCharacter player = CreatePlaceholderPlayer();
            player.SetSkillMorphLoader(_ => CreateMorphPart());

            PlayerMobStatusController controller = new(player, skills: null);
            MobSkillRuntimeData runtimeData = new()
            {
                X = 1,
                DurationMs = 10_000,
                PropPercent = 100
            };

            bool applied = controller.TryApplyMobSkill(172, runtimeData, currentTime: 1_000);
            PlayerMobStatusFrameState frameState = controller.Update(currentTime: 1_000);

            Assert.True(applied);
            Assert.True(frameState.SkillCastBlocked);
            Assert.Equal("Skills cannot be used while polymorphed.", controller.GetSkillCastRestrictionMessage(1_000));
            Assert.True(player.HasExternalAvatarTransform((int)PlayerMobStatusEffect.Polymorph));

            Assert.True(controller.ClearStatus(PlayerMobStatusEffect.Polymorph));
            Assert.False(player.HasExternalAvatarTransform((int)PlayerMobStatusEffect.Polymorph));
            Assert.Null(controller.GetSkillCastRestrictionMessage(1_000));
        }

        [Fact]
        public void RocketBoosterMaintenance_UsesSharedExternalRestrictionProvider()
        {
            PlayerCharacter player = CreatePlaceholderPlayer();
            SkillManager manager = new(new SkillLoader(skillWz: null, device: null, texturePool: null), player);
            SkillData rocketBooster = new()
            {
                SkillId = 35101004,
                Name = "Rocket Booster",
                IsMovement = true
            };

            manager.SetFieldSkillRestrictionEvaluator(_ => true);

            Assert.True(InvokeCanMaintainRocketBooster(manager, rocketBooster, currentTime: 2_000));

            manager.SetExternalStateRestrictionMessageProvider(_ => "blocked");

            Assert.False(InvokeCanMaintainRocketBooster(manager, rocketBooster, currentTime: 2_000));
        }

        [Fact]
        public void TimedExternalAvatarTransform_ExpiresDuringPlayerUpdate()
        {
            PlayerCharacter player = new(new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart(),
                HP = 100,
                MaxHP = 100,
                MP = 50,
                MaxMP = 50
            });
            player.SetSkillMorphLoader(_ => CreateMorphPart());

            Assert.True(player.ApplyExternalAvatarTransform(sourceId: 2210000, actionName: null, morphTemplateId: 1, expirationTime: 2_500));
            Assert.True(player.HasExternalAvatarTransform(2210000));

            player.Update(currentTime: 2_499, deltaTime: 0.016f);
            Assert.True(player.HasExternalAvatarTransform(2210000));

            player.Update(currentTime: 2_500, deltaTime: 0.016f);
            Assert.False(player.HasExternalAvatarTransform(2210000));
        }

        [Fact]
        public void UndeadMobSkill_UsesAuthoredRecoveryDamagePercent()
        {
            PlayerCharacter player = new(new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart(),
                HP = 100,
                MaxHP = 100,
                MP = 50,
                MaxMP = 50
            });
            player.HP = 80;

            PlayerMobStatusController controller = new(player, skills: null);
            MobSkillRuntimeData runtimeData = new()
            {
                X = 70,
                DurationMs = 10_000,
                PropPercent = 100
            };

            Assert.True(controller.TryApplyMobSkill(133, runtimeData, currentTime: 1_000));

            PlayerMobStatusFrameState frameState = controller.Update(currentTime: 1_000);
            player.ApplyMobRecoveryModifiers(
                frameState.HpRecoveryReversed,
                frameState.MaxHpPercentCap,
                frameState.MaxMpPercentCap,
                frameState.HpRecoveryDamagePercent);
            player.Recover(20, 0);

            Assert.True(frameState.HpRecoveryReversed);
            Assert.Equal(70, frameState.HpRecoveryDamagePercent);
            Assert.Equal(66, player.HP);
        }

        [Fact]
        public void BanishMobSkill_LocksMovementDuringActiveDuration()
        {
            PlayerCharacter player = CreatePlaceholderPlayer();
            bool teleported = false;

            PlayerMobStatusController controller = new(player, skills: null, teleportToSpawn: () => teleported = true);
            MobSkillRuntimeData runtimeData = new()
            {
                DurationMs = 1_000,
                PropPercent = 100
            };

            Assert.True(controller.TryApplyMobSkill(129, runtimeData, currentTime: 1_000));

            PlayerMobStatusFrameState frameState = controller.Update(currentTime: 1_000);

            Assert.True(teleported);
            Assert.True(frameState.MovementLocked);
            Assert.True(frameState.SkillCastBlocked);
            Assert.Equal("Skills cannot be used while banished.", controller.GetSkillCastRestrictionMessage(1_000));
        }

        [Fact]
        public void UnableToUseTamingMobFieldLimit_BlocksEventMountSkills()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Taming_Mob;
            SkillData mountSkill = new()
            {
                SkillId = 80001045,
                Name = "Monster Rider",
                UsesTamingMobMount = true
            };

            Assert.Equal(
                "Mount and mechanic vehicle skills cannot be used in this field.",
                FieldSkillRestrictionEvaluator.GetRestrictionMessage(fieldLimit, mountSkill));
        }

        [Fact]
        public void UnableToUseTamingMobFieldLimit_BlocksMechanicVehicleOwnershipSkills()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Taming_Mob;
            SkillData mechanicMountSkill = new()
            {
                SkillId = 35001002,
                Name = "Mech: Prototype",
                Description = "Summon and mount the Prototype Mech.",
                ClientInfoType = 13
            };

            Assert.Equal(
                "Mount and mechanic vehicle skills cannot be used in this field.",
                FieldSkillRestrictionEvaluator.GetRestrictionMessage(fieldLimit, mechanicMountSkill));
        }

        [Fact]
        public void LinkedAffectedSkillHelpers_AreClassifiedAsPassiveFollowUpData()
        {
            SkillLoader loader = new(skillWz: null, device: null, texturePool: null);
            SkillData helperSkill = new()
            {
                SkillId = 32110000,
                AffectedSkillId = 32101002,
                AffectedSkillEffect = "partyDamageSharing"
            };

            InvokeDetermineSkillType(loader, helperSkill, new WzSubProperty("32110000"));

            Assert.True(helperSkill.IsPassive);
            Assert.Equal(SkillType.Passive, helperSkill.Type);
            Assert.False(helperSkill.IsAttack);
        }

        [Fact]
        public void LinkedAffectedSkillHelpers_CannotBeAssignedOrCastDirectly()
        {
            PlayerCharacter player = new((GraphicsDevice)null, (TexturePool)null, new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart(),
                HP = 100,
                MaxHP = 100,
                MP = 100,
                MaxMP = 100
            });
            SkillManager manager = new(new SkillLoader(skillWz: null, device: null, texturePool: null), player);
            SkillData helperSkill = new()
            {
                SkillId = 5220011,
                Job = 522,
                Type = SkillType.Passive,
                IsPassive = true,
                AffectedSkillId = 5211006,
                AffectedSkillEffect = "amplifyDamage",
                MaxLevel = 20,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData()
                }
            };

            SetAvailableSkills(manager, helperSkill);
            manager.SetSkillLevel(helperSkill.SkillId, 1);

            Assert.False(manager.CanAssignHotkeySkill(helperSkill.SkillId));
            Assert.False(manager.CanCastSkill(helperSkill.SkillId, currentTime: 1_000));
        }

        private static PlayerCharacter CreatePlaceholderPlayer()
        {
            return new PlayerCharacter((GraphicsDevice)null, texturePool: null, build: null);
        }

        private static CharacterPart CreateMorphPart()
        {
            CharacterAnimation standAnimation = new()
            {
                ActionName = "stand"
            };
            standAnimation.Frames.Add(new CharacterFrame
            {
                Delay = 100,
                Origin = Point.Zero
            });
            standAnimation.CalculateTotalDuration();

            return new CharacterPart
            {
                ItemId = 1,
                Name = "TestMorph",
                Type = CharacterPartType.Morph,
                Animations =
                {
                    ["stand"] = standAnimation
                }
            };
        }

        private static bool InvokeCanMaintainRocketBooster(SkillManager manager, SkillData skill, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod(
                "CanMaintainRocketBooster",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            return (bool)method.Invoke(manager, new object[] { skill, currentTime });
        }

        private static void InvokeDetermineSkillType(SkillLoader loader, SkillData skill, WzSubProperty skillNode)
        {
            MethodInfo method = typeof(SkillLoader).GetMethod(
                "DetermineSkillType",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            method.Invoke(loader, new object[] { skill, skillNode });
        }

        private static void SetAvailableSkills(SkillManager manager, params SkillData[] skills)
        {
            FieldInfo field = typeof(SkillManager).GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(manager, skills.ToList());
        }
    }
}
