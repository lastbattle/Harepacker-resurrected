using HaSharedLibrary.Configuration;
using System;

namespace HaCreator.MapSimulator.Contracts;

/// <summary>
/// Resolves simulator-owned persistence paths for one host profile.
/// Implementations own profile selection; simulator stores only consume the resolved paths.
/// </summary>
public interface ISimulatorProfileStorage
{
    string CharactersDirectory { get; }

    string GetFile(string fileName);
}

/// <summary>
/// User-data-backed simulator profile implementation.
/// </summary>
public sealed class SimulatorProfileStorage : ISimulatorProfileStorage
{
    private readonly bool _migrateLegacyHaCreatorData;

    public SimulatorProfileStorage(
        string application,
        bool migrateLegacyHaCreatorData = false)
    {
        if (string.IsNullOrWhiteSpace(application))
        {
            throw new ArgumentException("A simulator profile application name is required.", nameof(application));
        }

        Application = application;
        _migrateLegacyHaCreatorData = migrateLegacyHaCreatorData;
    }

    public string Application { get; }

    public string CharactersDirectory => UserDataPaths.GetSimulatorProfileCharactersDirectory(
        Application,
        _migrateLegacyHaCreatorData);

    public string GetFile(string fileName) => UserDataPaths.GetSimulatorProfileFile(
        Application,
        fileName,
        _migrateLegacyHaCreatorData);

    public static SimulatorProfileStorage CreateHaCreatorPreview() => new(
        UserDataPaths.HaCreator,
        migrateLegacyHaCreatorData: true);

    public static SimulatorProfileStorage CreateStandaloneClient() => new(
        UserDataPaths.MapleGameClient);
}
