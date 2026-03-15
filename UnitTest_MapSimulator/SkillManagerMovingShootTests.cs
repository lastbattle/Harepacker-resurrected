using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using Xunit;
using DrawingPoint = System.Drawing.Point;

namespace UnitTest_MapSimulator
{
    public class SkillManagerMovingShootTests
    {
        [Fact]
        public void ExecuteSkillPayload_CasterMoveProjectilePreservesCastStartPreferredTarget()
        {
            SkillData skill = CreateCasterMoveProjectileSkill();
            SkillManager manager = CreateSkillManager(skill);

            MobItem nearerCastStartMob = CreateMob(80, 0);
            MobItem laterPositionMob = CreateMob(180, 0);
            var pool = new MobPool();
            pool.Initialize(new[] { nearerCastStartMob, laterPositionMob });
            manager.SetMobPool(pool);

            PlayerCharacter player = GetPlayer(manager);
            player.SetPosition(0, 0);
            player.FacingRight = true;

            InvokeExecuteSkillPayload(manager, skill, 1, 1000);

            player.SetPosition(-300, 0);

            manager.Update(1090, 0f);

            ActiveProjectile projectile = GetOnlyProjectile(manager);
            Assert.Equal(nearerCastStartMob.PoolId, projectile.PreferredTargetMobId);
            Assert.NotEqual(laterPositionMob.PoolId, projectile.PreferredTargetMobId);
        }

        [Fact]
        public void ExecuteSkillPayload_CasterMoveProjectilePreservesCastStartFacing()
        {
            SkillData skill = CreateCasterMoveProjectileSkill();
            SkillManager manager = CreateSkillManager(skill);

            PlayerCharacter player = GetPlayer(manager);
            player.SetPosition(0, 0);
            player.FacingRight = true;

            InvokeExecuteSkillPayload(manager, skill, 1, 1000);

            player.FacingRight = false;

            manager.Update(1090, 0f);

            ActiveProjectile projectile = GetOnlyProjectile(manager);
            Assert.True(projectile.FacingRight);
            Assert.True(projectile.VelocityX > 0f);
        }

        private static SkillData CreateCasterMoveProjectileSkill()
        {
            return new SkillData
            {
                SkillId = 5211004,
                MaxLevel = 1,
                IsAttack = true,
                CasterMove = true,
                AttackType = SkillAttackType.Ranged,
                Projectile = new ProjectileData
                {
                    Speed = 400f,
                    LifeTime = 2000f,
                    MaxHits = 1
                },
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 100,
                        AttackCount = 1,
                        MobCount = 1,
                        RangeL = 0,
                        RangeR = 120,
                        RangeTop = -60,
                        RangeBottom = 20
                    }
                }
            };
        }

        private static SkillManager CreateSkillManager(params SkillData[] availableSkills)
        {
            var build = new CharacterBuild
            {
                Attack = 100
            };

            var manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                new PlayerCharacter(device: null, texturePool: null, build));

            typeof(SkillManager)
                .GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(manager, new List<SkillData>(availableSkills));

            return manager;
        }

        private static PlayerCharacter GetPlayer(SkillManager manager)
        {
            return (PlayerCharacter)typeof(SkillManager)
                .GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(manager)!;
        }

        private static void InvokeExecuteSkillPayload(SkillManager manager, SkillData skill, int level, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("ExecuteSkillPayload", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(manager, new object[] { skill, level, currentTime, true, null, true });
        }

        private static ActiveProjectile GetOnlyProjectile(SkillManager manager)
        {
            var projectiles = (System.Collections.IEnumerable)typeof(SkillManager)
                .GetField("_projectiles", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(manager)!;

            foreach (object projectile in projectiles)
            {
                return Assert.IsType<ActiveProjectile>(projectile);
            }

            throw new Xunit.Sdk.XunitException("Expected a projectile to be spawned.");
        }

        private static MobItem CreateMob(float x, float y)
        {
            var mobInfo = new MobInfo(new Bitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData { MaxHP = 100, Level = 1 });

            var mobInstance = new MobInstance(
                mobInfo,
                board: null,
                x: (int)x,
                y: (int)y,
                rx0Shift: 0,
                rx1Shift: 0,
                yShift: 0,
                limitedname: null,
                mobTime: null,
                flip: (MapleBool)MapleBool.False,
                hide: (MapleBool)MapleBool.False,
                info: null,
                team: null);

            var animationSet = new MobAnimationSet();
            animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { new TestDxObject(80, 140, 40, 50, 100) });

            var mob = new MobItem(mobInstance, animationSet, null);
            mob.AI.SetState(MobAIState.Idle, 0);
            mob.MovementInfo.X = x;
            mob.MovementInfo.Y = y;
            return mob;
        }

        private sealed class TestDxObject : IDXObject
        {
            public TestDxObject(int x, int y, int width, int height, int delay)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Delay = delay;
            }

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public int Delay { get; }
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
            public object Tag { get; set; } = new();
        }
    }
}
