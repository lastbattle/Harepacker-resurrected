using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;
using System.Reflection;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator
{
    public class MobBodyHitboxTests
    {
        [Fact]
        public void GetBodyHitbox_UsesCurrentFrameWorldBoundsWithMovementOffset()
        {
            var mob = CreateMob(
                flip: false,
                standFrames: new[] { new TestDxObject(80, 140, 40, 50, 100) });

            mob.MovementInfo.X = 130;
            mob.MovementInfo.Y = 225;

            XnaRectangle hitbox = mob.GetBodyHitbox(0);

            Assert.Equal(new XnaRectangle(110, 165, 40, 50), hitbox);
        }

        [Fact]
        public void GetBodyHitbox_FlippedMobUsesFrameZeroAnchorLikeRenderer()
        {
            var mob = CreateMob(
                flip: true,
                standFrames: new[]
                {
                    new TestDxObject(90, 150, 40, 50, 100),
                    new TestDxObject(70, 148, 65, 52, 100)
                });

            mob.MovementInfo.X = 130;
            mob.MovementInfo.Y = 225;
            SetCurrentFrameIndex(mob, 1);

            XnaRectangle hitbox = mob.GetBodyHitbox(0);

            Assert.Equal(new XnaRectangle(120, 173, 65, 52), hitbox);
        }

        private static MobItem CreateMob(bool flip, IReadOnlyList<IDXObject> standFrames, IReadOnlyList<IDXObject>? attackFrames = null)
        {
            var mobInfo = new MobInfo(new DrawingBitmap(1, 1), DrawingPoint.Empty, "100100", "Test Mob", null);
            typeof(MobInfo)
                .GetField("_mobData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mobInfo, new MobData());

            var mobInstance = new MobInstance(
                mobInfo,
                board: null,
                x: 100,
                y: 200,
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
            animationSet.AddAnimation(AnimationKeys.Stand, standFrames.ToList());
            if (attackFrames != null)
            {
                animationSet.AddAnimation("attack1", attackFrames.ToList());
            }

            var mob = new MobItem(mobInstance, animationSet, null);
            mob.MovementInfo.FlipX = flip;
            typeof(BaseDXDrawableItem)
                .GetField("flip", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(mob, flip);
            return mob;
        }

        private static void SetCurrentFrameIndex(MobItem mob, int frameIndex)
        {
            object controller = typeof(MobItem)
                .GetField("_animationController", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(mob)!;

            typeof(AnimationController)
                .GetField("_currentFrameIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, frameIndex);
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
