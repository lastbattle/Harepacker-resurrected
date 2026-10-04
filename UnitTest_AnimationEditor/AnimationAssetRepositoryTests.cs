using HaCreator.GUI.FrameAnimation;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Reflection;

namespace UnitTest_AnimationEditor;

public class AnimationAssetRepositoryTests
{
    private readonly AnimationAssetRepository _repository = new();

    [Fact]
    public void MonsterDiscoveryFindsRootActionsAndIgnoresInfo()
    {
        WzImage image = new("0100100.img");
        image.AddProperty(new WzSubProperty("info"));
        WzSubProperty move = new("move");
        move.AddProperty(Frame("0", 180));
        move.AddProperty(Frame("1", 180));
        move.AddProperty(new WzIntProperty("zigzag", 1));
        image.AddProperty(move);

        AnimationTrackDescriptor track = Assert.Single(_repository.DiscoverTracks(AnimationAssetKind.Monster, image));

        Assert.Equal("move", track.Path);
        Assert.Equal(2, track.FrameCount);
    }

    [Fact]
    public void ReactorDiscoverySeparatesBaseAndHitAnimations()
    {
        WzImage image = new("2002000.img");
        WzSubProperty state = new("0");
        state.AddProperty(Frame("0", 100));
        WzSubProperty hit = new("hit");
        hit.AddProperty(Frame("0", 150));
        hit.AddProperty(Frame("1", 150));
        state.AddProperty(hit);
        WzSubProperty eventProperty = new("event");
        eventProperty.AddProperty(new WzIntProperty("0", 1));
        state.AddProperty(eventProperty);
        image.AddProperty(state);

        IReadOnlyList<AnimationTrackDescriptor> tracks = _repository.DiscoverTracks(AnimationAssetKind.Reactor, image);

        Assert.Collection(tracks,
            track => { Assert.Equal("0", track.Path); Assert.Equal(1, track.FrameCount); },
            track => { Assert.Equal("0/hit", track.Path); Assert.Equal(2, track.FrameCount); });
    }

    [Fact]
    public void MapObjectDiscoveryUsesOsHierarchy()
    {
        WzImage image = new("acc1.img");
        WzSubProperty l0 = new("grassySoil");
        WzSubProperty l1 = new("nature");
        WzSubProperty l2 = new("19");
        l2.AddProperty(Frame("0", 130));
        l1.AddProperty(l2);
        l0.AddProperty(l1);
        image.AddProperty(l0);

        AnimationTrackDescriptor track = Assert.Single(_repository.DiscoverTracks(AnimationAssetKind.MapObject, image));

        Assert.Equal("grassySoil/nature/19", track.Path);
    }

    [Fact]
    public void BackgroundDiscoveryIncludesStaticAndAnimatedEntries()
    {
        WzImage image = new("grassySoil.img");
        WzSubProperty back = new("back");
        back.AddProperty(Frame("0", 100));
        WzSubProperty ani = new("ani");
        WzSubProperty animation = new("0");
        animation.AddProperty(Frame("0", 100));
        animation.AddProperty(Frame("1", 100));
        ani.AddProperty(animation);
        image.AddProperty(back);
        image.AddProperty(ani);

        IReadOnlyList<AnimationTrackDescriptor> tracks = _repository.DiscoverTracks(AnimationAssetKind.MapBackground, image);

        Assert.Contains(tracks, track => track.Path == "back/0" && track.IsSingleCanvas);
        Assert.Contains(tracks, track => track.Path == "ani/0" && track.FrameCount == 2);
    }

    [Fact]
    public void SkillDiscoveryFindsNestedEffectChannelsAndPreservesContainerMetadata()
    {
        WzImage image = new("433.img");
        WzSubProperty skillRoot = new("skill");
        WzSubProperty skill = new("4331003");
        WzSubProperty repeat = new("repeat");
        repeat.AddProperty(new WzIntProperty("flip", 1));
        repeat.AddProperty(new WzIntProperty("z", -1));
        repeat.AddProperty(Frame("0", 90));
        repeat.AddProperty(Frame("1", 90));
        skill.AddProperty(repeat);
        skillRoot.AddProperty(skill);
        image.AddProperty(skillRoot);

        AnimationTrackDescriptor track = Assert.Single(_repository.DiscoverTracks(AnimationAssetKind.Skill, image));

        Assert.Equal("skill/4331003/repeat", track.Path);
        Assert.Equal(2, track.FrameCount);
    }

