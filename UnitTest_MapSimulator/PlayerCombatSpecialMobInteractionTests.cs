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
