using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Buffers.Binary;
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

        private static readonly (int MaskBit, string Key)[] JobBadgeDefinitions =
        {
            (1, "beginner"),
            (2, "warrior"),
            (4, "magician"),
            (8, "bowman"),
            (16, "thief"),
            (32, "pirate")
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

        internal static IReadOnlyList<(string Key, bool Enabled)> ResolveRequiredJobBadgeStates(int requiredJobMask)
        {
            var states = new (string Key, bool Enabled)[JobBadgeDefinitions.Length];
            for (int i = 0; i < JobBadgeDefinitions.Length; i++)
            {
                (int maskBit, string key) = JobBadgeDefinitions[i];
                bool enabled = requiredJobMask == 0 || (requiredJobMask & maskBit) != 0;
                states[i] = (key, enabled);
            }

            return states;
        }

        internal static byte[] BuildRepairRequestPayload(short operationCode, int encodedPosition)
        {
            if (operationCode == 130)
            {
                return Array.Empty<byte>();
            }

            if (operationCode != 131)
            {
                throw new ArgumentOutOfRangeException(nameof(operationCode), operationCode, "Repair durability only supports opcodes 130 and 131.");
            }

            byte[] payload = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(payload, encodedPosition);
            return payload;
        }

        internal static bool TryDecodeSyntheticResultPayload(
            byte[] payload,
            out bool success,
            out int? reasonCode,
            out string error)
        {
            success = true;
            reasonCode = null;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                return true;
            }

            if (payload.Length != 1 && payload.Length != 1 + sizeof(int))
            {
                error = "Repair-result payload must be empty, 1 byte, or 5 bytes (result + optional reason code).";
                return false;
            }

            success = payload[0] == 0;
            if (payload.Length >= 1 + sizeof(int))
            {
                reasonCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, sizeof(int)));
            }

            return true;
        }
    }
}
