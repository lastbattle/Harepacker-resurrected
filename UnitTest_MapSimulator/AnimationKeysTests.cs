using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;

namespace UnitTest_MapSimulator
{
    public sealed class AnimationKeysTests
    {
        [Fact]
        public void ResolveMobDeathAction_UsesGenericDieBeforeStand()
        {
            var animationSet = new MobAnimationSet();
            animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { new TestDxObject() });
            animationSet.AddAnimation(AnimationKeys.Die, new List<IDXObject> { new TestDxObject() });

            string action = AnimationKeys.ResolveMobDeathAction(animationSet);

            Assert.Equal(AnimationKeys.Die, action);
        }

        [Fact]
        public void ResolveMobHitAction_UsesGenericHitBeforeStand()
        {
            var animationSet = new MobAnimationSet();
            animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { new TestDxObject() });
            animationSet.AddAnimation(AnimationKeys.Hit, new List<IDXObject> { new TestDxObject() });

            string action = AnimationKeys.ResolveMobHitAction(animationSet);

            Assert.Equal(AnimationKeys.Hit, action);
        }

        private sealed class TestDxObject : IDXObject
        {
            public int Delay => 100;
            public int X => 0;
            public int Y => 0;
            public int Width => 1;
            public int Height => 1;
            public object Tag { get; set; } = new object();
            public Texture2D Texture => null!;

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }
        }
    }
}
