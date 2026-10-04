using HaSharedLibrary.Configuration;
using System;
using System.IO;

namespace UnitTest_MapSimulator;

public sealed class HarepackerUserDataPersistenceTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "harepacker-settings-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void HaRepackerSettingsStore_RoundTripsAllSettingsDocuments()
    {
        var writer = new HaRepackerSettingsStore(temporaryDirectory);
        writer.UserSettings.Sort = true;
        writer.UserSettings.ImageZoomLevel = 4.25;
        writer.ApplicationSettings.FirstRun = false;
        writer.ApplicationSettings.LastBrowserPath = @"C:\MapleStory";

        Assert.True(writer.Save());

        var reader = new HaRepackerSettingsStore(temporaryDirectory);
        Assert.True(reader.Load());
        Assert.True(reader.UserSettings.Sort);
        Assert.Equal(4.25, reader.UserSettings.ImageZoomLevel);
        Assert.False(reader.ApplicationSettings.FirstRun);
        Assert.Equal(@"C:\MapleStory", reader.ApplicationSettings.LastBrowserPath);
        Assert.Empty(reader.CustomKeys);
    }

    [Fact]
    public void HaRepackerSettingsStore_CreatesTheThreeLegacyCompatibleDocuments()
    {
        var store = new HaRepackerSettingsStore(temporaryDirectory);

        Assert.True(store.Save());

        Assert.True(File.Exists(Path.Combine(temporaryDirectory, "Settings.txt")));
        Assert.True(File.Exists(Path.Combine(temporaryDirectory, "ApplicationSettings.txt")));
        Assert.True(File.Exists(Path.Combine(temporaryDirectory, "CustomKeys.txt")));
    }

    [Fact]
    public void HaRepackerSettingsStore_LoadsHistoricalJsonFieldNames()
    {
        Directory.CreateDirectory(temporaryDirectory);
        File.WriteAllText(
            Path.Combine(temporaryDirectory, "Settings.txt"),
            """{"Sort":true,"LineBreakType":"None","ImageZoomLevel":2.5}""");
        File.WriteAllText(
            Path.Combine(temporaryDirectory, "ApplicationSettings.txt"),
            """{"FirstRun":false,"WindowWidth":1234,"MapleStoryVersion":"BMS"}""");
        File.WriteAllText(Path.Combine(temporaryDirectory, "CustomKeys.txt"), "[]");

        var store = new HaRepackerSettingsStore(temporaryDirectory);

        Assert.True(store.Load());
        Assert.True(store.UserSettings.Sort);
        Assert.Equal(2.5, store.UserSettings.ImageZoomLevel);
        Assert.False(store.ApplicationSettings.FirstRun);
        Assert.Equal(1234, store.ApplicationSettings.Width);
    }

    [Fact]
    public void MapHistoryStore_ImportsLegacyDatabaseAndDeduplicatesEntries()
    {
        string legacyPath = Path.Combine(temporaryDirectory, "legacy", "hacreator.db");
        var legacyStore = new MapHistoryStore(legacyPath);
        legacyStore.Add("100000000 - Henesys");
        legacyStore.Add("100000000 - Henesys");

        string canonicalPath = Path.Combine(temporaryDirectory, "canonical", "map-history.db");
        var canonicalStore = new MapHistoryStore(
            canonicalPath,
            new[] { legacyPath },
            Path.Combine(temporaryDirectory, "map-history-import.complete"));

        Assert.Equal(new[] { "100000000 - Henesys" }, canonicalStore.Load());

        canonicalStore.Add("100000000 - Henesys");
        canonicalStore.Add("101000000 - Ellinia");
        Assert.Equal(
            new[] { "100000000 - Henesys", "101000000 - Ellinia" },
            canonicalStore.Load());
    }

    [Fact]
    public void MapHistoryStore_RemovesAndClearsCanonicalHistory()
    {
        var store = new MapHistoryStore(Path.Combine(temporaryDirectory, "map-history.db"));
        store.Add("100000000 - Henesys");
        store.Add("101000000 - Ellinia");

        store.Remove("100000000 - Henesys");
        Assert.Equal(new[] { "101000000 - Ellinia" }, store.Load());

        store.Clear();
        Assert.Empty(store.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
            Directory.Delete(temporaryDirectory, recursive: true);
    }
}
