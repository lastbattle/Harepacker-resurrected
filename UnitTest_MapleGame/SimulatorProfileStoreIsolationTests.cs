using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Managers;
using System;
using System.IO;
using System.Linq;

namespace UnitTest_MapSimulator;

public sealed class SimulatorProfileStoreIsolationTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "harepacker-simulator-store-profile-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CharacterKeybindingsAndProgressionRemainIsolatedAndReloadable()
    {
        var previewProfile = new TestProfileStorage(Path.Combine(temporaryDirectory, "preview"));
        var standaloneProfile = new TestProfileStorage(Path.Combine(temporaryDirectory, "standalone"));
        var build = new CharacterBuild
        {
            Id = 501,
            Name = "PreviewHero",
            Level = 15
        };

        var previewCharacters = new CharacterConfigManager(profileStorage: previewProfile);
        CharacterPreset preset = previewCharacters.CreatePreset(build, "Preview hero");
        preset.SkillHotkeys[0] = 1001004;
        previewCharacters.SavePreset(preset.Id);

        var reloadedCharacters = new CharacterConfigManager(profileStorage: previewProfile);
        reloadedCharacters.LoadAllPresets();
        CharacterPreset reloadedPreset = reloadedCharacters.GetPreset(preset.Id);
        Assert.NotNull(reloadedPreset);
        Assert.Equal("Preview hero", reloadedPreset.Name);
        Assert.Equal(1001004, reloadedPreset.SkillHotkeys[0]);

        var standaloneCharacters = new CharacterConfigManager(profileStorage: standaloneProfile);
        standaloneCharacters.LoadAllPresets();
        Assert.Empty(standaloneCharacters.Presets);

        var previewQuestAlarms = new QuestAlarmStore(profileStorage: previewProfile);
        previewQuestAlarms.Save(build, new QuestAlarmPersistedState
        {
            AutoRegisterEnabled = false,
            IsOpened = true,
            TrackedQuestIds = new[] { 100, 101 },
            PacketRegisteredQuestIds = new[] { 101 },
            HiddenAutoQuestIds = new[] { 200 }
        });

        QuestAlarmPersistedState reloadedQuestAlarms =
            new QuestAlarmStore(profileStorage: previewProfile).GetState(build);
        Assert.False(reloadedQuestAlarms.AutoRegisterEnabled);
        Assert.True(reloadedQuestAlarms.IsOpened);
        Assert.Equal(new[] { 100, 101 }, reloadedQuestAlarms.TrackedQuestIds);
        Assert.Equal(new[] { 101 }, reloadedQuestAlarms.PacketRegisteredQuestIds);
        Assert.Equal(new[] { 200 }, reloadedQuestAlarms.HiddenAutoQuestIds);

        QuestAlarmPersistedState standaloneQuestAlarms =
            new QuestAlarmStore(profileStorage: standaloneProfile).GetState(build);
        Assert.True(standaloneQuestAlarms.AutoRegisterEnabled);
        Assert.Empty(standaloneQuestAlarms.TrackedQuestIds);

        new ItemMakerProgressionStore(profileStorage: previewProfile)
            .RecordDiscoveredRecipes(build, new[]
            {
                new ItemMakerRecipeProgressionEntry
                {
                    RecipeKey = "preview-recipe",
                    OutputItemId = 1234
                }
            });

        ItemMakerProgressionSnapshot reloadedProgression =
            new ItemMakerProgressionStore(profileStorage: previewProfile).GetSnapshot(build);
        Assert.True(reloadedProgression.IsRecipeDiscovered("preview-recipe"));
        Assert.Single(reloadedProgression.DiscoveredRecipeEntries);
        Assert.Equal(1234, reloadedProgression.DiscoveredRecipeEntries.Single().OutputItemId);

        ItemMakerProgressionSnapshot standaloneProgression =
            new ItemMakerProgressionStore(profileStorage: standaloneProfile).GetSnapshot(build);
        Assert.False(standaloneProgression.IsRecipeDiscovered("preview-recipe"));
        Assert.Empty(standaloneProgression.DiscoveredRecipeEntries);
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private sealed class TestProfileStorage : ISimulatorProfileStorage
    {
        private readonly string root;

        public TestProfileStorage(string root)
        {
            this.root = root;
        }

        public string CharactersDirectory => Path.Combine(root, "Characters");

        public string GetFile(string fileName) => Path.Combine(root, fileName);
    }
}
