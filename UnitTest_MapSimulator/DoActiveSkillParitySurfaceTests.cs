using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class DoActiveSkillParitySurfaceTests
    {
        [Fact]
        public void MovingShootMappedCandidates_KeepSwingT2PoleArmInTwoHandedSwingFamily()
        {
            var skill = new SkillData
            {
                ClientInfoType = 3
            };

            var candidates = SkillManager.EnumerateQueuedMovingShootEntryMappedActionCandidates(
                    skill,
                    queuedAttackActionType: 7,
                    currentActionName: "swingT2PoleArm",
                    currentRawActionCode: 127,
                    currentWeaponType: "polearm",
                    publishedWeaponActionNames: new[] { "swingT2PoleArm" })
                .ToArray();

            Assert.Contains(candidates, candidate =>
                string.Equals(candidate.ActionName, "swingT2PoleArm", StringComparison.OrdinalIgnoreCase)
                && candidate.RawActionCode == 127);
        }

        [Fact]
        public void MovingShootQueuedEntryAction_ResolvesSwingT2PoleArmUnderPublishedFilter()
        {
            var skill = new SkillData
            {
                ClientInfoType = 3
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: "swingT2PoleArm",
                currentRawActionCode: 127,
                queuedAttackActionType: 7,
                currentWeaponType: "polearm",
                publishedWeaponActionNames: new[] { "swingT2PoleArm" },
                nextCandidateIndex: _ => 0);

            Assert.Equal("swingT2PoleArm", actionName);
            Assert.Equal(127, rawActionCode);
        }

        [Fact]
        public void ResolveMovementFamily_TreatsWildHunterJaguarJumpAsExplicitBoundJump()
        {
            var skill = new SkillData
            {
                SkillId = 33001002,
                ClientInfoType = 0,
                CasterMove = false,
                AvailableInJumpingState = false
            };

            SkillManager.SkillMovementFamily movementFamily = SkillManager.ResolveMovementFamily(
                skill,
                movementActionName: null);

            Assert.Equal(SkillManager.SkillMovementFamily.BoundJump, movementFamily);
        }

        [Fact]
        public void ResolveMovementFamily_TreatsKnownType40IceDoubleJumpFamilyAsBoundJump_WithoutActionSurface()
        {
            var skill = new SkillData
            {
                SkillId = 50001098,
                ClientInfoType = 40,
                CasterMove = true,
                AvailableInJumpingState = true
            };

            SkillManager.SkillMovementFamily movementFamily = SkillManager.ResolveMovementFamily(
                skill,
                movementActionName: null);

            Assert.Equal(SkillManager.SkillMovementFamily.BoundJump, movementFamily);
        }

        [Fact]
        public void TryResolveClientBoundJumpImpact_UsesSharedFlashJumpLaneForKnownType40IceDoubleJumpFamily_WithoutActionSurface()
        {
            var skill = new SkillData
            {
                SkillId = 50001098,
                ClientInfoType = 40,
                CasterMove = true,
                AvailableInJumpingState = true
            };

            bool resolved = SkillManager.TryResolveClientBoundJumpImpact(
                skill,
                level: 8,
                facingRight: false,
                movementActionName: null,
                out float impactX,
                out float impactY);

            Assert.True(resolved);
            Assert.Equal(-430f, impactX);
            Assert.Equal(-290f, impactY);
        }

        [Fact]
        public void DoActiveSkillExecutionLane_GenericCast_IsExplicitForOrdinaryCastSurface()
        {
            var skill = new SkillData
            {
                SkillId = 2111007,
                ActionName = "alert2"
            };

            SkillManager.ClientDoActiveSkillExecutionLane lane = SkillManager.ResolveDoActiveSkillExecutionLane(skill);

            Assert.Equal(SkillManager.ClientDoActiveSkillExecutionLane.GenericCast, lane);
        }

        [Fact]
        public void DoActiveSkillExecutionLane_ExplicitNonMeleeFamilies_AreClassifiedBeforeGenericFallback()
        {
            var prepare = new SkillData
            {
                SkillId = 35101009,
                IsPrepareSkill = true,
                IsKeydownSkill = true,
                ActionName = "flamethrower2"
            };
            var movement = new SkillData
            {
                SkillId = 2111003,
                IsMovement = true,
                ActionName = "teleport"
            };
            var summon = new SkillData
            {
                SkillId = 3111002,
                IsSummon = true,
                ActionName = "summon"
            };
            var heal = new SkillData
            {
                SkillId = 2301002,
                IsHeal = true,
                ActionName = "alert2"
            };
            var townPortal = new SkillData
            {
                SkillId = 2311002,
                ActionName = "alert2"
            };
            var beginnerTownPortal = new SkillData
            {
                SkillId = 8001,
                ActionName = "alert2"
            };

            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.Prepare,
                SkillManager.ResolveDoActiveSkillExecutionLane(prepare));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.Movement,
                SkillManager.ResolveDoActiveSkillExecutionLane(movement));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.Summon,
                SkillManager.ResolveDoActiveSkillExecutionLane(summon));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.Heal,
                SkillManager.ResolveDoActiveSkillExecutionLane(heal));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.TownPortal,
                SkillManager.ResolveDoActiveSkillExecutionLane(townPortal));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.TownPortal,
                SkillManager.ResolveDoActiveSkillExecutionLane(beginnerTownPortal));
        }

        [Fact]
        public void DoActiveSkillExecutionLane_PrepareFamilyPrecedesMovementSurface()
        {
            var prepareMovement = new SkillData
            {
                SkillId = 33101005,
                IsPrepareSkill = true,
                IsKeydownSkill = true,
                IsMovement = true,
                CasterMove = true,
                ActionName = "jump"
            };

            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.Prepare,
                SkillManager.ResolveDoActiveSkillExecutionLane(prepareMovement));
        }

        [Fact]
        public void HelperOnlyAffectedSkillEffect_NoStandaloneSurface_SuppressesDirectCastLane()
        {
            var skill = new SkillData
            {
                SkillId = 9900000,
                AffectedSkillIds = new[] { 9900001 },
                AffectedSkillEffect = "bodyAttack&&stun"
            };

            Assert.True(skill.UsesHelperOnlyAffectedSkillPassiveData);
            Assert.True(skill.SuppressesStandaloneActiveCast);
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.None,
                SkillManager.ResolveDoActiveSkillExecutionLane(skill));
        }

        [Theory]
        [InlineData(2111007, "alert2")]
        [InlineData(32111010, "alert2")]
        [InlineData(32120000, "alert2")]
        [InlineData(32110000, "alert2")]
        [InlineData(5220011, "shoot1")]
        [InlineData(35101009, "flamethrower2")]
        public void AffectedSkillRowsWithAuthoredCastSurface_RemainDirectCastAdmitted(int skillId, string actionName)
        {
            var skill = new SkillData
            {
                SkillId = skillId,
                ActionName = actionName,
                AffectedSkillIds = new[] { 5211006 },
                AffectedSkillEffect = "amplifyDamage",
                Effect = CreateAnimation("effect"),
                HitEffect = CreateAnimation("hit")
            };

            Assert.False(skill.UsesHelperOnlyAffectedSkillPassiveData);
            Assert.False(skill.SuppressesStandaloneActiveCast);
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.GenericCast,
                SkillManager.ResolveDoActiveSkillExecutionLane(skill));
        }

        [Theory]
        [InlineData("teleport", 1, 0, false)]
        [InlineData("rush", 2, 0, false)]
        [InlineData("fly", 3, 0, false)]
        [InlineData("hop", 3, 0, false)]
        [InlineData("doubleJump", 5, 40, true)]
        public void MovementFamilyDispatch_MatchesClientOwnedSplit(
            string actionName,
            int expectedFamily,
            int clientInfoType,
            bool casterMove)
        {
            var skill = new SkillData
            {
                SkillId = 9900100,
                ActionName = actionName,
                ClientInfoType = clientInfoType,
                CasterMove = casterMove
            };

            SkillManager.SkillMovementFamily family = SkillManager.ResolveMovementFamily(skill, actionName);

            Assert.Equal((SkillManager.SkillMovementFamily)expectedFamily, family);
        }

        [Fact]
        public void MovementFamilyDispatch_Type41CasterMoveWithoutNamedAction_RemainsGroundedRush()
        {
            var skill = new SkillData
            {
                SkillId = 4321000,
                ClientInfoType = 41,
                CasterMove = true
            };

            SkillManager.SkillMovementFamily family = SkillManager.ResolveMovementFamily(skill, movementActionName: null);

            Assert.Equal(SkillManager.SkillMovementFamily.Rush, family);
        }

        [Fact]
        public void InvincibleZoneExecutionLane_IsLimitedToKnownClientSkillIds()
        {
            var smoke = new SkillData { SkillId = 4221006, ZoneType = "invincible", IsSummon = true };
            var partyShield = new SkillData { SkillId = 32121006, ZoneType = "invincible" };
            var unrelated = new SkillData { SkillId = 1234567, ZoneType = "invincible", ActionName = "buff" };

            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.InvincibleZone,
                SkillManager.ResolveDoActiveSkillExecutionLane(smoke));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.InvincibleZone,
                SkillManager.ResolveDoActiveSkillExecutionLane(partyShield));
            Assert.Equal(
                SkillManager.ClientDoActiveSkillExecutionLane.GenericCast,
                SkillManager.ResolveDoActiveSkillExecutionLane(unrelated));
        }

        [Fact]
        public void InvincibleZoneAnimation_UsesTileFirst_ThenSpecialFallback()
        {
            SkillAnimation tile = CreateAnimation("tile");
            SkillAnimation special = CreateAnimation("special");
            var smoke = new SkillData
            {
                SkillId = 4221006,
                ZoneType = "invincible",
                ZoneAnimation = tile,
                AvatarOverlayEffect = special
            };

            var partyShield = new SkillData
            {
                SkillId = 32121006,
                ZoneType = "invincible",
                AvatarOverlayEffect = special
            };

            Assert.Same(tile, SkillManager.ResolveInvincibleZoneAnimation(smoke));
            Assert.Same(special, SkillManager.ResolveInvincibleZoneAnimation(partyShield));
        }

        [Fact]
        public void BeholderDamagedFollowUp_StaysOnReactiveOwnerDamagedClassifier()
        {
            var skill = new SkillData
            {
                SkillId = 1320011,
                TriggerCondition = "damaged",
                MinionAttack = "normal",
                RequiredSkillIds = new[] { 1321007 }
            };

            Assert.True(SkillManager.IsReactiveOwnerDamageSummonAttackSkill(skill, requiredSummonSkillId: 1321007));
        }

        [Fact]
        public void QuickSlotAssignmentRule_RejectsPassiveOrHelperOnlySkills()
        {
            var castable = new SkillData
            {
                SkillId = 2111007,
                ActionName = "alert2"
            };
            var helperOnly = new SkillData
            {
                SkillId = 9900200,
                AffectedSkillIds = new[] { 9900201 },
                AffectedSkillEffect = "dot"
            };

            Assert.True(SkillManager.CanAssignHotkeySkillForKnownState(castable, learnedLevel: 1, isSkillAllowedForCurrentJob: true));
            Assert.False(SkillManager.CanAssignHotkeySkillForKnownState(castable, learnedLevel: 1, isSkillAllowedForCurrentJob: false));
            Assert.False(SkillManager.CanAssignHotkeySkillForKnownState(helperOnly, learnedLevel: 1, isSkillAllowedForCurrentJob: true));
        }

        [Fact]
        public void MovingShootUnsupportedWeaponAttackActionType_IsRejectedBeforeEntrySelection()
        {
            Assert.True(
                SkillManager.TryResolveMovingShootAttackActionTypeFromWeaponAttackMetadata(
                    currentWeaponAttackActionType: 13,
                    currentWeaponType: "bow",
                    out int attackActionType));
            Assert.Equal(-1, attackActionType);

            var skill = new SkillData
            {
                AttackType = SkillAttackType.Ranged,
                ActionName = "shoot1",
                ActionNames = new[] { "shoot1" }
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: "shoot1",
                currentRawActionCode: null,
                queuedAttackActionType: attackActionType,
                currentWeaponType: "bow",
                publishedWeaponActionNames: new[] { "shoot1" },
                nextCandidateIndex: _ => 0);

            Assert.Null(actionName);
            Assert.Null(rawActionCode);
        }

        [Theory]
        [InlineData("double bowgun", 11)]
        [InlineData("cannon", 12)]
        public void MovingShootFallback_PreservesPostV95OwnerFromWeaponTypeWithoutMetadata(string weaponType, int expectedType)
        {
            var skill = new SkillData
            {
                SkillId = 35101009,
                AttackType = SkillAttackType.Ranged,
                Projectile = null
            };

            int resolved = SkillManager.ResolveQueuedMovingShootAttackActionType(
                skill,
                currentActionName: null,
                currentRawActionCode: null,
                currentWeaponAttackActionType: null,
                currentWeaponType: weaponType);

            Assert.Equal(expectedType, resolved);
        }

        [Theory]
        [InlineData("double bowgun", "speedDualShot", 11)]
        [InlineData("cannon", "superCannon", 12)]
        public void MovingShootFallback_PreservesPostV95OwnerFromCurrentActionAndWeaponType(
            string weaponType,
            string currentActionName,
            int expectedType)
        {
            var skill = new SkillData
            {
                SkillId = 35101009,
                AttackType = SkillAttackType.Ranged
            };

            int resolved = SkillManager.ResolveQueuedMovingShootAttackActionType(
                skill,
                currentActionName: currentActionName,
                currentRawActionCode: null,
                currentWeaponAttackActionType: null,
                currentWeaponType: weaponType);

            Assert.Equal(expectedType, resolved);
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
                        Delay = 100,
                        Origin = Point.Empty,
                        Bounds = Rectangle.Empty
                    }
                }
            };
        }
    }
}
