using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Collections.Generic;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator
{
    internal enum TransportationWrapperKind
    {
        None,
        Transit,
        Balrog
    }

    internal readonly struct TransportationWrapperContract
    {
        public TransportationWrapperContract(
            TransportationWrapperKind kind,
            string sourceDescription,
            int dockX,
            int dockY,
            int awayX,
            int flip,
            int moveDurationSeconds,
            int shipKind,
            string shipObjectPath)
        {
            Kind = kind;
            SourceDescription = sourceDescription ?? string.Empty;
            DockX = dockX;
            DockY = dockY;
            AwayX = awayX;
            Flip = flip;
            MoveDurationSeconds = moveDurationSeconds;
            ShipKind = shipKind;
            ShipObjectPath = shipObjectPath ?? string.Empty;
        }

        public TransportationWrapperKind Kind { get; }
        public string SourceDescription { get; }
        public int DockX { get; }
        public int DockY { get; }
        public int AwayX { get; }
        public int Flip { get; }
        public int MoveDurationSeconds { get; }
        public int ShipKind { get; }
        public string ShipObjectPath { get; }
        public bool IsActive => Kind != TransportationWrapperKind.None;
    }

    internal static class TransportationFieldWrapperContractBuilder
    {
        public static bool TryCreate(MapInfo mapInfo, out TransportationWrapperContract contract)
        {
            contract = default;
            if (mapInfo == null)
            {
                return false;
            }

            TransportationWrapperKind kind = mapInfo.fieldType == FieldType.FIELDTYPE_BALROG
                ? TransportationWrapperKind.Balrog
                : mapInfo.fieldType == FieldType.FIELDTYPE_CONTIMOVE
                    ? TransportationWrapperKind.Transit
                    : TransportationWrapperKind.None;

            if (!TransportationFieldDefinitionLoader.TryCreate(mapInfo, out TransportationFieldDefinition definition))
            {
                if (kind == TransportationWrapperKind.None)
                {
                    return false;
                }

                string fallbackOwner = kind == TransportationWrapperKind.Balrog
                    ? $"client owner CField_Balrog::GetFieldType = {(int)FieldType.FIELDTYPE_BALROG} ({FieldType.FIELDTYPE_BALROG})"
                    : $"client owner CField_ContiMove via fieldType {(int)FieldType.FIELDTYPE_CONTIMOVE} ({FieldType.FIELDTYPE_CONTIMOVE})";
                contract = new TransportationWrapperContract(
                    kind,
                    $"{fallbackOwner}, but no WZ shipObj payload is available on this map",
                    0,
                    0,
                    0,
                    0,
                    0,
                    kind == TransportationWrapperKind.Balrog ? 1 : 0,
                    string.Empty);
                return true;
            }

            if (kind == TransportationWrapperKind.None)
            {
                kind = definition.ShipKind == 1
                    ? TransportationWrapperKind.Balrog
                    : TransportationWrapperKind.Transit;
            }

            string ownerDescription = kind == TransportationWrapperKind.Balrog
                ? $"client owner CField_Balrog::GetFieldType = {(int)FieldType.FIELDTYPE_BALROG} ({FieldType.FIELDTYPE_BALROG})"
                : $"client owner CField_ContiMove via fieldType {(int)FieldType.FIELDTYPE_CONTIMOVE} ({FieldType.FIELDTYPE_CONTIMOVE})";
            string payloadDescription = string.IsNullOrWhiteSpace(definition.ShipObjectPath)
                ? "WZ shipObj payload without an object path"
                : $"WZ shipObj payload path {definition.ShipObjectPath}";
            contract = new TransportationWrapperContract(
                kind,
                $"{ownerDescription}, {payloadDescription}",
                definition.DockX,
                definition.DockY,
                definition.AwayX,
                definition.Flip,
                definition.MoveDurationSeconds,
                definition.ShipKind,
                definition.ShipObjectPath);
            return true;
        }
    }

    public partial class MapSimulator
    {
        internal enum TutorialWrapperKind
        {
            None,
            Tutorial,
            AranTutorial
        }

        internal readonly struct TutorialWrapperContract
        {
            public TutorialWrapperContract(TutorialWrapperKind kind, string sourceDescription)
            {
                Kind = kind;
                SourceDescription = sourceDescription ?? string.Empty;
            }

            public TutorialWrapperKind Kind { get; }
            public string SourceDescription { get; }
            public bool IsActive => Kind != TutorialWrapperKind.None;
        }

        internal readonly struct TutorialAppearanceContract
        {
            public TutorialAppearanceContract(
                TutorialWrapperKind kind,
                string sourceDescription,
                int capItemId,
                int clothesItemId,
                int capeItemId,
                int shoesItemId,
                int weaponItemId,
                bool preserveActiveWeapon)
            {
                Kind = kind;
                SourceDescription = sourceDescription ?? string.Empty;
                CapItemId = Math.Max(0, capItemId);
                ClothesItemId = Math.Max(0, clothesItemId);
                CapeItemId = Math.Max(0, capeItemId);
                ShoesItemId = Math.Max(0, shoesItemId);
                WeaponItemId = Math.Max(0, weaponItemId);
                PreserveActiveWeapon = preserveActiveWeapon;
            }

            public TutorialWrapperKind Kind { get; }
            public string SourceDescription { get; }
            public int CapItemId { get; }
            public int ClothesItemId { get; }
            public int CapeItemId { get; }
            public int ShoesItemId { get; }
            public int WeaponItemId { get; }
            public bool PreserveActiveWeapon { get; }

            public bool HasAnyAppearanceItem =>
                CapItemId > 0
                || ClothesItemId > 0
                || CapeItemId > 0
                || ShoesItemId > 0
                || WeaponItemId > 0;
        }

        internal enum WeddingPhotoWrapperKind
        {
            None,
            SceneOwner,
            PhotoContract
        }

        internal readonly struct WeddingPhotoSceneContract
        {
            public WeddingPhotoSceneContract(
                WeddingPhotoWrapperKind kind,
                string sourceDescription,
                string sceneDescription,
                string backgroundMusicPath,
                int returnMapId,
                int side,
                int top,
                int bottom,
                int viewportLeft,
                int viewportTop,
                int viewportRight,
                int viewportBottom)
            {
                Kind = kind;
                SourceDescription = sourceDescription ?? string.Empty;
                SceneDescription = sceneDescription ?? string.Empty;
                BackgroundMusicPath = backgroundMusicPath ?? string.Empty;
                ReturnMapId = returnMapId;
                Side = Math.Max(0, side);
                Top = Math.Max(0, top);
                Bottom = Math.Max(0, bottom);
                ViewportLeft = viewportLeft;
                ViewportTop = viewportTop;
                ViewportRight = viewportRight;
                ViewportBottom = viewportBottom;
            }

            public WeddingPhotoWrapperKind Kind { get; }
            public string SourceDescription { get; }
            public string SceneDescription { get; }
            public string BackgroundMusicPath { get; }
            public int ReturnMapId { get; }
            public int Side { get; }
            public int Top { get; }
            public int Bottom { get; }
            public int ViewportLeft { get; }
            public int ViewportTop { get; }
            public int ViewportRight { get; }
            public int ViewportBottom { get; }
            public bool HasSafeArea => Side > 0 || Top > 0 || Bottom > 0;
            public bool HasViewport => ViewportRight > ViewportLeft && ViewportBottom > ViewportTop;
        }

        private const int ClientOwnedAranTutorialFieldType = 22;
        private const int ClientOwnedWeddingPhotoFieldType = 61;
        private const int TutorialForcedCapItemId = 1002562;
        private const int TutorialForcedLongcoatItemId = 1052081;
        private const int ShowaBathMaleLongcoatItemId = 1050100;
        private const int ShowaBathFemaleLongcoatItemId = 1051098;
        private const string ClientOwnedLimitedViewWzPath = "Viewrange/0";
        private const string AranTutorialOnUserEnterPrefix = "aranTutor";
        private const string AranTutorialMapMark = "BlackDragon";
        private const string WeddingMapMark = "Wedding";
        private const string WeddingPhotoSceneOnUserEnter = "pledgeEnter";
        private const int AranTutorialMapIdMin = 914000000;
        private const int AranTutorialMapIdMax = 914000500;
        private const int WeddingPhotoMapRegionPrefix = 680000;
        private const int ChaosZakumPortalSessionFallbackFieldId = 180000002;
        private const int ClientOwnedLimitedViewDarkCanvasWidth = 1024;
        private const int ClientOwnedLimitedViewDarkCanvasHeight = 768;
        private const int ClientOwnedLimitedViewDarkLayerOffsetX = -512;
        private const int ClientOwnedLimitedViewDarkLayerOffsetY = -468;
        private const float ClientOwnedLimitedViewFallbackRadius = 158f;
        private const float ClientOwnedLimitedViewFallbackMaskWidth = 316f;
        private const float ClientOwnedLimitedViewFallbackMaskHeight = 316f;
        private const float ClientOwnedLimitedViewFallbackOriginX = 158f;
        private const float ClientOwnedLimitedViewFallbackOriginY = 179f;
        private const int EscortFailOverlayDurationMs = 2500;
        private readonly DynamicFootholdField _dynamicFootholdField = new();
        private bool _tutorialAppearanceOverrideApplied;
        private CharacterBuild _tutorialAppearanceOverrideBuild;
        private Dictionary<EquipSlot, CharacterPart> _tutorialEquipmentSnapshot;
        private Dictionary<EquipSlot, CharacterPart> _tutorialHiddenEquipmentSnapshot;
        private bool _tutorialFieldSpecificAppearanceArmed;
        private int _tutorialFieldSpecificAppearanceMapId = int.MinValue;
        private bool _showaBathAppearanceOverrideApplied;
        private CharacterBuild _showaBathAppearanceOverrideBuild;
        private Dictionary<EquipSlot, CharacterPart> _showaBathEquipmentSnapshot;
        private Dictionary<EquipSlot, CharacterPart> _showaBathHiddenEquipmentSnapshot;
        private bool _coconutAppearanceOverrideApplied;
        private CharacterBuild _coconutAppearanceOverrideBuild;
        private Dictionary<EquipSlot, CharacterPart> _coconutEquipmentSnapshot;
        private Dictionary<EquipSlot, CharacterPart> _coconutHiddenEquipmentSnapshot;
        private int? _killCountWrapperValue;
        private int _escortFailOverlayUntilTick = int.MinValue;
        private bool _clientOwnedLimitedViewMetadataLoaded;
        private float _clientOwnedLimitedViewRadius = ClientOwnedLimitedViewFallbackRadius;
        private float _clientOwnedLimitedViewMaskWidth = ClientOwnedLimitedViewFallbackMaskWidth;
        private float _clientOwnedLimitedViewMaskHeight = ClientOwnedLimitedViewFallbackMaskHeight;
        private float _clientOwnedLimitedViewOriginX = ClientOwnedLimitedViewFallbackOriginX;
        private float _clientOwnedLimitedViewOriginY = ClientOwnedLimitedViewFallbackOriginY;
        private bool _clientOwnedLimitedViewShareView;
        private TutorialWrapperKind _activeTutorialWrapperKind;
        private WeddingPhotoSceneContract? _activeWeddingPhotoSceneContract;
        private bool _wrapperOwnedAranTutorActorApplied;

        private void ApplyClientOwnedFieldWrappers()
        {
            MapInfo mapInfo = _mapBoard?.MapInfo;
            ConfigureDynamicFootholdFieldWrapper(mapInfo);
            ConfigureClientOwnedLimitedView(mapInfo);
            ConfigureNoDragonPresentation(mapInfo);
            ApplyTransitAndVoyageFieldWrapper(mapInfo);
            ApplyTutorialFieldAppearance(mapInfo);
            SyncWeddingPhotoFieldWrapper(mapInfo);
            SyncClientOwnedResultFieldWrappers(mapInfo);
            TryApplyClientOwnedWeddingPhotoSceneCameraLock();
        }

        private void SyncClientOwnedResultFieldWrappers(MapInfo mapInfo)
        {
            if (mapInfo != null && mapInfo.fieldType == FieldType.FIELDTYPE_HUNTINGADBALLOON)
            {
                _specialFieldRuntime.PartyRaid.BindClientOwnedBossOverlay(mapInfo, "Hunting Ad Balloon");
            }

            if (!IsKillCountWrapperMap(mapInfo))
            {
                _killCountWrapperValue = null;
            }

            if (!IsEscortResultWrapperMap(mapInfo))
            {
                _escortFailOverlayUntilTick = int.MinValue;
            }

            CharacterBuild build = _playerManager?.Player?.Build;
            if (!IsShowaBathWrapperMap(mapInfo))
            {
                RestoreShowaBathFieldAppearance(build);
            }
            else
            {
                CharacterLoader loader = _playerManager?.Loader;
                if (loader != null && build != null)
                {
                    ApplyShowaBathFieldAppearance(build, loader);
                }
            }

            SyncCoconutFieldAppearance(build);
        }

        private string HandleClientOwnedFieldSpecificDataPacket(byte[] payload, int currentTick)
        {
            return _specialFieldRuntime.TryDispatchCurrentWrapperFieldSpecificData(
                TryApplyShowaBathFieldSpecificPresentationOwner,
                out string message)
                ? message
                : message;
        }

        private bool TryApplyClientOwnedWrapperFieldValue(string wrapperName, string key, string value, int currentTick, out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(wrapperName) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (string.Equals(wrapperName, "huntingadballoon", StringComparison.OrdinalIgnoreCase))
            {
                bool applied = _specialFieldRuntime.TryDispatchCurrentWrapperFieldValue(key, value, currentTick, out message);
                return applied;
            }

            if (string.Equals(wrapperName, "escortresult", StringComparison.OrdinalIgnoreCase))
            {
                bool applied = _specialFieldRuntime.TryDispatchCurrentWrapperFieldValue(key, value, currentTick, out message);
                return applied;
            }

            if (string.Equals(wrapperName, "limitedview", StringComparison.OrdinalIgnoreCase)
                && TryApplyClientOwnedLimitedViewFieldValue(key, value, out message))
            {
                return true;
            }

            return false;
        }

        private bool TryApplyClientOwnedLimitedViewFieldValue(string key, string value, out string message)
        {
            message = null;
            if (!IsLimitedViewWrapperMap(_mapBoard?.MapInfo))
            {
                message = "limited-view wrapper is inactive.";
                return false;
            }

            if (!string.Equals(key?.Trim(), "shareview", StringComparison.OrdinalIgnoreCase))
            {
                message = "limited-view field-value key is unsupported (expected shareview).";
                return false;
            }

            if (!TryParseClientOwnedBooleanToken(value, out bool shareView))
            {
                message = "limited-view shareview value must be true/false, on/off, yes/no, or 1/0.";
                return false;
            }

            _clientOwnedLimitedViewShareView = shareView;
            _limitedViewField.SetClientOwnedShareView(shareView);
            message = $"limited-view shareview set to {(shareView ? "on" : "off")} (CField_LimitedView::m_bShareView).";
            return true;
        }

        private static bool TryParseClientOwnedBooleanToken(string token, out bool value)
        {
            value = false;
            if (token == null)
            {
                return false;
            }

            string normalized = token.Trim();
            if (normalized.Length == 0)
            {
                return false;
            }

            if (bool.TryParse(normalized, out value))
            {
                return true;
            }

            switch (normalized.ToLowerInvariant())
            {
                case "1":
                case "on":
                case "yes":
                case "y":
                    value = true;
                    return true;
                case "0":
                case "off":
                case "no":
                case "n":
                    value = false;
                    return true;
                default:
                    return false;
            }
        }

        private bool TryApplyClientOwnedWrapperSessionValue(string wrapperName, string key, string value, out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(wrapperName) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (string.Equals(wrapperName, "chaoszakum", StringComparison.OrdinalIgnoreCase)
                && _specialFieldRuntime.TryDispatchCurrentWrapperSessionValue(key, value, out message))
            {
                return true;
            }

            return false;
        }

        private (bool Applied, string Message) TryApplyShowaBathFieldSpecificPresentationOwner()
        {
            CharacterBuild build = _playerManager?.Player?.Build;
            CharacterLoader loader = _playerManager?.Loader;
            if (build == null || loader == null)
            {
                return (false, "showa-bath wrapper pending character loader/build");
            }

            ApplyShowaBathFieldAppearance(build, loader);
            return (true, "showa-bath appearance override applied");
        }

        internal static bool IsChaosZakumPortalSessionWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_CHAOSZAKUM
                || mapInfo?.id == ChaosZakumPortalSessionFallbackFieldId;
        }

        internal static bool IsChaosZakumPortalSessionKey(string key)
        {
            return string.Equals(key?.Trim(), "fire", StringComparison.Ordinal);
        }

        private void DrawClientOwnedResultFieldWrappers(int currentTick)
        {
            DrawKillCountWrapperOverlay();
            DrawEscortResultFailOverlay(currentTick);
        }

        private void DrawKillCountWrapperOverlay()
        {
            if (_killCountWrapperValue is not int killCount
                || !IsKillCountWrapperMap(_mapBoard?.MapInfo)
                || _spriteBatch == null
                || _fontDebugValues == null)
            {
                return;
            }

            string overlayText = $"KILL COUNT {killCount}";
            Vector2 textSize = _fontDebugValues.MeasureString(overlayText);
            int padding = 10;
            int boxWidth = (int)Math.Ceiling(textSize.X) + (padding * 2);
            int boxHeight = (int)Math.Ceiling(textSize.Y) + (padding * 2);
            int boxX = (Width - boxWidth) / 2;
            int boxY = 18;

            if (_debugBoundaryTexture != null)
            {
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), Color.Black * 0.78f);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, boxWidth, 2), Color.Gold * 0.9f);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY + boxHeight - 2, boxWidth, 2), Color.Gold * 0.9f);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, 2, boxHeight), Color.Gold * 0.9f);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX + boxWidth - 2, boxY, 2, boxHeight), Color.Gold * 0.9f);
            }

            Vector2 textPos = new(boxX + padding, boxY + padding);
            _spriteBatch.DrawString(_fontDebugValues, overlayText, textPos + Vector2.One, Color.Black);
            _spriteBatch.DrawString(_fontDebugValues, overlayText, textPos, Color.White);
        }

        private void DrawEscortResultFailOverlay(int currentTick)
        {
            if (_escortFailOverlayUntilTick == int.MinValue
                || currentTick >= _escortFailOverlayUntilTick
                || !IsEscortResultWrapperMap(_mapBoard?.MapInfo)
                || _spriteBatch == null
                || _debugBoundaryTexture == null)
            {
                return;
            }

            float progress = Math.Clamp((_escortFailOverlayUntilTick - currentTick) / (float)EscortFailOverlayDurationMs, 0f, 1f);
            Color overlayColor = new Color(110, 0, 0) * (0.28f + (progress * 0.18f));
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(0, 0, Width, Height), overlayColor);

            if (_fontDebugValues == null)
            {
                return;
            }

            const string failText = "FAIL";
            Vector2 textSize = _fontDebugValues.MeasureString(failText) * 2f;
            Vector2 textPos = new((Width - textSize.X) * 0.5f, (Height - textSize.Y) * 0.25f);
            _spriteBatch.DrawString(_fontDebugValues, failText, textPos + new Vector2(2f, 2f), Color.Black, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_fontDebugValues, failText, textPos, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        }

        private void ApplyShowaBathFieldAppearance(CharacterBuild build, CharacterLoader loader)
        {
            int longcoatItemId = build.Gender == CharacterGender.Female
                ? ShowaBathFemaleLongcoatItemId
                : ShowaBathMaleLongcoatItemId;
            CharacterPart forcedLongcoat = loader.LoadEquipment(longcoatItemId);
            if (forcedLongcoat == null)
            {
                return;
            }

            if (!_showaBathAppearanceOverrideApplied || !ReferenceEquals(_showaBathAppearanceOverrideBuild, build))
            {
                _showaBathEquipmentSnapshot = new Dictionary<EquipSlot, CharacterPart>(build.Equipment);
                _showaBathHiddenEquipmentSnapshot = new Dictionary<EquipSlot, CharacterPart>(build.HiddenEquipment);
                _showaBathAppearanceOverrideBuild = build;
                _showaBathAppearanceOverrideApplied = true;
            }

            build.Equipment.Clear();
            build.HiddenEquipment.Clear();
            build.Equip(forcedLongcoat);
        }

        private void RestoreShowaBathFieldAppearance(CharacterBuild build)
        {
            if (!_showaBathAppearanceOverrideApplied)
            {
                return;
            }

            if (build != null && ReferenceEquals(_showaBathAppearanceOverrideBuild, build))
            {
                build.Equipment.Clear();
                build.HiddenEquipment.Clear();

                if (_showaBathEquipmentSnapshot != null)
                {
                    foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _showaBathEquipmentSnapshot)
                    {
                        build.Equipment[entry.Key] = entry.Value;
                    }
                }

                if (_showaBathHiddenEquipmentSnapshot != null)
                {
                    foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _showaBathHiddenEquipmentSnapshot)
                    {
                        build.HiddenEquipment[entry.Key] = entry.Value;
                    }
                }
            }

            _showaBathAppearanceOverrideApplied = false;
            _showaBathAppearanceOverrideBuild = null;
            _showaBathEquipmentSnapshot = null;
            _showaBathHiddenEquipmentSnapshot = null;
        }

        private static bool MatchesEscortFailKey(string key)
        {
            return string.Equals(key, "result", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "state", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "0x1767", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "1767", StringComparison.OrdinalIgnoreCase);
        }

        private void SyncCoconutFieldAppearance(CharacterBuild build)
        {
            CoconutField coconut = _specialFieldRuntime?.Minigames?.Coconut;
            if (build == null || coconut?.IsActive != true)
            {
                RestoreCoconutFieldAppearance(build);
                return;
            }

            IReadOnlyDictionary<EquipSlot, CharacterPart> baseEquipment =
                _coconutAppearanceOverrideApplied
                && ReferenceEquals(_coconutAppearanceOverrideBuild, build)
                && _coconutEquipmentSnapshot != null
                    ? _coconutEquipmentSnapshot
                    : build.Equipment;
            IReadOnlyDictionary<EquipSlot, CharacterPart> baseHiddenEquipment =
                _coconutAppearanceOverrideApplied
                && ReferenceEquals(_coconutAppearanceOverrideBuild, build)
                && _coconutHiddenEquipmentSnapshot != null
                    ? _coconutHiddenEquipmentSnapshot
                    : build.HiddenEquipment;

            coconut.TryInferLocalTeamFromAvatarAppearance(
                build.Gender,
                EnumerateCoconutAppearanceItemIds(baseEquipment, baseHiddenEquipment),
                out _);

            CharacterLoader loader = _playerManager?.Loader;
            if (loader == null
                || !coconut.TryGetLocalAvatarAppearanceContract(build.Gender, out CoconutField.AvatarAppearanceContract appearanceContract))
            {
                RestoreCoconutFieldAppearance(build);
                return;
            }

            List<CharacterPart> forcedParts = new();
            if (appearanceContract.CapItemId > 0 && loader.LoadEquipment(appearanceContract.CapItemId) is CharacterPart capPart)
            {
                forcedParts.Add(capPart);
            }

            if (appearanceContract.ClothesItemId > 0 && loader.LoadEquipment(appearanceContract.ClothesItemId) is CharacterPart clothesPart)
            {
                forcedParts.Add(clothesPart);
            }

            if (forcedParts.Count == 0)
            {
                RestoreCoconutFieldAppearance(build);
                return;
            }

            if (!_coconutAppearanceOverrideApplied || !ReferenceEquals(_coconutAppearanceOverrideBuild, build))
            {
                _coconutEquipmentSnapshot = new Dictionary<EquipSlot, CharacterPart>(build.Equipment);
                _coconutHiddenEquipmentSnapshot = new Dictionary<EquipSlot, CharacterPart>(build.HiddenEquipment);
                _coconutAppearanceOverrideBuild = build;
                _coconutAppearanceOverrideApplied = true;
            }

            ApplyCoconutAppearanceOverride(
                build,
                baseEquipment,
                baseHiddenEquipment,
                forcedParts);
        }

        private void RestoreCoconutFieldAppearance(CharacterBuild build)
        {
            if (!_coconutAppearanceOverrideApplied)
            {
                return;
            }

            if (build != null && ReferenceEquals(_coconutAppearanceOverrideBuild, build))
            {
                build.Equipment.Clear();
                build.HiddenEquipment.Clear();

                if (_coconutEquipmentSnapshot != null)
                {
                    foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _coconutEquipmentSnapshot)
                    {
                        build.Equipment[entry.Key] = entry.Value;
                    }
                }

                if (_coconutHiddenEquipmentSnapshot != null)
                {
                    foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _coconutHiddenEquipmentSnapshot)
                    {
                        build.HiddenEquipment[entry.Key] = entry.Value;
                    }
                }
            }

            _coconutAppearanceOverrideApplied = false;
            _coconutAppearanceOverrideBuild = null;
            _coconutEquipmentSnapshot = null;
            _coconutHiddenEquipmentSnapshot = null;
        }

        internal static void ApplyCoconutAppearanceOverride(
            CharacterBuild build,
            IReadOnlyDictionary<EquipSlot, CharacterPart> baseEquipment,
            IReadOnlyDictionary<EquipSlot, CharacterPart> baseHiddenEquipment,
            IEnumerable<CharacterPart> forcedParts)
        {
            if (build == null)
            {
                return;
            }

            build.Equipment.Clear();
            build.HiddenEquipment.Clear();

            if (baseEquipment != null)
            {
                foreach (KeyValuePair<EquipSlot, CharacterPart> entry in baseEquipment)
                {
                    build.Equipment[entry.Key] = entry.Value;
                }
            }

            if (baseHiddenEquipment != null)
            {
                foreach (KeyValuePair<EquipSlot, CharacterPart> entry in baseHiddenEquipment)
                {
                    build.HiddenEquipment[entry.Key] = entry.Value;
                }
            }

            foreach (CharacterPart forcedPart in forcedParts ?? Enumerable.Empty<CharacterPart>())
            {
                if (forcedPart == null)
                {
                    continue;
                }

                foreach (EquipSlot slot in EnumerateCoconutAppearanceAffectedSlots(forcedPart.Slot))
                {
                    build.Equipment.Remove(slot);
                    build.HiddenEquipment.Remove(slot);
                }

                build.Equipment[forcedPart.Slot] = forcedPart;
            }
        }

        internal static IReadOnlyCollection<EquipSlot> EnumerateCoconutAppearanceAffectedSlots(EquipSlot slot)
        {
            HashSet<EquipSlot> affectedSlots = new() { slot };
            switch (slot)
            {
                case EquipSlot.Longcoat:
                    affectedSlots.Add(EquipSlot.Coat);
                    affectedSlots.Add(EquipSlot.Pants);
                    break;
                case EquipSlot.Coat:
                case EquipSlot.Pants:
                    affectedSlots.Add(EquipSlot.Longcoat);
                    break;
            }

            return affectedSlots;
        }

        internal static IEnumerable<int> EnumerateCoconutAppearanceItemIds(
            IReadOnlyDictionary<EquipSlot, CharacterPart> equipment,
            IReadOnlyDictionary<EquipSlot, CharacterPart> hiddenEquipment)
        {
            if (equipment != null)
            {
                foreach (CharacterPart part in equipment.Values)
                {
                    if (part?.ItemId > 0)
                    {
                        yield return part.ItemId;
                    }
                }
            }

            if (hiddenEquipment != null)
            {
                foreach (CharacterPart part in hiddenEquipment.Values)
                {
                    if (part?.ItemId > 0)
                    {
                        yield return part.ItemId;
                    }
                }
            }
        }

        private void ApplyTransitAndVoyageFieldWrapper(MapInfo mapInfo)
        {
            if (!IsTransitVoyageWrapperMap(mapInfo) || !_transportField.HasRouteConfiguration)
            {
                return;
            }

            _transportField.ApplyClientOwnedDefaultState(mapInfo?.fieldType);
        }

        private void ConfigureClientOwnedLimitedView(MapInfo mapInfo)
        {
            if (!IsLimitedViewWrapperMap(mapInfo))
            {
                _clientOwnedLimitedViewShareView = false;
                _limitedViewField.ClearClientOwnedMask();
                _limitedViewField.DisableImmediate();
                return;
            }

            EnsureLimitedViewFieldInitialized();
            EnsureClientOwnedLimitedViewMetadataLoaded();
            _limitedViewField.ConfigureClientOwnedDarkLayer(
                ClientOwnedLimitedViewDarkCanvasWidth,
                ClientOwnedLimitedViewDarkCanvasHeight,
                ClientOwnedLimitedViewDarkLayerOffsetX,
                ClientOwnedLimitedViewDarkLayerOffsetY);
            _limitedViewField.SetFollowPlayer(true);
            _limitedViewField.SetPulse(false);
            _limitedViewField.SetEdgeSoftness(0f);
            _limitedViewField.SetFogColor(LimitedViewField.ResolveClientOwnedDarkLayerColor());
            _limitedViewField.SetClientOwnedShareView(_clientOwnedLimitedViewShareView);
            _limitedViewField.EnableClientOwnedCircleMask(
                _clientOwnedLimitedViewRadius,
                _clientOwnedLimitedViewMaskWidth,
                _clientOwnedLimitedViewMaskHeight,
                _clientOwnedLimitedViewOriginX,
                _clientOwnedLimitedViewOriginY,
                (int)MathF.Round(_clientOwnedLimitedViewMaskWidth),
                (int)MathF.Round(_clientOwnedLimitedViewMaskHeight));
        }

        private void SyncClientOwnedLimitedViewFocus(float playerX, float playerY)
        {
            if (!IsLimitedViewWrapperMap(_mapBoard?.MapInfo))
            {
                return;
            }

            if (_playerManager?.IsPlayerActive != true || _playerManager.Player == null)
            {
                _limitedViewField.ClearClientOwnedFocusWorldPosition();
                return;
            }

            _limitedViewField.SetClientOwnedFocusWorldPosition(playerX, playerY);
            _limitedViewField.SetClientOwnedRemoteFocusWorldPositions(EnumerateClientOwnedLimitedViewRemoteFocusWorldPositions());
        }

        private IEnumerable<Vector2> EnumerateClientOwnedLimitedViewRemoteFocusWorldPositions()
        {
            if (_remoteUserPool == null)
            {
                yield break;
            }

            foreach (RemoteUserActor actor in _remoteUserPool.Actors)
            {
                if (actor == null || !actor.IsVisibleInWorld || actor.HiddenLikeClient)
                {
                    continue;
                }

                yield return actor.Position;
            }
        }

        private void ConfigureDynamicFootholdFieldWrapper(MapInfo mapInfo)
        {
            _dynamicFootholdField.Configure(mapInfo, _dynamicFootholds, ResolveDynamicFootholdLinkedContractImage);
        }

        private static WzImage ResolveDynamicFootholdLinkedContractImage(int linkedMapId)
        {
            if (linkedMapId <= 0 || Program.WzManager == null)
            {
                return null;
            }

            return WzInfoTools.FindMapImage(linkedMapId.ToString(), Program.WzManager);
        }

        private void ApplyTutorialFieldAppearance(MapInfo mapInfo)
        {
            int mapId = mapInfo?.id ?? int.MinValue;
            if (_tutorialFieldSpecificAppearanceMapId != mapId)
            {
                _tutorialFieldSpecificAppearanceMapId = mapId;
                _tutorialFieldSpecificAppearanceArmed = false;
            }

            TutorialWrapperContract contract = TryBuildTutorialWrapperContract(mapInfo, out TutorialWrapperContract activeContract)
                ? activeContract
                : default;
            _activeTutorialWrapperKind = contract.Kind;
            CharacterBuild build = _playerManager?.Player?.Build;
            if (!contract.IsActive)
            {
                RestoreTutorialFieldAppearance(build);
                return;
            }

            CharacterLoader loader = _playerManager?.Loader;
            if (loader == null || build == null)
            {
                return;
            }

            bool requiresFieldSpecificPacketArm = contract.Kind == TutorialWrapperKind.Tutorial;
            if (requiresFieldSpecificPacketArm && !_tutorialFieldSpecificAppearanceArmed)
            {
                RestoreTutorialFieldAppearance(build);
                return;
            }

            if (!TryBuildTutorialAppearanceContract(mapInfo, build.Gender, out TutorialAppearanceContract appearanceContract))
            {
                return;
            }

            List<CharacterPart> forcedParts = LoadTutorialAppearanceParts(loader, appearanceContract);
            if (forcedParts.Count == 0)
            {
                return;
            }

            if (!_tutorialAppearanceOverrideApplied || !ReferenceEquals(_tutorialAppearanceOverrideBuild, build))
            {
                _tutorialEquipmentSnapshot = new Dictionary<EquipSlot, CharacterPart>(build.Equipment);
                _tutorialHiddenEquipmentSnapshot = new Dictionary<EquipSlot, CharacterPart>(build.HiddenEquipment);
                _tutorialAppearanceOverrideBuild = build;
                _tutorialAppearanceOverrideApplied = true;
            }

            CharacterPart visibleWeapon = appearanceContract.PreserveActiveWeapon
                && build.Equipment.TryGetValue(EquipSlot.Weapon, out CharacterPart weapon)
                ? weapon
                : null;
            CharacterPart hiddenWeapon = appearanceContract.PreserveActiveWeapon
                && build.HiddenEquipment.TryGetValue(EquipSlot.Weapon, out CharacterPart concealedWeapon)
                ? concealedWeapon
                : null;

            build.Equipment.Clear();
            build.HiddenEquipment.Clear();

            if (visibleWeapon != null)
            {
                build.Equipment[EquipSlot.Weapon] = visibleWeapon;
            }

            if (hiddenWeapon != null)
            {
                build.HiddenEquipment[EquipSlot.Weapon] = hiddenWeapon;
            }

            foreach (CharacterPart forcedPart in forcedParts)
            {
                build.Equip(forcedPart);
            }
        }

        private void RestoreTutorialFieldAppearance(CharacterBuild build)
        {
            if (!_tutorialAppearanceOverrideApplied)
            {
                _activeTutorialWrapperKind = TutorialWrapperKind.None;
                return;
            }

            if (build != null && ReferenceEquals(_tutorialAppearanceOverrideBuild, build))
            {
                build.Equipment.Clear();
                build.HiddenEquipment.Clear();

                if (_tutorialEquipmentSnapshot != null)
                {
                    foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _tutorialEquipmentSnapshot)
                    {
                        build.Equipment[entry.Key] = entry.Value;
                    }
                }

                if (_tutorialHiddenEquipmentSnapshot != null)
                {
                    foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _tutorialHiddenEquipmentSnapshot)
                    {
                        build.HiddenEquipment[entry.Key] = entry.Value;
                    }
                }
            }

            _tutorialAppearanceOverrideApplied = false;
            _tutorialAppearanceOverrideBuild = null;
            _tutorialEquipmentSnapshot = null;
            _tutorialHiddenEquipmentSnapshot = null;
            _tutorialFieldSpecificAppearanceArmed = false;
            _tutorialFieldSpecificAppearanceMapId = int.MinValue;
            _activeTutorialWrapperKind = TutorialWrapperKind.None;
        }

        private bool TryApplyTutorialFieldSpecificAppearanceOwner(byte[] payload, out string message)
        {
            message = null;
            if (!TryBuildTutorialWrapperContract(_mapBoard?.MapInfo, out TutorialWrapperContract contract)
                || contract.Kind != TutorialWrapperKind.Tutorial)
            {
                return false;
            }

            _tutorialFieldSpecificAppearanceArmed = true;
            _tutorialFieldSpecificAppearanceMapId = _mapBoard?.MapInfo?.id ?? _tutorialFieldSpecificAppearanceMapId;

            CharacterBuild build = _playerManager?.Player?.Build;
            CharacterLoader loader = _playerManager?.Loader;
            if (build == null || loader == null)
            {
                message =
                    $"CField_Tutorial::DecodeFieldSpecificData accepted packet-owned appearance trigger and armed forcing appearance (cap {TutorialForcedCapItemId}, longcoat {TutorialForcedLongcoatItemId}), but character runtime is not ready yet.";
                return true;
            }

            ApplyTutorialFieldAppearance(_mapBoard?.MapInfo);
            message =
                $"CField_Tutorial::DecodeFieldSpecificData accepted packet-owned appearance trigger and applied forcing appearance (cap {TutorialForcedCapItemId}, longcoat {TutorialForcedLongcoatItemId}); payload bytes ignored by the client-owner seam (length={payload?.Length ?? 0}).";
            return true;
        }

        private void SyncWeddingPhotoFieldWrapper(MapInfo mapInfo)
        {
            _activeWeddingPhotoSceneContract = TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract)
                ? contract
                : null;

            if (_activeWeddingPhotoSceneContract is not WeddingPhotoSceneContract activeContract
                || activeContract.Kind != WeddingPhotoWrapperKind.SceneOwner)
            {
                _specialFieldRuntime?.SpecialEffects?.Wedding?.ClearWeddingPhotoSceneOwner();
                return;
            }

            Rectangle? viewport = activeContract.HasViewport
                ? new Rectangle(
                    activeContract.ViewportLeft,
                    activeContract.ViewportTop,
                    activeContract.ViewportRight - activeContract.ViewportLeft,
                    activeContract.ViewportBottom - activeContract.ViewportTop)
                : null;
            _specialFieldRuntime?.SpecialEffects?.Wedding?.BindWeddingPhotoSceneOwner(
                mapInfo?.id ?? 0,
                $"CField_WeddingPhoto::GetFieldType = {ClientOwnedWeddingPhotoFieldType}; {activeContract.SourceDescription}; {activeContract.SceneDescription}",
                viewport,
                activeContract.BackgroundMusicPath);
        }

        private void SyncClientOwnedTutorialTutorOwner(int currentTick)
        {
            if (_activeTutorialWrapperKind != TutorialWrapperKind.AranTutorial)
            {
                ReleaseClientOwnedTutorialTutorOwner(currentTick);
                return;
            }

            int runtimeCharacterId = ResolvePacketOwnedTutorRuntimeCharacterId();
            if (runtimeCharacterId <= 0)
            {
                return;
            }

            if (_packetOwnedTutorRuntime.IsActive)
            {
                if (_wrapperOwnedAranTutorActorApplied
                    && _packetOwnedTutorRuntime.ActiveSkillId != TutorRuntime.AranTutorSkillId)
                {
                    _wrapperOwnedAranTutorActorApplied = false;
                }

                return;
            }

            _packetOwnedTutorRuntime.ApplyHireRequest(
                TutorRuntime.AranTutorSkillId,
                TutorRuntime.AranTutorHeight,
                currentTick,
                runtimeCharacterId);
            _wrapperOwnedAranTutorActorApplied = true;
        }

        private void ReleaseClientOwnedTutorialTutorOwner(int currentTick)
        {
            if (!_wrapperOwnedAranTutorActorApplied)
            {
                return;
            }

            if (_packetOwnedTutorRuntime.IsActive
                && _packetOwnedTutorRuntime.ActiveSkillId == TutorRuntime.AranTutorSkillId)
            {
                _packetOwnedTutorRuntime.ApplyRemovalRequest(
                    TutorRuntime.AranTutorSkillId,
                    currentTick,
                    "leaving aran tutorial wrapper owner");
                RemovePacketOwnedTutorSummonForRuntimeOwner();
                SyncPacketOwnedTutorSummonState(currentTick);
            }

            _wrapperOwnedAranTutorActorApplied = false;
        }

        private bool TryApplyClientOwnedWeddingPhotoSceneCameraLock()
        {
            if (_activeWeddingPhotoSceneContract is not WeddingPhotoSceneContract contract
                || contract.Kind != WeddingPhotoWrapperKind.SceneOwner
                || !TryResolveWeddingPhotoSceneViewportCenter(contract, out Vector2 viewportCenter))
            {
                return false;
            }

            _cameraController?.SetPosition(viewportCenter.X, viewportCenter.Y);
            CenterCameraOnWorldPosition(viewportCenter.X, viewportCenter.Y);
            ClampCameraToBoundaries();
            return true;
        }

        private void ConfigureNoDragonPresentation(MapInfo mapInfo)
        {
            bool allowDragonPresentation = !SuppressesDragonPresentation(mapInfo);
            _playerManager?.SetDragonWrapperOwnedNoDragonSuppression(!allowDragonPresentation);

            if (uiWindowManager?.EquipWindow is EquipUI equipWindow)
            {
                equipWindow.SetDragonPaneAvailable(allowDragonPresentation);
            }

            if (uiWindowManager?.EquipWindow is EquipUIBigBang equipBigBang)
            {
                equipBigBang.SetDragonPaneAvailable(allowDragonPresentation);
            }
        }

        private void EnsureClientOwnedLimitedViewMetadataLoaded()
        {
            if (_clientOwnedLimitedViewMetadataLoaded)
            {
                return;
            }

            _clientOwnedLimitedViewMetadataLoaded = true;

            WzImage mapEffectImage = Program.FindImage("Effect", "MapEff.img");
            if (mapEffectImage == null)
            {
                return;
            }

            if (!mapEffectImage.Parsed)
            {
                mapEffectImage.ParseImage();
            }

            if (ResolveProperty(mapEffectImage, ClientOwnedLimitedViewWzPath) is not WzCanvasProperty canvas)
            {
                return;
            }

            _limitedViewField.SetClientOwnedViewrangeTexture(canvas.GetBitmap());
            _clientOwnedLimitedViewMaskWidth = Math.Max(1f, canvas.PngProperty?.Width ?? ClientOwnedLimitedViewFallbackMaskWidth);
            _clientOwnedLimitedViewMaskHeight = Math.Max(1f, canvas.PngProperty?.Height ?? ClientOwnedLimitedViewFallbackMaskHeight);

            if (canvas["origin"] is WzVectorProperty origin)
            {
                _clientOwnedLimitedViewOriginX = Math.Clamp(origin.X?.Value ?? ClientOwnedLimitedViewFallbackOriginX, 0f, _clientOwnedLimitedViewMaskWidth);
                _clientOwnedLimitedViewRadius = Math.Max(1f, origin.X?.Value ?? 0);
                _clientOwnedLimitedViewOriginY = Math.Max(1f, origin.Y?.Value ?? 0);
                return;
            }

            _clientOwnedLimitedViewOriginX = Math.Max(1f, _clientOwnedLimitedViewMaskWidth * 0.5f);
            _clientOwnedLimitedViewRadius = Math.Max(1f, _clientOwnedLimitedViewMaskWidth * 0.5f);
            _clientOwnedLimitedViewOriginY = Math.Max(1f, _clientOwnedLimitedViewMaskHeight * 0.5f);
        }

        private string DescribeClientOwnedFieldWrapperStatus()
        {
            MapInfo mapInfo = _mapBoard?.MapInfo;
            if (mapInfo == null)
            {
                return "Client-owned wrappers: no active map.";
            }

            List<string> activeWrappers = new();

            if (TryBuildTutorialWrapperContract(mapInfo, out TutorialWrapperContract tutorialContract))
            {
                if (tutorialContract.Kind == TutorialWrapperKind.AranTutorial)
                {
                    TutorialAppearanceContract appearanceContract = TryBuildTutorialAppearanceContract(
                        mapInfo,
                        _playerManager?.Player?.Build?.Gender ?? CharacterGender.Male,
                        out TutorialAppearanceContract activeAppearanceContract)
                        ? activeAppearanceContract
                        : default;
                    activeWrappers.Add(
                        $"aran-tutorial wrapper active ({tutorialContract.SourceDescription}, distinct from the generic kill-count wrapper, map {mapInfo.id}, onUserEnter {mapInfo.onUserEnter ?? "<none>"}, mapMark {mapInfo.mapMark ?? "<none>"}): {DescribeTutorialAppearanceContract(appearanceContract)}, tutor owner {(_wrapperOwnedAranTutorActorApplied ? "active through the Aran tutor summon seam" : "waiting for the packet-owned tutor lane")}.");
                }
                else
                {
                    TryBuildTutorialAppearanceContract(
                        mapInfo,
                        _playerManager?.Player?.Build?.Gender ?? CharacterGender.Male,
                        out TutorialAppearanceContract appearanceContract);
                    activeWrappers.Add(
                        $"tutorial wrapper active ({tutorialContract.SourceDescription}): {DescribeTutorialAppearanceContract(appearanceContract)}.");
                }
            }

            if (_transportField.HasRouteConfiguration
                && TransportationFieldWrapperContractBuilder.TryCreate(mapInfo, out TransportationWrapperContract transportContract))
            {
                string ownerLabel = transportContract.Kind == TransportationWrapperKind.Balrog
                    ? "balrog voyage wrapper active"
                    : "transit/voyage wrapper active";
                string shipPath = string.IsNullOrWhiteSpace(transportContract.ShipObjectPath)
                    ? "<editor ShipObject fallback>"
                    : transportContract.ShipObjectPath;
                activeWrappers.Add(
                    $"{ownerLabel} ({transportContract.SourceDescription}): dock ({transportContract.DockX}, {transportContract.DockY}), awayX {transportContract.AwayX}, flip {transportContract.Flip}, tMove {transportContract.MoveDurationSeconds}s, shipKind {transportContract.ShipKind}, shipPath {shipPath}, runtime {_transportField.State} at ({_transportField.ShipX:0.##}, {_transportField.ShipY:0.##}) alpha {_transportField.ShipAlpha:0.##}.");
            }

            if (IsLimitedViewWrapperMap(mapInfo))
            {
                EnsureClientOwnedLimitedViewMetadataLoaded();
                activeWrappers.Add(
                    $"limited-view wrapper active (fieldType {(int)FieldType.FIELDTYPE_LIMITEDVIEW}): dark {ClientOwnedLimitedViewDarkCanvasWidth}x{ClientOwnedLimitedViewDarkCanvasHeight}, layer offset ({ClientOwnedLimitedViewDarkLayerOffsetX},{ClientOwnedLimitedViewDarkLayerOffsetY}), mask {_clientOwnedLimitedViewMaskWidth:F0}x{_clientOwnedLimitedViewMaskHeight:F0}, origin ({_clientOwnedLimitedViewOriginX:F0},{_clientOwnedLimitedViewOriginY:F0}), radius {_clientOwnedLimitedViewRadius:F0}, update mirrors CField::Update + DrawViewrange ownership, source Effect/MapEff.img/{ClientOwnedLimitedViewWzPath}.");
                activeWrappers[^1] = activeWrappers[^1] + $" share-view {(_clientOwnedLimitedViewShareView ? "on" : "off")}.";
            }

            if (SuppressesDragonPresentation(mapInfo))
            {
                string source = mapInfo.fieldType == FieldType.FIELDTYPE_NODRAGON
                    ? $"fieldType {(int)FieldType.FIELDTYPE_NODRAGON}"
                    : "info/vanishDragon";
                activeWrappers.Add($"no-dragon presentation active ({source}): dragon actor and equipment pane stay suppressed.");
            }

            if (_dynamicFootholdField.IsActive)
            {
                activeWrappers.Add($"{_dynamicFootholdField.DescribeStatus(_dynamicFootholds)}.");
            }

            if (_activeWeddingPhotoSceneContract is WeddingPhotoSceneContract weddingPhotoContract)
            {
                string safeArea = weddingPhotoContract.HasSafeArea
                    ? $" safeArea(side={weddingPhotoContract.Side}, top={weddingPhotoContract.Top}, bottom={weddingPhotoContract.Bottom})."
                    : string.Empty;
                string viewport = weddingPhotoContract.HasViewport
                    ? $" viewport(left={weddingPhotoContract.ViewportLeft}, top={weddingPhotoContract.ViewportTop}, right={weddingPhotoContract.ViewportRight}, bottom={weddingPhotoContract.ViewportBottom})."
                    : string.Empty;
                string ownerLabel = weddingPhotoContract.Kind == WeddingPhotoWrapperKind.SceneOwner
                    ? "wedding-photo field owner active"
                    : "wedding-photo ceremony/photo contract active";
                string cameraFocus = weddingPhotoContract.Kind == WeddingPhotoWrapperKind.SceneOwner
                    && TryResolveWeddingPhotoSceneViewportCenter(weddingPhotoContract, out Vector2 viewportCenter)
                    ? $" camera lock enforced at viewport center ({viewportCenter.X:0.#}, {viewportCenter.Y:0.#})."
                    : string.Empty;
                string bgm = string.IsNullOrWhiteSpace(weddingPhotoContract.BackgroundMusicPath)
                    ? string.Empty
                    : $" bgm={weddingPhotoContract.BackgroundMusicPath}.";
                string presentationState = _specialFieldRuntime?.SpecialEffects?.Wedding?.DescribeWeddingPhotoScenePresentationState();
                string presentation = string.IsNullOrWhiteSpace(presentationState)
                    ? string.Empty
                    : $" {presentationState}";
                activeWrappers.Add(
                    $"{ownerLabel} ({weddingPhotoContract.SceneDescription}, client owner {weddingPhotoContract.SourceDescription}) on map {mapInfo.id}, returnMap {weddingPhotoContract.ReturnMapId}.{safeArea}{viewport}{cameraFocus}{bgm}{presentation}");
            }

            if (_specialFieldRuntime.PartyRaid.HasNativePartyRaidWrapperOwner)
            {
                activeWrappers.Add($"{_specialFieldRuntime.PartyRaid.ActiveRuntimeOwnerName} active ({_specialFieldRuntime.PartyRaid.DescribeClientWrapperContract()}) on map {mapInfo.id}.");
            }

            return activeWrappers.Count == 0
                ? "Client-owned wrappers: none of transport, tutorial, limited-view, no-dragon, dynamic-foothold, or wedding-photo are active on this map."
                : "Client-owned wrappers: " + string.Join(" ", activeWrappers);
        }

        private static WzImageProperty ResolveProperty(WzObject root, string propertyPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return root as WzImageProperty;
            }

            WzObject current = root;
            string[] pathSegments = propertyPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pathSegments.Length && current != null; i++)
            {
                current = current switch
                {
                    WzImage image => image[pathSegments[i]],
                    WzImageProperty property => property[pathSegments[i]],
                    _ => null
                };
            }

            return current as WzImageProperty;
        }

        private static bool IsLimitedViewWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_LIMITEDVIEW;
        }

        private static bool IsTutorialWrapperMap(MapInfo mapInfo)
        {
            return TryBuildTutorialWrapperContract(mapInfo, out _);
        }

        private static bool IsNoDragonWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_NODRAGON;
        }

        private static bool IsDynamicFootholdWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_DYNAMICFOOTHOLD;
        }

        internal static bool SuppressesDragonPresentation(MapInfo mapInfo)
        {
            return mapInfo?.vanishDragon == true || IsNoDragonWrapperMap(mapInfo);
        }

        private static bool IsWeddingPhotoWrapperMap(MapInfo mapInfo)
        {
            return TryBuildWeddingPhotoSceneContract(mapInfo, out _);
        }

        private static bool IsTransitVoyageWrapperMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_CONTIMOVE
                || mapInfo.fieldType == FieldType.FIELDTYPE_BALROG
                || TransportationFieldDefinitionLoader.TryCreate(mapInfo, out _);
        }

        private static bool IsKillCountWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_KILLCOUNT
                && !IsAranTutorialWrapperMap(mapInfo);
        }

        private static bool IsEscortResultWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_ESCORT_RESULT;
        }

        private static bool IsShowaBathWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_SHOWABATH
                || mapInfo?.id == 801000200
                || mapInfo?.id == 801000210;
        }

        internal static bool TryBuildTutorialWrapperContract(MapInfo mapInfo, out TutorialWrapperContract contract)
        {
            if (IsAranTutorialWrapperMap(mapInfo))
            {
                contract = new TutorialWrapperContract(
                    TutorialWrapperKind.AranTutorial,
                    $"client owner CField_AranTutorial::GetFieldType = {ClientOwnedAranTutorialFieldType}, WZ info/fieldType={mapInfo?.fieldType}, onUserEnter={mapInfo?.onUserEnter ?? "<none>"}, mapMark={mapInfo?.mapMark ?? "<none>"}, mapId={mapInfo?.id ?? 0}");
                return true;
            }

            if (mapInfo?.fieldType == FieldType.FIELDTYPE_TUTORIAL)
            {
                contract = new TutorialWrapperContract(
                    TutorialWrapperKind.Tutorial,
                    $"client owner CField_Tutorial, fieldType {(int)FieldType.FIELDTYPE_TUTORIAL} ({FieldType.FIELDTYPE_TUTORIAL})");
                return true;
            }

            contract = default;
            return false;
        }

        internal static bool TryBuildTutorialAppearanceContract(MapInfo mapInfo, CharacterGender gender, out TutorialAppearanceContract contract)
        {
            return TryBuildTutorialAppearanceContract(mapInfo, gender, out contract, null);
        }

        internal static bool TryBuildTutorialAppearanceContract(
            MapInfo mapInfo,
            CharacterGender gender,
            out TutorialAppearanceContract contract,
            Func<int, WzImage> linkedMapResolver)
        {
            if (!TryBuildTutorialWrapperContract(mapInfo, out TutorialWrapperContract wrapperContract))
            {
                contract = default;
                return false;
            }

            if (wrapperContract.Kind == TutorialWrapperKind.AranTutorial
                && TryBuildAranTutorialAppearanceContract(mapInfo, gender, out TutorialAppearanceContract aranContract, linkedMapResolver))
            {
                contract = aranContract;
                return true;
            }

            contract = new TutorialAppearanceContract(
                wrapperContract.Kind,
                $"{wrapperContract.SourceDescription}, tutorial decode appearance pair from CField_Tutorial::DecodeFieldSpecificData",
                TutorialForcedCapItemId,
                TutorialForcedLongcoatItemId,
                0,
                0,
                0,
                preserveActiveWeapon: true);
            return true;
        }

        private static TutorialWrapperKind GetTutorialWrapperKind(MapInfo mapInfo)
        {
            return TryBuildTutorialWrapperContract(mapInfo, out TutorialWrapperContract contract)
                ? contract.Kind
                : TutorialWrapperKind.None;
        }

        private static bool IsAranTutorialWrapperMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            bool hasAranFieldType = mapInfo.fieldType.HasValue
                && (int)mapInfo.fieldType.Value == ClientOwnedAranTutorialFieldType;
            bool hasAranOnUserEnter = !string.IsNullOrWhiteSpace(mapInfo.onUserEnter)
                && mapInfo.onUserEnter.StartsWith(AranTutorialOnUserEnterPrefix, StringComparison.OrdinalIgnoreCase);
            bool inAranTutorialRange = mapInfo.id >= AranTutorialMapIdMin
                && mapInfo.id <= AranTutorialMapIdMax;
            bool hasAranMapMark = string.Equals(mapInfo.mapMark, AranTutorialMapMark, StringComparison.OrdinalIgnoreCase);

            if (hasAranFieldType && hasAranOnUserEnter)
            {
                return true;
            }

            if (!hasAranFieldType)
            {
                return false;
            }

            return inAranTutorialRange && (hasAranMapMark || hasAranOnUserEnter);
        }

        private static bool TryBuildAranTutorialAppearanceContract(
            MapInfo mapInfo,
            CharacterGender gender,
            out TutorialAppearanceContract contract,
            Func<int, WzImage> linkedMapResolver = null)
        {
            contract = default;

            WzImage mapImage = ResolveTutorialContractImage(mapInfo, out _, linkedMapResolver);
            if (mapImage == null)
            {
                return false;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            if (ResolveProperty(mapImage, "user") is not WzImageProperty userRoot)
            {
                return false;
            }

            foreach (WzImageProperty userEntry in userRoot.WzProperties)
            {
                if (TryBuildAranTutorialAppearanceContractFromEntry(mapInfo, userEntry, gender, out contract))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryBuildWeddingPhotoSceneContract(MapInfo mapInfo, out WeddingPhotoSceneContract contract)
        {
            return TryBuildWeddingPhotoSceneContract(mapInfo, out contract, null);
        }

        internal static bool TryBuildWeddingPhotoSceneContract(
            MapInfo mapInfo,
            out WeddingPhotoSceneContract contract,
            Func<int, WzImage> linkedMapResolver)
        {
            WzImage contractImage = ResolveClientOwnedContractMapImage(mapInfo, out int contractMapId, linkedMapResolver);
            WzImageProperty contractInfo = ResolveProperty(contractImage, "info");
            string mapMark = GetResolvedContractStringValue(contractInfo, "mapMark") ?? mapInfo?.mapMark;
            string onUserEnter = GetResolvedContractStringValue(contractInfo, "onUserEnter") ?? mapInfo?.onUserEnter;
            string backgroundMusic = GetResolvedContractStringValue(contractInfo, "bgm") ?? mapInfo?.bgm;
            int returnMapId = GetResolvedContractIntValue(contractInfo, "returnMap") ?? mapInfo?.returnMap ?? 0;
            int? fieldType = GetResolvedContractIntValue(contractInfo, "fieldType") ?? (int?)mapInfo?.fieldType;
            int sceneSide = GetResolvedContractIntValue(contractInfo, "LBSide") ?? Math.Max(0, mapInfo?.LBSide ?? 0);
            int sceneTop = GetResolvedContractIntValue(contractInfo, "LBTop") ?? Math.Max(0, mapInfo?.LBTop ?? 0);
            int sceneBottom = GetResolvedContractIntValue(contractInfo, "LBBottom") ?? Math.Max(0, mapInfo?.LBBottom ?? 0);
            int sceneViewportLeft = GetResolvedContractIntValue(contractInfo, "VRLeft") ?? mapInfo?.VRLeft ?? 0;
            int sceneViewportTop = GetResolvedContractIntValue(contractInfo, "VRTop") ?? mapInfo?.VRTop ?? 0;
            int sceneViewportRight = GetResolvedContractIntValue(contractInfo, "VRRight") ?? mapInfo?.VRRight ?? 0;
            int sceneViewportBottom = GetResolvedContractIntValue(contractInfo, "VRBottom") ?? mapInfo?.VRBottom ?? 0;
            bool hasSceneSafeArea = sceneSide > 0 || sceneTop > 0 || sceneBottom > 0;
            bool hasSceneViewport = sceneViewportRight > sceneViewportLeft && sceneViewportBottom > sceneViewportTop;
            string linkedSourceSuffix = contractMapId > 0 && contractMapId != (mapInfo?.id ?? 0)
                ? $", linked WZ contract map {contractMapId}"
                : string.Empty;

            if (fieldType == ClientOwnedWeddingPhotoFieldType || HasWeddingPhotoSceneOwnerContract(mapInfo, mapMark, onUserEnter))
            {
                string sourceDescription = fieldType == ClientOwnedWeddingPhotoFieldType
                    ? $"CField_WeddingPhoto::GetFieldType = {ClientOwnedWeddingPhotoFieldType}{linkedSourceSuffix}"
                    : $"WZ wedding scene owner contract (onUserEnter={onUserEnter ?? "<none>"}, mapMark={mapMark ?? "<none>"}, region={mapInfo?.id / 1000 ?? 0}{linkedSourceSuffix})";
                string sceneDescription = hasSceneViewport
                    ? "wedding pledge/photo scene owner with WZ viewport envelope"
                    : "wedding pledge/photo scene owner";
                contract = new WeddingPhotoSceneContract(
                    WeddingPhotoWrapperKind.SceneOwner,
                    sourceDescription,
                    sceneDescription,
                    backgroundMusic,
                    returnMapId,
                    sceneSide,
                    sceneTop,
                    sceneBottom,
                    sceneViewportLeft,
                    sceneViewportTop,
                    sceneViewportRight,
                    sceneViewportBottom);
                return true;
            }

            if (HasWeddingPhotoSafeAreaContract(mapInfo, mapMark, hasSceneSafeArea))
            {
                contract = new WeddingPhotoSceneContract(
                    WeddingPhotoWrapperKind.PhotoContract,
                    $"wedding photo safe-area contract (mapMark={mapMark}, region={mapInfo.id / 1000}{linkedSourceSuffix})",
                    "wedding ceremony/photo safe-area contract",
                    backgroundMusic,
                    returnMapId,
                    sceneSide,
                    sceneTop,
                    sceneBottom,
                    sceneViewportLeft,
                    sceneViewportTop,
                    sceneViewportRight,
                    sceneViewportBottom);
                return true;
            }

            contract = default;
            return false;
        }

        internal static bool TryGetWeddingPhotoSceneSafeArea(MapInfo mapInfo, out int side, out int top, out int bottom)
        {
            if (TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract)
                && contract.HasSafeArea)
            {
                side = contract.Side;
                top = contract.Top;
                bottom = contract.Bottom;
                return true;
            }

            side = Math.Max(0, mapInfo?.LBSide ?? 0);
            top = Math.Max(0, mapInfo?.LBTop ?? 0);
            bottom = Math.Max(0, mapInfo?.LBBottom ?? 0);
            return side > 0 || top > 0 || bottom > 0;
        }

        internal static bool TryGetWeddingPhotoSceneViewport(MapInfo mapInfo, out int left, out int top, out int right, out int bottom)
        {
            if (TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract)
                && contract.HasViewport)
            {
                left = contract.ViewportLeft;
                top = contract.ViewportTop;
                right = contract.ViewportRight;
                bottom = contract.ViewportBottom;
                return true;
            }

            left = mapInfo?.VRLeft ?? 0;
            top = mapInfo?.VRTop ?? 0;
            right = mapInfo?.VRRight ?? 0;
            bottom = mapInfo?.VRBottom ?? 0;
            return right > left && bottom > top;
        }

        internal static bool TryResolveWeddingPhotoSceneViewportCenter(WeddingPhotoSceneContract contract, out Vector2 center)
        {
            if (!contract.HasViewport)
            {
                center = Vector2.Zero;
                return false;
            }

            center = new Vector2(
                (contract.ViewportLeft + contract.ViewportRight) * 0.5f,
                (contract.ViewportTop + contract.ViewportBottom) * 0.5f);
            return true;
        }

        internal static bool TryResolveWeddingPhotoSceneLockedCameraCenter(MapInfo mapInfo, out Vector2 center)
        {
            center = Vector2.Zero;
            if (!TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract)
                || contract.Kind != WeddingPhotoWrapperKind.SceneOwner)
            {
                return false;
            }

            return TryResolveWeddingPhotoSceneViewportCenter(contract, out center);
        }

        private static bool HasWeddingPhotoSafeAreaContract(MapInfo mapInfo, string mapMark, bool hasSafeArea)
        {
            return mapInfo != null
                && mapInfo.id / 1000 == WeddingPhotoMapRegionPrefix
                && string.Equals(mapMark, WeddingMapMark, StringComparison.OrdinalIgnoreCase)
                && hasSafeArea;
        }

        private static bool HasWeddingPhotoSceneOwnerContract(MapInfo mapInfo, string mapMark, string onUserEnter)
        {
            return mapInfo != null
                && mapInfo.id / 1000 == WeddingPhotoMapRegionPrefix
                && string.Equals(mapMark, WeddingMapMark, StringComparison.OrdinalIgnoreCase)
                && string.Equals(onUserEnter, WeddingPhotoSceneOnUserEnter, StringComparison.OrdinalIgnoreCase);
        }

        private static WzImage ResolveTutorialContractImage(MapInfo mapInfo, out int resolvedMapId, Func<int, WzImage> linkedMapResolver = null)
        {
            return ResolveClientOwnedContractMapImage(mapInfo, out resolvedMapId, linkedMapResolver);
        }

        internal static WzImage ResolveClientOwnedContractMapImage(
            MapInfo mapInfo,
            out int resolvedMapId,
            Func<int, WzImage> linkedMapResolver = null)
        {
            resolvedMapId = mapInfo?.id ?? 0;
            if (mapInfo?.Image != null)
            {
                WzImage linkedImage = TryResolveLinkedContractMapImage(mapInfo.Image, mapInfo.id, out resolvedMapId, linkedMapResolver);
                return linkedImage ?? mapInfo.Image;
            }

            if (mapInfo?.id > 0 && Program.WzManager != null)
            {
                WzImage mapImage = WzInfoTools.FindMapImage(mapInfo.id.ToString(), Program.WzManager);
                if (mapImage == null)
                {
                    return null;
                }

                WzImage linkedImage = TryResolveLinkedContractMapImage(mapImage, mapInfo.id, out resolvedMapId, linkedMapResolver);
                return linkedImage ?? mapImage;
            }

            return null;
        }

        private static WzImage TryResolveLinkedContractMapImage(
            WzImage mapImage,
            int fallbackMapId,
            out int resolvedMapId,
            Func<int, WzImage> linkedMapResolver = null)
        {
            resolvedMapId = fallbackMapId;
            if (mapImage == null)
            {
                return null;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            if (ResolveProperty(mapImage, "info/link") is not WzStringProperty linkedMapProperty
                || string.IsNullOrWhiteSpace(linkedMapProperty.Value)
                || !int.TryParse(linkedMapProperty.Value, out int linkedMapId)
                || linkedMapId <= 0)
            {
                return null;
            }

            WzImage linkedMapImage = linkedMapResolver?.Invoke(linkedMapId);
            if (linkedMapImage == null && Program.WzManager != null)
            {
                linkedMapImage = WzInfoTools.FindMapImage(linkedMapId.ToString(), Program.WzManager);
            }

            if (linkedMapImage == null)
            {
                return null;
            }

            resolvedMapId = linkedMapId;
            return linkedMapImage;
        }

        private static int? GetResolvedContractIntValue(WzImageProperty contractInfo, string propertyName)
        {
            if (contractInfo?[propertyName] is WzIntProperty intProperty)
            {
                return intProperty.GetInt();
            }

            return null;
        }

        private static string GetResolvedContractStringValue(WzImageProperty contractInfo, string propertyName)
        {
            return (contractInfo?[propertyName] as WzStringProperty)?.Value;
        }

        private static bool TryBuildAranTutorialAppearanceContractFromEntry(
            MapInfo mapInfo,
            WzImageProperty userEntry,
            CharacterGender gender,
            out TutorialAppearanceContract contract)
        {
            contract = default;
            if (userEntry == null)
            {
                return false;
            }

            WzImageProperty cond = userEntry["cond"];
            WzImageProperty look = userEntry["look"];
            if (look == null)
            {
                return false;
            }

            int target = (cond?["target"] as WzIntProperty)?.GetInt() ?? 0;
            int? configuredGender = (cond?["gender"] as WzIntProperty)?.GetInt();
            if (target != 1)
            {
                return false;
            }

            if (configuredGender.HasValue && configuredGender.Value != (int)gender)
            {
                return false;
            }

            int capItemId = (look["cap"] as WzIntProperty)?.GetInt() ?? 0;
            int clothesItemId = (look["clothes"] as WzIntProperty)?.GetInt() ?? 0;
            int capeItemId = (look["cape"] as WzIntProperty)?.GetInt() ?? 0;
            int shoesItemId = (look["shoes"] as WzIntProperty)?.GetInt() ?? 0;
            int weaponItemId = (look["weapon"] as WzIntProperty)?.GetInt() ?? 0;
            contract = new TutorialAppearanceContract(
                TutorialWrapperKind.AranTutorial,
                $"WZ user/{userEntry.Name}/look target={target}, gender={(configuredGender.HasValue ? configuredGender.Value.ToString() : "any")}, mapId={mapInfo?.id ?? 0}",
                capItemId,
                clothesItemId,
                capeItemId,
                shoesItemId,
                weaponItemId,
                preserveActiveWeapon: false);
            return contract.HasAnyAppearanceItem;
        }

        private static List<CharacterPart> LoadTutorialAppearanceParts(CharacterLoader loader, TutorialAppearanceContract contract)
        {
            List<CharacterPart> parts = new();
            TryAddTutorialAppearancePart(parts, loader, contract.CapItemId);
            TryAddTutorialAppearancePart(parts, loader, contract.ClothesItemId);
            TryAddTutorialAppearancePart(parts, loader, contract.CapeItemId);
            TryAddTutorialAppearancePart(parts, loader, contract.ShoesItemId);
            TryAddTutorialAppearancePart(parts, loader, contract.WeaponItemId);
            return parts;
        }

        private static void TryAddTutorialAppearancePart(List<CharacterPart> parts, CharacterLoader loader, int itemId)
        {
            if (itemId <= 0 || loader == null)
            {
                return;
            }

            CharacterPart part = loader.LoadEquipment(itemId);
            if (part != null)
            {
                parts.Add(part);
            }
        }

        private static string DescribeTutorialAppearanceContract(TutorialAppearanceContract contract)
        {
            if (!contract.HasAnyAppearanceItem)
            {
                return "appearance override pending WZ contract";
            }

            List<string> items = new();
            if (contract.CapItemId > 0)
            {
                items.Add($"cap {contract.CapItemId}");
            }

            if (contract.ClothesItemId > 0)
            {
                items.Add($"clothes {contract.ClothesItemId}");
            }

            if (contract.CapeItemId > 0)
            {
                items.Add($"cape {contract.CapeItemId}");
            }

            if (contract.ShoesItemId > 0)
            {
                items.Add($"shoes {contract.ShoesItemId}");
            }

            if (contract.WeaponItemId > 0)
            {
                items.Add($"weapon {contract.WeaponItemId}");
            }

            string behavior = contract.PreserveActiveWeapon
                ? "preserving the active weapon slot"
                : "replacing the local tutorial loadout from the WZ user/look contract";
            return $"{behavior} with {string.Join(", ", items)} ({contract.SourceDescription})";
        }

        private static string DescribeWeddingPhotoWrapperSource(MapInfo mapInfo)
        {
            if (!TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract))
            {
                return "scene wrapper";
            }

            if (contract.Kind == WeddingPhotoWrapperKind.SceneOwner)
            {
                return $"fieldType {(int)FieldType.FIELDTYPE_WEDDINGPHOTO}";
            }

            if (contract.Kind == WeddingPhotoWrapperKind.PhotoContract)
            {
                return $"CField_WeddingPhoto photo contract (mapMark={mapInfo.mapMark})";
            }

            return "scene wrapper";
        }
    }
}
