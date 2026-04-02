using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using MapleLib.WzLib;

namespace HaCreator.MapSimulator
{
    internal static class RepairDurabilityClientParity
    {
        private static readonly string[] ExplicitNpcActionFallbacks =
        {
            "shop",
            "say",
            "speak",
            "stand1",
            AnimationKeys.Stand
        };

        internal static IEnumerable<string> EnumerateNpcActionCandidates(int? shopActionId)
        {
            int clientShopAction = shopActionId.GetValueOrDefault();
            if (clientShopAction <= 0)
            {
                clientShopAction = 1;
            }

            yield return clientShopAction.ToString(CultureInfo.InvariantCulture);
            foreach (string candidate in ExplicitNpcActionFallbacks)
            {
                yield return candidate;
            }
        }

        internal static IEnumerable<string> EnumerateNpcSpeakFallbackActions(WzImage source)
        {
            if (source == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in source.WzProperties)
            {
                if (child == null || string.Equals(child.Name, "info", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (child["speak"] != null)
                {
                    yield return child.Name;
                }
            }
        }

        internal static string ResolvePreferredNpcAction(
            int? shopActionId,
            IEnumerable<string> availableActions,
            IEnumerable<string> speakFallbackActions)
        {
            var availableActionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in availableActions ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(action) && !availableActionMap.ContainsKey(action))
                {
                    availableActionMap[action] = action;
                }
            }

            if (availableActionMap.Count <= 0)
            {
                return AnimationKeys.Stand;
            }

            foreach (string candidate in EnumerateNpcActionCandidates(shopActionId))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            foreach (string candidate in speakFallbackActions ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            foreach (string action in availableActionMap.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (action.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0
                    || action.IndexOf("say", StringComparison.OrdinalIgnoreCase) >= 0
                    || action.IndexOf("speak", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return action;
                }
            }

            foreach (string action in availableActionMap.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (action.StartsWith("stand", StringComparison.OrdinalIgnoreCase))
                {
                    return action;
                }
            }

            return availableActionMap.Values.FirstOrDefault() ?? AnimationKeys.Stand;
        }

        internal static bool TryEncodeEquippedPosition(EquipSlot slot, int itemId, out int encodedPosition)
        {
            int bodyPart = ResolveBodyPart(slot, itemId);
            if (bodyPart > 0 && bodyPart <= 59)
            {
                encodedPosition = -bodyPart;
                return true;
            }

            encodedPosition = int.MinValue;
            return false;
        }

        private static int ResolveBodyPart(EquipSlot slot, int itemId)
        {
            int category = Math.Max(0, itemId / 10000);
            return category switch
            {
                100 => 1,
                101 => 2,
                102 => 3,
                103 => 4,
                104 or 105 => 5,
                106 => 6,
                107 => 7,
                108 => 8,
                109 or 119 or 134 => 10,
                110 => 9,
                111 => ResolveRingBodyPart(slot),
                112 => slot == EquipSlot.Pendant2 ? 59 : 17,
                113 => 50,
                114 => 49,
                115 => 51,
                116 => 52,
                118 => 53,
                190 => 18,
                191 => 19,
                _ when IsWeaponCategory(category) => 11,
                _ => ResolveLegacyBodyPart(slot)
            };
        }

        private static int ResolveRingBodyPart(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Ring1 => 12,
                EquipSlot.Ring2 => 13,
                EquipSlot.Ring3 => 15,
                EquipSlot.Ring4 => 16,
                _ => ResolveLegacyBodyPart(slot)
            };
        }

        private static bool IsWeaponCategory(int category)
        {
            int bucket = category / 10;
            return bucket == 13 || bucket == 14 || bucket == 16 || bucket == 17;
        }

        private static int ResolveLegacyBodyPart(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Cap => 1,
                EquipSlot.FaceAccessory => 2,
                EquipSlot.EyeAccessory => 3,
                EquipSlot.Earrings => 4,
                EquipSlot.Coat => 5,
                EquipSlot.Longcoat => 5,
                EquipSlot.Pants => 6,
                EquipSlot.Shoes => 7,
                EquipSlot.Glove => 8,
                EquipSlot.Cape => 9,
                EquipSlot.Shield => 10,
                EquipSlot.Weapon => 11,
                EquipSlot.Ring1 => 12,
                EquipSlot.Ring2 => 13,
                EquipSlot.Ring3 => 15,
                EquipSlot.Ring4 => 16,
                EquipSlot.Pendant => 17,
                EquipSlot.TamingMob => 18,
                EquipSlot.Saddle => 19,
                EquipSlot.Medal => 49,
                EquipSlot.Belt => 50,
                EquipSlot.Shoulder => 51,
                EquipSlot.Pocket => 52,
                EquipSlot.Badge => 53,
                EquipSlot.Pendant2 => 59,
                _ => 0
            };
        }
    }
}
