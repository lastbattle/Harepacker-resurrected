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
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator
{
    public class SkillManagerProjectileExplosionTests
    {
        [Fact]
        public void CheckProjectileCollisions_ExplodingProjectileHitsNearbyMobsWithinRadius()
        {
            var skill = CreateExplodingProjectileSkill();
            var manager = CreateSkillManager(skill);

            var directMob = CreateMob(100, 100);
            var splashMob = CreateMob(145, 100);
            var distantMob = CreateMob(240, 100);

            var pool = new MobPool();
            pool.Initialize(new[] { directMob, splashMob, distantMob });
            manager.SetMobPool(pool);
            XnaRectangle directHitbox = directMob.GetBodyHitbox(1100);

            var projectile = new ActiveProjectile
            {
                Id = 1,
                SkillId = skill.SkillId,
                SkillLevel = 1,
                Data = skill.Projectile,
                LevelData = skill.GetLevel(1),
                X = directHitbox.Center.X,
                Y = directHitbox.Center.Y,
                VelocityX = 300f,
                VelocityY = 0f,
                FacingRight = true,
                SpawnTime = 1000
            };

            InvokeCheckProjectileCollisions(manager, projectile, 1100);

            Assert.True(projectile.IsExploding);
            Assert.True(directMob.AI.CurrentHp < directMob.AI.MaxHp);
            Assert.True(splashMob.AI.CurrentHp < splashMob.AI.MaxHp);
            Assert.Equal(distantMob.AI.MaxHp, distantMob.AI.CurrentHp);
            Assert.Equal(2, projectile.HitCount);
        }

        [Fact]
        public void CheckProjectileCollisions_SkipsMobsProtectedByDamagedByMobFlag()
        {
            var skill = CreateExplodingProjectileSkill();
            var manager = CreateSkillManager(skill);

            var protectedMob = CreateMob(100, 100, isProtectedFromPlayerDamage: true);
            var normalMob = CreateMob(145, 100);

            var pool = new MobPool();
            pool.Initialize(new[] { protectedMob, normalMob });
            manager.SetMobPool(pool);
            XnaRectangle protectedHitbox = protectedMob.GetBodyHitbox(1100);

            var projectile = new ActiveProjectile
            {
                Id = 1,
                SkillId = skill.SkillId,
                SkillLevel = 1,
                Data = skill.Projectile,
                LevelData = skill.GetLevel(1),
                X = protectedHitbox.Center.X,
                Y = protectedHitbox.Center.Y,
                VelocityX = 300f,
                VelocityY = 0f,
                FacingRight = true,
                SpawnTime = 1000
            };

            InvokeCheckProjectileCollisions(manager, projectile, 1100);

            Assert.True(projectile.IsExploding);
            Assert.Equal(protectedMob.AI.MaxHp, protectedMob.AI.CurrentHp);
            Assert.True(normalMob.AI.CurrentHp < normalMob.AI.MaxHp);
            Assert.Equal(1, projectile.HitCount);
        }

        [Fact]
        public void UpdateProjectileBehavior_HomingFlagStillSteersExplodingProjectiles()
        {
            var skill = CreateExplodingProjectileSkill();
            skill.Projectile.Homing = true;
            skill.Projectile.Behavior = ProjectileBehavior.Exploding;

            var manager = CreateSkillManager(skill);
            var targetMob = CreateMob(160, 40);
            var pool = new MobPool();
            pool.Initialize(new[] { targetMob });
            manager.SetMobPool(pool);

            var projectile = new ActiveProjectile
            {
                Id = 1,
                SkillId = skill.SkillId,
                SkillLevel = 1,
                Data = skill.Projectile,
                LevelData = skill.GetLevel(1),
                X = 100,
                Y = 100,
                VelocityX = 300f,
                VelocityY = 0f,
                FacingRight = true,
                SpawnTime = 1000,
                PreferredTargetMobId = targetMob.PoolId
            };

            InvokeUpdateProjectileBehavior(manager, projectile, 1100);

            Assert.True(projectile.VelocityX > 0f);
            Assert.True(projectile.VelocityY < 0f);
        }

        [Fact]
        public void CheckProjectileCollisions_RectBasedOnTargetHitsAdditionalMobsInTargetAnchoredRange()
        {
            var skill = CreateRectBasedProjectileSkill();
            var manager = CreateSkillManager(skill);

            var directMob = CreateMob(100, 100);
            var forwardSplashMob = CreateMob(155, 100);
            var outsideRectMob = CreateMob(240, 100);

            var pool = new MobPool();
            pool.Initialize(new[] { directMob, forwardSplashMob, outsideRectMob });
            manager.SetMobPool(pool);
            XnaRectangle directHitbox = directMob.GetBodyHitbox(1100);

            var projectile = new ActiveProjectile
            {
                Id = 2,
                SkillId = skill.SkillId,
                SkillLevel = 1,
                Data = skill.Projectile,
                LevelData = skill.GetLevel(1),
                X = directHitbox.Center.X,
                Y = directHitbox.Center.Y,
                VelocityX = 300f,
                VelocityY = 0f,
                FacingRight = true,
                SpawnTime = 1000
            };

            InvokeCheckProjectileCollisions(manager, projectile, 1100);

            Assert.True(directMob.AI.CurrentHp < directMob.AI.MaxHp);
            Assert.True(forwardSplashMob.AI.CurrentHp < forwardSplashMob.AI.MaxHp);
            Assert.Equal(outsideRectMob.AI.MaxHp, outsideRectMob.AI.CurrentHp);
            Assert.Equal(2, projectile.HitCount);
        }

        private static SkillData CreateExplodingProjectileSkill()
        {
            return new SkillData
            {
                SkillId = 2221003,
                MaxLevel = 1,
                IsAttack = true,
                AttackType = SkillAttackType.Ranged,
                Projectile = new ProjectileData
                {
                    Behavior = ProjectileBehavior.Exploding,
                    ExplosionRadius = 80f,
                    MaxHits = 2,
                    LifeTime = 2000f
                },
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 100,
                        AttackCount = 1,
                        MobCount = 2
                    }
                }
            };
        }

        private static SkillData CreateRectBasedProjectileSkill()
        {
            return new SkillData
            {
                SkillId = 3001004,
                MaxLevel = 1,
                IsAttack = true,
                AttackType = SkillAttackType.Ranged,
                RectBasedOnTarget = true,
                Projectile = new ProjectileData
                {
                    Behavior = ProjectileBehavior.Straight,
                    MaxHits = 2,
                    LifeTime = 2000f
                },
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 100,
                        AttackCount = 1,
                        MobCount = 2,
                        RangeL = 120,
                        RangeR = 120,
                        RangeTop = -75,
                        RangeBottom = 30
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

        private static MobItem CreateMob(float x, float y, bool isProtectedFromPlayerDamage = false)
        {
            var mobInfo = new MobInfo(new Bitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData { MaxHP = 100, Level = 1, Friendly = isProtectedFromPlayerDamage });

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

        private static void InvokeCheckProjectileCollisions(SkillManager manager, ActiveProjectile projectile, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("CheckProjectileCollisions", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(manager, new object[] { projectile, currentTime });
        }

        private static void InvokeUpdateProjectileBehavior(SkillManager manager, ActiveProjectile projectile, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("UpdateProjectileBehavior", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(manager, new object[] { projectile, currentTime });
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
