using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HaCreator.MapSimulator.Contracts
{
    /// <summary>
    /// A detached, session-owned description of an editor map. It contains no editor model objects
    /// and no graphics-device resources. Runtime state may be materialized from it independently.
    /// </summary>
    public sealed class RuntimeMapDefinition : IDisposable
    {
        private readonly MapInfo _mapInfo;
        private readonly byte[] _minimapPng;
        private readonly IReadOnlyDictionary<RuntimeAssetKey, RuntimeOwnedAssetOverride> _assetOverrides;
        private readonly IReadOnlyDictionary<RuntimeAssetKey, RuntimeAssetOverrideInfo> _assetOverrideInfo;
        private bool _disposed;

        public RuntimeMapDefinition(
            MapInfo mapInfo,
            Point mapSize,
            Point centerPoint,
            Rectangle? virtualBounds,
            Rectangle minimapArea,
            System.Drawing.Point minimapPosition,
            byte[] minimapPng,
            IEnumerable<RuntimeTileDefinition> tiles,
            IEnumerable<RuntimeObjectDefinition> objects,
            IEnumerable<RuntimeBackgroundDefinition> backgrounds,
            IEnumerable<RuntimeLifeDefinition> mobs,
            IEnumerable<RuntimeLifeDefinition> npcs,
            IEnumerable<RuntimeReactorDefinition> reactors,
            IEnumerable<RuntimePortalDefinition> portals,
            IEnumerable<RuntimeFootholdDefinition> footholds,
            IEnumerable<RuntimeRopeDefinition> ropes,
            IEnumerable<RuntimeChairDefinition> chairs,
            IEnumerable<RuntimeTooltipDefinition> tooltips,
            IEnumerable<RuntimeMiscDefinition> misc,
            IEnumerable<RuntimeMirrorFieldDefinition> mirrorFields,
            IEnumerable<RuntimeOwnedAssetOverride> assetOverrides)
        {
            _mapInfo = RuntimeMapInfoCloner.Clone(mapInfo ?? throw new ArgumentNullException(nameof(mapInfo)));
            Dictionary<RuntimeAssetKey, RuntimeOwnedAssetOverride> overrides = new();
            try
            {
                MapSize = mapSize;
                CenterPoint = centerPoint;
                VirtualBounds = virtualBounds;
                MinimapArea = minimapArea;
                MinimapPosition = minimapPosition;
                _minimapPng = minimapPng?.ToArray() ?? Array.Empty<byte>();
                Tiles = RuntimeReadOnly.Copy(tiles);
                Objects = RuntimeReadOnly.Copy((objects ?? Array.Empty<RuntimeObjectDefinition>()).Select(CloneObject));
                Backgrounds = RuntimeReadOnly.Copy(backgrounds);
                Mobs = RuntimeReadOnly.Copy(mobs);
                Npcs = RuntimeReadOnly.Copy(npcs);
                Reactors = RuntimeReadOnly.Copy(reactors);
                Portals = RuntimeReadOnly.Copy(portals);
                Footholds = RuntimeReadOnly.Copy(footholds);
                Ropes = RuntimeReadOnly.Copy(ropes);
                Chairs = RuntimeReadOnly.Copy(chairs);
                Tooltips = RuntimeReadOnly.Copy(tooltips);
                Misc = RuntimeReadOnly.Copy(misc);
                MirrorFields = RuntimeReadOnly.Copy(mirrorFields);

                foreach (RuntimeOwnedAssetOverride assetOverride in assetOverrides ?? Array.Empty<RuntimeOwnedAssetOverride>())
                {
                    if (assetOverride == null)
                        throw new ArgumentException("Asset overrides cannot contain null entries.", nameof(assetOverrides));
                    if (overrides.ContainsKey(assetOverride.Key))
                        continue;

                    RuntimeOwnedAssetOverride ownedOverride = assetOverride.CloneForOwner();
                    try
                    {
                        overrides.Add(ownedOverride.Key, ownedOverride);
                    }
                    catch
                    {
                        ownedOverride.Dispose();
                        throw;
                    }
                }
                _assetOverrides = new ReadOnlyDictionary<RuntimeAssetKey, RuntimeOwnedAssetOverride>(overrides);
                _assetOverrideInfo = new ReadOnlyDictionary<RuntimeAssetKey, RuntimeAssetOverrideInfo>(
                    overrides.ToDictionary(pair => pair.Key, pair => pair.Value.Info));
            }
            catch
            {
                foreach (RuntimeOwnedAssetOverride assetOverride in overrides.Values)
                    assetOverride.Dispose();
                _mapInfo.Image?.Dispose();
                throw;
            }
        }

        public int MapId => _mapInfo.id;
        public string MapName => _mapInfo.strMapName;
        public string StreetName => _mapInfo.strStreetName;
        public string CategoryName => _mapInfo.strCategoryName;
        public Point MapSize { get; }
        public Point CenterPoint { get; }
        public Rectangle? VirtualBounds { get; }
        public Rectangle MinimapArea { get; }
        public System.Drawing.Point MinimapPosition { get; }
        public IReadOnlyList<RuntimeTileDefinition> Tiles { get; }
        public IReadOnlyList<RuntimeObjectDefinition> Objects { get; }
        public IReadOnlyList<RuntimeBackgroundDefinition> Backgrounds { get; }
        public IReadOnlyList<RuntimeLifeDefinition> Mobs { get; }
        public IReadOnlyList<RuntimeLifeDefinition> Npcs { get; }
        public IReadOnlyList<RuntimeReactorDefinition> Reactors { get; }
        public IReadOnlyList<RuntimePortalDefinition> Portals { get; }
        public IReadOnlyList<RuntimeFootholdDefinition> Footholds { get; }
        public IReadOnlyList<RuntimeRopeDefinition> Ropes { get; }
        public IReadOnlyList<RuntimeChairDefinition> Chairs { get; }
        public IReadOnlyList<RuntimeTooltipDefinition> Tooltips { get; }
        public IReadOnlyList<RuntimeMiscDefinition> Misc { get; }
        public IReadOnlyList<RuntimeMirrorFieldDefinition> MirrorFields { get; }
        public IReadOnlyDictionary<RuntimeAssetKey, RuntimeAssetOverrideInfo> AssetOverrides => _assetOverrideInfo;

        /// <summary>Version of the lossless WZ property-tree extension representation.</summary>
        public int ExtensionSchemaVersion => 1;

        /// <summary>
        /// Returns an independently owned property tree for fields without typed runtime contracts.
        /// Group and property order are preserved; no editor or source parents are retained.
        /// The caller must dispose the returned image.
        /// </summary>
        public WzImage CreateExtensionTreeCopy()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var image = new WzImage("runtime-map-extensions.img") { Parsed = true, Changed = true };
            try
            {
                image.AddProperty(new WzIntProperty("schemaVersion", ExtensionSchemaVersion));
                AddGroup("additionalProps", _mapInfo.additionalProps);
                AddGroup("additionalNonInfoProps", _mapInfo.additionalNonInfoProps);
                AddGroup("unsupportedInfoProperties", _mapInfo.unsupportedInfoProperties);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }

            void AddGroup(string name, IEnumerable<WzImageProperty> properties)
            {
                var group = new WzSubProperty(name);
                image.AddProperty(group);
                foreach (WzImageProperty property in properties)
                    if (property != null)
                        group.AddProperty(property.DeepClone());
            }
        }

        public byte[] CreateMinimapPngCopy() => _minimapPng.ToArray();

        /// <summary>Creates a mutable session copy. Dispose its Image to release all owned metadata trees.</summary>
        public MapInfo CreateMapInfo()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return RuntimeMapInfoCloner.Clone(_mapInfo);
        }

        /// <summary>Creates an independently owned definition for another map visit.</summary>
        public RuntimeMapDefinition Clone()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new RuntimeMapDefinition(_mapInfo, MapSize, CenterPoint, VirtualBounds,
                MinimapArea, MinimapPosition, _minimapPng, Tiles, Objects, Backgrounds,
                Mobs, Npcs, Reactors, Portals, Footholds, Ropes, Chairs, Tooltips,
                Misc, MirrorFields, _assetOverrides.Values);
        }

        public bool TryCreateAssetImageRoot(RuntimeAssetKey key, out WzImage image)
        {
            if (_assetOverrides.TryGetValue(key, out RuntimeOwnedAssetOverride assetOverride))
            {
                image = assetOverride.CreateImageRootCopy();
                return image != null;
            }
            image = null;
            return false;
        }

        public byte[] CreateAssetPreviewPngCopy(RuntimeAssetKey key)
        {
            return _assetOverrides.TryGetValue(key, out RuntimeOwnedAssetOverride assetOverride)
                ? assetOverride.CreatePreviewPngCopy()
                : Array.Empty<byte>();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (RuntimeOwnedAssetOverride assetOverride in _assetOverrides.Values)
                assetOverride.Dispose();
            _mapInfo.Image?.Dispose();
        }

        private static RuntimeObjectDefinition CloneObject(RuntimeObjectDefinition source)
        {
            if (source == null)
                throw new ArgumentException("Object definitions cannot contain null entries.", nameof(source));
            return source with { Quests = RuntimeReadOnly.Copy(source.Quests) };
        }
    }

    public readonly record struct RuntimeAssetOverrideInfo(bool HasImageRoot, bool HasPreviewPng);

    public sealed class RuntimeOwnedAssetOverride : IDisposable
    {
        private readonly WzImage _imageRoot;
        private readonly byte[] _previewPng;
        private bool _disposed;

        public RuntimeOwnedAssetOverride(RuntimeAssetKey key, WzImage imageRoot, byte[] previewPng)
        {
            Key = key;
            _imageRoot = imageRoot?.DeepClone();
            _previewPng = previewPng?.ToArray() ?? Array.Empty<byte>();
        }

        public RuntimeAssetKey Key { get; }
        public bool HasImageRoot => _imageRoot != null;
        public bool HasPreviewPng => _previewPng.Length != 0;
        public RuntimeAssetOverrideInfo Info => new(HasImageRoot, HasPreviewPng);
        public WzImage CreateImageRootCopy() => _imageRoot?.DeepClone();
        public byte[] CreatePreviewPngCopy() => _previewPng.ToArray();
        internal RuntimeOwnedAssetOverride CloneForOwner() =>
            new(Key, _imageRoot, _previewPng);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _imageRoot?.Dispose();
        }
    }
}
