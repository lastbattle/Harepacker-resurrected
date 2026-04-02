using MapleLib.WzLib;
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

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int FootholdLayerCount => _footholdLayerCount;
        public int FootholdGroupCount => _footholdGroupCount;
        public int FootholdSegmentCount => _footholdSegmentCount;
        public bool HasWzFootholdRoot => _hasWzFootholdRoot;

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
            LoadWzFootholdSummary(mapInfo);
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
            string runtimeSummary = dynamicFootholds?.DescribeClientOwnedWrapperState() ?? "platforms=0, active=0, visible=0, moving=0";

            return $"Dynamic foothold: active | owner={ClientOwnerName} | map={_mapId} | fieldType={(int)FieldType.FIELDTYPE_DYNAMICFOOTHOLD} | {wzSummary} | runtime {runtimeSummary}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _footholdLayerCount = 0;
            _footholdGroupCount = 0;
            _footholdSegmentCount = 0;
            _hasWzFootholdRoot = false;
        }

        private void LoadWzFootholdSummary(MapInfo mapInfo)
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
                return;
            }

            _hasWzFootholdRoot = true;
            if (footholdRoot.WzProperties == null)
            {
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
        }
    }
}
