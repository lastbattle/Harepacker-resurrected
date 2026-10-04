using HaCreator.Audio;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_AudioEditor;

public sealed class AudioIntegrationTests
{
    [Fact]
    public void MapAudioProjectionPreservesPrimaryAmbientAndBgmSubReferences()
    {
        var map = new MapInfo();
        map.SetPrimaryBgm("Bgm00/Town");
        map.SetAmbientBgm("Field/Ambient", 72);
        var layers = new WzSubProperty("bgmSub");
        layers.AddProperty(new WzStringProperty("layer0", "Bgm00/Town"));
        layers.AddProperty(new WzUOLProperty("layer1", "../Field/Ambient"));
        map.BgmSub = layers;

        var catalog = new FakeCatalog(
            Entry("Sound/Bgm00.img/Town", "Bgm00/Town"),
            Entry("Sound/Field.img/Ambient", "Field/Ambient"),
            Entry("Sound/Field.img/AmbientLink", "Field/AmbientLink"));

        var references = MapAudioCatalogIntegration.GetReferences(map, catalog);

        Assert.Equal("Bgm00/Town", map.PrimaryBgm);
        Assert.Equal("Field/Ambient", map.AmbientBgm);
        Assert.Equal(72, map.AmbientVolume);
        Assert.Contains(references, reference => reference.Role == "PrimaryBgm" && reference.Asset != null);
        Assert.Contains(references, reference => reference.Role == "AmbientBgm" && reference.Asset != null);
        Assert.Contains(references, reference => reference.Role.StartsWith("BgmSub/", StringComparison.Ordinal));
    }

    [Fact]
    public void RemovingAmbientBgmClearsItsVolumeOverride()
    {
        var map = new MapInfo();
        map.SetAmbientBgm("Field/Ambient", 50);
        map.SetAmbientBgm(null);

        Assert.Null(map.AmbientBgm);
        Assert.Null(map.AmbientVolume);
    }

    [Fact]
    public async Task VersionComparisonReportsMetadataCopyConflict()
    {
        var active = new FakeCatalog(Entry("Sound/Bgm00.img/Town", "Bgm00/Town", duration: 1000, payload: 10));
        var comparison = new FakeCatalog(Entry("Sound/Bgm00.img/Town", "Bgm00/Town", duration: 1100, payload: 12));

        var differences = await new AudioVersionComparisonService().CompareAsync(active, comparison);

        var difference = Assert.Single(differences);
        Assert.Equal(AudioAssetDifferenceKind.Changed, difference.Kind);
        Assert.Contains("duration", difference.Differences);
        Assert.Contains("encoded content", difference.Differences);
        Assert.True(difference.CopyConflict);
    }

    private static AudioAssetEntry Entry(string canonicalPath, string originalPath,
        int duration = 1000, long payload = 10)
    {
        var segments = canonicalPath.Split('/');
        return new AudioAssetEntry(new AudioAssetMetadata
        {
            Category = AudioAssetCategory.Bgm,
            ImagePath = segments[1],
            PropertyPath = string.Join('/', segments.Skip(2)),
            OriginalPath = originalPath,
            CanonicalPath = canonicalPath,
            SourceVersion = "test",
            Encoding = "MP3",
            DurationMilliseconds = duration,
            PayloadSize = payload,
        });
    }

    private sealed class FakeCatalog : IAudioAssetCatalog
    {
        private readonly IReadOnlyList<AudioAssetEntry> entries;

        public FakeCatalog(params AudioAssetEntry[] entries) => this.entries = entries;
        public MapleLib.Img.IDataSource DataSource => null;
        public IReadOnlyList<AudioAssetEntry> Entries => entries;
        public IReadOnlyList<AudioAssetMetadata> Warnings => Array.Empty<AudioAssetMetadata>();
        public Task<IReadOnlyList<AudioAssetEntry>> BuildIndexAsync(bool forceRefresh = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries);
        public Task<IReadOnlyList<AudioAssetEntry>> SearchAsync(AudioAssetSearchFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries);
        public AudioAssetEntry Find(string path)
            => entries.FirstOrDefault(entry => string.Equals(entry.CanonicalPath,
                AudioAssetCatalog.NormalizePath(path), StringComparison.OrdinalIgnoreCase));
        public AudioAssetEntry Find(string imagePath, string propertyPath)
            => Find($"Sound/{imagePath}/{propertyPath}");
        public Task<WzBinaryProperty> LoadPropertyAsync(AudioAssetEntry entry,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WzBinaryProperty>(null);
        public void SetFavorite(string path, bool favorite) { }
        public void SetTags(string path, IEnumerable<string> tags) { }
        public void Invalidate() { }
    }
}
