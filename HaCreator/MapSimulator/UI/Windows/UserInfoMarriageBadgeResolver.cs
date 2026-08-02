using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class UserInfoMarriageBadgeResolver
    {
        // WZ-backed wedding ring ids from String/Eqp.img Eqp/Ring:
        // 1112315-1112320 and 1112803/1112806/1112807/1112809 all resolve to "* Wedding Ring".
        private static readonly HashSet<int> WeddingRingItemIds = new()
        {
            1112315,
            1112316,
            1112317,
            1112318,
            1112319,
            1112320,
            1112803,
            1112806,
            1112807,
            1112809
        };

        private static readonly EquipSlot[] RingSlots =
        {
            EquipSlot.Ring1,
            EquipSlot.Ring2,
            EquipSlot.Ring3,
            EquipSlot.Ring4
        };

        internal static bool HasMarriageBadge(CharacterBuild build)
        {
            if (build == null)
            {
                return false;
            }

            foreach (EquipSlot slot in RingSlots)
            {
                if (TryResolveEquippedItem(build, slot, out CharacterPart part) &&
                    IsWeddingRing(part))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsWeddingRing(CharacterPart part)
        {
            if (part == null)
            {
                return false;
            }

            if (part.ItemId > 0 && WeddingRingItemIds.Contains(part.ItemId))
            {
                return true;
            }

            // Preserve a narrow legacy/custom-asset fallback when the item id is unknown.
            return !string.IsNullOrWhiteSpace(part.Name) &&
                   part.Name.IndexOf("wedding ring", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryResolveEquippedItem(CharacterBuild build, EquipSlot slot, out CharacterPart part)
        {
            part = null;
            if (build?.Equipment != null &&
                build.Equipment.TryGetValue(slot, out CharacterPart equippedPart) &&
                equippedPart != null)
            {
                part = equippedPart;
                return true;
            }

            if (build?.HiddenEquipment != null &&
                build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) &&
                hiddenPart != null)
            {
                part = hiddenPart;
                return true;
            }

            return false;
        }
    }
}
