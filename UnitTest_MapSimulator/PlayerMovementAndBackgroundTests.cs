using System;
using System.Linq;
using System.Reflection;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Physics;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using Moq;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class PlayerMovementAndBackgroundTests
    {
        private static FootholdLine CreateFoothold(int x1, int y1, int x2, int y2)
        {
            var first = new FootholdAnchor(board: null, x: x1, y: y1, layer: 0, zm: 0, user: true);
            var second = new FootholdAnchor(board: null, x: x2, y: y2, layer: 0, zm: 0, user: true);
            return new FootholdLine(board: null, first, second);
        }

        private static Func<float, float, float, FootholdLine> CreateFootholdLookup(params FootholdLine[] footholds)
        {
            return (x, y, searchRange) =>
            {
                FootholdLine bestFoothold = null;
                float bestDistance = float.MaxValue;
                const float upwardTolerance = 10f;

                foreach (var foothold in footholds)
                {
                    float minX = Math.Min(foothold.FirstDot.X, foothold.SecondDot.X);
                    float maxX = Math.Max(foothold.FirstDot.X, foothold.SecondDot.X);

                    if (x < minX || x > maxX)
                    {
                        continue;
                    }

                    float dx = foothold.SecondDot.X - foothold.FirstDot.X;
                    float dy = foothold.SecondDot.Y - foothold.FirstDot.Y;
                    float t = dx != 0 ? (x - foothold.FirstDot.X) / dx : 0f;
                    float footholdY = foothold.FirstDot.Y + t * dy;
                    float distance = footholdY - y;
                    float absDistance = Math.Abs(distance);

                    if ((distance >= 0 && distance < searchRange) || (distance < 0 && -distance <= upwardTolerance))
                    {
                        if (absDistance < bestDistance)
                        {
                            bestDistance = absDistance;
                            bestFoothold = foothold;
                        }
                    }
                }

                return bestFoothold;
            };
        }

        [Fact]
        public void JumpOffLadder_ClearsLadderStateAndSetsJumpMotion()
        {
            var physics = new CVecCtrl();
            physics.GrabLadder(120, 80, 240, true);
            physics.IsJumpingDown = true;

            physics.JumpOffLadder(130, -180);

            Assert.False(physics.IsOnLadderOrRope);
            Assert.Null(physics.CurrentFoothold);
            Assert.False(physics.IsJumpingDown);
            Assert.Equal(130, physics.VelocityX);
            Assert.Equal(-180, physics.VelocityY);
            Assert.Equal(JumpState.Jumping, physics.CurrentJumpState);
            Assert.Equal(MoveAction.Jump, physics.CurrentAction);
        }

        [Fact]
        public void ImpactWhileOnLadder_DetachesIntoAirborneKnockback()
        {
            var physics = new CVecCtrl();
            physics.SetPosition(120, 180);
            physics.GrabLadder(120, 80, 240, true);

            physics.Impact(250, -150);
            physics.Update(0.016f);

            Assert.False(physics.IsOnLadderOrRope);
            Assert.Null(physics.CurrentFoothold);
            Assert.Equal(JumpState.Jumping, physics.CurrentJumpState);
            Assert.True(physics.X > 120, $"Expected knockback to move horizontally off the ladder, but X={physics.X}");
            Assert.True(physics.Y < 180, $"Expected upward knockback to move above the ladder hit position, but Y={physics.Y}");
        }

        [Fact]
        public void TakingDamageWhileHoldingUp_RegrabsLadderBeforeFalling()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(120, 180);
            player.Physics.GrabLadder(120, 80, 240, true);
            player.SetLadderLookup((x, y, range) => (120, 80, 240, true));
            player.SetInput(left: false, right: false, up: true, down: false, jump: false, attack: false, pickup: false);

            player.TakeDamage(10, knockbackX: 250, knockbackY: -150);
            player.Update(Environment.TickCount, 0.016f);

            Assert.Equal(PlayerState.Ladder, player.State);
            Assert.True(player.Physics.IsOnLadderOrRope);
            Assert.True(player.Physics.IsOnLadder());
            Assert.Equal(120, player.Physics.LadderX);
            Assert.Equal(120, player.X);
        }

        [Fact]
        public void ImpactWhileSwimming_MergesWithCurrentFloatVelocityLikeClient()
        {
            var physics = new CVecCtrl();
            physics.SetPosition(100, 80);
            physics.IsInSwimArea = true;
            physics.VelocityX = 60;
            physics.VelocityY = 120;

            physics.Impact(200, -150);

            Assert.Equal(200, physics.VelocityX);
            Assert.Equal(-30, physics.VelocityY);
            Assert.Equal(JumpState.Jumping, physics.CurrentJumpState);
            Assert.False(physics.IsInKnockback);
        }

        [Fact]
        public void PlayerCombat_ScalesKnockbackDownWhileSwimming()
        {
            var swimPlayer = new PlayerCharacter(device: null, texturePool: null, build: null);
            var landPlayer = new PlayerCharacter(device: null, texturePool: null, build: null);
            var getKnockback = typeof(PlayerCombat).GetMethod("GetPlayerKnockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(getKnockback);

            swimPlayer.SetPosition(100, 80);
            swimPlayer.SetSwimAreaCheck((x, y, range) => true);
            swimPlayer.Update(1000, 0.016f);

            landPlayer.SetPosition(100, 80);

            var swimCombat = new PlayerCombat(swimPlayer);
            var landCombat = new PlayerCombat(landPlayer);

            var swimKnockback = (Microsoft.Xna.Framework.Vector2)getKnockback!.Invoke(swimCombat, new object[] { 40f })!;
            var landKnockback = (Microsoft.Xna.Framework.Vector2)getKnockback.Invoke(landCombat, new object[] { 40f })!;

            Assert.InRange(swimKnockback.X, 149.5f, 150.5f);
            Assert.InRange(swimKnockback.Y, -68f, -67f);
            Assert.True(Math.Abs(swimKnockback.X) < Math.Abs(landKnockback.X));
            Assert.True(Math.Abs(swimKnockback.Y) < Math.Abs(landKnockback.Y));
        }

        [Fact]
        public void JumpInSwimArea_TransitionsIntoSwimmingWithoutGroundJumpLaunch()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            var foothold = CreateFoothold(0, 100, 200, 100);

            player.SetPosition(100, 100);
            player.Physics.LandOnFoothold(foothold);
            player.SetFootholdLookup(CreateFootholdLookup(foothold));
            player.SetSwimAreaCheck((x, y, range) => true);
            player.SetInput(left: false, right: false, up: false, down: false, jump: true, attack: false, pickup: false);

            player.Update(Environment.TickCount, 0.016f);

            Assert.False(player.Physics.IsOnFoothold());
            Assert.Equal(PlayerState.Swimming, player.State);
            Assert.Equal(CharacterAction.Swim, player.CurrentAction);
            Assert.True(player.Y < 100f, $"Expected swim jump to move upward immediately, but Y={player.Y}");
            Assert.InRange(player.Physics.VelocityY, -CVecCtrl.JumpVelocity, -CVecCtrl.JumpVelocity * 0.5f);
        }

        [Fact]
        public void JumpWhileSwimming_AppliesClientStyleSwimJumpImpulse()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);

            player.SetPosition(100, 80);
            player.SetSwimAreaCheck((x, y, range) => true);
            player.Update(1000, 0.016f);

            player.Physics.VelocityY = 120f;
            player.SetInput(left: false, right: false, up: false, down: false, jump: true, attack: false, pickup: false);
            player.Update(1400, 0.016f);

            Assert.Equal(PlayerState.Swimming, player.State);
            double expectedImpulse = PhysicsConstants.Instance.SwimSpeed * 5.0 * PhysicsConstants.Instance.JumpSpeedTuningScale;
            Assert.InRange(
                player.Physics.VelocityY,
                (float)(-expectedImpulse * 1.15),
                (float)(-expectedImpulse * 0.85));
        }

        [Fact]
        public void SwimDownOntoFoothold_LandsInsteadOfPassingThrough()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            var foothold = CreateFoothold(0, 100, 200, 100);

            player.SetPosition(100, 80);
            player.SetFootholdLookup(CreateFootholdLookup(foothold));
            player.SetSwimAreaCheck((x, y, range) => true);
            player.SetInput(left: false, right: false, up: false, down: true, jump: false, attack: false, pickup: false);

            for (int i = 0; i < 30 && !player.Physics.IsOnFoothold(); i++)
            {
                player.Update(Environment.TickCount + (i * 16), 0.016f);
            }

            Assert.True(player.Physics.IsOnFoothold(), $"Expected swim descent to land on foothold, but position is ({player.X}, {player.Y})");
            Assert.Equal(PlayerState.Standing, player.State);
            Assert.InRange(player.Y, 99.5f, 100.5f);
            Assert.True(player.Y < 120f, $"Expected foothold collision before drifting below the platform, but Y={player.Y}");
        }

        [Fact]
        public void SwimAnimation_HoldsIdleFrameUntilFloatMovementStarts()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            var getRenderAnimationTime = typeof(PlayerCharacter).GetMethod("GetRenderAnimationTime", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(getRenderAnimationTime);

            player.SetPosition(100, 80);
            player.SetSwimAreaCheck((x, y, range) => true);
            player.SetInput(left: false, right: false, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Update(1000, 0.016f);

            Assert.Equal(PlayerState.Swimming, player.State);
            Assert.Equal(CharacterAction.Swim, player.CurrentAction);
            Assert.Equal(0, (int)getRenderAnimationTime!.Invoke(player, new object[] { 1300 })!);

            player.SetInput(left: false, right: true, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Update(1400, 0.016f);

            Assert.True((int)getRenderAnimationTime.Invoke(player, new object[] { 1460 })! > 0,
                "Expected swim animation to advance once float movement starts.");

            player.SetInput(left: false, right: false, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Physics.VelocityX = 0;
            player.Physics.VelocityY = 0;
            player.Update(1600, 0.016f);

            Assert.Equal(0, (int)getRenderAnimationTime.Invoke(player, new object[] { 1900 })!);
        }

        [Fact]
        public void IsUserFlying_RequiresFlyingMapLikeClientVecCtrlUser()
        {
            var physics = new CVecCtrl
            {
                HasFlyingAbility = true
            };

            Assert.False(physics.IsUserFlying());

            physics.IsFlyingMap = true;
            Assert.True(physics.IsUserFlying());
        }

        [Fact]
        public void IsUserFlying_RespectsFlyingSkillGateWhenMapRequiresIt()
        {
            var physics = new CVecCtrl
            {
                IsFlyingMap = true,
                RequiresFlyingSkillForMap = true
            };

            Assert.False(physics.IsUserFlying());

            physics.HasFlyingAbility = true;
            Assert.True(physics.IsUserFlying());

            physics.HasFlyingAbility = false;
            physics.IsFlying = true;
            Assert.True(physics.IsUserFlying());
        }

        [Fact]
        public void CharacterLoader_StandardActionsIncludeSwimAndFly()
        {
            var standardActionsField = typeof(CharacterLoader).GetField("StandardActions", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(standardActionsField);

            var standardActions = (string[])standardActionsField!.GetValue(null)!;

            Assert.Contains("swim", standardActions);
            Assert.Contains("fly", standardActions);
        }

        [Fact]
        public void CharacterLoader_ActionLoadOrder_IncludesRareAndDeathActionsFromImageSurface()
        {
            var buildActionLoadOrder = typeof(CharacterLoader).GetMethod("BuildActionLoadOrder", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(buildActionLoadOrder);

            var actionOrder = ((IReadOnlyList<string>)buildActionLoadOrder!.Invoke(null, new object[]
            {
                new[] { "info", "dead", "ghost", "stand1", "swingO3", "dash", "_canvas" },
                true
            })!).ToList();

            Assert.Contains("stand1", actionOrder);
            Assert.Contains("swingO3", actionOrder);
            Assert.Contains("dead", actionOrder);
            Assert.Contains("ghost", actionOrder);
            Assert.Contains("dash", actionOrder);
            Assert.DoesNotContain("info", actionOrder);
            Assert.DoesNotContain("_canvas", actionOrder);
            Assert.True(actionOrder.IndexOf("stand1") < actionOrder.IndexOf("dead"));
            Assert.True(actionOrder.IndexOf("swingO3") < actionOrder.IndexOf("dead"));
        }

        [Fact]
        public void CharacterPart_SwimAndFlyLookupAliasEachOther()
        {
            var part = new BodyPart();
            var flyAnimation = new CharacterAnimation();
            flyAnimation.Frames.Add(new CharacterFrame());
            part.Animations["fly"] = flyAnimation;

            Assert.Same(flyAnimation, part.GetAnimation(CharacterAction.Swim));

            var swimOnlyPart = new BodyPart();
            var swimAnimation = new CharacterAnimation();
            swimAnimation.Frames.Add(new CharacterFrame());
            swimOnlyPart.Animations["swim"] = swimAnimation;

            Assert.Same(swimAnimation, swimOnlyPart.GetAnimation(CharacterAction.Fly));
        }

        [Fact]
        public void CharacterPart_ActionLookup_FollowsRawActionFamilyFallbacksBeforeStand()
        {
            var part = new BodyPart();
            var stabAnimation = new CharacterAnimation();
            stabAnimation.Frames.Add(new CharacterFrame());
            part.Animations["stabO1"] = stabAnimation;

            var ghostFallbackPart = new BodyPart();
            var deadAnimation = new CharacterAnimation();
            deadAnimation.Frames.Add(new CharacterFrame());
            ghostFallbackPart.Animations["dead"] = deadAnimation;

            Assert.Same(stabAnimation, part.GetAnimation("stabOF"));
            Assert.Same(deadAnimation, ghostFallbackPart.GetAnimation("ghost"));
        }

        [Fact]
        public void TriggerSkillAnimation_PreservesRareActionNameUntilStateMachineClearsIt()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);

            player.TriggerSkillAnimation("ghost");

            Assert.Equal(PlayerState.Attacking, player.State);
            Assert.Equal("ghost", player.CurrentActionName);

            player.Update(Environment.TickCount + 400, 0.016f);

            Assert.Equal(PlayerState.Falling, player.State);
            Assert.Equal("jump", player.CurrentActionName);
        }

        [Fact]
        public void TakingDamage_DrivesHitFaceExpressionUntilHitWindowExpires()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            int now = Environment.TickCount;

            player.TakeDamage(10);
            player.Update(now, 0.016f);

            Assert.Equal("hit", player.CurrentFaceExpressionName);

            player.Update(now + 500, 0.016f);

            Assert.NotEqual("hit", player.CurrentFaceExpressionName);
        }

        [Fact]
        public void FaceExpressionScheduler_CanEnterBlinkState()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            var updateFaceExpression = typeof(PlayerCharacter).GetMethod("UpdateFaceExpression", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextBlinkTime = typeof(PlayerCharacter).GetField("_nextBlinkTime", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(updateFaceExpression);
            Assert.NotNull(nextBlinkTime);

            int now = Environment.TickCount;
            nextBlinkTime!.SetValue(player, now);
            updateFaceExpression!.Invoke(player, new object[] { now });

            Assert.Equal("blink", player.CurrentFaceExpressionName);
        }

        [Fact]
        public void CharacterAssembler_WeaponFallback_UsesSharedNavelAnchorWithoutLiteralNudge()
        {
            var assembler = new CharacterAssembler(new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart()
            });
            var calculateEquipOffset = typeof(CharacterAssembler).GetMethod("CalculateEquipOffset", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(calculateEquipOffset);

            var bodyFrame = new CharacterFrame();
            bodyFrame.Map["navel"] = new Microsoft.Xna.Framework.Point(20, 30);

            var equipFrame = new CharacterFrame();
            equipFrame.Map["navel"] = new Microsoft.Xna.Framework.Point(4, 8);

            var offset = (Microsoft.Xna.Framework.Point)calculateEquipOffset!.Invoke(assembler, new object[]
            {
                equipFrame,
                bodyFrame,
                null,
                new Microsoft.Xna.Framework.Point(-20, -30),
                null,
                CharacterPartType.Weapon
            })!;

            Assert.Equal(new Microsoft.Xna.Framework.Point(-4, -8), offset);
        }

        [Fact]
        public void CharacterAssembler_EarringFallback_UsesSharedHeadAnchorOrder()
        {
            var assembler = new CharacterAssembler(new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart()
            });
            var tryCalculateHeadEquipOffset = typeof(CharacterAssembler).GetMethod("TryCalculateHeadEquipOffset", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(tryCalculateHeadEquipOffset);

            var headFrame = new CharacterFrame();
            headFrame.Map["brow"] = new Microsoft.Xna.Framework.Point(6, 10);

            var equipFrame = new CharacterFrame();
            equipFrame.Map["brow"] = new Microsoft.Xna.Framework.Point(2, 4);

            object[] args =
            {
                equipFrame,
                headFrame,
                new Microsoft.Xna.Framework.Point(100, 200),
                CharacterPartType.Earrings,
                null
            };

            bool resolved = (bool)tryCalculateHeadEquipOffset!.Invoke(assembler, args)!;
            var offset = (Microsoft.Xna.Framework.Point)args[4]!;

            Assert.True(resolved);
            Assert.Equal(new Microsoft.Xna.Framework.Point(104, 206), offset);
        }

        [Fact]
        public void CharacterAssembler_TamingMob_PreservesExactRideFramesAndFallsBackToSitOnlyVariants()
        {
            var assembler = new CharacterAssembler(new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart()
            });
            var getPartFrame = typeof(CharacterAssembler).GetMethod("GetPartFrame", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(getPartFrame);

            var standardMount = new CharacterPart
            {
                Type = CharacterPartType.TamingMob,
                Slot = EquipSlot.TamingMob
            };

            var standFrame = new CharacterFrame();
            var standAnimation = new CharacterAnimation();
            standAnimation.Frames.Add(standFrame);
            standardMount.Animations["stand1"] = standAnimation;

            var walkFrame = new CharacterFrame();
            var walkAnimation = new CharacterAnimation();
            walkAnimation.Frames.Add(walkFrame);
            standardMount.Animations["walk1"] = walkAnimation;

            var flyFrame = new CharacterFrame();
            var flyAnimation = new CharacterAnimation();
            flyAnimation.Frames.Add(flyFrame);
            standardMount.Animations["fly"] = flyAnimation;

            var passengerMount = new CharacterPart
            {
                Type = CharacterPartType.TamingMob,
                Slot = EquipSlot.TamingMob
            };

            var sitFrame = new CharacterFrame();
            var sitAnimation = new CharacterAnimation();
            sitAnimation.Frames.Add(sitFrame);
            passengerMount.Animations["sit"] = sitAnimation;

            var standResult = (CharacterFrame)getPartFrame!.Invoke(assembler, new object[] { standardMount, "stand1", 0 })!;
            var walkResult = (CharacterFrame)getPartFrame.Invoke(assembler, new object[] { standardMount, "walk1", 0 })!;
            var flyResult = (CharacterFrame)getPartFrame.Invoke(assembler, new object[] { standardMount, "swim", 0 })!;

            var passengerStandResult = (CharacterFrame)getPartFrame.Invoke(assembler, new object[] { passengerMount, "stand1", 0 })!;
            var passengerWalkResult = (CharacterFrame)getPartFrame.Invoke(assembler, new object[] { passengerMount, "walk1", 0 })!;
            var passengerAttackResult = (CharacterFrame)getPartFrame.Invoke(assembler, new object[] { passengerMount, "swingO1", 0 })!;

            Assert.Same(standFrame, standResult);
            Assert.Same(walkFrame, walkResult);
            Assert.Same(flyFrame, flyResult);
            Assert.Same(sitFrame, passengerStandResult);
            Assert.Same(sitFrame, passengerWalkResult);
            Assert.Same(sitFrame, passengerAttackResult);
        }

        [Theory]
        [InlineData(35121005, "tank_pre", "tank_stand", "tank_walk", "tank")]
        [InlineData(35111004, "siege_pre", "siege_stand", "siege_stand", "siege")]
        [InlineData(35121013, "tank_siegepre", "tank_siegestand", "tank_siegestand", "tank_siegeattack")]
        public void PlayerCharacter_SkillAvatarTransforms_MapMechanicActionFamilies(
            int skillId,
            string actionName,
            string expectedStandAction,
            string expectedWalkAction,
            string expectedAttackAction)
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            var foothold = CreateFoothold(0, 100, 200, 100);

            player.SetPosition(100, 100);
            player.Physics.LandOnFoothold(foothold);
            player.SetFootholdLookup(CreateFootholdLookup(foothold));

            Assert.True(player.ApplySkillAvatarTransform(skillId, actionName));

            player.SetInput(left: false, right: false, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Update(1000, 0.016f);
            Assert.Equal(PlayerState.Standing, player.State);
            Assert.Equal(expectedStandAction, player.CurrentActionName);

            player.SetInput(left: false, right: true, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Update(1200, 0.016f);
            Assert.Equal(PlayerState.Walking, player.State);
            Assert.Equal(expectedWalkAction, player.CurrentActionName);

            player.SetInput(left: false, right: false, up: false, down: false, jump: false, attack: true, pickup: false);
            player.Update(1400, 0.016f);
            Assert.Equal(PlayerState.Attacking, player.State);
            Assert.Equal(expectedAttackAction, player.CurrentActionName);
        }

        [Fact]
        public void SkillManager_MechanicTransforms_ClearOnMapResetAndBuffExpiry()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            var foothold = CreateFoothold(0, 100, 200, 100);
            var skillManager = new SkillManager(new SkillLoader(skillWz: null, device: null, texturePool: null), player);
            var startCast = typeof(SkillManager).GetMethod("StartCast", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(startCast);

            player.SetPosition(100, 100);
            player.Physics.LandOnFoothold(foothold);
            player.SetFootholdLookup(CreateFootholdLookup(foothold));

            var tankSkill = new SkillData
            {
                SkillId = 35121005,
                MaxLevel = 1,
                ActionName = "tank_pre",
                Levels =
                {
                    [1] = new SkillLevelData { Level = 1 }
                }
            };

            startCast!.Invoke(skillManager, new object[] { tankSkill, 1, 1000 });

            player.SetInput(left: false, right: false, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Update(Environment.TickCount + 400, 0.016f);
            Assert.Equal("tank_stand", player.CurrentActionName);

            skillManager.ClearMapState();
            player.Update(Environment.TickCount + 450, 0.016f);
            Assert.Equal("stand1", player.CurrentActionName);

            var siegeSkill = new SkillData
            {
                SkillId = 35111004,
                MaxLevel = 1,
                ActionName = "siege_pre",
                IsBuff = true,
                Levels =
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Time = 1
                    }
                }
            };

            startCast.Invoke(skillManager, new object[] { siegeSkill, 1, 2000 });

            player.SetInput(left: false, right: false, up: false, down: false, jump: false, attack: false, pickup: false);
            player.Update(Environment.TickCount + 400, 0.016f);
            Assert.Equal("siege_stand", player.CurrentActionName);

            skillManager.Update(3201, 0.016f);
            player.Update(Environment.TickCount + 450, 0.016f);
            Assert.Equal("stand1", player.CurrentActionName);
        }

        [Fact]
        public void SkillManager_EventTamingMobBuffs_EquipRidePartAndRestorePreviousMount()
        {
            var build = new CharacterBuild
            {
                Body = new BodyPart(),
                Head = new BodyPart()
            };
            var player = new PlayerCharacter(device: null, texturePool: null, build);
            var skillManager = new SkillManager(new SkillLoader(skillWz: null, device: null, texturePool: null), player);
            var startCast = typeof(SkillManager).GetMethod("StartCast", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(startCast);

            var originalMount = new CharacterPart
            {
                ItemId = 1902000,
                Type = CharacterPartType.TamingMob,
                Slot = EquipSlot.TamingMob
            };
            var eventMount = new CharacterPart
            {
                ItemId = 1932017,
                Type = CharacterPartType.TamingMob,
                Slot = EquipSlot.TamingMob
            };

            build.Equip(originalMount);
            skillManager.SetTamingMobLoader(itemId => itemId == eventMount.ItemId ? eventMount : null);

            var rideSkill = new SkillData
            {
                SkillId = 80001045,
                MaxLevel = 1,
                IsBuff = true,
                Levels =
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Time = 1,
                        ItemConNo = eventMount.ItemId
                    }
                }
            };

            startCast!.Invoke(skillManager, new object[] { rideSkill, 1, 1000 });
            Assert.Same(eventMount, build.Equipment[EquipSlot.TamingMob]);

            skillManager.Clear();
            Assert.Same(originalMount, build.Equipment[EquipSlot.TamingMob]);

            startCast.Invoke(skillManager, new object[] { rideSkill, 1, 2000 });
            Assert.Same(eventMount, build.Equipment[EquipSlot.TamingMob]);

            skillManager.Update(3201, 0.016f);
            Assert.Same(originalMount, build.Equipment[EquipSlot.TamingMob]);
        }

        [Fact]
        public void VerticalMovingHVTiling_AppliesVerticalShiftBeforeDrawing()
        {
            var firstDrawY = int.MinValue;
            var capturedFirstDraw = false;

            var frame = new Mock<IDXObject>();
            frame.SetupGet(x => x.Delay).Returns(100);
            frame.SetupGet(x => x.X).Returns(0);
            frame.SetupGet(x => x.Y).Returns(0);
            frame.SetupGet(x => x.Width).Returns(32);
            frame.SetupGet(x => x.Height).Returns(32);
            frame.SetupProperty(x => x.Tag);
            frame.Setup(x => x.DrawBackground(null, null, null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Microsoft.Xna.Framework.Color>(), false, null))
                .Callback<object, object, object, int, int, Microsoft.Xna.Framework.Color, bool, object>((_, _, _, _, y, _, _, _) =>
                {
                    if (!capturedFirstDraw)
                    {
                        firstDrawY = y;
                        capturedFirstDraw = true;
                    }
                });

            var background = new BackgroundItem(
                _cx: 300,
                _cy: 300,
                _rx: 0,
                _ry: 100,
                _type: BackgroundType.VerticalMovingHVTiling,
                a: 255,
                front: false,
                frame0: frame.Object,
                flip: false,
                screenMode: (int)RenderResolution.Res_All);

            var renderParameters = new RenderParameters(100, 100, 1f, RenderResolution.Res_All);
            int baseY = background.CalculateBackgroundPosY(frame.Object, 0, 0, renderParameters.RenderHeight, renderParameters.RenderObjectScaling);
            int tickCount = Environment.TickCount + 200;

            background.Draw(null, null, null, 0, 0, 0, 0, null, renderParameters, tickCount);

            Assert.True(capturedFirstDraw);
            Assert.True(firstDrawY > baseY + 30, $"Expected moving background to advance before draw. BaseY={baseY}, DrawY={firstDrawY}");
        }
    }
}
