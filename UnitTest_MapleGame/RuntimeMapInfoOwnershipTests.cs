using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.MapStructure;

namespace UnitTest_MapleGame;

public sealed class RuntimeMapInfoOwnershipTests
{
    [Fact]
    public void ImageOwnsAllDetachedMetadataAndRepeatClonesStayIndependent()
    {
        var probes = new List<ProbeProperty>();
        var source = new MapInfo { Image = new WzImage("map.img") };
        source.Image.AddProperty(new ProbeProperty("image", probes));
        source.additionalProps.Add(new ProbeProperty("additional", probes));
        source.additionalNonInfoProps.Add(new ProbeProperty("rootExtension", probes));
        source.unsupportedInfoProperties.Add(new ProbeProperty("unsupported", probes));
        source.audio.BgmSub = new ProbeProperty("bgmSub", probes);
        source.audio.UnknownAudioProperties.Add(new ProbeProperty("audioExtension", probes));
        source.directionInfo = new MapDirectionInfo();
        source.directionInfo.UnknownProperties.Add(new ProbeProperty("directionExtension", probes));
        var directionEvent = new MapDirectionEvent();
        directionEvent.UnknownProperties.Add(new ProbeProperty("eventExtension", probes));
        directionEvent.UnknownEventQueueProperties.Add(new ProbeProperty("1", probes));
        source.directionInfo.Events.Add(directionEvent);
        int sourceCount = probes.Count;

        MapInfo first = RuntimeMapInfoCloner.Clone(source);
        MapInfo second = RuntimeMapInfoCloner.Clone(first);
        Assert.Equal("image", Assert.Single(first.Image.WzProperties).Name);
        Assert.Equal("image", Assert.Single(second.Image.WzProperties).Name);
        first.Image.Dispose();
        first.Image.Dispose();
        Assert.Equal("unsupported", Assert.Single(second.unsupportedInfoProperties).Name);
        second.Image.Dispose();

        Assert.All(probes.Take(sourceCount), probe => Assert.Equal(0, probe.DisposeCount));
        Assert.All(probes.Skip(sourceCount), probe => Assert.Equal(1, probe.DisposeCount));
        source.Image.Dispose();
    }

    [Fact]
    public void DetachedCanvasIsDisposedEvenWhenSourceHasNoImage()
    {
        var source = new MapInfo();
        using var bitmap = new System.Drawing.Bitmap(3, 4);
        var canvas = new WzCanvasProperty("extension") { PngProperty = new WzPngProperty() };
        canvas.PngProperty.PNG = bitmap;
        source.additionalNonInfoProps.Add(canvas);
        MapInfo clone = RuntimeMapInfoCloner.Clone(source);
        var copiedCanvas = Assert.IsType<WzCanvasProperty>(Assert.Single(clone.additionalNonInfoProps));

        clone.Image.Dispose();

        Assert.Null(copiedCanvas.PngProperty);
        Assert.NotNull(canvas.PngProperty);
        canvas.Dispose();
    }

    [Fact]
    public void ParserMetadataCleanupDoesNotDisposeBorrowedImageOrExtensionAliases()
    {
        var probes = new List<ProbeProperty>();
        using var image = new WzImage("map.img");
        var borrowed = new ProbeProperty("borrowed", probes);
        image.AddProperty(borrowed);
        var info = new MapInfo { Image = image };
        info.additionalProps.Add(borrowed);
        var owned = new ProbeProperty("audio", probes);
        info.audio.BgmSub = owned;
        info.audio.UnknownAudioProperties.Add(owned);

        RuntimeMapInfoCloner.DisposeParsedTypedMetadata(info);

        Assert.Equal(1, owned.DisposeCount);
        Assert.Equal(0, borrowed.DisposeCount);
        Assert.Same(borrowed, Assert.Single(image.WzProperties));
    }

    [Fact]
    public void CloneFailureRetiresPreviouslyClonedMetadataWithoutDisposingSource()
    {
        var probes = new List<ProbeProperty>();
        var source = new MapInfo();
        var original = new ProbeProperty("ownedFirst", probes);
        source.additionalProps.Add(original);
        source.additionalNonInfoProps.Add(new ThrowingProperty());

        Assert.Throws<InvalidOperationException>(() => RuntimeMapInfoCloner.Clone(source));

        Assert.Equal(0, original.DisposeCount);
        Assert.Equal(1, probes[1].DisposeCount);
    }

    private sealed class ProbeProperty : WzIntProperty
    {
        private readonly List<ProbeProperty> _probes;
        public int DisposeCount { get; private set; }
        public ProbeProperty(string name, List<ProbeProperty> probes) : base(name, 1)
        {
            _probes = probes;
            probes.Add(this);
        }
        public override WzImageProperty DeepClone() => new ProbeProperty(Name, _probes);
        public override void Dispose() { DisposeCount++; base.Dispose(); }
    }

    private sealed class ThrowingProperty : WzIntProperty
    {
        public override WzImageProperty DeepClone() => throw new InvalidOperationException("clone failed");
    }
}
