using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class SimulatorStorageRuntimeTests : IDisposable
{
    private readonly string _storageFilePath;

    public SimulatorStorageRuntimeTests()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "HaCreator-StorageRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        _storageFilePath = Path.Combine(directoryPath, "storage-accounts.json");
    }

    [Fact]
    public void ConfigureAccess_BootstrapsAuthorizedRoster_AndPersistsIt()
    {
        StorageAccountStore store = new(_storageFilePath);
        SimulatorStorageRuntime runtime = new(store, "Simulator Account Storage (World 1)");

        runtime.ConfigureAccess(
            "Simulator Account Storage (World 1)",
            "Alice",
            new[] { "Alice", "Bob" });

        Assert.True(runtime.CanCurrentCharacterAccess);
        Assert.Equal(new[] { "Alice", "Bob" }, runtime.AuthorizedCharacterNames);

        StorageAccountStore.StorageAccountState persisted = store.GetState("Simulator Account Storage (World 1)");
        Assert.NotNull(persisted);
        Assert.Equal(new[] { "Alice", "Bob" }, persisted.AuthorizedCharacterNames);
    }

    [Fact]
    public void ConfigureAccess_DeniesCharactersOutsidePersistedAuthorizedRoster()
    {
        StorageAccountStore store = new(_storageFilePath);
        SimulatorStorageRuntime ownerRuntime = new(store, "Simulator Account Storage (World 1)");
        ownerRuntime.ConfigureAccess(
            "Simulator Account Storage (World 1)",
            "Alice",
            new[] { "Alice", "Bob" });

        SimulatorStorageRuntime unauthorizedRuntime = new(store, "Simulator Account Storage (World 1)");
        unauthorizedRuntime.ConfigureAccess(
            "Simulator Account Storage (World 1)",
            "Mallory",
            new[] { "Mallory", "Eve" });

        Assert.False(unauthorizedRuntime.CanCurrentCharacterAccess);
        Assert.Equal(new[] { "Alice", "Bob" }, unauthorizedRuntime.AuthorizedCharacterNames);
    }

    [Fact]
    public void ConfigureAccess_AuthorizedOwnerCanMergeExpandedRoster()
    {
        StorageAccountStore store = new(_storageFilePath);
        SimulatorStorageRuntime initialRuntime = new(store, "Simulator Account Storage (World 1)");
        initialRuntime.ConfigureAccess(
            "Simulator Account Storage (World 1)",
            "Alice",
            new[] { "Alice", "Bob" });

        SimulatorStorageRuntime mergedRuntime = new(store, "Simulator Account Storage (World 1)");
        mergedRuntime.ConfigureAccess(
            "Simulator Account Storage (World 1)",
            "Alice",
            new[] { "Alice", "Bob", "Carol" });

        Assert.True(mergedRuntime.CanCurrentCharacterAccess);
        Assert.Equal(new[] { "Alice", "Bob", "Carol" }, mergedRuntime.AuthorizedCharacterNames);

        StorageAccountStore.StorageAccountState persisted = store.GetState("Simulator Account Storage (World 1)");
        Assert.NotNull(persisted);
        Assert.Equal(new[] { "Alice", "Bob", "Carol" }, persisted.AuthorizedCharacterNames);
    }

    public void Dispose()
    {
        string? directoryPath = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