    [Fact]
    public void ItemDiscoveryFindsNestedEffectsAndSkipsInfoIcons()
    {
        WzImage image = new("0501.img");
        WzSubProperty item = new("05010000");
        WzSubProperty info = new("info");
        info.AddProperty(Frame("icon", 100));
        item.AddProperty(info);
        WzSubProperty effect = new("effect");
        WzSubProperty animation = new("default");
        animation.AddProperty(Frame("0", 90));
        animation.AddProperty(Frame("1", 90));
        effect.AddProperty(animation);
        item.AddProperty(effect);
        image.AddProperty(item);

        AnimationTrackDescriptor track = Assert.Single(_repository.DiscoverTracks(AnimationAssetKind.Item, image));

        Assert.Equal("05010000/effect/default", track.Path);
        Assert.Equal(2, track.FrameCount);
    }

    [Fact]
    public void EquipmentDiscoveryFindsAnimatedActionsAndStaticCompositePoses()
    {
        WzImage image = new("01002000.img");
        image.AddProperty(new WzSubProperty("info"));
        WzSubProperty defaultPose = new("default");
        defaultPose.AddProperty(Frame("cap", 100));
        image.AddProperty(defaultPose);
        WzSubProperty walk = new("walk1");
        WzSubProperty firstFrame = new("0");
        firstFrame.AddProperty(Frame("cap", 120));
        walk.AddProperty(firstFrame);
        WzSubProperty secondFrame = new("1");
        secondFrame.AddProperty(Frame("cap", 120));
        walk.AddProperty(secondFrame);
        image.AddProperty(walk);

        IReadOnlyList<AnimationTrackDescriptor> tracks = _repository.DiscoverTracks(AnimationAssetKind.Equipment, image);

        Assert.Contains(tracks, track => track.Path == "default" && track.IsSingleCanvas && track.FrameCount == 1);
        Assert.Contains(tracks, track => track.Path == "walk1" && !track.IsSingleCanvas && track.FrameCount == 2);
        Assert.DoesNotContain(tracks, track => track.Path == "info");
    }

    [Fact]
    public void ItemAndEquipmentAssetsRetainTheirWzSubdirectories()
    {
        RecordingDataSource dataSource = new();
        dataSource.ImageNames[("Item", "Cash")] = new[] { "0501" };
        dataSource.ImageNames[("Item", "Pet")] = new[] { "5000000.img" };
        dataSource.ImageNames[("Character", "Cap")] = new[] { "01002000" };
        dataSource.ImageNames[("Character", "Weapon")] = new[] { "01302000.img" };

        WithDataSource(dataSource, () =>
        {
            IReadOnlyList<AnimationAssetDescriptor> items = _repository.GetAssets(AnimationAssetKind.Item);
            IReadOnlyList<AnimationAssetDescriptor> equipment = _repository.GetAssets(AnimationAssetKind.Equipment);

            Assert.Contains(items, asset => asset.LookupName == "Cash/0501.img");
            Assert.Contains(items, asset => asset.LookupName == "Pet/5000000.img");
            Assert.Contains(equipment, asset => asset.LookupName == "Cap/01002000.img");
            Assert.Contains(equipment, asset => asset.LookupName == "Weapon/01302000.img");
        });
    }

    [Fact]
    public void CommitPreservesUnknownSiblingsAndRestoresValidParents()
    {
        WzImage image = new("0100100.img");
        WzSubProperty move = new("move");
        move.AddProperty(new WzStringProperty("before", "keep-before"));
        move.AddProperty(Frame("0", 100));
        move.AddProperty(new WzIntProperty("between", 42));
        move.AddProperty(Frame("1", 200));
        move.AddProperty(new WzIntProperty("99", 9001));
        move.AddProperty(new WzStringProperty("after", "keep-after"));
        image.AddProperty(move);
        image.Changed = false;

        AnimationDocument document = Document(image, move, 2);
        document.Frames.Move(1, 0);
        document.Reindex();

        WzImageProperty saved = _repository.Commit(document);

        Assert.Same(image, saved.Parent);
        Assert.True(image.Changed);
        Assert.Equal(new[] { "before", "0", "1", "between", "99", "after" },
            saved.WzProperties.Select(property => property.Name));
        Assert.Equal("keep-before", ((WzStringProperty)saved["before"]).Value);
        Assert.Equal(42, ((WzIntProperty)saved["between"]).Value);
        Assert.Equal(9001, ((WzIntProperty)saved["99"]).Value);
        Assert.Equal("keep-after", ((WzStringProperty)saved["after"]).Value);
        Assert.All(new[] { saved["0"], saved["1"] }, frame => Assert.Same(saved, frame.Parent));
        Assert.Equal(200, ((WzIntProperty)saved["0"]["delay"]).Value);
        Assert.Equal(100, ((WzIntProperty)saved["1"]["delay"]).Value);
    }

