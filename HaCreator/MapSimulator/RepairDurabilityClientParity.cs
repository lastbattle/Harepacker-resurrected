using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
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
            if (LoginAvatarLookCodec.TryGetBodyPart(slot, itemId, out byte bodyPart)
                && bodyPart > 0
                && bodyPart <= 59)
            {
                encodedPosition = -bodyPart;
                return true;
            }

            encodedPosition = int.MinValue;
            return false;
        }
    }
}
