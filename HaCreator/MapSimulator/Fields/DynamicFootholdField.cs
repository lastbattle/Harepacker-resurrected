using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Client-owned wrapper metadata for maps that bind through CField_DynamicFoothold.
    /// The client-side Init body is effectively a stub, so the parity seam here focuses on
    /// explicit ownership, map rebinding, and WZ foothold-root diagnostics instead of
    /// inventing deeper behavior that has not been recovered yet.
    /// </summary>
    public sealed class DynamicFootholdField
    {
        public const int ClientFieldFactoryAddress = 0x53F220;
        public const string ClientOwnerName = "CField_DynamicFoothold";
        public const int ClientGetFieldTypeAddress = 0x551020;
        public const int ClientInitStubAddress = 0x551050;

        private bool _isActive;
        private int _mapId;
        private int _footholdLayerCount;
        private int _footholdGroupCount;
        private int _footholdSegmentCount;
        private bool _hasWzFootholdRoot;
        private int _contractMapId;
        private int _linkedContractMapId;
        private bool _usesLinkedContract;
        private bool _linkedContractMissing;
        private int _dynamicObjectLayerCount;
        private int _dynamicObjectCount;
        private int _dynamicEnabledObjectCount;
        private readonly List<string> _authoredDynamicObjectNames = new();
        private readonly List<string> _packetOwnedSnapshotDynamicObjectNames = new();
        private readonly Dictionary<string, int> _authoredDynamicObjectPlatformByName = new(StringComparer.OrdinalIgnoreCase);

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int ContractMapId => _contractMapId;
        public int LinkedContractMapId => _linkedContractMapId;
        public bool UsesLinkedContract => _usesLinkedContract;
        public bool LinkedContractMissing => _linkedContractMissing;
        public int FootholdLayerCount => _footholdLayerCount;
        public int FootholdGroupCount => _footholdGroupCount;
        public int FootholdSegmentCount => _footholdSegmentCount;
        public bool HasWzFootholdRoot => _hasWzFootholdRoot;
        public int DynamicObjectLayerCount => _dynamicObjectLayerCount;
        public int DynamicObjectCount => _dynamicObjectCount;
        public int DynamicEnabledObjectCount => _dynamicEnabledObjectCount;

        public void Configure(MapInfo mapInfo, DynamicFootholdSystem dynamicFootholds, Func<int, WzImage> linkedMapResolver = null)
        {
            dynamicFootholds?.ResetForClientOwnedWrapper();
            Reset();

            if (mapInfo?.fieldType != FieldType.FIELDTYPE_DYNAMICFOOTHOLD)
            {
                return;
            }

            _isActive = true;
            _mapId = mapInfo.id;
            _contractMapId = mapInfo.id;
            LoadMapContractSummary(mapInfo, linkedMapResolver);
        }

        public string DescribeStatus(DynamicFootholdSystem dynamicFootholds)
        {
            if (!_isActive)
            {
                return "Dynamic foothold wrapper is inactive on this map.";
            }

            string wzSummary = _hasWzFootholdRoot
                ? $"WZ foothold root present ({_footholdLayerCount} layers, {_footholdGroupCount} groups, {_footholdSegmentCount} segments)"
                : "WZ foothold root unavailable";
            string dynamicObjectSummary = _dynamicObjectCount > 0
                ? $"map contract has {_dynamicObjectCount} dynamic-tagged object nodes across {_dynamicObjectLayerCount} layers ({_dynamicEnabledObjectCount} enabled)"
                : "map contract has no dynamic-tagged object nodes";
            string runtimeSummary = dynamicFootholds?.DescribeClientOwnedWrapperState() ?? "platforms=0, active=0, visible=0, moving=0";
            string contractSummary = _usesLinkedContract
                ? $"contractMap={_contractMapId} via info/link from map {_mapId}"
                : _linkedContractMissing
                    ? $"contractMap unresolved (info/link={_linkedContractMapId}), using local shell map {_mapId}"
                    : $"contractMap={_contractMapId}";

            return $"Dynamic foothold: active | factory=0x{ClientFieldFactoryAddress:X} | owner={ClientOwnerName} | getFieldType=0x{ClientGetFieldTypeAddress:X} | init=0x{ClientInitStubAddress:X} | map={_mapId} | fieldType={(int)FieldType.FIELDTYPE_DYNAMICFOOTHOLD} | {contractSummary} | {wzSummary} | {dynamicObjectSummary} | runtime {runtimeSummary}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _contractMapId = 0;
            _linkedContractMapId = 0;
            _usesLinkedContract = false;
            _linkedContractMissing = false;
            _footholdLayerCount = 0;
            _footholdGroupCount = 0;
            _footholdSegmentCount = 0;
            _hasWzFootholdRoot = false;
            _dynamicObjectLayerCount = 0;
            _dynamicObjectCount = 0;
            _dynamicEnabledObjectCount = 0;
            _authoredDynamicObjectNames.Clear();
            _packetOwnedSnapshotDynamicObjectNames.Clear();
            _authoredDynamicObjectPlatformByName.Clear();
        }

        public bool TryResolveAuthoredDynamicObjectName(int platformId, out string name)
        {
            name = null;
            if (platformId < 0 || platformId >= _authoredDynamicObjectNames.Count)
            {
                return false;
            }

            name = _authoredDynamicObjectNames[platformId];
            return !string.IsNullOrWhiteSpace(name);
        }

        public bool TryResolvePacketOwnedSnapshotDynamicObjectName(int platformId, out string name)
        {
            name = null;
            if (platformId < 0 || platformId >= _packetOwnedSnapshotDynamicObjectNames.Count)
            {
                return false;
            }

            name = _packetOwnedSnapshotDynamicObjectNames[platformId];
            return !string.IsNullOrWhiteSpace(name);
        }

        public bool TryResolveAuthoredDynamicObjectPlatformId(string name, out int platformId)
        {
            platformId = -1;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalized = NormalizeDynamicObjectKey(name);
            if (normalized.Length == 0)
            {
                return false;
            }

            return _authoredDynamicObjectPlatformByName.TryGetValue(normalized, out platformId);
        }

        private void LoadMapContractSummary(MapInfo mapInfo, Func<int, WzImage> linkedMapResolver)
        {
            WzImage mapImage = mapInfo?.Image;
            if (mapImage == null)
            {
                return;
            }

            WzImage contractImage = ResolveContractImage(mapInfo, mapImage, linkedMapResolver);
            EnsureImageParsed(contractImage);

            if (contractImage["foothold"] is not WzImageProperty footholdRoot)
            {
                LoadDynamicObjectSummary(contractImage);
                return;
            }

            _hasWzFootholdRoot = true;
            if (footholdRoot.WzProperties == null)
            {
                LoadDynamicObjectSummary(contractImage);
                return;
            }

            foreach (WzImageProperty layer in footholdRoot.WzProperties)
            {
                _footholdLayerCount++;
                if (layer?.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty group in layer.WzProperties)
                {
                    _footholdGroupCount++;
                    _footholdSegmentCount += group?.WzProperties?.Count ?? 0;
                }
            }

            LoadDynamicObjectSummary(contractImage);
        }

        private WzImage ResolveContractImage(MapInfo mapInfo, WzImage mapImage, Func<int, WzImage> linkedMapResolver)
        {
            EnsureImageParsed(mapImage);

            if (!TryGetLinkedMapId(mapImage, out int linkedMapId))
            {
                return mapImage;
            }

            _linkedContractMapId = linkedMapId;
            WzImage linkedImage = linkedMapResolver?.Invoke(linkedMapId);
            if (linkedImage == null)
            {
                _linkedContractMissing = true;
                return mapImage;
            }

            EnsureImageParsed(linkedImage);
            _usesLinkedContract = true;
            _contractMapId = linkedMapId;
            return linkedImage;
        }

        private void LoadDynamicObjectSummary(WzImage mapImage)
        {
            if (mapImage?.WzProperties == null)
            {
                return;
            }

            foreach (WzImageProperty rootChild in mapImage.WzProperties)
            {
                if (rootChild?.Name == null
                    || rootChild.Name.Equals("info", StringComparison.OrdinalIgnoreCase)
                    || rootChild.WzProperties == null)
                {
                    continue;
                }

                if (rootChild["obj"] is not WzImageProperty objRoot || objRoot.WzProperties == null)
                {
                    continue;
                }

                bool layerHasDynamicObject = false;
                foreach (WzImageProperty mapObject in objRoot.WzProperties)
                {
                    if (mapObject?["dynamic"] is not WzImageProperty dynamicProperty)
                    {
                        continue;
                    }

                    layerHasDynamicObject = true;
                    _dynamicObjectCount++;
                    int platformId = _authoredDynamicObjectNames.Count;
                    string resolvedName = ResolveDynamicObjectName(rootChild, mapObject, platformId);
                    string packetOwnedSnapshotName = ResolvePacketOwnedSnapshotDynamicObjectName(rootChild, mapObject, resolvedName);
                    _authoredDynamicObjectNames.Add(resolvedName);
                    _packetOwnedSnapshotDynamicObjectNames.Add(packetOwnedSnapshotName);
                    RegisterDynamicObjectAliases(rootChild, mapObject, resolvedName, platformId);

                    if (TryReadDynamicFlag(dynamicProperty, out int dynamicFlag) && dynamicFlag != 0)
                    {
                        _dynamicEnabledObjectCount++;
                    }
                }

                if (layerHasDynamicObject)
                {
                    _dynamicObjectLayerCount++;
                }
            }
        }

        private static void EnsureImageParsed(WzImage image)
        {
            if (image != null && !image.Parsed)
            {
                image.ParseImage();
            }
        }

        private static bool TryGetLinkedMapId(WzImage mapImage, out int linkedMapId)
        {
            linkedMapId = 0;
            if (mapImage?["info"]?["link"] is not WzStringProperty linkProperty)
            {
                return false;
            }

            return int.TryParse(linkProperty.Value, out linkedMapId) && linkedMapId > 0;
        }

        private static bool TryReadDynamicFlag(WzImageProperty dynamicProperty, out int dynamicFlag)
        {
            dynamicFlag = 0;
            if (dynamicProperty is WzIntProperty intProperty)
            {
                dynamicFlag = intProperty.Value;
                return true;
            }

            return false;
        }

        private void RegisterAuthoredDynamicObjectName(string name, int platformId)
        {
            if (platformId < 0 || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            string normalized = NormalizeDynamicObjectKey(name);
            if (normalized.Length == 0 || _authoredDynamicObjectPlatformByName.ContainsKey(normalized))
            {
                return;
            }

            _authoredDynamicObjectPlatformByName[normalized] = platformId;
        }

        private void RegisterDynamicObjectAliases(WzImageProperty layer, WzImageProperty mapObject, string resolvedName, int platformId)
        {
            if (platformId < 0)
            {
                return;
            }

            string objectKeyName = TryResolveObjectKeyName(mapObject, out string candidateObjectKeyName)
                ? candidateObjectKeyName
                : null;
            int? piece = TryReadIntProperty(mapObject, "piece", out int pieceValue)
                ? pieceValue
                : null;
            int? x = TryReadIntProperty(mapObject, "x", out int xValue)
                ? xValue
                : null;
            int? y = TryReadIntProperty(mapObject, "y", out int yValue)
                ? yValue
                : null;

            foreach (string alias in BuildDynamicObjectAliasCandidates(resolvedName, objectKeyName, layer?.Name, mapObject?.Name, piece, x, y))
            {
                RegisterAuthoredDynamicObjectName(alias, platformId);
            }
        }

        private static string NormalizeDynamicObjectKey(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : name.Trim().Replace('\\', '/');
        }

        private static IReadOnlyList<string> BuildDynamicObjectAliasCandidates(
            string resolvedName,
            string objectKeyName,
            string layerName,
            string objectName,
            int? piece,
            int? x,
            int? y)
        {
            HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
            AddDynamicObjectAlias(aliases, resolvedName);
            AddDynamicObjectAlias(aliases, objectKeyName);

            string layerObjectAlias = BuildLayerObjectAlias(layerName, objectName);
            AddDynamicObjectAlias(aliases, layerObjectAlias);
            AddCoordinateAliases(aliases, objectKeyName, x, y);
            AddCoordinateAliases(aliases, layerObjectAlias, x, y);

            if (piece is int pieceValue && pieceValue >= 0)
            {
                string pieceSuffix = pieceValue.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(objectKeyName))
                {
                    AddDynamicObjectAlias(aliases, $"{objectKeyName}/{pieceSuffix}");
                    AddDynamicObjectAlias(aliases, $"{objectKeyName}/piece/{pieceSuffix}");
                    AddCoordinateAliases(aliases, $"{objectKeyName}/{pieceSuffix}", x, y);
                    AddCoordinateAliases(aliases, $"{objectKeyName}/piece/{pieceSuffix}", x, y);
                }

                if (!string.IsNullOrWhiteSpace(layerObjectAlias))
                {
                    AddDynamicObjectAlias(aliases, $"{layerObjectAlias}/{pieceSuffix}");
                    AddDynamicObjectAlias(aliases, $"{layerObjectAlias}/piece/{pieceSuffix}");
                    AddCoordinateAliases(aliases, $"{layerObjectAlias}/{pieceSuffix}", x, y);
                    AddCoordinateAliases(aliases, $"{layerObjectAlias}/piece/{pieceSuffix}", x, y);
                }
            }

            List<string> aliasList = new(aliases.Count);
            foreach (string alias in aliases)
            {
                aliasList.Add(alias);
            }

            return aliasList;
        }

        private static void AddDynamicObjectAlias(ISet<string> aliases, string candidate)
        {
            string normalized = NormalizeDynamicObjectKey(candidate);
            if (normalized.Length > 0)
            {
                aliases.Add(normalized);
            }
        }

        private static void AddCoordinateAliases(ISet<string> aliases, string baseAlias, int? x, int? y)
        {
            if (string.IsNullOrWhiteSpace(baseAlias) || x is not int xValue || y is not int yValue)
            {
                return;
            }

            string xToken = xValue.ToString(CultureInfo.InvariantCulture);
            string yToken = yValue.ToString(CultureInfo.InvariantCulture);
            AddDynamicObjectAlias(aliases, $"{baseAlias}/{xToken}/{yToken}");
            AddDynamicObjectAlias(aliases, $"{baseAlias}/{xToken},{yToken}");
        }

        private static string BuildLayerObjectAlias(string layerName, string objectName)
        {
            if (string.IsNullOrWhiteSpace(layerName) || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            return $"{layerName.Trim()}/{objectName.Trim()}";
        }

        private static string ResolveDynamicObjectName(WzImageProperty layer, WzImageProperty mapObject, int fallbackIndex)
        {
            if (TryReadStringProperty(mapObject, "name", out string name)
                || TryReadStringProperty(mapObject, "objName", out name)
                || TryReadStringProperty(mapObject, "tag", out name))
            {
                return name;
            }

            if (TryResolveObjectKeyName(mapObject, out string objectKeyName))
            {
                int? piece = TryReadIntProperty(mapObject, "piece", out int pieceValue)
                    ? pieceValue
                    : null;
                int? x = TryReadIntProperty(mapObject, "x", out int xValue)
                    ? xValue
                    : null;
                int? y = TryReadIntProperty(mapObject, "y", out int yValue)
                    ? yValue
                    : null;
                return BuildCanonicalObjectKeyName(objectKeyName, piece, x, y);
            }

            string layerName = layer?.Name ?? "layer";
            string objectName = mapObject?.Name ?? fallbackIndex.ToString(CultureInfo.InvariantCulture);
            return $"dynamic-{layerName}-{objectName}";
        }

        private static string ResolvePacketOwnedSnapshotDynamicObjectName(WzImageProperty layer, WzImageProperty mapObject, string fallbackName)
        {
            if (TryResolveObjectKeyName(mapObject, out string objectKeyName))
            {
                int? piece = TryReadIntProperty(mapObject, "piece", out int pieceValue)
                    ? pieceValue
                    : null;
                int? x = TryReadIntProperty(mapObject, "x", out int xValue)
                    ? xValue
                    : null;
                int? y = TryReadIntProperty(mapObject, "y", out int yValue)
                    ? yValue
                    : null;
                string packetOwnedName = BuildPacketOwnedSnapshotObjectKeyName(objectKeyName, piece, x, y);
                if (!string.IsNullOrWhiteSpace(packetOwnedName))
                {
                    return packetOwnedName;
                }
            }

            return string.IsNullOrWhiteSpace(fallbackName)
                ? ResolveDynamicObjectName(layer, mapObject, fallbackIndex: 0)
                : fallbackName;
        }

        private static string BuildCanonicalObjectKeyName(string objectKeyName, int? piece, int? x, int? y)
        {
            string canonicalName = NormalizeDynamicObjectKey(objectKeyName);
            if (canonicalName.Length == 0)
            {
                return canonicalName;
            }

            if (piece is int pieceValue && pieceValue >= 0)
            {
                canonicalName = $"{canonicalName}/piece/{pieceValue.ToString(CultureInfo.InvariantCulture)}";
            }

            if (x is int xValue && y is int yValue)
            {
                canonicalName = $"{canonicalName}/{xValue.ToString(CultureInfo.InvariantCulture)},{yValue.ToString(CultureInfo.InvariantCulture)}";
            }

            return canonicalName;
        }

        internal static string BuildPacketOwnedSnapshotObjectKeyName(string objectKeyName, int? piece, int? x, int? y)
        {
            string packetOwnedName = NormalizeDynamicObjectKey(objectKeyName);
            if (packetOwnedName.Length == 0)
            {
                return packetOwnedName;
            }

            if (piece is int pieceValue && pieceValue > 0)
            {
                packetOwnedName = $"{packetOwnedName}/{pieceValue.ToString(CultureInfo.InvariantCulture)}";
            }

            if (x is int xValue && y is int yValue)
            {
                packetOwnedName = $"{packetOwnedName}/{xValue.ToString(CultureInfo.InvariantCulture)},{yValue.ToString(CultureInfo.InvariantCulture)}";
            }

            return packetOwnedName;
        }

        private static bool TryResolveObjectKeyName(WzImageProperty mapObject, out string name)
        {
            name = null;
            if (!TryReadTokenProperty(mapObject, "oS", out string objectSet)
                || !TryReadTokenProperty(mapObject, "l0", out string layer0)
                || !TryReadTokenProperty(mapObject, "l1", out string layer1)
                || !TryReadTokenProperty(mapObject, "l2", out string layer2))
            {
                return false;
            }

            name = $"{objectSet}/{layer0}/{layer1}/{layer2}";
            return true;
        }

        private static bool TryReadStringProperty(WzImageProperty parent, string propertyName, out string value)
        {
            value = null;
            if (parent?[propertyName] is not WzStringProperty stringProperty || string.IsNullOrWhiteSpace(stringProperty.Value))
            {
                return false;
            }

            value = stringProperty.Value.Trim();
            return true;
        }

        private static bool TryReadIntProperty(WzImageProperty parent, string propertyName, out int value)
        {
            value = 0;
            if (parent?[propertyName] is not WzImageProperty property)
            {
                return false;
            }

            switch (property)
            {
                case WzIntProperty intProperty:
                    value = intProperty.Value;
                    return true;
                case WzShortProperty shortProperty:
                    value = shortProperty.Value;
                    return true;
                case WzLongProperty longProperty when longProperty.Value >= int.MinValue && longProperty.Value <= int.MaxValue:
                    value = (int)longProperty.Value;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadTokenProperty(WzImageProperty parent, string propertyName, out string value)
        {
            value = null;
            if (parent?[propertyName] is not WzImageProperty property)
            {
                return false;
            }

            switch (property)
            {
                case WzStringProperty stringProperty when !string.IsNullOrWhiteSpace(stringProperty.Value):
                    value = stringProperty.Value.Trim();
                    break;
                case WzIntProperty intProperty:
                    value = intProperty.Value.ToString(CultureInfo.InvariantCulture);
                    break;
                case WzShortProperty shortProperty:
                    value = shortProperty.Value.ToString(CultureInfo.InvariantCulture);
                    break;
                case WzLongProperty longProperty:
                    value = longProperty.Value.ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    return false;
            }

            return value.Length > 0;
        }
    }
}
