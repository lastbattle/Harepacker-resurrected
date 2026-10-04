using System;
using System.Collections.Generic;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Assets;

namespace HaCreator.MapSimulator.Contracts;

/// <summary>Map names from String/Map.img, detached from editor information caches.</summary>
public sealed record RuntimeMapName(string StreetName, string MapName, string CategoryName);

/// <summary>Localized item metadata from the String item images.</summary>
public sealed record RuntimeItemName(string Category, string Name, string Description);

/// <summary>Localized NPC metadata from String/Npc.img.</summary>
public sealed record RuntimeNpcName(string Name, string Function);

/// <summary>Localized mob metadata from String/Mob.img.</summary>
public sealed record RuntimeMobName(string Name);

/// <summary>Localized skill metadata from String/Skill.img.</summary>
public sealed record RuntimeSkillName(string Name, string Description);

/// <summary>A BGM property location and its resolved binary payload.</summary>
public sealed record RuntimeBgmAsset(
    string ImagePath,
    string PropertyPath,
    WzBinaryProperty Data);

/// <summary>Small, runtime-owned reactor metadata record.</summary>
public sealed record RuntimeReactorAsset(
    string Id,
    string Name,
    WzImage Image);

/// <summary>Optional host diagnostics sink. Runtime code must not show editor UI.</summary>
public interface IRuntimeDiagnostics
{
    void Trace(string message);
    void Report(string message, Exception error = null);
}

public sealed class NullRuntimeDiagnostics : IRuntimeDiagnostics
{
    public static readonly NullRuntimeDiagnostics Instance = new();
    private NullRuntimeDiagnostics() { }
    public void Trace(string message) { }
    public void Report(string message, Exception error = null) { }
}

/// <summary>
/// Runtime metadata and asset services. Implementations may cache parsed metadata,
/// but must return only runtime-owned assets and immutable metadata records.
/// </summary>
public interface IRuntimeAssetCatalog
{
    IRuntimeAssetSource Assets { get; }
    bool IsPreBBDataWzFormat { get; }

    WzImage GetTileSet(string name);
    WzImage GetObjectSet(string name);
    WzImage GetBackgroundSet(string name);

    bool TryGetMapName(string mapId, out RuntimeMapName value);
    IReadOnlyDictionary<string, RuntimeMapName> GetMapNames();
    bool TryGetItemName(int itemId, out RuntimeItemName value);
    IReadOnlyDictionary<int, RuntimeItemName> GetItemNames();
    bool TryGetMobName(string mobId, out RuntimeMobName value);
    bool TryGetNpcName(string npcId, out RuntimeNpcName value);
    bool TryGetSkillName(string skillId, out RuntimeSkillName value);
    bool TryGetBgm(string name, out RuntimeBgmAsset value);
    bool TryGetReactor(string reactorId, out RuntimeReactorAsset value);

    bool TryGetItemIcon(int itemId, string categoryName, out WzCanvasProperty value);
    bool TryGetMobIcon(int mobId, out WzCanvasProperty value);
    bool TryGetEquipment(int itemId, string categoryName, out WzImage value);

    IReadOnlyList<string> GetMobIds();
    IReadOnlyList<string> GetNpcIds();
    IReadOnlyList<string> GetReactorIds();
}

/// <summary>
/// Aggregate passed to game sessions. It is deliberately instance-scoped so a
/// standalone client and an editor preview cannot accidentally share Program state.
/// </summary>
public interface IRuntimeDataServices
{
    IRuntimeAssetSource Assets { get; }
    IRuntimeAssetCatalog Catalog { get; }
    IRuntimeDiagnostics Diagnostics { get; }
    bool IsPreBBDataWzFormat { get; }
}

public sealed class RuntimeDataServices : IRuntimeDataServices
{
    public RuntimeDataServices(
        IRuntimeAssetSource assets,
        IRuntimeAssetCatalog catalog,
        IRuntimeDiagnostics diagnostics = null,
        bool? isPreBBDataWzFormat = null)
    {
        Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (!ReferenceEquals(catalog.Assets, assets))
            throw new ArgumentException("The catalog must use the supplied asset source.", nameof(catalog));
        Diagnostics = diagnostics ?? NullRuntimeDiagnostics.Instance;
        IsPreBBDataWzFormat = isPreBBDataWzFormat ?? catalog.IsPreBBDataWzFormat;
    }

    public IRuntimeAssetSource Assets { get; }
    public IRuntimeAssetCatalog Catalog { get; }
    public IRuntimeDiagnostics Diagnostics { get; }
    public bool IsPreBBDataWzFormat { get; }
}