    [Fact]
    public void DetachedWorkingTrackStillLoadsUolFramesFromAttachedSourceSlots()
    {
        WzImage image = new("0100100.img");
        WzSubProperty move = new("move");
        move.AddProperty(Frame("0", 100));
        move.AddProperty(new WzUOLProperty("1", "move/0"));
        image.AddProperty(move);

        AnimationDocument document = Document(image, move, 2);

        Assert.Equal(2, document.Frames.Count);
        Assert.IsType<WzUOLProperty>(document.Frames[1].WorkingFrame);
        Assert.True(document.Frames[1].IsLinked);
        Assert.Single(document.Frames[1].Layers);
    }

    [Fact]
    public void RelativeSiblingUolFramesAreIncludedAndResolved()
    {
        WzImage image = new("5000000.img");
        WzSubProperty stand = new("stand1");
        stand.AddProperty(Frame("0", 100));
        stand.AddProperty(Frame("1", 140));
        stand.AddProperty(new WzUOLProperty("2", "1"));
        image.AddProperty(stand);

        AnimationTrackDescriptor track = Assert.Single(_repository.DiscoverTracks(AnimationAssetKind.Item, image));
        AnimationDocument document = new(
            new AnimationAssetDescriptor { Kind = AnimationAssetKind.Item, Category = "Item", Subdirectory = "Pet", ImageName = image.Name },
            track, "Item", "Pet/5000000.img", image, stand, false);

        Assert.Equal(3, track.FrameCount);
        Assert.Equal(3, document.Frames.Count);
        Assert.Equal(140, document.Frames[2].Delay);
    }

    [Fact]
    public void UolActionTrackOpensAsAnIndependentEditableContainer()
    {
        WzImage image = new("0100100.img");
        WzSubProperty stand = new("stand");
        stand.AddProperty(Frame("0", 100));
        stand.AddProperty(Frame("1", 140));
        image.AddProperty(stand);
        WzUOLProperty move = new("move", "stand");
        image.AddProperty(move);

        AnimationTrackDescriptor track = Assert.Single(_repository.DiscoverTracks(AnimationAssetKind.Monster, image),
            descriptor => descriptor.Path == "move");
        AnimationDocument document = Document(image, move, track.FrameCount);

        Assert.Equal(2, document.Frames.Count);
        Assert.IsType<WzSubProperty>(document.WorkingTrack);
        Assert.All(document.Frames, frame => Assert.False(frame.IsLinked));
    }

    [Fact]
    public void ReorderedUolFrameMaterializesBeforeItsNumericNameChanges()
    {
        WzImage image = new("0100100.img");
        WzSubProperty move = new("move");
        move.AddProperty(Frame("0", 175));
        move.AddProperty(new WzUOLProperty("1", "move/0"));
        image.AddProperty(move);

        AnimationDocument document = Document(image, move, 2);
        document.Frames.Move(1, 0);
        document.Reindex();

        WzImageProperty saved = document.BuildCommittedTrack();

        Assert.IsType<WzCanvasProperty>(saved["0"]);
        Assert.Equal(175, ((WzIntProperty)saved["0"]["delay"]).Value);
        Assert.IsType<WzCanvasProperty>(saved["1"]);
    }

