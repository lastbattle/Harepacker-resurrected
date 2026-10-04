using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Assets;

namespace HaCreator.MapSimulator.Loaders;

/// <summary>
/// Owns the GPU resources cached by one simulator/client UI session. A caller
/// creates one instance per GraphicsDevice/session and passes it to UILoader;
/// no process-wide cache or ambient current-session state is used.
/// </summary>
public sealed class UILoaderResourceCache : IDisposable
{
    internal readonly ConcurrentDictionary<string, Tuple<StatusBarUI, StatusBarChatUI>> StatusBars = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, MinimapUI> Minimap = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Dictionary<string, Texture2D>> BuffIconTextures = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, BuffIconCatalogEntry>> BuffIconCatalog = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Texture2D[]> SkillTooltipTextures = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Texture2D[]> StatusBarCooldownMasks = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Texture2D> StatusBarTemporaryStatView = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Dictionary<int, Texture2D>> StatusBarTemporaryStatViewShadows = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Dictionary<string, StatusBarKeyDownBarTextures>> KeyDownBarTextures = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, StatusBarWarningAnimation> WarningAnimations = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, Texture2D> GuildMarkTextures = new(StringComparer.Ordinal);
    private GuildMarkCatalogData _guildMarkCatalog;
    internal readonly ConcurrentDictionary<string, (Dictionary<MapSimulatorChatTargetType, Texture2D> Textures, Dictionary<MapSimulatorChatTargetType, Point> Origins)> ChatTargetTextures = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, StatusBarChatUI.StatusBarPointNotificationAnimation> PointNotificationAnimations = new(StringComparer.Ordinal);
    internal Point SharedMinimapWindowPosition { get; set; } = new Point(10, 10);

    internal GuildMarkCatalogData GetGuildMarkCatalog(IRuntimeAssetSource runtimeAssets)
    {
        ArgumentNullException.ThrowIfNull(runtimeAssets);
        return _guildMarkCatalog ??= GuildMarkCatalog.LoadCatalog(runtimeAssets);
    }

    internal readonly ConcurrentDictionary<(IRuntimeAssetSource Source, GraphicsDevice Device), SkillDataLoader.SkillDisplayCache> SkillDisplays = new();
    private readonly HashSet<Texture2D> _skillTextures = new();
    private readonly HashSet<IDisposable> _ownedGraphicsResources = new(ReferenceEqualityComparer.Instance);
    private readonly object _ownedGraphicsResourcesLock = new();

    internal Texture2D OwnSkillTexture(Texture2D texture)
    {
        if (texture == null) return null;
        if (_disposed)
        {
            texture.Dispose();
            throw new ObjectDisposedException(nameof(UILoaderResourceCache));
        }
        _skillTextures.Add(texture);
        return texture;
    }

    internal void OwnGraphicsResource(IDisposable resource)
    {
        if (resource == null) return;
        lock (_ownedGraphicsResourcesLock)
        {
            if (_disposed)
            {
                resource.Dispose();
                throw new ObjectDisposedException(nameof(UILoaderResourceCache));
            }
            _ownedGraphicsResources.Add(resource);
        }
    }
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        HashSet<Texture2D> textures = new(_skillTextures);
        _skillTextures.Clear();
        SkillDisplays.Clear();
        foreach (Dictionary<string, Texture2D> values in BuffIconTextures.Values)
            AddRange(textures, values?.Values);
        foreach (Texture2D[] values in SkillTooltipTextures.Values)
            AddRange(textures, values);
        foreach (Texture2D[] values in StatusBarCooldownMasks.Values)
            AddRange(textures, values);
        AddRange(textures, StatusBarTemporaryStatView.Values);
        foreach (Dictionary<int, Texture2D> values in StatusBarTemporaryStatViewShadows.Values)
            AddRange(textures, values?.Values);
        foreach (Dictionary<string, StatusBarKeyDownBarTextures> values in KeyDownBarTextures.Values)
        {
            foreach (StatusBarKeyDownBarTextures entry in values.Values)
            {
                if (entry == null)
                    continue;
                textures.Add(entry.Bar);
                textures.Add(entry.Gauge);
                textures.Add(entry.Graduation);
            }
        }
        foreach (StatusBarWarningAnimation animation in WarningAnimations.Values)
            AddRange(textures, animation?.Frames);
        AddRange(textures, GuildMarkTextures.Values);
        foreach ((Dictionary<MapSimulatorChatTargetType, Texture2D> Textures, Dictionary<MapSimulatorChatTargetType, Point> Origins) value in ChatTargetTextures.Values)
            AddRange(textures, value.Textures?.Values);
        foreach (StatusBarChatUI.StatusBarPointNotificationAnimation animation in PointNotificationAnimations.Values)
            AddRange(textures, animation?.Frames);
        foreach (Tuple<StatusBarUI, StatusBarChatUI> statusBar in StatusBars.Values)
        {
            if (statusBar?.Item1 != null)
                AddRange(textures, statusBar.Item1.EnumerateOwnedTextures());
            if (statusBar?.Item2 != null)
                AddRange(textures, statusBar.Item2.EnumerateOwnedTextures());
        }
        foreach (MinimapUI minimap in Minimap.Values)
        {
            if (minimap != null)
                AddRange(textures, minimap.EnumerateOwnedTextures());
        }

        foreach (Texture2D texture in textures)
        {
            if (texture == null)
                continue;
            try { texture.Dispose(); } catch (ObjectDisposedException) { }
        }

        IDisposable[] ownedGraphicsResources;
        lock (_ownedGraphicsResourcesLock)
        {
            ownedGraphicsResources = new IDisposable[_ownedGraphicsResources.Count];
            _ownedGraphicsResources.CopyTo(ownedGraphicsResources);
            _ownedGraphicsResources.Clear();
        }
        foreach (IDisposable resource in ownedGraphicsResources)
        {
            if (resource is GraphicsResource graphicsResource && graphicsResource.IsDisposed)
                continue;
            try { resource.Dispose(); } catch (ObjectDisposedException) { }
        }

        StatusBars.Clear();
        Minimap.Clear();
        BuffIconTextures.Clear();
        BuffIconCatalog.Clear();
        SkillTooltipTextures.Clear();
        StatusBarCooldownMasks.Clear();
        StatusBarTemporaryStatView.Clear();
        StatusBarTemporaryStatViewShadows.Clear();
        KeyDownBarTextures.Clear();
        WarningAnimations.Clear();
        GuildMarkTextures.Clear();
        _guildMarkCatalog = null;
        ChatTargetTextures.Clear();
        PointNotificationAnimations.Clear();
    }

    private static void AddRange(HashSet<Texture2D> destination, IEnumerable<Texture2D> values)
    {
        if (values == null)
            return;
        foreach (Texture2D value in values)
        {
            if (value != null)
                destination.Add(value);
        }
    }
}
