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
    public class SkillManagerMobStatusTests
    {
        [Fact]
        public void ExecuteSkillPayload_PoisonSkillAppliesMobDotStatus()
        {
            var skill = new SkillData
            {
                SkillId = 2111003,
                Name = "Poison Mist",
                IsAttack = true,
                Type = SkillType.Debuff,
                AttackType = SkillAttackType.Magic,
                Element = SkillElement.Poison,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 100,
                        AttackCount = 1,
                        MobCount = 1,
                        Time = 4,
                        X = 7,
                        Prop = 100,
                        Range = 120,
                        RangeL = 60,
                        RangeR = 60,
                        RangeTop = -60,
                        RangeBottom = 40,
                        RangeY = 100
                    }
                }
            };

            var manager = CreateSkillManager(skill);
            var mob = CreateMob(140, 100);
            var pool = new MobPool();
            pool.Initialize(new[] { mob });
            manager.SetMobPool(pool);

            InvokeExecuteSkillPayload(manager, skill, level: 1, currentTime: 1000);

            Assert.True(mob.AI.HasStatusEffect(MobStatusEffect.Poison));
            Assert.True(mob.AI.StatusEntries.TryGetValue(MobStatusEffect.Poison, out MobStatusEntry poisonEntry));
            Assert.Equal(7, poisonEntry.Value);
            Assert.Equal(5000, poisonEntry.ExpirationTime);
        }

        [Fact]
        public void ExecuteSkillPayload_SlowSkillAppliesWebStatusUsingSpeedMagnitude()
        {
            var skill = new SkillData
            {
                SkillId = 2211004,
                Name = "Slow",
                IsAttack = true,
                Type = SkillType.Debuff,
                AttackType = SkillAttackType.Magic,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 100,
                        AttackCount = 1,
                        MobCount = 1,
                        Time = 5,
                        Speed = 35,
                        Prop = 100,
                        Range = 120,
                        RangeL = 60,
                        RangeR = 60,
                        RangeTop = -60,
                        RangeBottom = 40,
                        RangeY = 100
                    }
                }
            };

            var manager = CreateSkillManager(skill);
            var mob = CreateMob(140, 100);
            var pool = new MobPool();
            pool.Initialize(new[] { mob });
            manager.SetMobPool(pool);

            InvokeExecuteSkillPayload(manager, skill, level: 1, currentTime: 2000);

            Assert.True(mob.AI.HasStatusEffect(MobStatusEffect.Web));
            Assert.True(mob.AI.StatusEntries.TryGetValue(MobStatusEffect.Web, out MobStatusEntry webEntry));
            Assert.Equal(35, webEntry.Value);
            Assert.Equal(7000, webEntry.ExpirationTime);
        }

        private static SkillManager CreateSkillManager(params SkillData[] availableSkills)
        {
            var build = new CharacterBuild
            {
                Attack = 100,
                MagicAttack = 120
            };

            var player = new PlayerCharacter(device: null, texturePool: null, build);
            player.SetPosition(100, 100);

            var manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                player);

            typeof(SkillManager)
                .GetField("_availableSkills", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(manager, new List<SkillData>(availableSkills));

            return manager;
        }

        private static void InvokeExecuteSkillPayload(SkillManager manager, SkillData skill, int level, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("ExecuteSkillPayload", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(manager, new object[] { skill, level, currentTime, true, null, true });
        }

        private static MobItem CreateMob(float x, float y)
        {
            var mobInfo = new MobInfo(new Bitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData { MaxHP = 1000, Level = 1 });

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