    [Fact]
    public void LayoutChangeMaterializesUolEvenWhenItReturnsToItsOriginalIndex()
    {
        WzImage image = new("0100100.img");
        WzSubProperty move = new("move");
        move.AddProperty(Frame("0", 175));
        move.AddProperty(new WzUOLProperty("1", "move/0"));
        move.AddProperty(Frame("2", 350));
        image.AddProperty(move);

        AnimationDocument document = Document(image, move, 3);
        document.Frames.Move(0, 2);
        document.Frames.Move(0, 1);
        document.Reindex();

        WzImageProperty saved = document.BuildCommittedTrack();

        Assert.Equal(350, ((WzIntProperty)saved["0"]["delay"]).Value);
        Assert.Equal(175, ((WzIntProperty)saved["1"]["delay"]).Value);
        Assert.IsType<WzCanvasProperty>(saved["1"]);
        Assert.Equal(175, ((WzIntProperty)saved["2"]["delay"]).Value);
    }

    [Fact]
    public void MakeIndependentClonesCompositeUolTargetWithoutDroppingLayers()
    {
        WzImage image = new("0100100.img");
        WzSubProperty source = new("source");
        WzSubProperty sourceFrame = new("0");
        sourceFrame.AddProperty(Frame("body", 120));
        sourceFrame.AddProperty(Frame("effect", 120));
        source.AddProperty(sourceFrame);
        image.AddProperty(source);
        WzSubProperty move = new("move");
        move.AddProperty(new WzUOLProperty("0", "source/0"));
        image.AddProperty(move);

        AnimationDocument document = Document(image, move, 1);
        AnimationFrameModel frame = Assert.Single(document.Frames);

        frame.MakeIndependent();

        WzSubProperty independent = Assert.IsType<WzSubProperty>(frame.WorkingFrame);
        Assert.Equal("0", independent.Name);
        Assert.Equal(2, frame.Layers.Count);
        Assert.All(frame.Layers, layer => Assert.False(layer.IsLinked));
    }

    [Fact]
    public void CommitSavesImgSourcesWithTheFullMapSubdirectoryPath()
    {
        WzImage image = new("acc1.img");
        WzSubProperty l0 = new("grassySoil");
        WzSubProperty l1 = new("nature");
        WzSubProperty l2 = new("19");
        l2.AddProperty(Frame("0", 100));
        l1.AddProperty(l2);
        l0.AddProperty(l1);
        image.AddProperty(l0);
        image.Changed = false;
        RecordingDataSource dataSource = new() { SaveResult = true };

        WithDataSource(dataSource, () =>
        {
            AnimationDocument document = new(
                new AnimationAssetDescriptor
                {
                    Kind = AnimationAssetKind.MapObject,
                    Category = "Map",
                    Subdirectory = "Obj",
                    ImageName = image.Name,
                    DisplayName = image.Name
                },
                new AnimationTrackDescriptor { Name = "19", Path = "grassySoil/nature/19", FrameCount = 1 },
                "Map", "Obj/acc1.img", image, l2, false);
            document.Frames[0].SelectedLayer!.Delay = 180;

            _repository.Commit(document);
        });

        Assert.Equal("Map", dataSource.SavedCategory);
        Assert.Equal("Obj/acc1.img", dataSource.SavedRelativePath);
        Assert.True(dataSource.ImageWasChangedWhenSaved);
    }

    [Theory]
    [InlineData(AnimationAssetKind.Item, "Item", "Cash", "0501.img")]
    [InlineData(AnimationAssetKind.Equipment, "Character", "Weapon", "01302000.img")]
    public void CommitSavesItemAndEquipmentToTheirExactSubdirectory(AnimationAssetKind kind,
        string category, string subdirectory, string imageName)
    {
        WzImage image = new(imageName);
        WzSubProperty track = new("stand1");
        track.AddProperty(Frame("0", 100));
        image.AddProperty(track);
        RecordingDataSource dataSource = new() { SaveResult = true };

        WithDataSource(dataSource, () =>
        {
            AnimationDocument document = new(
                new AnimationAssetDescriptor
                {
                    Kind = kind,
                    Category = category,
                    Subdirectory = subdirectory,
                    ImageName = imageName,
                    DisplayName = imageName
                },
                new AnimationTrackDescriptor { Name = "stand1", Path = "stand1", FrameCount = 1 },
                category, $"{subdirectory}/{imageName}", image, track, false);
            document.Frames[0].SelectedLayer!.Delay = 175;

            _repository.Commit(document);
        });

        Assert.Equal(category, dataSource.SavedCategory);
        Assert.Equal($"{subdirectory}/{imageName}", dataSource.SavedRelativePath);
        Assert.True(dataSource.ImageWasChangedWhenSaved);
    }

