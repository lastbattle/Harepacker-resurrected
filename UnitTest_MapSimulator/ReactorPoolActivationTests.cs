using System.Collections.Generic;
using System.Drawing;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using DrawingPoint = System.Drawing.Point;

namespace UnitTest_MapSimulator
{
    public class ReactorPoolActivationTests
    {
        [Fact]
        public void Initialize_ReadsWzActivationTypeForEachReactor()
        {
            var touchReactor = CreateReactorItem("touch", (int)ReactorType.ActivatedByTouch, x: 0, y: 0);
            var skillReactor = CreateReactorItem("skill", (int)ReactorType.ActivatedBySkill, x: 40, y: 0);
            var hitReactor = CreateReactorItem("hit", (int)ReactorType.ActivatedByAnyHit, x: 80, y: 0);

            var pool = new ReactorPool();
            pool.Initialize(new[] { touchReactor, skillReactor, hitReactor });

            Assert.Equal(ReactorActivationType.Touch, pool.GetReactorData(0)?.ActivationType);
            Assert.Equal(ReactorActivationType.Skill, pool.GetReactorData(1)?.ActivationType);
            Assert.Equal(ReactorActivationType.Hit, pool.GetReactorData(2)?.ActivationType);
        }

        [Fact]
        public void FindTouchReactorAroundLocalUser_IgnoresNonTouchReactors()
        {
            var touchReactor = CreateReactorItem("touch", (int)ReactorType.ActivatedByTouch, x: 0, y: 0);
            var skillReactor = CreateReactorItem("skill", (int)ReactorType.ActivatedBySkill, x: 0, y: 0);
            var hitReactor = CreateReactorItem("hit", (int)ReactorType.ActivatedByAnyHit, x: 0, y: 0);

            var pool = new ReactorPool();
            pool.Initialize(new[] { touchReactor, skillReactor, hitReactor });

            List<(ReactorItem reactor, int index)> touched = pool.FindTouchReactorAroundLocalUser(0, 0);

            Assert.Single(touched);
            Assert.Same(touchReactor, touched[0].reactor);
            Assert.Equal(0, touched[0].index);
        }

        [Fact]
        public void TriggerSkillReactors_ActivatesSkillAndHitReactorsOnly()
        {
            var touchReactor = CreateReactorItem("touch", (int)ReactorType.ActivatedByTouch, x: 0, y: 0);
            var skillReactor = CreateReactorItem("skill", (int)ReactorType.ActivatedBySkill, x: 0, y: 0);
            var hitReactor = CreateReactorItem("hit", (int)ReactorType.ActivatedByAnyHit, x: 0, y: 0);

            var pool = new ReactorPool();
            pool.Initialize(new[] { touchReactor, skillReactor, hitReactor });

            List<ReactorItem> triggered = pool.TriggerSkillReactors(0, 0, skillRange: 30, playerId: 7, currentTick: 1000);

            Assert.Equal(2, triggered.Count);
            Assert.Contains(skillReactor, triggered);
            Assert.Contains(hitReactor, triggered);
            Assert.DoesNotContain(touchReactor, triggered);
            Assert.Equal(ReactorState.Idle, pool.GetReactorData(0)?.State);
            Assert.Equal(ReactorState.Activated, pool.GetReactorData(1)?.State);
            Assert.Equal(ReactorState.Activated, pool.GetReactorData(2)?.State);
        }

        private static ReactorItem CreateReactorItem(string id, int reactorType, int x, int y)
        {
            var reactorInfo = new ReactorInfo(new Bitmap(1, 1), DrawingPoint.Empty, id, id, parentObject: null)
            {
                LinkedWzImage = CreateReactorImage(reactorType)
            };

            var instance = new ReactorInstance(
                reactorInfo,
                board: null,
                x: x,
                y: y,
                reactorTime: 0,
                name: id,
                flip: false);

            return new ReactorItem(instance, new List<IDXObject> { new TestDxObject(width: 40, height: 40) });
        }

        private static WzImage CreateReactorImage(int reactorType)
        {
            var image = new WzImage("0000000.img");
            var info = new WzSubProperty("info");
            info.AddProperty(new WzIntProperty("reactorType", reactorType));
            image.AddProperty(info);
            return image;
        }

        private sealed class TestDxObject : IDXObject
        {
            public TestDxObject(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Delay => 100;
            public int X => 0;
            public int Y => 0;
            public int Width { get; }
            public int Height { get; }
            public object Tag { get; set; } = new();

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }
        }
    }
}
