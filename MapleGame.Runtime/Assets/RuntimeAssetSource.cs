using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Contracts;

namespace HaCreator.MapSimulator.Assets;

/// <summary>
/// Describes who is responsible for disposing the IDataSource passed to a runtime
/// asset source.  A borrowed source is only protected from concurrent calls while
/// the facade is using it; it is not pinned against eviction, hot swap, or disposal
/// by its owner.
/// </summary>
public enum RuntimeAssetSourceOwnership
{
    Borrowed,
    Owned
}

/// <summary>
/// Immutable version metadata exposed by the runtime asset boundary.  Returning a
/// copy rather than IDataSource.VersionInfo keeps callers from mutating source
/// metadata through the read-only facade.
/// </summary>
public sealed class RuntimeAssetSourceVersion
{
    private RuntimeAssetSourceVersion(
        string version,
        string displayName,
        string sourceRegion,
        DateTime extractedDate,
        string encryption,
        bool is64Bit,
        bool isPreBb,
        bool isPreBbDataWzFormat,
        bool isBetaMs,
        bool isBigBang2,
        bool isVUpdate,
        int patchVersion,
        IReadOnlyDictionary<string, RuntimeAssetCategoryInfo> categories,
        string directoryPath)
    {
        Version = version;
        DisplayName = displayName;
        SourceRegion = sourceRegion;
        ExtractedDate = extractedDate;
        Encryption = encryption;
        Is64Bit = is64Bit;
        IsPreBB = isPreBb;
        IsPreBBDataWzFormat = isPreBbDataWzFormat;
        IsBetaMs = isBetaMs;
        IsBigBang2 = isBigBang2;
        IsVUpdate = isVUpdate;
        PatchVersion = patchVersion;
        Categories = categories;
        DirectoryPath = directoryPath;
    }

    public string Version { get; }
    public string DisplayName { get; }
    public string SourceRegion { get; }
    public DateTime ExtractedDate { get; }
    public string Encryption { get; }
    public bool Is64Bit { get; }
    public bool IsPreBB { get; }
    public bool IsPreBBDataWzFormat { get; }
    public bool IsBetaMs { get; }
    public bool IsBigBang2 { get; }
    public bool IsVUpdate { get; }
    public int PatchVersion { get; }
    public IReadOnlyDictionary<string, RuntimeAssetCategoryInfo> Categories { get; }
    public string DirectoryPath { get; }

    internal static RuntimeAssetSourceVersion From(VersionInfo info)
    {
        if (info == null)
            return null;

        Dictionary<string, RuntimeAssetCategoryInfo> categories =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, CategoryInfo> pair in
                 info.Categories ?? new Dictionary<string, CategoryInfo>())
        {
            CategoryInfo category = pair.Value;
            categories[pair.Key] = new RuntimeAssetCategoryInfo(
                category?.FileCount ?? 0,
                category?.TotalSize ?? 0,
                category?.LastModified ?? default,
                (IEnumerable<string>)category?.Subdirectories ?? Array.Empty<string>());
        }

        return new RuntimeAssetSourceVersion(
            info.Version,
            info.DisplayName,
            info.SourceRegion,
            info.ExtractedDate,
            info.Encryption,
            info.Is64Bit,
            info.IsPreBB,
            info.IsPreBBDataWzFormat,
            info.IsBetaMs,
            info.IsBigBang2,
            info.IsVUpdate,
            info.PatchVersion,
            new ReadOnlyDictionary<string, RuntimeAssetCategoryInfo>(categories),
            info.DirectoryPath);
    }
}

public sealed class RuntimeAssetCategoryInfo
{
    internal RuntimeAssetCategoryInfo(
        int fileCount,
        long totalSize,
        DateTime lastModified,
        IEnumerable<string> subdirectories)
    {
        FileCount = fileCount;
        TotalSize = totalSize;
        LastModified = lastModified;
        Subdirectories = new ReadOnlyCollection<string>(
            (subdirectories ?? Array.Empty<string>()).ToArray());
    }

    public int FileCount { get; }
    public long TotalSize { get; }
    public DateTime LastModified { get; }
    public IReadOnlyList<string> Subdirectories { get; }
}

