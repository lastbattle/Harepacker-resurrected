using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Buffers.Binary;
using System.Text;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    internal static class RepairDurabilityClientParity
    {
        internal readonly struct HoverTooltipPlacement
        {
            public HoverTooltipPlacement(Rectangle bounds, int frameIndex)
            {
                Bounds = bounds;
                FrameIndex = frameIndex;
            }

            public Rectangle Bounds { get; }
            public int FrameIndex { get; }
        }

        internal readonly struct ResultPayload
        {
            public ResultPayload(bool success, int? reasonCode, short? operationCode, string statusText)
            {
                Success = success;
                ReasonCode = reasonCode;
                OperationCode = operationCode;
                StatusText = statusText ?? string.Empty;
            }

            public bool Success { get; }
            public int? ReasonCode { get; }
            public short? OperationCode { get; }
            public string StatusText { get; }
        }

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

            foreach (string candidate in NpcClientActionSetLoader.EnumerateClientActionNameCandidates(clientShopAction))
            {
                yield return candidate;
            }

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

        internal static HoverTooltipPlacement ResolveHoverTooltipPlacement(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            int viewportPadding,
            int cursorGap)
        {
            Rectangle[] candidates =
            {
                new(anchorPoint.X, anchorPoint.Y, tooltipWidth, tooltipHeight),
                new(anchorPoint.X - tooltipWidth, anchorPoint.Y, tooltipWidth, tooltipHeight),
                new(anchorPoint.X, anchorPoint.Y - tooltipHeight - cursorGap, tooltipWidth, tooltipHeight),
                new(anchorPoint.X - tooltipWidth, anchorPoint.Y - tooltipHeight - cursorGap, tooltipWidth, tooltipHeight)
            };

            Rectangle bestRect = candidates[0];
            int bestFrame = ResolveHoverTooltipFrameIndex(anchorPoint, bestRect);
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Rectangle candidate = candidates[i];
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight, viewportPadding);
                int frameIndex = ResolveHoverTooltipFrameIndex(anchorPoint, candidate);
                if (overflow == 0)
                {
                    return new HoverTooltipPlacement(candidate, frameIndex);
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestRect = candidate;
                    bestFrame = frameIndex;
                }
            }

            return new HoverTooltipPlacement(
                ClampTooltipRect(bestRect, renderWidth, renderHeight, viewportPadding),
                bestFrame);
        }

        internal static bool MatchesPendingResultOperation(short pendingOperationCode, short? resultOperationCode)
        {
            return !resultOperationCode.HasValue || resultOperationCode.Value == pendingOperationCode;
        }

        internal static bool TryDecodeSyntheticResultPayload(
            byte[] payload,
            out ResultPayload result,
            out string error)
        {
            result = new ResultPayload(success: true, reasonCode: null, operationCode: null, statusText: string.Empty);
            error = null;

            if (payload == null || payload.Length == 0)
            {
                return true;
            }

            int offset = 0;
            short? operationCode = null;
            if (payload[0] == 130 || payload[0] == 131)
            {
                operationCode = payload[0];
                offset++;
            }

            int remainingLength = payload.Length - offset;
            if (remainingLength != 1 && remainingLength < 1 + sizeof(int))
            {
                error = "Repair-result payload must be empty, [result], [result+reason], [opcode+result], or [opcode+result+reason(+text)].";
                return false;
            }

            bool success = payload[offset] == 0;
            int? reasonCode = null;
            string statusText = string.Empty;
            if (remainingLength >= 1 + sizeof(int))
            {
                reasonCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset + 1, sizeof(int)));
                int textOffset = offset + 1 + sizeof(int);
                int textLength = payload.Length - textOffset;
                if (textLength > 0)
                {
                    statusText = DecodeResultStatusText(payload.AsSpan(textOffset, textLength));
                }
            }

            result = new ResultPayload(success, reasonCode, operationCode, statusText);
            return true;
        }

        private static string DecodeResultStatusText(ReadOnlySpan<byte> payload)
        {
            if (payload.Length <= 0)
            {
                return string.Empty;
            }

            if (LooksLikeUtf16Le(payload))
            {
                int terminatorIndex = payload.IndexOf(stackalloc byte[] { 0, 0 });
                if (terminatorIndex >= 0)
                {
                    payload = payload[..terminatorIndex];
                }

                if ((payload.Length & 1) != 0)
                {
                    payload = payload[..^1];
                }

                return payload.Length <= 0
                    ? string.Empty
                    : Encoding.Unicode.GetString(payload).Trim();
            }

            int utf8TerminatorIndex = payload.IndexOf((byte)0);
            if (utf8TerminatorIndex >= 0)
            {
                payload = payload[..utf8TerminatorIndex];
            }

            return Encoding.UTF8.GetString(payload).Trim();
        }

        private static bool LooksLikeUtf16Le(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 2 || (payload.Length & 1) != 0)
            {
                return false;
            }

            int zeroCount = 0;
            for (int i = 1; i < payload.Length; i += 2)
            {
                if (payload[i] == 0)
                {
                    zeroCount++;
                }
            }

            return zeroCount >= payload.Length / 4;
        }

        private static int ResolveHoverTooltipFrameIndex(Point anchorPoint, Rectangle rect)
        {
            bool drawsLeftOfAnchor = rect.X < anchorPoint.X;
            bool drawsBelowAnchor = rect.Y >= anchorPoint.Y;

            if (drawsLeftOfAnchor)
            {
                return drawsBelowAnchor ? 2 : 0;
            }

            return 1;
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight, int viewportPadding)
        {
            int overflow = 0;

            if (rect.Left < viewportPadding)
            {
                overflow += viewportPadding - rect.Left;
            }

            if (rect.Top < viewportPadding)
            {
                overflow += viewportPadding - rect.Top;
            }

            if (rect.Right > renderWidth - viewportPadding)
            {
                overflow += rect.Right - (renderWidth - viewportPadding);
            }

            if (rect.Bottom > renderHeight - viewportPadding)
            {
                overflow += rect.Bottom - (renderHeight - viewportPadding);
            }

            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight, int viewportPadding)
        {
            int minX = viewportPadding;
            int minY = viewportPadding;
            int maxX = Math.Max(minX, renderWidth - viewportPadding - rect.Width);
            int maxY = Math.Max(minY, renderHeight - viewportPadding - rect.Height);

            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }
    }
}