    [Fact]
    public void FailedPersistenceRollsBackTheOriginalTrackAndImageState()
    {
        WzImage image = new("0100100.img");
        WzSubProperty move = new("move");
        move.AddProperty(Frame("0", 100));
        image.AddProperty(move);
        object originalTag = new();
        image.HCTag = originalTag;
        image.Changed = false;
        RecordingDataSource dataSource = new() { SaveResult = false };
        AnimationDocument document = Document(image, move, 1);
        document.Frames[0].SelectedLayer!.Delay = 180;

        WithDataSource(dataSource, () =>
            Assert.Throws<InvalidOperationException>(() => _repository.Commit(document)));

        Assert.Same(move, image["move"]);
        Assert.Same(image, move.Parent);
        Assert.Same(originalTag, image.HCTag);
        Assert.False(image.Changed);
        Assert.Equal(100, ((WzIntProperty)move["0"]["delay"]).Value);
    }

    private static AnimationDocument Document(WzImage image, WzImageProperty track, int frameCount)
    {
        AnimationAssetDescriptor asset = new()
        {
            Kind = AnimationAssetKind.Monster,
            Category = "Mob",
            ImageName = image.Name,
            DisplayName = image.Name
        };
        AnimationTrackDescriptor descriptor = new()
        {
            Name = track.Name,
            Path = track.Name,
            FrameCount = frameCount
        };
        return new AnimationDocument(asset, descriptor, "Mob", image.Name, image, track, false);
    }

    private static void WithDataSource(IDataSource dataSource, Action action)
    {
        Type programType = typeof(AnimationAssetRepository).Assembly.GetType("HaCreator.Program", throwOnError: true)!;
        FieldInfo field = programType.GetField("DataSource", BindingFlags.Public | BindingFlags.Static)!;
        object? previous = field.GetValue(null);
        try
        {
            field.SetValue(null, dataSource);
            action();
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    private sealed class RecordingDataSource : IDataSource
    {
        public Dictionary<(string category, string subdirectory), IEnumerable<string>> ImageNames { get; } = new();
        public bool SaveResult { get; init; }
        public string? SavedCategory { get; private set; }
        public string? SavedRelativePath { get; private set; }
        public bool ImageWasChangedWhenSaved { get; private set; }
        public string Name => "Recording";
        public bool IsInitialized => true;
        public VersionInfo VersionInfo => null!;
        public WzImage GetImage(string category, string imageName) => null!;
        public WzImage GetImageByPath(string relativePath) => null!;
        public IEnumerable<WzImage> GetImagesInCategory(string category) => Array.Empty<WzImage>();
        public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory) => Array.Empty<WzImage>();
        public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory) =>
            ImageNames.TryGetValue((category, subDirectory), out IEnumerable<string>? names) ? names : Array.Empty<string>();
        public bool ImageExists(string category, string imageName) => false;
        public bool CategoryExists(string category) => false;
        public IEnumerable<string> GetCategories() => Array.Empty<string>();
        public IEnumerable<string> GetSubdirectories(string category) => Array.Empty<string>();
        public WzDirectory GetDirectory(string category) => null!;
        public IEnumerable<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
        public void PreloadCategory(string category) { }
        public void ClearCache() { }
        public DataSourceStats GetStats() => new();
        public bool SaveImage(string category, WzImage image, string relativePath = null!)
        {
            SavedCategory = category;
            SavedRelativePath = relativePath;
            ImageWasChangedWhenSaved = image.Changed;
            return SaveResult;
        }
        public void MarkImageUpdated(string category, WzImage image) { }
        public void Dispose() { }
    }

    private static WzCanvasProperty Frame(string name, int delay)
    {
        WzCanvasProperty canvas = new(name) { PngProperty = new WzPngProperty() };
        canvas.PngProperty.PNG = new System.Drawing.Bitmap(1, 1);
        canvas.AddProperty(new WzVectorProperty("origin", 0, 0));
        canvas.AddProperty(new WzIntProperty("delay", delay));
        return canvas;
    }
}