/// <summary>
/// Stores detached WZ image roots for a runtime session. Inputs are cloned on
/// entry. Once attached to a RuntimeAssetSource the store is frozen and the
/// source returns these stable, session-owned roots; callers never dispose them.
/// </summary>
public sealed class RuntimeAssetOverrideStore : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<RuntimeAssetKey, WzImage> _images =
        new(RuntimeAssetKeyComparer.Instance);
    private bool _disposed;
    private bool _frozen;

    public RuntimeAssetOverrideStore()
    {
    }

    public RuntimeAssetOverrideStore(IEnumerable<RuntimeOwnedAssetOverride> overrides)
    {
        foreach (RuntimeOwnedAssetOverride assetOverride in
                 overrides ?? Array.Empty<RuntimeOwnedAssetOverride>())
        {
            Set(assetOverride);
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                return _images.Count;
            }
        }
    }

    public IReadOnlyList<RuntimeAssetKey> Keys
    {
        get
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                return new ReadOnlyCollection<RuntimeAssetKey>(_images.Keys.ToArray());
            }
        }
    }

    /// <summary>Adds or replaces an image by taking a detached deep copy.</summary>
    public void Set(RuntimeAssetKey key, WzImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        WzImage detached = image.DeepClone();
        SetDetached(key, detached);
    }

    /// <summary>
    /// Adds an image root from the map-definition ownership object.  The returned
    /// root is already a copy, and is adopted by this store without retaining the
    /// definition's root.
    /// </summary>
    public void Set(RuntimeOwnedAssetOverride assetOverride)
    {
        ArgumentNullException.ThrowIfNull(assetOverride);
        WzImage detached = assetOverride.CreateImageRootCopy();
        if (detached != null)
            SetDetached(assetOverride.Key, detached);
    }

    public void Add(RuntimeAssetKey key, WzImage image) => Set(key, image);

    public bool Remove(RuntimeAssetKey key)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            ThrowIfFrozen();
            if (!_images.Remove(key, out WzImage image))
                return false;
            image.Dispose();
            return true;
        }
    }

    internal bool TryGetOwned(RuntimeAssetKey key, out WzImage image)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_images.TryGetValue(key, out WzImage stored))
            {
                image = null;
                return false;
            }

            image = stored;
            return true;
        }
    }

    internal IReadOnlyList<(RuntimeAssetKey Key, WzImage Image)> SnapshotOwned(
        string category,
        string subDirectory = null,
        bool includeDescendants = false)
    {
        string normalizedCategory = NormalizePart(category);
        string normalizedDirectory = NormalizePart(subDirectory);
        string prefix = string.IsNullOrEmpty(normalizedDirectory)
            ? normalizedCategory + "/"
            : normalizedCategory + "/" + normalizedDirectory + "/";

        lock (_gate)
        {
            ThrowIfDisposed();
            List<(RuntimeAssetKey Key, WzImage Image)> result = new();
            foreach (KeyValuePair<RuntimeAssetKey, WzImage> pair in _images)
            {
                string keyPath = GetNormalizedPath(pair.Key);
                if (!keyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string remainder = keyPath.Substring(prefix.Length);
                if (!includeDescendants && remainder.Contains('/'))
                    continue;

                result.Add((pair.Key, pair.Value));
            }

            return new ReadOnlyCollection<(RuntimeAssetKey Key, WzImage Image)>(result);
        }
    }

    internal IReadOnlyList<RuntimeAssetKey> GetKeys(string category)
    {
        string normalizedCategory = NormalizePart(category);
        lock (_gate)
        {
            ThrowIfDisposed();
            return new ReadOnlyCollection<RuntimeAssetKey>(_images.Keys
                .Where(key => string.Equals(key.Category, normalizedCategory,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray());
        }
    }

    internal void Freeze()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _frozen = true;
        }
    }

    private void SetDetached(RuntimeAssetKey key, WzImage detached)
    {
        RuntimeAssetKey normalizedKey = NormalizeKey(key);
        int imageEnd = normalizedKey.Path.IndexOf(".img", StringComparison.OrdinalIgnoreCase);
        if (imageEnd >= 0)
            normalizedKey = normalizedKey with { Path = normalizedKey.Path.Substring(0, imageEnd + 4) };
        if (string.IsNullOrEmpty(normalizedKey.Category) || string.IsNullOrEmpty(normalizedKey.Path))
        {
            detached.Dispose();
            throw new ArgumentException("An asset override requires a category and path.", nameof(key));
        }

        lock (_gate)
        {
            try
            {
                ThrowIfDisposed();
                ThrowIfFrozen();
                if (_images.Remove(normalizedKey, out WzImage previous))
                    previous.Dispose();
                _images[normalizedKey] = detached;
            }
            catch
            {
                detached.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (WzImage image in _images.Values)
                image.Dispose();
            _images.Clear();
        }
    }

    internal static RuntimeAssetKey NormalizeKey(RuntimeAssetKey key)
    {
        string category = NormalizePart(key.Category);
        string path = NormalizePart(key.Path);
        int separator = category.IndexOf('/');
        if (separator >= 0)
        {
            path = category.Substring(separator + 1) + "/" + path;
            category = category.Substring(0, separator);
        }
        return new RuntimeAssetKey(category, path);
    }

    internal static string NormalizePart(string value)
    {
        return (value ?? string.Empty).Replace('\\', '/').Trim('/');
    }

    private static string GetNormalizedPath(RuntimeAssetKey key)
    {
        RuntimeAssetKey normalized = NormalizeKey(key);
        return normalized.Category + "/" + normalized.Path;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("Runtime asset overrides are frozen for this session.");
    }

}

internal sealed class RuntimeAssetKeyComparer : IEqualityComparer<RuntimeAssetKey>
{
    public static readonly RuntimeAssetKeyComparer Instance = new();

    public bool Equals(RuntimeAssetKey x, RuntimeAssetKey y)
    {
        return string.Equals(x.Category, y.Category, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(RuntimeAssetKey value)
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Category ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Path ?? string.Empty));
    }
}

