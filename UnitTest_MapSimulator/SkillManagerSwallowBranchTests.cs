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
using Microsoft.Xna.Framework.Graphics;
using Spine;
using Xunit;
using DrawingPoint = System.Drawing.Point;

namespace UnitTest_MapSimulator
{
    public class SkillManagerSwallowBranchTests
    {
        [Fact]
        public void SwallowBranch_ConsumesMobWithSwallowedDeathTypeAndAppliesBuff()
        {
            SkillData skill = CreateSwallowSkill(timeSeconds: 5);
            SkillManager manager = CreateSkillManager(skill);
            MobPool pool = new MobPool();
            MobItem target = CreateMob(120f, 100f);

            pool.Initialize(new[] { target });
            manager.SetMobPool(pool);

            bool handled = InvokeSwallowBranch(manager, skill, currentTime: 1000);

            Assert.True(handled);
            Assert.True(manager.HasBuff(skill.SkillId));
        }

        [Fact]
        public void SwallowBranch_WithoutTargetDoesNotLeaveBuffLatched()
        {
            SkillData skill = CreateSwallowSkill(timeSeconds: 5);
            SkillManager manager = CreateSkillManager(skill);
            MobPool pool = new MobPool();

            pool.Initialize(new MobItem[0]);
            manager.SetMobPool(pool);

            bool handled = InvokeSwallowBranch(manager, skill, currentTime: 1000);

            Assert.True(handled);
            Assert.False(manager.HasBuff(skill.SkillId));
            Assert.Empty(pool.DyingMobs);
        }

        [Fact]
        public void SwallowBranch_ClientTimerBuffExpiryClearsSwallowState()
        {
            SkillData skill = CreateSwallowSkill(timeSeconds: 5);
            SkillManager manager = CreateSkillManager(skill);
            MobPool pool = new MobPool();
            MobItem target = CreateMob(120f, 100f);

            pool.Initialize(new[] { target });
            manager.SetMobPool(pool);

            bool handled = InvokeSwallowBranch(manager, skill, currentTime: 1000);

            Assert.True(handled);
            Assert.NotNull(GetSwallowState(manager));
            Assert.True(manager.HasBuff(skill.SkillId));

            manager.Update(currentTime: 6000, deltaTime: 0f);

            Assert.Null(GetSwallowState(manager));
            Assert.False(manager.HasBuff(skill.SkillId));
        }

        [Fact]
        public void CancelActiveBuff_ClearsSwallowStateImmediately()
        {
            SkillData skill = CreateSwallowSkill(timeSeconds: 5);
            SkillManager manager = CreateSkillManager(skill);
            MobPool pool = new MobPool();
            MobItem target = CreateMob(120f, 100f);

            pool.Initialize(new[] { target });
            manager.SetMobPool(pool);

            bool handled = InvokeSwallowBranch(manager, skill, currentTime: 1000);

            Assert.True(handled);
            Assert.NotNull(GetSwallowState(manager));

            bool canceled = manager.CancelActiveBuff(skill.SkillId);

            Assert.True(canceled);
            Assert.Null(GetSwallowState(manager));
            Assert.False(manager.HasBuff(skill.SkillId));
        }

        [Fact]
        public void SwallowBranch_WildHunterCaptureDefersKillUntilDigestAndAppliesFollowUpBuff()
        {
            SkillData captureSkill = CreateWildHunterCaptureSkill();
            SkillData digestBuffSkill = CreateWildHunterDigestBuffSkill(timeSeconds: 5);
            SkillManager manager = CreateSkillManager(
                new Dictionary<int, int>
                {
                    [captureSkill.SkillId] = 1,
                    [digestBuffSkill.SkillId] = 1
                },
                captureSkill,
                digestBuffSkill);
            MobPool pool = new MobPool();
            MobItem target = CreateMob(120f, 100f);

            pool.Initialize(new[] { target });
            manager.SetMobPool(pool);

            bool handled = InvokeSwallowBranch(manager, captureSkill, currentTime: 1000);

            Assert.True(handled);
            Assert.Single(pool.ActiveMobs);
            Assert.Empty(pool.DyingMobs);
            Assert.True(target.AI.HasStatusEffect(MobStatusEffect.Stun));

            manager.Update(currentTime: 4100, deltaTime: 0f);

            Assert.Empty(pool.ActiveMobs);
            Assert.Single(pool.DyingMobs);
            Assert.Same(target, pool.DyingMobs[0]);
            Assert.Equal(MobDeathType.Swallowed, target.AI.DeathType);
            Assert.True(manager.HasBuff(digestBuffSkill.SkillId));
        }

        private static SkillManager CreateSkillManager(params SkillData[] availableSkills)
        {
            return CreateSkillManager(new Dictionary<int, int>(), availableSkills);
        }

        private static SkillManager CreateSkillManager(IReadOnlyDictionary<int, int> skillLevels, params SkillData[] availableSkills)
        {
            CharacterBuild build = new CharacterBuild
            {
                Attack = 100,
                HP = 1000,
                MP = 1000,
                MaxHP = 1000,
                MaxMP = 1000
            };

            PlayerCharacter player = new PlayerCharacter(device: null, texturePool: null, build);
            player.SetPosition(100f, 100f);

            SkillManager manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                player);

            typeof(SkillManager)
                .GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(manager, new List<SkillData>(availableSkills));

            typeof(SkillManager)
                .GetField("_skillLevels", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(manager, new Dictionary<int, int>(skillLevels));

            return manager;
        }

        private static SkillData CreateSwallowSkill(int timeSeconds)
        {
            return new SkillData
            {
                SkillId = 1121010,
                MaxLevel = 1,
                Name = "Test Swallow",
                ActionName = "swallow",
                IsAttack = true,
                IsBuff = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Time = timeSeconds,
                        RangeL = 120,
                        RangeR = 120,
                        RangeTop = -80,
                        RangeBottom = 40
                    }
                }
            };
        }

        private static SkillData CreateWildHunterCaptureSkill()
        {
            return new SkillData
            {
                SkillId = 33101005,
                MaxLevel = 1,
                Name = "Wild Hunter Capture",
                ActionName = "swallow",
                IsAttack = true,
                IsKeydownSkill = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        RangeL = 120,
                        RangeR = 120,
                        RangeTop = -80,
                        RangeBottom = 40
                    }
                }
            };
        }

        private static SkillData CreateWildHunterDigestBuffSkill(int timeSeconds)
        {
            return new SkillData
            {
                SkillId = 33101006,
                MaxLevel = 1,
                Name = "Wild Hunter Digest",
                ActionName = "swallow",
                IsBuff = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Time = timeSeconds
                    }
                }
            };
        }

        private static bool InvokeSwallowBranch(SkillManager manager, SkillData skill, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("TryExecuteClientSkillBranch", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (bool)method.Invoke(manager, new object[] { skill, 1, currentTime })!;
        }

        private static object GetSwallowState(SkillManager manager)
        {
            return typeof(SkillManager)
                .GetField("_swallowState", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(manager);
        }

        private static MobItem CreateMob(float x, float y)
        {
            MobInfo mobInfo = new MobInfo(new Bitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData { MaxHP = 100, Level = 1 });

            MobInstance mobInstance = new MobInstance(
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

            MobAnimationSet animationSet = new MobAnimationSet();
            animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { new TestDxObject(80, 140, 40, 50, 100) });

            MobItem mob = new MobItem(mobInstance, animationSet, null);
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

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, Microsoft.Xna.Framework.GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, Microsoft.Xna.Framework.GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
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
