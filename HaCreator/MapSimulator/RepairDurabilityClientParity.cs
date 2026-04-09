using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Loaders;
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
            "speak"
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

            HashSet<string> yieldedActions = new(StringComparer.OrdinalIgnoreCase);
            foreach (NpcClientActionSetLoader.NpcClientActionSetDefinition actionSet in NpcClientActionSetLoader.GetClientActionSets(source))
            {
                foreach (WzImageProperty action in actionSet.Actions ?? Array.Empty<WzImageProperty>())
                {
                    if (action == null
                        || string.IsNullOrWhiteSpace(action.Name)
                        || action["speak"] == null
                        || !yieldedActions.Add(action.Name))
                    {
                        continue;
                    }

                    yield return action.Name;
                }
            }
        }

        internal static string ResolvePreferredNpcAction(
            int? shopActionId,
            IEnumerable<string> availableActions,
            IEnumerable<string> speakFallbackActions)
        {
            List<string> availableActionOrder = new();
            var availableActionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in availableActions ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(action) && !availableActionMap.ContainsKey(action))
                {
                    availableActionMap[action] = action;
                    availableActionOrder.Add(action);
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

            foreach (string action in availableActionOrder)
            {
                if (action.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0
                    || action.IndexOf("say", StringComparison.OrdinalIgnoreCase) >= 0
                    || action.IndexOf("speak", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return action;
                }
            }

            foreach (string action in availableActionOrder)
            {
                if (action.StartsWith("stand", StringComparison.OrdinalIgnoreCase))
                {
                    return action;
                }
            }

            return availableActionOrder.FirstOrDefault() ?? AnimationKeys.Stand;
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