/// <summary>
/// Read-only, serialized access to an IDataSource for one runtime session.
/// This facade deliberately omits IDataSource mutation methods.  Owned instances
/// dispose the source; borrowed instances leave source disposal and cache lifetime
/// to the host.
/// </summary>
public interface IRuntimeAssetSource
{
    string Name { get; }
    bool IsInitialized { get; }
    RuntimeAssetSourceOwnership Ownership { get; }
    RuntimeAssetSourceVersion Version { get; }
    WzImage FindImage(string category, string imageName);
    WzObject FindObject(string category, string name);
    IReadOnlyList<WzImage> GetImagesInCategory(string category);
    IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory);
    IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory);
    bool ImageExists(string category, string imageName);
    bool CategoryExists(string category);
    IReadOnlyList<string> GetCategories();
    IReadOnlyList<string> GetSubdirectories(string category);
    IReadOnlyList<WzDirectory> GetDirectories(string baseCategory);
    void PreloadCategory(string category);
}

public sealed class RuntimeAssetSource : IRuntimeAssetSource, IDisposable
{
    private readonly object _sourceGate = new();
    private readonly IDataSource _source;
    private readonly RuntimeAssetSourceOwnership _ownership;
    private readonly RuntimeAssetOverrideStore _overrides;
    private readonly Dictionary<RuntimeAssetKey, WzImage> _imageCache =
        new(RuntimeAssetKeyComparer.Instance);
    private readonly HashSet<RuntimeAssetKey> _resolvingImageKeys =
        new(RuntimeAssetKeyComparer.Instance);
    private readonly HashSet<RuntimeAssetKey> _resolvedImageKeys =
        new(RuntimeAssetKeyComparer.Instance);
    private readonly Dictionary<string, WzDirectory> _directoryCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string _name;
    private readonly RuntimeAssetSourceVersion _version;
    private bool _disposed;

    public RuntimeAssetSource(
        IDataSource source,
        RuntimeAssetSourceOwnership ownership,
        RuntimeAssetOverrideStore overrides = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _ownership = ownership;
        _overrides = overrides ?? new RuntimeAssetOverrideStore();
        try
        {
            // Override roots are always owned by the runtime session. A caller may
            // populate a store before handing it to the source, but cannot mutate or
            // dispose it while this source is active.
            _overrides.Freeze();
            lock (_sourceGate)
            {
                _name = source.Name;
                _version = RuntimeAssetSourceVersion.From(source.VersionInfo);
            }
        }
        catch
        {
            _overrides.Dispose();
            if (ownership == RuntimeAssetSourceOwnership.Owned)
            {
                try { source.Dispose(); } catch { }
            }
            throw;
        }
    }

