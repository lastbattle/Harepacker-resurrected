using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.Simulation;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.Wz;
using HaSharedLibrary.Render;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using System.Reflection;

namespace UnitTest_MapSimulator;

[CollectionDefinition("Runtime producer parity", DisableParallelization = true)]
public sealed class RuntimeProducerParityCollection { }

[Collection("Runtime producer parity")]
public sealed class RuntimeMapProducerParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceBackedFrameAnchorsMatchForSceneryLifeAndReactors(bool flip) => OnBoard(board =>
    {
        using var bitmap = new System.Drawing.Bitmap(12, 16);
        var origin = new System.Drawing.Point(3, 5);
        using var objectImage = new WzImage("test.img");
        using var backgroundImage = new WzImage("sky.img");
        using var mobImage = new WzImage("0100100.img");
        using var npcImage = new WzImage("9000000.img");
        using var reactorImage = new WzImage("0001000.img");
        WzSubProperty objectRoot = AddPreview(objectImage, "building", "house", "0");
        WzSubProperty backgroundRoot = AddPreview(backgroundImage, "ani", "2");
        AddPreview(mobImage, "stand");
        AddPreview(npcImage, "stand");
        AddPreview(reactorImage, "0");
        var source = new Moq.Mock<IRuntimeAssetSource>();
        source.Setup(x => x.FindImage("Map", "Obj/test.img")).Returns(objectImage);
        source.Setup(x => x.FindImage("Map", "Back/sky.img")).Returns(backgroundImage);
        source.Setup(x => x.FindImage("Mob", "0100100.img")).Returns(mobImage);
        source.Setup(x => x.FindImage("Npc", "9000000.img")).Returns(npcImage);
        source.Setup(x => x.FindImage("Reactor", "0001000.img")).Returns(reactorImage);
        board.BoardItems.TileObjs.Add(new ObjectInstance(
            new ObjectInfo(bitmap, origin, "test", "building", "house", "0", objectRoot),
            board.Layers[1], board, 23, 31, 0, 2, null, null, null, null, null, null, null, null,
            null, null, null, flip));
        board.BoardItems.BackBackgrounds.Add(new BackgroundInstance(
            new BackgroundInfo(null, bitmap, origin, "sky", BackgroundInfoType.Animation, "2", backgroundRoot, null),
            board, 23, 31, 1, 0, 0, 0, 0, (BackgroundType)0, 255, false, flip, 0, 0, null, false));
        board.BoardItems.Mobs.Add(new MobInstance(
            new MobInfo(bitmap, origin, "0100100", "Snail", mobImage) { LinkedWzImage = mobImage },
            board, 23, 31, 10, 20, 7, null, null, flip, null, null, null));
        board.BoardItems.NPCs.Add(new NpcInstance(
            new NpcInfo(bitmap, origin, "9000000", npcImage) { LinkedWzImage = npcImage },
            board, 23, 31, 10, 20, 7, null, null, flip, null, null, null));
        board.BoardItems.Reactors.Add(new ReactorInstance(
            new ReactorInfo(bitmap, origin, "0001000", "Reactor", reactorImage) { LinkedWzImage = reactorImage },
            board, 23, 31, 15, "test", flip));
        Compare(board, saver => { saver.SaveLayers(); saver.SaveBackgrounds(); saver.SaveLife(); saver.SaveReactors(); },
            (snapshot, decoded) =>
            {
                var obj = Assert.Single(decoded.Objects);
                var expectedObject = Assert.Single(snapshot.Objects);
                AssertPersistedEqual(expectedObject with { Quests = obj.Quests }, obj);
                Assert.Equal(snapshot.Backgrounds, decoded.Backgrounds);
                var mob = Assert.Single(decoded.Mobs);
                AssertPersistedEqual(Assert.Single(snapshot.Mobs) with { DisplayName = mob.DisplayName }, mob);
                var npc = Assert.Single(decoded.Npcs);
                AssertPersistedEqual(Assert.Single(snapshot.Npcs) with { DisplayName = npc.DisplayName }, npc);
                var reactor = Assert.Single(decoded.Reactors);
                Assert.Equal(Assert.Single(snapshot.Reactors) with { DisplayName = reactor.DisplayName }, reactor);
                Assert.Equal(flip ? 17 : 23, obj.X);
                Assert.Equal(origin, obj.Origin);
            }, source.Object);
    });

    private static WzSubProperty AddPreview(WzImage image, params string[] path)
    {
        WzSubProperty parent = null;
        foreach (string part in path)
        {
            var next = new WzSubProperty(part);
            if (parent == null) image.AddProperty(next); else parent.AddProperty(next);
            parent = next;
        }
        var canvas = new WzCanvasProperty("0") { PngProperty = new WzPngProperty { Width = 12, Height = 16 } };
        canvas.AddProperty(new WzVectorProperty("origin", new WzIntProperty("X", 3), new WzIntProperty("Y", 5)));
        parent!.AddProperty(canvas);
        return parent;
    }

    [Fact]
    public void TileSetMagnificationAndFrameDepthMatchSourceMetadata() => OnBoard(board =>
    {
        using var bitmap = new System.Drawing.Bitmap(12, 16);
        using var tileImage = new WzImage("grass.img");
        var info = new WzSubProperty("info");
        info.AddProperty(new WzIntProperty("mag", 4));
        tileImage.AddProperty(info);
        var variant = new WzSubProperty("enH0");
        var frame = new WzSubProperty("0");
        frame.AddProperty(new WzIntProperty("z", -2));
        variant.AddProperty(frame);
        tileImage.AddProperty(variant);
        var source = new Moq.Mock<IRuntimeAssetSource>();
        source.Setup(x => x.FindImage("Map", "Tile/grass.img")).Returns(tileImage);
        board.BoardItems.TileObjs.Add(new TileInstance(
            new TileInfo(bitmap, default, "grass", "enH0", "0", 4, -2, frame),
            board.Layers[1], board, -30, 40, 0, 2));
        Compare(board, saver => saver.SaveLayers(), (snapshot, decoded) =>
            Assert.Equal(Assert.Single(snapshot.Tiles), Assert.Single(decoded.Tiles)), source.Object);
    });

    [Fact]
    public void LayeredObjectAndTilePersistedFieldsMatch() => OnBoard(board =>
    {
        using var bitmap = new System.Drawing.Bitmap(12, 16);
        var objInfo = new ObjectInfo(bitmap, default, "test", "building", "house", "0", null);
        var obj = new ObjectInstance(objInfo, board.Layers[2], board, 23, -41, 7, 3,
            true, false, true, true, 11, -12, 13, 14, "house", "tagA,tagB",
            new() { new ObjectInstanceQuest(1001, (QuestStateType)1) }, false, true);
        var tile = new TileInstance(new TileInfo(bitmap, default, "grass", "enH0", "0", 1, 0, null),
            board.Layers[2], board, -33, 45, 0, 3);
        board.BoardItems.TileObjs.Add(obj);
        board.BoardItems.TileObjs.Add(tile);
        Compare(board, saver => saver.SaveLayers(), (snapshot, decoded) =>
        {
            var expectedObject = Assert.Single(snapshot.Objects);
            var actualObject = Assert.Single(decoded.Objects);
            Assert.Equal(expectedObject.Quests, actualObject.Quests);
            // In-memory bitmap overrides and durable WZ keys intentionally differ.
            AssertPersistedEqual(expectedObject with { Asset = actualObject.Asset, Quests = actualObject.Quests }, actualObject);
            var expectedTile = Assert.Single(snapshot.Tiles);
            var actualTile = Assert.Single(decoded.Tiles);
            Assert.Equal(expectedTile with { Asset = actualTile.Asset }, actualTile);
            Assert.Equal(new RuntimeAssetKey("Map/Tile", "grass.img/enH0/0"), actualTile.Asset);
            Assert.Equal(new RuntimeAssetKey("Map/Obj", "test.img/building/house/0"), actualObject.Asset);
        });
    });

    [Fact]
    public void FrontAndBackBackgroundPersistedFieldsMatch() => OnBoard(board =>
    {
        using var bitmap = new System.Drawing.Bitmap(12, 16);
        var info = new BackgroundInfo(null, bitmap, default, "sky", BackgroundInfoType.Animation, "2", null, null);
        board.BoardItems.BackBackgrounds.Add(new BackgroundInstance(info, board, -31, 47, 1,
            20, -30, 80, 90, (BackgroundType)2, 180, false, false, 2, 1, null, false));
        board.BoardItems.FrontBackgrounds.Add(new BackgroundInstance(info, board, 51, -17, 2,
            -40, 50, 60, 70, (BackgroundType)1, 220, true, false, 3, 2, null, false));
        Compare(board, saver => saver.SaveBackgrounds(), (snapshot, decoded) =>
        {
            Assert.Equal(2, decoded.Backgrounds.Count);
            for (int i = 0; i < 2; i++)
                Assert.Equal(snapshot.Backgrounds[i] with { Asset = decoded.Backgrounds[i].Asset }, decoded.Backgrounds[i]);
            Assert.Equal(new RuntimeAssetKey("Map/Back", "sky.img/ani/2"), decoded.Backgrounds[0].Asset);
        });
    });

    [Fact]
    public void MobAndNpcMovementAndSpawnFieldsMatch() => OnBoard(board =>
    {
        using var bitmap = new System.Drawing.Bitmap(12, 16);
        using var mobImage = new WzImage("0100100.img");
        using var npcImage = new WzImage("9000000.img");
        var mob = new MobInfo(bitmap, default, "0100100", "Snail", mobImage) { LinkedWzImage = mobImage };
        var npc = new NpcInfo(bitmap, default, "9000000", npcImage) { LinkedWzImage = npcImage };
        board.BoardItems.Mobs.Add(new MobInstance(mob, board, 20, 30, 11, 19, 7, "limited", 30, false, true, 4, 2));
        board.BoardItems.NPCs.Add(new NpcInstance(npc, board, -20, -30, 15, 25, 9, "guide", null, false, false, 7, null));
        Compare(board, saver => saver.SaveLife(), (snapshot, decoded) =>
        {
            // Localized names are catalog metadata, not fields in the saved map IMG.
            var expectedMob = Assert.Single(snapshot.Mobs);
            var actualMob = Assert.Single(decoded.Mobs);
            AssertPersistedEqual(expectedMob with { DisplayName = actualMob.DisplayName }, actualMob);
            var expectedNpc = Assert.Single(snapshot.Npcs);
            var actualNpc = Assert.Single(decoded.Npcs);
            AssertPersistedEqual(expectedNpc with { DisplayName = actualNpc.DisplayName }, actualNpc);
        });
    });

    [Fact]
    public void PortalRoutingAndImpactFieldsMatch() => OnBoard(board =>
    {
        var type = (PortalType)0;
        var mapping = HaCreator.Program.InfoManager.PortalIdByType;
        bool existed = mapping.TryGetValue(type, out int previous);
        mapping[type] = 0;
        try
        {
            board.BoardItems.Portals.Add(new PortalInstance(null, board, 17, -31, "sp", type, "out", 100000000,
                "script", 31, true, true, -140, 210, "default", 55, 66, "reactor", "quest", "done"));
            Compare(board, saver => saver.SavePortals(), (snapshot, decoded) =>
            {
                var expected = Assert.Single(snapshot.Portals);
                var actual = Assert.Single(decoded.Portals);
                AssertPersistedEqual(expected with { Asset = actual.Asset }, actual);
            });
        }
        finally { if (existed) mapping[type] = previous; else mapping.Remove(type); }
    });

    [Fact]
    public void FootholdGeometryAndMirrorReflectionFieldsMatch() => OnBoard(board =>
    {
        var a = new FootholdAnchor(board, -70, 50, 2, 3, true);
        var b = new FootholdAnchor(board, 70, 60, 2, 3, true);
        board.BoardItems.FootholdLines.Add(new FootholdLine(board, a, b, true, false, 7, 12));
        board.BoardItems.MirrorFieldDatas.Add(new MirrorFieldData(board, new Rectangle(-11, 23, 80, 40),
            new Vector2(5, -9), new ReflectionDrawableBoundary(70, 160, "water", true, true), MirrorFieldDataType.mob));
        Compare(board, saver => { saver.SaveFootholds(); saver.SaveMirrorFieldData(); }, (snapshot, decoded) =>
        {
            var expected = Assert.Single(snapshot.Footholds);
            var actual = Assert.Single(decoded.Footholds);
            // Anchor identity exists only while editing; saved WZ uses numeric prev/next links.
            AssertPersistedEqual(expected with { FirstEndpointId = 0, SecondEndpointId = 0 }, actual);
            Assert.Equal(snapshot.MirrorFields, decoded.MirrorFields);
        });
    });

    private static void Compare(Board board, Action<MapSaver> serialize, Action<RuntimeMapDefinition, RuntimeMapDefinition> assert,
        IRuntimeAssetSource source = null)
    {
        var saver = new MapSaver(board);
        typeof(MapSaver).GetMethod("CreateImage", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(saver, null);
        using WzImage image = saver.MapImage;
        // These serializer stages operate only on this disposable fixture. Never call InsertImage.
        serialize(saver);
        var info = new WzSubProperty("info");
        info.AddProperty(new WzIntProperty("VRLeft", -100));
        info.AddProperty(new WzIntProperty("VRRight", 100));
        info.AddProperty(new WzIntProperty("VRTop", -50));
        info.AddProperty(new WzIntProperty("VRBottom", 50));
        image.AddProperty(info);
        using var snapshot = new BoardSnapshotBuilder(_ => false).Create(board);
        using var decoded = new WzRuntimeMapReader().Read(image,
            new WzRuntimeMapReaderOptions { MapId = board.MapInfo.id, AssetSource = source });
        assert(snapshot, decoded);
    }

    private static void AssertPersistedEqual<T>(T expected, T actual)
    {
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object left = property.GetValue(expected);
            object right = property.GetValue(actual);
            // MapSaver deliberately omits explicit false optional booleans. The runtime
            // behavior is equivalent to missing, while true must remain true.
            if (left is MapleBool leftBool && right is MapleBool rightBool)
                Assert.True((bool)leftBool == (bool)rightBool, $"{property.Name}: optional boolean differs");
            else
                Assert.True(Equals(left, right), $"{property.Name}: expected {left}, actual {right}");
        }
    }

    private static void OnBoard(Action<Board> action)
    {
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var previousInfoManager = HaCreator.Program.InfoManager;
                HaCreator.Program.InfoManager = new WzInformationManager();
                var board = new Board(new Point(200, 100), new Point(100, 50), new MultiBoard(), true,
                    null, ItemTypes.All, ItemTypes.All);
                try
                {
                    board.CreateMapLayers();
                    board.MapInfo.id = 100000001;
                    action(board);
                }
                finally
                {
                    board.Dispose();
                    HaCreator.Program.InfoManager = previousInfoManager;
                }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Producer parity fixture timed out.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
