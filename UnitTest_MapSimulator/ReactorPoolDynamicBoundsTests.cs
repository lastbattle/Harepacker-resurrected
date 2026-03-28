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
using System.Drawing;

namespace UnitTest_MapSimulator;

public class ReactorPoolDynamicBoundsTests
{
    [Fact]
    public void FindTouchReactorAroundLocalUser_UsesLiveFrameBounds()
    {
        ReactorItem reactor = CreateReactorItem(
            reactorX: 100,
            reactorY: 100,
            reactorType: ReactorType.ActivatedByTouch,
            frameX: 170,
            frameY: 80,
            frameWidth: 20,
            frameHeight: 20);

        var pool = new ReactorPool();
        pool.Initialize(new[] { reactor });

        List<(ReactorItem reactor, int index)> touched = pool.FindTouchReactorAroundLocalUser(
            playerX: 180,
            playerY: 100,
            currentTick: 0);

        Assert.Single(touched);
        Assert.Same(reactor, touched[0].reactor);
    }

    [Fact]
    public void FindItemReactorsAroundLocalUser_UsesLiveFrameBounds()
    {
        ReactorItem reactor = CreateReactorItem(
            reactorX: 100,
            reactorY: 100,
            reactorType: ReactorType.ActivatedbyItem,
            frameX: 170,
            frameY: 80,
            frameWidth: 20,
            frameHeight: 20,
            requiredItemId: 4001000);

        var pool = new ReactorPool();
        pool.Initialize(new[] { reactor });

        List<(ReactorItem reactor, int index)> touched = pool.FindItemReactorsAroundLocalUser(
            playerX: 180,
            playerY: 100,
            itemId: 4001000,
            currentTick: 0);

        Assert.Single(touched);
        Assert.Same(reactor, touched[0].reactor);
    }

    [Fact]
    public void Update_TracksCurrentAnimatedFrameIndex()
    {
        ReactorItem reactor = CreateReactorItem(
            reactorX: 100,
            reactorY: 100,
            reactorType: ReactorType.ActivatedByTouch,
            stateFrames: new Dictionary<int, List<IDXObject>>
            {
                [0] = new List<IDXObject>
                {
                    new FakeDxObject(90, 80, 20, 20, delay: 10),
                    new FakeDxObject(92, 80, 20, 20, delay: 10),
                    new FakeDxObject(94, 80, 20, 20, delay: 10)
                }
            });

        var pool = new ReactorPool();
        pool.Initialize(new[] { reactor });

        pool.Update(currentTick: 25, deltaTime: 0.016f);

        Assert.Equal((0, 2), pool.GetReactorAnimationState(0));
    }

    [Fact]
    public void Update_ActivatedTouchReactor_AutoChainsAuthoredStates()
    {
        ReactorItem reactor = CreateReactorItem(
            reactorX: 100,
            reactorY: 100,
            reactorType: ReactorType.ActivatedByTouch,
            stateFrames: new Dictionary<int, List<IDXObject>>
            {
                [0] = new List<IDXObject> { new FakeDxObject(100, 80, 20, 20, delay: 5) },
                [1] = new List<IDXObject> { new FakeDxObject(102, 80, 20, 20, delay: 5) },
                [2] = new List<IDXObject> { new FakeDxObject(104, 80, 20, 20, delay: 5) }
            });

        var pool = new ReactorPool();
        pool.Initialize(new[] { reactor });

        pool.ActivateReactor(index: 0, playerId: 0, currentTick: 0, activationType: ReactorActivationType.Touch);
        pool.Update(currentTick: 5, deltaTime: 0.016f);

        ReactorRuntimeData runtimeAfterFirstAdvance = pool.GetReactorData(0);
        Assert.Equal(ReactorState.Activated, runtimeAfterFirstAdvance.State);
        Assert.Equal(2, runtimeAfterFirstAdvance.VisualState);

        pool.Update(currentTick: 10, deltaTime: 0.016f);

        ReactorRuntimeData runtimeAfterChain = pool.GetReactorData(0);
        Assert.Equal(ReactorState.Active, runtimeAfterChain.State);
        Assert.Equal(2, runtimeAfterChain.VisualState);
    }

    [Fact]
    public void Update_ActivatedHitReactor_WaitsForFollowUpHit()
    {
        ReactorItem reactor = CreateReactorItem(
            reactorX: 100,
            reactorY: 100,
            reactorType: ReactorType.ActivatedByAnyHit,
            stateFrames: new Dictionary<int, List<IDXObject>>
            {
                [0] = new List<IDXObject> { new FakeDxObject(100, 80, 20, 20, delay: 5) },
                [1] = new List<IDXObject> { new FakeDxObject(102, 80, 20, 20, delay: 5) },
                [2] = new List<IDXObject> { new FakeDxObject(104, 80, 20, 20, delay: 5) }
            });

        var pool = new ReactorPool();
        pool.Initialize(new[] { reactor });

        pool.ActivateReactor(index: 0, playerId: 0, currentTick: 0, activationType: ReactorActivationType.Hit);
        pool.Update(currentTick: 5, deltaTime: 0.016f);

        ReactorRuntimeData runtime = pool.GetReactorData(0);
        Assert.Equal(ReactorState.Active, runtime.State);
        Assert.Equal(1, runtime.VisualState);
    }

    private static ReactorItem CreateReactorItem(
        int reactorX,
        int reactorY,
        ReactorType reactorType,
        int frameX,
        int frameY,
        int frameWidth,
        int frameHeight,
        int? requiredItemId = null)
    {
        var stateFrames = new Dictionary<int, List<IDXObject>>
        {
            [0] = new List<IDXObject> { new FakeDxObject(frameX, frameY, frameWidth, frameHeight) }
        };

        return CreateReactorItem(
            reactorX,
            reactorY,
            reactorType,
            stateFrames,
            requiredItemId);
    }

    private static ReactorItem CreateReactorItem(
        int reactorX,
        int reactorY,
        ReactorType reactorType,
        Dictionary<int, List<IDXObject>> stateFrames,
        int? requiredItemId = null)
    {
        var linkedImage = new WzImage("0100000.img") { Parsed = true };
        var info = new WzSubProperty { Name = "info" };
        info["reactorType"] = new WzIntProperty("reactorType", (int)reactorType);
        if (requiredItemId.HasValue)
        {
            info["itemID"] = new WzIntProperty("itemID", requiredItemId.Value);
        }

        linkedImage["info"] = info;

        var reactorInfo = new ReactorInfo(new Bitmap(10, 10), System.Drawing.Point.Empty, "0100000", "test", parentObject: null)
        {
            LinkedWzImage = linkedImage
        };

        var instance = new ReactorInstance(reactorInfo, board: null, reactorX, reactorY, reactorTime: 0, name: "test", flip: false);
        return new ReactorItem(instance, stateFrames);
    }

    private sealed class FakeDxObject : IDXObject
    {
        public FakeDxObject(int x, int y, int width, int height, int delay = 0)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Delay = delay;
        }

        public int Delay { get; }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public object Tag { get; set; }

        public Texture2D Texture => null!;

        public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }

        public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }
    }
}
