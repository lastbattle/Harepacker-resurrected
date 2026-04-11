using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
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
            public ResultPayload(bool success, int? reasonCode, short? operationCode, int? encodedSlotPosition, string statusText)
            {
                Success = success;
                ReasonCode = reasonCode;
                OperationCode = operationCode;
                EncodedSlotPosition = encodedSlotPosition;
                StatusText = statusText ?? string.Empty;
            }

            public bool Success { get; }
            public int? ReasonCode { get; }
            public short? OperationCode { get; }
            public int? EncodedSlotPosition { get; }
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
            return EnumerateNpcActionCandidates(shopActionId, source: null);
        }

        internal static IEnumerable<string> EnumerateNpcActionCandidates(int? shopActionId, WzImage source)
        {
            int clientShopAction = shopActionId.GetValueOrDefault();
            if (clientShopAction <= 0)
            {
                clientShopAction = 1;
            }

            foreach (string candidate in NpcClientActionSetLoader.EnumerateClientActionNameCandidates(clientShopAction, source))
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
            return NpcClientActionSetLoader.EnumerateAuthoredSpeakFallbackActions(source);
        }

        internal static string ResolvePreferredNpcAction(
            int? shopActionId,
            IEnumerable<string> availableActions,
            IEnumerable<string> speakFallbackActions,
            WzImage source = null)
        {
            List<string> availableActionOrder = availableActions?
                .Where(static action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            var availableActionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in availableActionOrder)
            {
                availableActionMap[action] = action;
            }

            if (shopActionId.GetValueOrDefault() > 0)
            {
                IReadOnlyList<string> clientTemplateActionOrder =
                    NpcClientActionSetLoader.BuildClientTemplateActionOrder(source);
                if (clientTemplateActionOrder.Count == 0)
                {
                    clientTemplateActionOrder = availableActionOrder;
                }

                foreach (string candidate in NpcClientActionSetLoader.EnumerateClientActionNameCandidates(
                             shopActionId.Value,
                             clientTemplateActionOrder))
                {
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                    {
                        return resolvedCandidate;
                    }
                }
            }

            foreach (string candidate in ExplicitNpcActionFallbacks)
            {
                if (availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            foreach (string candidate in speakFallbackActions?
                         .Where(static action => !string.IsNullOrWhiteSpace(action))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                     ?? Array.Empty<string>())
            {
                if (availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            foreach (string action in availableActionOrder)
            {
                if (action.StartsWith(AnimationKeys.Stand, StringComparison.OrdinalIgnoreCase))
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

            return TryEncodeLegacyEquippedPosition(slot, out encodedPosition);
        }

        internal static bool TryEncodeLegacyEquippedPosition(EquipSlot slot, out int encodedPosition)
        {
            int legacySlotPosition = (int)slot;
            if (legacySlotPosition > 0 && legacySlotPosition <= 59)
            {
                encodedPosition = -legacySlotPosition;
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
            int cursorGap,
            IReadOnlyList<Point> tooltipFrameOrigins = null,
            IReadOnlyList<Point> tooltipFrameSizes = null)
        {
            (Rectangle Rect, int FrameIndex)[] candidates = TryBuildOriginAwareHoverCandidates(
                anchorPoint,
                tooltipWidth,
                tooltipHeight,
                cursorGap,
                tooltipFrameOrigins,
                tooltipFrameSizes,
                out (Rectangle Rect, int FrameIndex)[] originAwareCandidates)
                ? originAwareCandidates
                : new[]
                {
                    (new Rectangle(anchorPoint.X, anchorPoint.Y, tooltipWidth, tooltipHeight), 1),
                    (new Rectangle(anchorPoint.X - tooltipWidth, anchorPoint.Y, tooltipWidth, tooltipHeight), 2),
                    (new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight - cursorGap, tooltipWidth, tooltipHeight), 1),
                    (new Rectangle(anchorPoint.X - tooltipWidth, anchorPoint.Y - tooltipHeight - cursorGap, tooltipWidth, tooltipHeight), 0)
                };

            Rectangle bestRect = candidates[0].Rect;
            int bestFrame = candidates[0].FrameIndex;
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Rectangle candidate = candidates[i].Rect;
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight, viewportPadding);
                int frameIndex = candidates[i].FrameIndex;
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

        internal static bool MatchesPendingResultTarget(bool repairAllRequest, int pendingEncodedSlotPosition, int? resultEncodedSlotPosition)
        {
            return repairAllRequest
                || !resultEncodedSlotPosition.HasValue
                || resultEncodedSlotPosition.Value == pendingEncodedSlotPosition;
        }

        internal static bool TryDecodeSyntheticResultPayload(
            byte[] payload,
            out ResultPayload result,
            out string error)
        {
            result = new ResultPayload(success: true, reasonCode: null, operationCode: null, encodedSlotPosition: null, statusText: string.Empty);
            error = null;

            if (payload == null || payload.Length == 0)
            {
                return true;
            }

            if (TryDecodeJsonResultPayload(payload, out result, out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            int offset = 0;
            short? operationCode = null;
            if (payload.Length >= sizeof(int) + 1)
            {
                int intOperationCode = BinaryPrimitives.ReadInt32LittleEndian(payload);
                if (intOperationCode == 130 || intOperationCode == 131)
                {
                    operationCode = (short)intOperationCode;
                    offset += sizeof(int);
                }
            }

            if (!operationCode.HasValue && payload.Length >= sizeof(short) + 1)
            {
                short shortOperationCode = BinaryPrimitives.ReadInt16LittleEndian(payload);
                if (shortOperationCode == 130 || shortOperationCode == 131)
                {
                    operationCode = shortOperationCode;
                    offset += sizeof(short);
                }
            }

            if (!operationCode.HasValue && (payload[0] == 130 || payload[0] == 131))
            {
                operationCode = payload[0];
                offset++;
            }

            int? encodedSlotPosition = null;
            int remainingLength = payload.Length - offset;
            if (remainingLength >= sizeof(int) + 1
                && payload[offset] != 0
                && payload[offset] != 1)
            {
                int candidateEncodedSlotPosition = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                int remainingAfterSlotPosition = payload.Length - (offset + sizeof(int));
                if (LooksLikeEncodedSlotPosition(candidateEncodedSlotPosition)
                    && (remainingAfterSlotPosition == 1 || remainingAfterSlotPosition >= 1 + sizeof(int)))
                {
                    encodedSlotPosition = candidateEncodedSlotPosition;
                    offset += sizeof(int);
                }
            }

            remainingLength = payload.Length - offset;
            if (remainingLength != 1 && remainingLength < 1 + sizeof(int))
            {
                error = "Repair-result payload must be empty, [result], [result+reason], [opcode+result], [opcode16+result], [slot+result], or [opcode/slot+result+reason(+text)].";
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

            result = new ResultPayload(success, reasonCode, operationCode, encodedSlotPosition, statusText);
            return true;
        }

        private static bool TryDecodeJsonResultPayload(byte[] payload, out ResultPayload result, out string error)
        {
            result = new ResultPayload(success: true, reasonCode: null, operationCode: null, encodedSlotPosition: null, statusText: string.Empty);
            error = null;

            ReadOnlySpan<byte> trimmedPayload = TrimJsonPayload(payload);
            if (trimmedPayload.Length <= 0 || trimmedPayload[0] != (byte)'{')
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmedPayload.ToArray());
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "Repair-result JSON payload must be an object.";
                    return false;
                }

                bool success = ReadBoolean(root, true, "success", "succeeded", "ok", "accepted")
                    && !ReadBoolean(root, false, "failure", "failed", "rejected", "error");
                short? operationCode = ReadInt(root, "operationCode", "opcode", "op", "repairOpcode") is int op
                    && (op == 130 || op == 131)
                        ? (short)op
                        : null;
                int? encodedSlotPosition = ReadInt(root, "encodedSlotPosition", "encodedPosition", "nPOS", "slotPosition", "position");
                if (encodedSlotPosition.HasValue && !LooksLikeEncodedSlotPosition(encodedSlotPosition.Value))
                {
                    error = $"Repair-result JSON encoded slot position {encodedSlotPosition.Value} is outside the client slot range.";
                    return false;
                }

                int? reasonCode = ReadInt(root, "reasonCode", "reason", "errorCode", "rejectReason");
                string statusText = ReadString(root, "statusText", "message", "text", "localizedText", "notice");
                result = new ResultPayload(success, reasonCode, operationCode, encodedSlotPosition, statusText);
                return true;
            }
            catch (JsonException ex)
            {
                error = $"Repair-result JSON payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static ReadOnlySpan<byte> TrimJsonPayload(byte[] payload)
        {
            ReadOnlySpan<byte> span = payload ?? Array.Empty<byte>();
            while (span.Length > 0 && char.IsWhiteSpace((char)span[0]))
            {
                span = span[1..];
            }

            while (span.Length > 0 && (span[^1] == 0 || char.IsWhiteSpace((char)span[^1])))
            {
                span = span[..^1];
            }

            return span;
        }

        private static int? ReadInt(JsonElement root, params string[] names)
        {
            foreach (string name in names)
            {
                if (!root.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String
                    && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    return number;
                }
            }

            return null;
        }

        private static bool ReadBoolean(JsonElement root, bool defaultValue, params string[] names)
        {
            foreach (string name in names)
            {
                if (!root.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                {
                    return number != 0;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    string text = value.GetString();
                    if (bool.TryParse(text, out bool parsed))
                    {
                        return parsed;
                    }

                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        return number != 0;
                    }
                }
            }

            return defaultValue;
        }

        private static string ReadString(JsonElement root, params string[] names)
        {
            foreach (string name in names)
            {
                if (root.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static bool LooksLikeEncodedSlotPosition(int encodedSlotPosition)
        {
            return encodedSlotPosition is >= 1 and <= 255
                || encodedSlotPosition is <= -1 and >= -255;
        }

        private static string DecodeResultStatusText(ReadOnlySpan<byte> payload)
        {
            if (payload.Length <= 0)
            {
                return string.Empty;
            }

            if (TryDecodeLengthPrefixedStatusText(payload, out string lengthPrefixedText))
            {
                return lengthPrefixedText;
            }

            if (LooksLikeUtf16Le(payload))
            {
                int terminatorIndex = FindUtf16LeTerminator(payload);
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

        private static bool TryDecodeLengthPrefixedStatusText(ReadOnlySpan<byte> payload, out string statusText)
        {
            statusText = string.Empty;
            if (payload.Length < sizeof(ushort))
            {
                return false;
            }

            ushort lengthPrefix = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            ReadOnlySpan<byte> encodedText = payload[sizeof(ushort)..];
            if (lengthPrefix == encodedText.Length)
            {
                statusText = DecodeResultStatusText(encodedText);
                return true;
            }

            if (encodedText.Length > 0
                && (lengthPrefix * sizeof(char)) == encodedText.Length
                && LooksLikeUtf16Le(encodedText))
            {
                statusText = DecodeResultStatusText(encodedText);
                return true;
            }

            return false;
        }

        private static int FindUtf16LeTerminator(ReadOnlySpan<byte> payload)
        {
            for (int i = 0; i + 1 < payload.Length; i += 2)
            {
                if (payload[i] == 0 && payload[i + 1] == 0)
                {
                    return i;
                }
            }

            return -1;
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

        private static bool TryBuildOriginAwareHoverCandidates(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int cursorGap,
            IReadOnlyList<Point> tooltipFrameOrigins,
            IReadOnlyList<Point> tooltipFrameSizes,
            out (Rectangle Rect, int FrameIndex)[] candidates)
        {
            candidates = null;
            if (!TryGetTooltipFrameLayout(tooltipFrameOrigins, tooltipFrameSizes, 0, out Point leftOrigin, out Point leftSize)
                || !TryGetTooltipFrameLayout(tooltipFrameOrigins, tooltipFrameSizes, 1, out Point rightOrigin, out Point rightSize))
            {
                return false;
            }

            Point aboveAnchorPoint = new(anchorPoint.X, anchorPoint.Y - tooltipHeight - cursorGap);
            candidates = new (Rectangle Rect, int FrameIndex)[]
            {
                (CreateTooltipRectFromFrameOrigin(anchorPoint, tooltipWidth, tooltipHeight, rightOrigin, rightSize), 1),
                (CreateTooltipRectFromFrameOrigin(anchorPoint, tooltipWidth, tooltipHeight, leftOrigin, leftSize), 0),
                (CreateTooltipRectFromFrameOrigin(aboveAnchorPoint, tooltipWidth, tooltipHeight, rightOrigin, rightSize), 1),
                (CreateTooltipRectFromFrameOrigin(aboveAnchorPoint, tooltipWidth, tooltipHeight, leftOrigin, leftSize), 0)
            };
            return true;
        }

        private static bool TryGetTooltipFrameLayout(
            IReadOnlyList<Point> tooltipFrameOrigins,
            IReadOnlyList<Point> tooltipFrameSizes,
            int frameIndex,
            out Point origin,
            out Point size)
        {
            origin = Point.Zero;
            size = Point.Zero;
            if (tooltipFrameOrigins == null
                || tooltipFrameSizes == null
                || frameIndex < 0
                || frameIndex >= tooltipFrameOrigins.Count
                || frameIndex >= tooltipFrameSizes.Count)
            {
                return false;
            }

            origin = tooltipFrameOrigins[frameIndex];
            size = tooltipFrameSizes[frameIndex];
            return origin != Point.Zero && size.X > 0 && size.Y > 0;
        }

        private static Rectangle CreateTooltipRectFromFrameOrigin(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            Point origin,
            Point frameSize)
        {
            float scaleX = frameSize.X > 0 ? tooltipWidth / (float)frameSize.X : 1f;
            float scaleY = frameSize.Y > 0 ? tooltipHeight / (float)frameSize.Y : 1f;
            return new Rectangle(
                anchorPoint.X - (int)Math.Round(origin.X * scaleX),
                anchorPoint.Y - (int)Math.Round(origin.Y * scaleY),
                tooltipWidth,
                tooltipHeight);
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
