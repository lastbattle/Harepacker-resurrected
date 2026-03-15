using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Combat;
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
    public class MobAttackSystemTests
    {
        [Fact]
        public void Update_DirectAttackDamagesTargetedMobBranch()
        {
            var sourceMob = CreateMob(100, 100);
            var targetMob = CreateMob(128, 100, friendly: true);

            var pool = new MobPool();
            pool.Initialize(new[] { sourceMob, targetMob });

            ForceCurrentAttack(sourceMob.AI, 0, currentTick: 0);
            sourceMob.AI.ForceAggro(targetMob.CurrentX, targetMob.CurrentY, 0, targetMob.PoolId, MobTargetType.Mob);

            var attackSystem = new MobAttackSystem();
            attackSystem.SetMobTargeting(pool.GetMob);

            attackSystem.QueueMobAttackActions(sourceMob, 0, playerX: null, playerY: null);
            attackSystem.Update(250, 0.25f, playerManager: null, animationEffects: null, onBossGroundImpact: null);

            Assert.True(targetMob.AI.CurrentHp < targetMob.AI.MaxHp);
            Assert.Equal(MobTargetType.Mob, sourceMob.AI.Target.TargetType);
        }

        [Fact]
        public void Update_CanAttackExternalMobTargetWhenPlayerTargetingDisabled()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 100, level: 10, exp: 50, isBoss: false, isUndead: false, autoAggro: false);
            ai.AddAttack(1, "attack1", damage: 10, range: 60, cooldown: 1000);
            ai.ConfigureSpecialBehavior(canTargetPlayer: false, isEscortMob: false);
            ai.SetState(MobAIState.Chase, 0);
            ai.ForceAggro(120, 0, 0, targetId: 7, targetType: MobTargetType.Mob);

            ai.Update(600, 80, 0, null, null);

            Assert.Equal(MobAIState.Attack, ai.State);
            Assert.Equal(MobTargetType.Mob, ai.Target.TargetType);
            Assert.Equal(7, ai.Target.TargetId);
        }

        private static void ForceCurrentAttack(MobAI ai, int attackIndex, int currentTick)
        {
            typeof(MobAI)
                .GetField("_currentAttackIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(ai, attackIndex);
            ai.SetState(MobAIState.Attack, currentTick);
        }

        private static MobItem CreateMob(float x, float y, bool friendly = false)
        {
            var mobInfo = new MobInfo(new Bitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData { MaxHP = 100, Level = 1, Friendly = friendly, PADamage = 20 });

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
            animationSet.AddAnimation("attack1", new List<IDXObject> { new TestDxObject(80, 140, 40, 50, 100) });

            var mob = new MobItem(mobInstance, animationSet, null);
            mob.AI.SetState(MobAIState.Idle, 0);
            mob.MovementInfo.X = x;
            mob.MovementInfo.Y = y;
            mob.MovementInfo.FlipX = true;
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
