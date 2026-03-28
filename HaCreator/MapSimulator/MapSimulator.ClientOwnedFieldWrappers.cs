using HaCreator.MapSimulator.Character;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int TutorialForcedCapItemId = 1002562;
        private const int TutorialForcedLongcoatItemId = 1052081;
        private const float ClientOwnedLimitedViewRadius = 158f;

        private void ApplyClientOwnedFieldWrappers()
        {
            MapInfo mapInfo = _mapBoard?.MapInfo;
            ConfigureClientOwnedLimitedView(mapInfo);
            ApplyTutorialFieldAppearance(mapInfo);
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
            if (!IsTutorialWrapperMap(mapInfo))
            {
                return;
            }

            CharacterLoader loader = _playerManager?.Loader;
            CharacterBuild build = _playerManager?.Player?.Build;
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

            EquipSlot[] equippedSlots = build.Equipment.Keys.ToArray();
            for (int i = 0; i < equippedSlots.Length; i++)
            {
                EquipSlot slot = equippedSlots[i];
                if (slot != EquipSlot.Weapon)
                {
                    build.Unequip(slot);
                }
            }

            EquipSlot[] hiddenSlots = build.HiddenEquipment.Keys.ToArray();
            for (int i = 0; i < hiddenSlots.Length; i++)
            {
                EquipSlot slot = hiddenSlots[i];
                if (slot != EquipSlot.Weapon)
                {
                    build.UnequipHidden(slot);
                }
            }

            build.Equip(forcedCap);
            build.Equip(forcedLongcoat);
        }

        private static bool IsLimitedViewWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_LIMITEDVIEW;
        }

        private static bool IsTutorialWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_TUTORIAL;
        }
    }
}