    public static RuntimeAssetSource CreateOwned(
        IDataSource source,
        IEnumerable<RuntimeOwnedAssetOverride> overrides = null)
    {
        RuntimeAssetOverrideStore store = new(overrides);
        try
        {
            return new RuntimeAssetSource(source, RuntimeAssetSourceOwnership.Owned, store);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    public static RuntimeAssetSource CreateBorrowed(
        IDataSource source,
        IEnumerable<RuntimeOwnedAssetOverride> overrides = null)
    {
        RuntimeAssetOverrideStore store = new(overrides);
        try
        {
            return new RuntimeAssetSource(source, RuntimeAssetSourceOwnership.Borrowed, store);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    public string Name
    {
        get
        {
            ThrowIfDisposed();
            return _name;
        }
    }

    public bool IsInitialized
    {
        get
        {
            lock (_sourceGate)
            {
                ThrowIfDisposed();
                return _source.IsInitialized;
            }
        }
    }

    public RuntimeAssetSourceOwnership Ownership => _ownership;
    public RuntimeAssetSourceVersion Version => _version;

    public WzImage FindImage(string category, string imageName)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(imageName))
            return null;
        RuntimeAssetKey request = RuntimeAssetOverrideStore.NormalizeKey(new(category, imageName));
        category = request.Category;
        imageName = request.Path;

        lock (_sourceGate)
        {
            ThrowIfDisposed();
            foreach (string candidate in ImageNameCandidates(imageName))
            {
                RuntimeAssetKey key = RuntimeAssetOverrideStore.NormalizeKey(
                    new RuntimeAssetKey(category, candidate));
                if (_overrides.TryGetOwned(key, out WzImage overrideImage))
                {
                    EnsureImageLinksResolved(key, overrideImage);
                    return overrideImage;
                }
                if (_imageCache.TryGetValue(key, out WzImage cached))
                {
                    EnsureImageLinksResolved(key, cached);
                    return cached;
                }
            }

            foreach (string candidate in ImageNameCandidates(imageName))
            {
                WzImage image = _source.GetImage(category, candidate);
                if (image != null)
                {
                    WzImage detached = image.DeepClone();
                    RuntimeAssetKey requestedKey = RuntimeAssetOverrideStore.NormalizeKey(
                        new RuntimeAssetKey(category, candidate));
                    _imageCache[requestedKey] = detached;
                    EnsureImageLinksResolved(requestedKey, detached);
                    return detached;
                }
            }
            return null;
        }
    }

    public WzObject FindObject(string category, string name)
    {
        string normalizedName = RuntimeAssetOverrideStore.NormalizePart(name);
        int imageEnd = normalizedName.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
        if (imageEnd >= 0)
        {
            WzObject node = FindImage(category, normalizedName.Substring(0, imageEnd + 4));
            foreach (string segment in normalizedName.Substring(imageEnd + 5).Split('/'))
            {
                if (node == null) return null;
                node = node[segment];
            }
            return node;
        }
        WzImage image = FindImage(category, name);
        if (image != null)
            return image;

        lock (_sourceGate)
        {
            ThrowIfDisposed();
            string cacheKey = RuntimeAssetOverrideStore.NormalizePart(category);
            if (_directoryCache.TryGetValue(cacheKey, out WzDirectory cachedDirectory))
            {
                return FindObjectInDirectory(cachedDirectory, name);
            }

            WzDirectory categoryDirectory = _source.GetDirectory(category);
            if (categoryDirectory == null || string.IsNullOrWhiteSpace(name))
            {
                if (categoryDirectory == null)
                    return null;
                WzDirectory detachedDirectory = categoryDirectory.DeepClone();
                _directoryCache[cacheKey] = detachedDirectory;
                return string.IsNullOrWhiteSpace(name)
                    ? detachedDirectory
                    : FindObjectInDirectory(detachedDirectory, name);
            }

            WzDirectory detached = categoryDirectory.DeepClone();
            _directoryCache[cacheKey] = detached;
            return FindObjectInDirectory(detached, name);
        }
    }

    public IReadOnlyList<WzImage> GetImagesInCategory(string category)
    {
        return GetImages(category, null, (source, value) => source.GetImagesInCategory(value));
    }

    public IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory)
    {
        return GetImages(category, subDirectory,
            (source, value) => source.GetImagesInDirectory(value, subDirectory));
    }

    public IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory)
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (string name in _source.GetImageNamesInDirectory(category, subDirectory)
                         ?? Array.Empty<string>())
                names.Add(name);

