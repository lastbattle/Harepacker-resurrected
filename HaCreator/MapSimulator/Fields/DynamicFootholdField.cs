using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;

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
        private int _dynamicObjectLayerCount;
        private int _dynamicObjectCount;
        private int _dynamicEnabledObjectCount;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int FootholdLayerCount => _footholdLayerCount;
        public int FootholdGroupCount => _footholdGroupCount;
        public int FootholdSegmentCount => _footholdSegmentCount;
        public bool HasWzFootholdRoot => _hasWzFootholdRoot;
        public int DynamicObjectLayerCount => _dynamicObjectLayerCount;
        public int DynamicObjectCount => _dynamicObjectCount;
        public int DynamicEnabledObjectCount => _dynamicEnabledObjectCount;

        public void Configure(MapInfo mapInfo, DynamicFootholdSystem dynamicFootholds)
        {
            dynamicFootholds?.ResetForClientOwnedWrapper();
            Reset();

            if (mapInfo?.fieldType != FieldType.FIELDTYPE_DYNAMICFOOTHOLD)
            {
                return;
            }

            _isActive = true;
            _mapId = mapInfo.id;
            LoadMapContractSummary(mapInfo);
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

            return $"Dynamic foothold: active | owner={ClientOwnerName} | map={_mapId} | fieldType={(int)FieldType.FIELDTYPE_DYNAMICFOOTHOLD} | {wzSummary} | {dynamicObjectSummary} | runtime {runtimeSummary}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _footholdLayerCount = 0;
            _footholdGroupCount = 0;
            _footholdSegmentCount = 0;
            _hasWzFootholdRoot = false;
            _dynamicObjectLayerCount = 0;
            _dynamicObjectCount = 0;
            _dynamicEnabledObjectCount = 0;
        }

        private void LoadMapContractSummary(MapInfo mapInfo)
        {
            WzImage mapImage = mapInfo?.Image;
            if (mapImage == null)
            {
                return;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            if (mapImage["foothold"] is not WzImageProperty footholdRoot)
            {
                LoadDynamicObjectSummary(mapImage);
                return;
            }

            _hasWzFootholdRoot = true;
            if (footholdRoot.WzProperties == null)
            {
                LoadDynamicObjectSummary(mapImage);
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

            LoadDynamicObjectSummary(mapImage);
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
    }
}
