using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
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
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using DrawingPoint = System.Drawing.Point;

namespace UnitTest_MapSimulator
{
    public class PlayerCombatSpecialMobInteractionTests
    {
        [Fact]
        public void ProcessAttack_SkipsMobsProtectedByDamagedByMobFlag()
        {
            var player = new PlayerCharacter(
                device: null,
                texturePool: null,
                build: new CharacterBuild { Attack = 100 });
            player.SetPosition(100, 100);

            var protectedMob = CreateMob(120, 100, isProtectedFromPlayerDamage: true);
            var normalMob = CreateMob(145, 100);

            var pool = new MobPool();
            pool.Initialize(new[] { protectedMob, normalMob });

            var combat = new PlayerCombat(player);
            List<DamageResult> results = combat.ProcessAttack(
                pool,
                new XnaRectangle(0, -70, 120, 90),
                maxTargets: 3);

            Assert.Single(results);
            Assert.Same(normalMob, results[0].Target);
            Assert.Equal(protectedMob.AI.MaxHp, protectedMob.AI.CurrentHp);
            Assert.True(normalMob.AI.CurrentHp < normalMob.AI.MaxHp);
        }

        [Fact]
        public void TryApplyMobHit_BlindMobStatusForcesMissAndDoesNotDamagePlayer()
        {
            var player = new PlayerCharacter(
                device: null,
                texturePool: null,
                build: new CharacterBuild { MaxHP = 100, HP = 100, Defense = 0 });
            player.SetPosition(100, 100);

            var mob = CreateMob(120, 100);
            mob.AI.ApplyStatusEffect(MobStatusEffect.Blind, durationMs: 4000, currentTick: 2000);

            var combat = new PlayerCombat(player);
            int missCount = 0;
            combat.OnMobAttackMissPlayer = (_, _, _) => missCount++;

            bool applied = combat.TryApplyMobHit(mob, player.GetHitbox(), currentTime: 3000);

            Assert.True(applied);
            Assert.Equal(100, player.HP);
            Assert.Equal(1, missCount);
            Assert.False(combat.IsInvincible(3000));
        }

        [Fact]
        public void GetMobHitChance_DarknessAndAccuracyStatusesAdjustChance()
        {
            var player = new PlayerCharacter(
                device: null,
                texturePool: null,
                build: new CharacterBuild { Avoidability = 80 });
            var mob = CreateMob(120, 100);
            var combat = new PlayerCombat(player);

            MethodInfo method = typeof(PlayerCombat).GetMethod(
                "GetMobHitChance",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            float baseChance = (float)method.Invoke(combat, new object[] { mob.AI })!;

            mob.AI.ApplyStatusEffect(MobStatusEffect.Darkness, durationMs: 5000, currentTick: 0, value: 40);
            float darknessChance = (float)method.Invoke(combat, new object[] { mob.AI })!;

            mob.AI.ApplyStatusEffect(MobStatusEffect.ACC, durationMs: 5000, currentTick: 0, value: 25);
            float recoveredChance = (float)method.Invoke(combat, new object[] { mob.AI })!;

            Assert.True(darknessChance < baseChance);
            Assert.True(recoveredChance > darknessChance);
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