            string normalizedSubDirectory = RuntimeAssetOverrideStore.NormalizePart(subDirectory);
            string prefix = string.IsNullOrEmpty(normalizedSubDirectory)
                ? string.Empty
                : normalizedSubDirectory + "/";
            foreach (RuntimeAssetKey key in _overrides.GetKeys(category))
            {
                string keyPath = RuntimeAssetOverrideStore.NormalizePart(key.Path);
                if (!keyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                string remainder = keyPath.Substring(prefix.Length);
                if (remainder.Contains('/'))
                    continue;
                string imageName = remainder;
                if (imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                    imageName = imageName[..^4];
                names.Add(imageName);
            }

            return new ReadOnlyCollection<string>(names.ToArray());
        }
    }

    public bool ImageExists(string category, string imageName) => FindImage(category, imageName) != null;

    public bool CategoryExists(string category)
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            return GetOverrideCategories().Contains(category, StringComparer.OrdinalIgnoreCase) ||
                   _source.CategoryExists(category);
        }
    }

    public IReadOnlyList<string> GetCategories()
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            HashSet<string> categories = new(StringComparer.OrdinalIgnoreCase);
            foreach (string category in _source.GetCategories() ?? Array.Empty<string>())
                categories.Add(category);
            foreach (string category in GetOverrideCategories())
                categories.Add(category);
            return new ReadOnlyCollection<string>(categories.ToArray());
        }
    }

    public IReadOnlyList<string> GetSubdirectories(string category)
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in _source.GetSubdirectories(category) ?? Array.Empty<string>())
                directories.Add(directory);
            foreach (RuntimeAssetKey key in _overrides.GetKeys(category))
            {
                int separator = key.Path.LastIndexOf('/');
                if (separator > 0)
                {
                    string path = key.Path[..separator];
                    while (!string.IsNullOrEmpty(path))
                    {
                        directories.Add(path);
                        int parentSeparator = path.LastIndexOf('/');
                        if (parentSeparator < 0)
                            break;
                        path = path[..parentSeparator];
                    }
                }
            }
            return new ReadOnlyCollection<string>(directories.ToArray());
        }
    }

    public IReadOnlyList<WzDirectory> GetDirectories(string baseCategory)
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            List<WzDirectory> directories = new();
            foreach (WzDirectory directory in
                     _source.GetDirectories(baseCategory) ?? Array.Empty<WzDirectory>())
            {
                string key = RuntimeAssetOverrideStore.NormalizePart(baseCategory) + "/" +
                    RuntimeAssetOverrideStore.NormalizePart(directory.Name);
                if (!_directoryCache.TryGetValue(key, out WzDirectory detached))
                {
                    detached = directory.DeepClone();
                    _directoryCache[key] = detached;
                }
                directories.Add(detached);
            }
            return new ReadOnlyCollection<WzDirectory>(directories);
        }
    }

    public void PreloadCategory(string category)
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            _source.PreloadCategory(category);
        }
    }

    public void Dispose()
    {
        lock (_sourceGate)
        {
            if (_disposed)
                return;
            _disposed = true;
            Exception failure = null;
            try
            {
                if (_ownership == RuntimeAssetSourceOwnership.Owned)
                    _source.Dispose();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                foreach (WzDirectory directory in _directoryCache.Values)
                {
                    try { directory.Dispose(); } catch (Exception exception) { failure ??= exception; }
                }
                foreach (WzImage image in _imageCache.Values.Distinct())
                {
                    try { image.Dispose(); } catch (Exception exception) { failure ??= exception; }
                }
                try { _overrides.Dispose(); } catch (Exception exception) { failure ??= exception; }
                _directoryCache.Clear();
                _imageCache.Clear();
                _resolvingImageKeys.Clear();
                _resolvedImageKeys.Clear();
            }

            if (failure != null)
                throw failure;
        }
    }

    private IReadOnlyList<WzImage> GetImages(
        string category,
        string subDirectory,
        Func<IDataSource, string, IEnumerable<WzImage>> readSource)
    {
        lock (_sourceGate)
        {
            ThrowIfDisposed();
            List<WzImage> images = new();
            HashSet<RuntimeAssetKey> materializedKeys =
                new(RuntimeAssetKeyComparer.Instance);
            foreach (WzImage image in readSource(_source, category) ?? Array.Empty<WzImage>())
            {
                if (image != null)
                {
                    RuntimeAssetKey key = CanonicalImageKey(category, subDirectory, image);
                    if (_overrides.TryGetOwned(key, out WzImage overrideImage))
                    {
                        EnsureImageLinksResolved(key, overrideImage);
                        images.Add(overrideImage);
                    }
                    else
                    {
                        images.Add(GetOrCloneImage(key, image));
                    }
                    materializedKeys.Add(key);
                }
            }

            foreach ((RuntimeAssetKey Key, WzImage Image) entry in
                     _overrides.SnapshotOwned(category, subDirectory, includeDescendants: true))
            {
                if (!materializedKeys.Contains(entry.Key))
                {
                    EnsureImageLinksResolved(entry.Key, entry.Image);
                    images.Add(entry.Image);
                }
            }

            return new ReadOnlyCollection<WzImage>(images);
        }
    }

    private IReadOnlyList<string> GetOverrideCategories()
    {
        return new ReadOnlyCollection<string>(_overrides.Keys
            .Select(key => key.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private WzImage GetOrCloneImage(RuntimeAssetKey key, WzImage sourceImage)
    {
        if (_imageCache.TryGetValue(key, out WzImage cached))
            return cached;
        WzImage detached = sourceImage.DeepClone();
        _imageCache[key] = detached;
        EnsureImageLinksResolved(key, detached);
        return detached;
    }

    /// <summary>
    /// Resolves image links after the image has been detached from its source.
    /// WzImage.DeepClone intentionally does not retain a WzDirectory/WzFile
    /// parent, so the normal WZ link lookup cannot resolve _outlink values. A
    /// runtime session owns this resolver and asks the asset source for target
    /// images, keeping links independent of editor/source lifetime.
    /// </summary>
    private void EnsureImageLinksResolved(RuntimeAssetKey key, WzImage image)
    {
        if (image == null || _resolvedImageKeys.Contains(key))
            return;
        if (!_resolvingImageKeys.Add(key))
            return;

        try
        {
            foreach (WzImageProperty property in
                     image.WzProperties?.ToArray() ?? Array.Empty<WzImageProperty>())
                ResolvePropertyLinks(property, key.Category);
            _resolvedImageKeys.Add(key);
        }
        catch
        {
            // A malformed link must not make an otherwise usable runtime image
            // unavailable. Individual link failures leave their metadata intact.
        }
        finally
        {
            _resolvingImageKeys.Remove(key);
        }
    }

    private void ResolvePropertyLinks(WzImageProperty property, string currentCategory)
    {
        if (property == null)
            return;

        if (property is WzUOLProperty uol)
        {
            ResolveUolProperty(uol, currentCategory);
            return;
        }

        if (property is WzCanvasProperty canvas)
            ResolveCanvasLink(canvas, currentCategory);

        // WzUOLProperty.WzProperties follows the link dynamically. It was
        // handled above so a failed/cyclic UOL cannot traverse another tree.
        foreach (WzImageProperty child in
                 property.WzProperties?.ToArray() ?? Array.Empty<WzImageProperty>())
            ResolvePropertyLinks(child, currentCategory);
    }

    private void ResolveCanvasLink(WzCanvasProperty canvas, string currentCategory)
    {
        bool hasInlink = canvas.ContainsInlinkProperty();
        bool hasOutlink = canvas.ContainsOutlinkProperty();
        if (!hasInlink && !hasOutlink)
            return;

        try
        {
            WzImageProperty target;
            if (hasInlink)
            {
                // The clone retains the image/property parent chain, so an
                // inlink is safe to resolve without touching the source tree.
                target = canvas.GetLinkedWzImageProperty();
            }
            else
            {
                string link = (canvas[WzCanvasProperty.OutlinkPropertyName] as WzStringProperty)?.Value;
                target = ResolveOutlink(link, currentCategory);
            }

            if (target == null || ReferenceEquals(target, canvas) || !CopyCanvasData(canvas, target))
                return;

            // A resolved canvas now owns its bitmap bytes and no longer needs
            // either source-side link metadata.
            canvas.RemoveProperty(WzCanvasProperty.InlinkPropertyName);
            canvas.RemoveProperty(WzCanvasProperty.OutlinkPropertyName);
        }
        catch
        {
            // Keep unresolved metadata for callers that want to diagnose or
            // retry a link after loading additional assets.
        }
    }

    private WzImageProperty ResolveOutlink(string link, string currentCategory)
    {
        if (!TryParseImagePath(link, currentCategory,
                out string category, out string imageName, out string propertyPath))
            return null;

        WzImage targetImage = FindImage(category, imageName);
        if (targetImage == null || string.IsNullOrWhiteSpace(propertyPath))
            return null;

        bool isCanvasPath = link.Contains("/_Canvas/", StringComparison.OrdinalIgnoreCase) ||
            link.Contains("_Canvas/", StringComparison.OrdinalIgnoreCase);
        return FindLinkedProperty(targetImage, propertyPath, isCanvasPath);
    }

    private void ResolveUolProperty(WzUOLProperty uol, string currentCategory)
    {
        // Ordinary UOLs are already self-contained after DeepClone: their
        // parent chain terminates at the detached image. Only external UOL
        // values need materialization through this session's image cache.
        try
        {
            if (uol.LinkValue != null)
                return;
        }
        catch
        {
            return;
        }

        if (!TryParseImagePath(uol.Value, currentCategory,
                out string category, out string imageName, out string propertyPath) ||
            string.IsNullOrWhiteSpace(propertyPath))
            return;

        WzImage targetImage = FindImage(category, imageName);
        WzImageProperty target = targetImage == null
            ? null
            : FindLinkedProperty(targetImage, propertyPath,
                imageName.Contains("_Canvas", StringComparison.OrdinalIgnoreCase));
        if (target == null || ReferenceEquals(target, uol) || uol.Parent is not IPropertyContainer parent)
            return;

        try
        {
            WzImageProperty replacement = target.DeepClone();
            replacement.Name = uol.Name;
            parent.RemoveProperty(uol);
            parent.AddProperty(replacement);
        }
        catch
        {
            // Leave the UOL intact when its target cannot be cloned.
        }
    }

    private static bool TryParseImagePath(
        string value,
        string currentCategory,
        out string category,
        out string imageName,
        out string propertyPath)
    {
        category = null;
        imageName = null;
        propertyPath = null;
        string normalized = RuntimeAssetOverrideStore.NormalizePart(value);
        if (string.IsNullOrEmpty(normalized))
            return false;

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int imageIndex = Array.FindIndex(parts, part =>
            part.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
        if (imageIndex < 0)
            return false;

        category = imageIndex == 0
            ? RuntimeAssetOverrideStore.NormalizePart(currentCategory)
            : RuntimeAssetOverrideStore.NormalizePart(parts[0]);
        if (string.IsNullOrEmpty(category))
            return false;

        int imageStart = imageIndex == 0 ? 0 : 1;
        imageName = string.Join("/", parts.Skip(imageStart).Take(imageIndex - imageStart + 1));
        propertyPath = imageIndex + 1 < parts.Length
            ? string.Join("/", parts.Skip(imageIndex + 1))
            : null;
        return !string.IsNullOrEmpty(imageName);
    }

    private static WzImageProperty FindLinkedProperty(
        WzImage image,
        string propertyPath,
        bool isCanvasPath)
    {
        WzImageProperty target = image.GetFromPath(propertyPath);
        if (target != null || !isCanvasPath)
            return target;

        string[] parts = RuntimeAssetOverrideStore.NormalizePart(propertyPath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        // Modern _Canvas images commonly replace AnimSet/<name> with
        // Anims/0/<name>. Try that shape and segment aliases first.
        List<string> candidates = new();
        if (parts.Length > 2 && string.Equals(parts[0], "AnimSet", StringComparison.OrdinalIgnoreCase))
        {
            string remainder = string.Join("/", parts.Skip(2));
            candidates.Add("Anims/0/" + parts[1] + "/" + remainder);
            candidates.Add("Anims/0/" + remainder);
            candidates.Add("Anims/0/" + parts[1] + "/" +
                System.Text.RegularExpressions.Regex.Replace(remainder, "Segment\\d+", "Segment0"));
            candidates.Add("Anims/0/" + parts[1] + "/" +
                System.Text.RegularExpressions.Regex.Replace(remainder, "Segment[^/]+", "Segment:All"));
        }
        foreach (string candidate in candidates)
        {
            target = image.GetFromPath(candidate);
            if (target != null)
                return target;
        }

        string lastName = parts[^1];
        target = image[lastName];
        if (target != null)
            return target;

        return FindPropertyByName(image.WzProperties, lastName, requireCanvas: true);
    }

    private static WzImageProperty FindPropertyByName(
        WzPropertyCollection properties,
        string name,
        bool requireCanvas)
    {
        if (properties == null)
            return null;
        foreach (WzImageProperty property in properties)
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (!requireCanvas || property is WzCanvasProperty))
                return property;
            WzImageProperty nested = FindPropertyByName(property.WzProperties, name, requireCanvas);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static bool CopyCanvasData(WzCanvasProperty destination, WzImageProperty target)
    {
        WzPngProperty sourcePng = target switch
        {
            WzCanvasProperty sourceCanvas => sourceCanvas.PngProperty,
            WzPngProperty png => png,
            WzUOLProperty uol => uol.LinkValue as WzPngProperty ??
                (uol.LinkValue as WzCanvasProperty)?.PngProperty,
            _ => null
        };
        WzPngProperty destinationPng = destination.PngProperty;
        if (sourcePng == null || destinationPng == null)
            return false;

        byte[] compressed = sourcePng.GetCompressedBytesForExtraction(false);
        if (compressed == null || compressed.Length == 0)
            return false;
        destinationPng.SetCompressedBytes((byte[])compressed.Clone(), sourcePng.Width,
            sourcePng.Height, sourcePng.Format);
        return true;
    }

    private static RuntimeAssetKey CanonicalImageKey(
        string category,
        string subDirectory,
        WzImage image)
    {
        string categoryPath = RuntimeAssetOverrideStore.NormalizePart(category);
        string imagePath = RuntimeAssetOverrideStore.NormalizePart(image.FullPath);
        string categoryPrefix = categoryPath + "/";
        if (imagePath.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            imagePath = imagePath[categoryPrefix.Length..];

        if (string.IsNullOrEmpty(imagePath) || !imagePath.Contains('/'))
        {
            string directory = RuntimeAssetOverrideStore.NormalizePart(subDirectory);
            imagePath = string.IsNullOrEmpty(directory)
                ? RuntimeAssetOverrideStore.NormalizePart(image.Name)
                : directory + "/" + RuntimeAssetOverrideStore.NormalizePart(image.Name);
        }

        return RuntimeAssetOverrideStore.NormalizeKey(new RuntimeAssetKey(category, imagePath));
    }

    private static WzObject FindObjectInDirectory(WzDirectory directory, string name)
    {
        if (directory == null)
            return null;
        if (string.IsNullOrWhiteSpace(name))
            return directory;

        WzObject current = directory;
        foreach (string segment in RuntimeAssetOverrideStore.NormalizePart(name)
                     .Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current is WzDirectory currentDirectory
                ? currentDirectory[segment]
                : current[segment];
            if (current == null)
                return null;
        }
        return current;
    }

    private static IEnumerable<string> ImageNameCandidates(string imageName)
    {
        string normalized = RuntimeAssetOverrideStore.NormalizePart(imageName);
        if (string.IsNullOrEmpty(normalized))
            yield break;
        yield return normalized;
        if (normalized.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
        {
            yield return normalized[..^4];
        }
        else
        {
            yield return normalized + ".img";
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
