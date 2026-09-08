using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Configuration;
using System;
using System.IO;
using System.Linq;

namespace UnitTest_MapSimulator;

public sealed class SimulatorProfileStorageTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "harepacker-simulator-profile-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void InjectedProfilesRemainIsolatedAcrossStoreInstances()
    {
        var previewProfile = new TestProfileStorage(Path.Combine(temporaryDirectory, "preview"));
        var standaloneProfile = new TestProfileStorage(Path.Combine(temporaryDirectory, "standalone"));
        var build = new CharacterBuild { Id = 100, Name = "PreviewCharacter" };

        var previewStore = new SkillMacroStore(profileStorage: previewProfile);
        previewStore.Save(build, new[]
        {
            new SkillMacro { Name = "Preview macro", SkillIds = new[] { 1001004 } }
        });

        var standaloneStore = new SkillMacroStore(profileStorage: standaloneProfile);
        Assert.Empty(standaloneStore.GetMacros(build));

        var reloadedPreviewStore = new SkillMacroStore(profileStorage: previewProfile);
        SkillMacro[] macros = reloadedPreviewStore.GetMacros(build).ToArray();
        Assert.Single(macros);
        Assert.Equal("Preview macro", macros[0].Name);
        Assert.NotEqual(previewProfile.GetFile("skill-macros.json"), standaloneProfile.GetFile("skill-macros.json"));
    }

    [Fact]
    public void BuiltInProfilesUseSeparateApplicationNames()
    {
        SimulatorProfileStorage previewProfile = SimulatorProfileStorage.CreateHaCreatorPreview();
        SimulatorProfileStorage standaloneProfile = SimulatorProfileStorage.CreateStandaloneClient();

        Assert.Equal(UserDataPaths.HaCreator, previewProfile.Application);
        Assert.Equal(UserDataPaths.MapleGameClient, standaloneProfile.Application);
        Assert.NotEqual(previewProfile.Application, standaloneProfile.Application);
    }

    [Fact]
    public void ExplicitStorePathOverridesInjectedProfile()
    {
        var profile = new TestProfileStorage(Path.Combine(temporaryDirectory, "profile"));
        string explicitPath = Path.Combine(temporaryDirectory, "explicit", "skill-macros.json");
        var build = new CharacterBuild { Id = 200, Name = "ExplicitCharacter" };

        var store = new SkillMacroStore(explicitPath, profile);
        store.Save(build, new[]
        {
            new SkillMacro { Name = "Explicit macro", SkillIds = new[] { 2001002 } }
        });

        Assert.True(File.Exists(explicitPath));
        Assert.False(File.Exists(profile.GetFile("skill-macros.json")));
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
        private readonly string _root;

        public TestProfileStorage(string root)
        {
            _root = root;
        }

        public string CharactersDirectory => Path.Combine(_root, "Characters");

        public string GetFile(string fileName) => Path.Combine(_root, fileName);
    }
}
