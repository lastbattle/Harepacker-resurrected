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

            return $"Dynamic foothold: active | owner={ClientOwnerName} | getFieldType=0x{ClientGetFieldTypeAddress:X} | init=0x{ClientInitStubAddress:X} | map={_mapId} | fieldType={(int)FieldType.FIELDTYPE_DYNAMICFOOTHOLD} | {contractSummary} | {wzSummary} | {dynamicObjectSummary} | runtime {runtimeSummary}";
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
                    _authoredDynamicObjectNames.Add(resolvedName);
                    RegisterAuthoredDynamicObjectName(resolvedName, platformId);

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

        private static string NormalizeDynamicObjectKey(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : name.Trim().Replace('\\', '/');
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
                return objectKeyName;
            }

            string layerName = layer?.Name ?? "layer";
            string objectName = mapObject?.Name ?? fallbackIndex.ToString(CultureInfo.InvariantCulture);
            return $"dynamic-{layerName}-{objectName}";
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
