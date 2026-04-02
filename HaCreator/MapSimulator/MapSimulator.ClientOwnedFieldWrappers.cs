using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Collections.Generic;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private enum TutorialWrapperKind
        {
            None,
            Tutorial,
            AranTutorial
        }

        private enum WeddingPhotoWrapperKind
        {
            None,
            SceneOwner,
            SafeAreaContract
        }

        private readonly struct WeddingPhotoSceneContract
        {
            public WeddingPhotoSceneContract(WeddingPhotoWrapperKind kind, string sourceDescription, int returnMapId, int side, int top, int bottom)
            {
                Kind = kind;
                SourceDescription = sourceDescription ?? string.Empty;
                ReturnMapId = returnMapId;
                Side = Math.Max(0, side);
                Top = Math.Max(0, top);
                Bottom = Math.Max(0, bottom);
            }

            public WeddingPhotoWrapperKind Kind { get; }
            public string SourceDescription { get; }
            public int ReturnMapId { get; }
            public int Side { get; }
            public int Top { get; }
            public int Bottom { get; }
            public bool HasSafeArea => Side > 0 || Top > 0 || Bottom > 0;
        }

        private const int TutorialForcedCapItemId = 1002562;
        private const int TutorialForcedLongcoatItemId = 1052081;
        private const int ShowaBathMaleLongcoatItemId = 1050100;
        private const int ShowaBathFemaleLongcoatItemId = 1051098;
        private const string ClientOwnedLimitedViewWzPath = "Viewrange/0";
        private const string AranTutorialOnUserEnterPrefix = "aranTutor";
        private const string AranTutorialMapMark = "BlackDragon";
        private const string WeddingMapMark = "Wedding";
        private const int AranTutorialMapIdMin = 914000000;
        private const int AranTutorialMapIdMax = 914000500;
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
        private bool _showaBathAppearanceOverrideApplied;
        private CharacterBuild _showaBathAppearanceOverrideBuild;
        private Dictionary<EquipSlot, CharacterPart> _showaBathEquipmentSnapshot;
        private Dictionary<EquipSlot, CharacterPart> _showaBathHiddenEquipmentSnapshot;
        private int? _killCountWrapperValue;
        private int _escortFailOverlayUntilTick = int.MinValue;
        private bool _clientOwnedLimitedViewMetadataLoaded;
        private float _clientOwnedLimitedViewRadius = ClientOwnedLimitedViewFallbackRadius;
        private float _clientOwnedLimitedViewMaskWidth = ClientOwnedLimitedViewFallbackMaskWidth;
        private float _clientOwnedLimitedViewMaskHeight = ClientOwnedLimitedViewFallbackMaskHeight;
        private float _clientOwnedLimitedViewOriginX = ClientOwnedLimitedViewFallbackOriginX;
        private float _clientOwnedLimitedViewOriginY = ClientOwnedLimitedViewFallbackOriginY;
        private TutorialWrapperKind _activeTutorialWrapperKind;
        private WeddingPhotoSceneContract? _activeWeddingPhotoSceneContract;

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
                return;
            }

            CharacterLoader loader = _playerManager?.Loader;
            if (loader == null || build == null)
            {
                return;
            }

            ApplyShowaBathFieldAppearance(build, loader);
        }

        private string HandleClientOwnedFieldSpecificDataPacket(byte[] payload, int currentTick)
        {
            if (!IsShowaBathWrapperMap(_mapBoard?.MapInfo))
            {
                return null;
            }

            CharacterBuild build = _playerManager?.Player?.Build;
            CharacterLoader loader = _playerManager?.Loader;
            if (build == null || loader == null)
            {
                return "showa-bath wrapper pending character loader/build";
            }

            ApplyShowaBathFieldAppearance(build, loader);
            return "showa-bath appearance override applied";
        }

        private bool TryApplyClientOwnedWrapperPacket(int packetType, byte[] payload, int currentTick, out string message)
        {
            message = null;
            MapInfo mapInfo = _mapBoard?.MapInfo;
            if (packetType == 178 && IsKillCountWrapperMap(mapInfo))
            {
                if (payload == null || payload.Length < sizeof(int))
                {
                    message = "Kill-count packet requires a 4-byte payload.";
                    return false;
                }

                _killCountWrapperValue = Math.Max(0, BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int))));
                message = $"kill-count={_killCountWrapperValue.Value}";
                return true;
            }

            if (packetType == Effects.MassacreField.PacketTypeResult && _specialFieldRuntime.SpecialEffects.Massacre.IsActive)
            {
                if (_specialFieldRuntime.SpecialEffects.Massacre.TryApplyMassacreResultPayload(payload, currentTick, out string error))
                {
                    message = _specialFieldRuntime.SpecialEffects.Massacre.DescribeStatus();
                    return true;
                }

                message = error;
                return false;
            }

            return false;
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
                if (!_specialFieldRuntime.PartyRaid.IsActive)
                {
                    message = "Hunting Ad Balloon overlay inactive.";
                    return false;
                }

                bool applied = _specialFieldRuntime.PartyRaid.OnFieldSetVariable(key, value);
                message = applied ? _specialFieldRuntime.PartyRaid.DescribeStatus() : $"field key not accepted ({key}={value})";
                return applied;
            }

            if (string.Equals(wrapperName, "escortresult", StringComparison.OrdinalIgnoreCase)
                && IsEscortResultWrapperMap(_mapBoard?.MapInfo)
                && MatchesEscortFailKey(key)
                && string.Equals(value, "fail", StringComparison.OrdinalIgnoreCase))
            {
                _escortFailOverlayUntilTick = currentTick + EscortFailOverlayDurationMs;
                message = "escort-result fail overlay armed";
                return true;
            }

            return false;
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
                _limitedViewField.ClearClientOwnedMask();
                _limitedViewField.DisableImmediate();
                return;
            }

            EnsureLimitedViewFieldInitialized();
            EnsureClientOwnedLimitedViewMetadataLoaded();
            _limitedViewField.SetFollowPlayer(true);
            _limitedViewField.SetPulse(false);
            _limitedViewField.SetEdgeSoftness(0f);
            _limitedViewField.SetFogColor(Color.Black);
            _limitedViewField.EnableClientOwnedCircleMask(
                _clientOwnedLimitedViewRadius,
                _clientOwnedLimitedViewMaskWidth,
                _clientOwnedLimitedViewMaskHeight,
                _clientOwnedLimitedViewOriginX,
                _clientOwnedLimitedViewOriginY);
        }

        private void ConfigureDynamicFootholdFieldWrapper(MapInfo mapInfo)
        {
            _dynamicFootholdField.Configure(mapInfo, _dynamicFootholds);
        }

        private void ApplyTutorialFieldAppearance(MapInfo mapInfo)
        {
            TutorialWrapperKind wrapperKind = GetTutorialWrapperKind(mapInfo);
            _activeTutorialWrapperKind = wrapperKind;
            CharacterBuild build = _playerManager?.Player?.Build;
            if (wrapperKind == TutorialWrapperKind.None)
            {
                RestoreTutorialFieldAppearance(build);
                return;
            }

            CharacterLoader loader = _playerManager?.Loader;
            if (loader == null || build == null)
            {
                return;
            }

            CharacterPart forcedCap = loader.LoadEquipment(TutorialForcedCapItemId);
            CharacterPart forcedLongcoat = loader.LoadEquipment(TutorialForcedLongcoatItemId);
            if (forcedCap == null || forcedLongcoat == null)
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

            CharacterPart visibleWeapon = build.Equipment.TryGetValue(EquipSlot.Weapon, out CharacterPart weapon) ? weapon : null;
            CharacterPart hiddenWeapon = build.HiddenEquipment.TryGetValue(EquipSlot.Weapon, out CharacterPart concealedWeapon) ? concealedWeapon : null;

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

            build.Equip(forcedCap);
            build.Equip(forcedLongcoat);
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
            _activeTutorialWrapperKind = TutorialWrapperKind.None;
        }

        private void SyncWeddingPhotoFieldWrapper(MapInfo mapInfo)
        {
            _activeWeddingPhotoSceneContract = TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract)
                ? contract
                : null;
        }

        private void ConfigureNoDragonPresentation(MapInfo mapInfo)
        {
            bool allowDragonPresentation = !SuppressesDragonPresentation(mapInfo);

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

            TutorialWrapperKind tutorialWrapperKind = GetTutorialWrapperKind(mapInfo);
            if (tutorialWrapperKind != TutorialWrapperKind.None)
            {
                if (tutorialWrapperKind == TutorialWrapperKind.AranTutorial)
                {
                    activeWrappers.Add(
                        $"aran-tutorial wrapper active (client owner CField_AranTutorial, GetFieldType 22, map {mapInfo.id}, onUserEnter {mapInfo.onUserEnter ?? "<none>"}, mapMark {mapInfo.mapMark ?? "<none>"}): forcing hat {TutorialForcedCapItemId} and longcoat {TutorialForcedLongcoatItemId}.");
                }
                else
                {
                    activeWrappers.Add(
                        $"tutorial wrapper active (client owner CField_Tutorial, fieldType {(int)FieldType.FIELDTYPE_TUTORIAL}): forcing hat {TutorialForcedCapItemId} and longcoat {TutorialForcedLongcoatItemId}.");
                }
            }

            if (IsLimitedViewWrapperMap(mapInfo))
            {
                EnsureClientOwnedLimitedViewMetadataLoaded();
                activeWrappers.Add(
                    $"limited-view wrapper active (fieldType {(int)FieldType.FIELDTYPE_LIMITEDVIEW}): dark {ClientOwnedLimitedViewDarkCanvasWidth}x{ClientOwnedLimitedViewDarkCanvasHeight}, layer offset ({ClientOwnedLimitedViewDarkLayerOffsetX},{ClientOwnedLimitedViewDarkLayerOffsetY}), mask {_clientOwnedLimitedViewMaskWidth:F0}x{_clientOwnedLimitedViewMaskHeight:F0}, origin ({_clientOwnedLimitedViewOriginX:F0},{_clientOwnedLimitedViewOriginY:F0}), radius {_clientOwnedLimitedViewRadius:F0}, update mirrors CField::Update + DrawViewrange ownership, source Effect/MapEff.img/{ClientOwnedLimitedViewWzPath}.");
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
                string wzSummary = _dynamicFootholdField.HasWzFootholdRoot
                    ? $"WZ foothold root present with {_dynamicFootholdField.FootholdLayerCount} layers, {_dynamicFootholdField.FootholdGroupCount} groups, and {_dynamicFootholdField.FootholdSegmentCount} segments"
                    : "WZ foothold root unavailable";
                activeWrappers.Add(
                    $"dynamic-foothold wrapper active (client owner {DynamicFootholdField.ClientOwnerName}, fieldType {(int)FieldType.FIELDTYPE_DYNAMICFOOTHOLD}, GetFieldType 0x{DynamicFootholdField.ClientGetFieldTypeAddress:X}, Init stub 0x{DynamicFootholdField.ClientInitStubAddress:X}): {wzSummary}; runtime {_dynamicFootholds.DescribeClientOwnedWrapperState()}.");
            }

            if (_activeWeddingPhotoSceneContract is WeddingPhotoSceneContract weddingPhotoContract)
            {
                string safeArea = weddingPhotoContract.HasSafeArea
                    ? $" safeArea(side={weddingPhotoContract.Side}, top={weddingPhotoContract.Top}, bottom={weddingPhotoContract.Bottom})."
                    : string.Empty;
                activeWrappers.Add(
                    $"wedding-photo scene owner active (client owner {weddingPhotoContract.SourceDescription}) on map {mapInfo.id}, returnMap {weddingPhotoContract.ReturnMapId}.{safeArea}");
            }

            return activeWrappers.Count == 0
                ? "Client-owned wrappers: none of tutorial, limited-view, no-dragon, dynamic-foothold, or wedding-photo are active on this map."
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
            return GetTutorialWrapperKind(mapInfo) != TutorialWrapperKind.None;
        }

        private static bool IsNoDragonWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_NODRAGON;
        }

        private static bool IsDynamicFootholdWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_DYNAMICFOOTHOLD;
        }

        private static bool SuppressesDragonPresentation(MapInfo mapInfo)
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

        private static TutorialWrapperKind GetTutorialWrapperKind(MapInfo mapInfo)
        {
            if (IsAranTutorialWrapperMap(mapInfo))
            {
                return TutorialWrapperKind.AranTutorial;
            }

            if (mapInfo?.fieldType == FieldType.FIELDTYPE_TUTORIAL)
            {
                return TutorialWrapperKind.Tutorial;
            }

            return TutorialWrapperKind.None;
        }

        private static bool IsAranTutorialWrapperMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mapInfo.onUserEnter)
                && mapInfo.onUserEnter.StartsWith(AranTutorialOnUserEnterPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return mapInfo.id >= AranTutorialMapIdMin
                && mapInfo.id <= AranTutorialMapIdMax
                && string.Equals(mapInfo.mapMark, AranTutorialMapMark, StringComparison.OrdinalIgnoreCase)
                && mapInfo.fieldType == FieldType.FIELDTYPE_KILLCOUNT;
        }

        private static bool TryBuildWeddingPhotoSceneContract(MapInfo mapInfo, out WeddingPhotoSceneContract contract)
        {
            if (mapInfo?.fieldType == FieldType.FIELDTYPE_WEDDINGPHOTO)
            {
                TryGetWeddingPhotoSceneSafeArea(mapInfo, out int sceneSide, out int sceneTop, out int sceneBottom);
                contract = new WeddingPhotoSceneContract(
                    WeddingPhotoWrapperKind.SceneOwner,
                    $"CField_WeddingPhoto, fieldType {(int)FieldType.FIELDTYPE_WEDDINGPHOTO}",
                    mapInfo.returnMap,
                    sceneSide,
                    sceneTop,
                    sceneBottom);
                return true;
            }

            if (HasWeddingPhotoSafeAreaContract(mapInfo))
            {
                TryGetWeddingPhotoSceneSafeArea(mapInfo, out int safeSide, out int safeTop, out int safeBottom);
                contract = new WeddingPhotoSceneContract(
                    WeddingPhotoWrapperKind.SafeAreaContract,
                    $"wedding safe-area contract (mapMark={mapInfo.mapMark})",
                    mapInfo.returnMap,
                    safeSide,
                    safeTop,
                    safeBottom);
                return true;
            }

            contract = default;
            return false;
        }

        internal static bool TryGetWeddingPhotoSceneSafeArea(MapInfo mapInfo, out int side, out int top, out int bottom)
        {
            side = Math.Max(0, mapInfo?.LBSide ?? 0);
            top = Math.Max(0, mapInfo?.LBTop ?? 0);
            bottom = Math.Max(0, mapInfo?.LBBottom ?? 0);
            return side > 0 || top > 0 || bottom > 0;
        }

        private static bool HasWeddingPhotoSafeAreaContract(MapInfo mapInfo)
        {
            return mapInfo != null
                && string.Equals(mapInfo.mapMark, WeddingMapMark, StringComparison.OrdinalIgnoreCase)
                && TryGetWeddingPhotoSceneSafeArea(mapInfo, out _, out _, out _);
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

            if (contract.Kind == WeddingPhotoWrapperKind.SafeAreaContract)
            {
                return $"Wedding safe-area contract (mapMark={mapInfo.mapMark})";
            }

            return "scene wrapper";
        }
    }
}
