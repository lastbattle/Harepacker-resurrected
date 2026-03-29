using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int TutorialForcedCapItemId = 1002562;
        private const int TutorialForcedLongcoatItemId = 1052081;
        private const float ClientOwnedLimitedViewRadius = 158f;
        private bool _tutorialAppearanceOverrideApplied;
        private CharacterBuild _tutorialAppearanceOverrideBuild;
        private Dictionary<EquipSlot, CharacterPart> _tutorialEquipmentSnapshot;
        private Dictionary<EquipSlot, CharacterPart> _tutorialHiddenEquipmentSnapshot;

        private void ApplyClientOwnedFieldWrappers()
        {
            MapInfo mapInfo = _mapBoard?.MapInfo;
            ConfigureClientOwnedLimitedView(mapInfo);
            ConfigureNoDragonPresentation(mapInfo);
            ApplyTransitAndVoyageFieldWrapper(mapInfo);
            ApplyTutorialFieldAppearance(mapInfo);
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
    }
}
