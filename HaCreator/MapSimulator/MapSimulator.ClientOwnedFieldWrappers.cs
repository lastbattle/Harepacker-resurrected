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

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int TutorialForcedCapItemId = 1002562;
        private const int TutorialForcedLongcoatItemId = 1052081;
        private const int ShowaBathMaleLongcoatItemId = 1050100;
        private const int ShowaBathFemaleLongcoatItemId = 1051098;
        private const float ClientOwnedLimitedViewRadius = 158f;
        private const int EscortFailOverlayDurationMs = 2500;
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

        private void ApplyClientOwnedFieldWrappers()
        {
            MapInfo mapInfo = _mapBoard?.MapInfo;
            ConfigureClientOwnedLimitedView(mapInfo);
            ConfigureNoDragonPresentation(mapInfo);
            ApplyTransitAndVoyageFieldWrapper(mapInfo);
            ApplyTutorialFieldAppearance(mapInfo);
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
                _limitedViewField.DisableImmediate();
                return;
            }

            EnsureLimitedViewFieldInitialized();
            _limitedViewField.SetPulse(false);
            _limitedViewField.SetEdgeSoftness(0f);
            _limitedViewField.SetFogColor(Color.Black);
            _limitedViewField.EnableCircle(ClientOwnedLimitedViewRadius);
        }

        private void ApplyTutorialFieldAppearance(MapInfo mapInfo)
        {
            CharacterBuild build = _playerManager?.Player?.Build;
            if (!IsTutorialWrapperMap(mapInfo))
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
        }

        private void ConfigureNoDragonPresentation(MapInfo mapInfo)
        {
            bool allowDragonPresentation = !IsNoDragonWrapperMap(mapInfo);

            if (uiWindowManager?.EquipWindow is EquipUI equipWindow)
            {
                equipWindow.SetDragonPaneAvailable(allowDragonPresentation);
            }

            if (uiWindowManager?.EquipWindow is EquipUIBigBang equipBigBang)
            {
                equipBigBang.SetDragonPaneAvailable(allowDragonPresentation);
            }
        }

        private static bool IsLimitedViewWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_LIMITEDVIEW;
        }

        private static bool IsTutorialWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_TUTORIAL;
        }

        private static bool IsNoDragonWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_NODRAGON;
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
            return mapInfo?.fieldType == FieldType.FIELDTYPE_KILLCOUNT;
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
    }
}
