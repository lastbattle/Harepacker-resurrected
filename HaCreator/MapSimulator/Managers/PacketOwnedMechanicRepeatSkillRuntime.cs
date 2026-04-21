using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HaCreator.MapSimulator.Managers
{
    public readonly record struct PacketOwnedRepeatSkillModeEndAck(
        int SkillId,
        int ReturnSkillId,
        int RequestedAt);

    public readonly record struct PacketOwnedSg88ManualAttackConfirm(
        int SummonObjectId,
        int RequestedAt);

    public readonly record struct PacketOwnedSkillEffectRequest(
        int Opcode,
        int SkillId,
        int SkillLevel,
        bool SendLocal,
        byte[] Payload);

    public readonly record struct PacketOwnedSg88FirstUseRequest(
        int Opcode,
        int SkillId,
        int SkillLevel,
        int RequestTime,
        short X,
        short Y,
        byte MoveActionLowBit,
        byte RawMoveActionByte,
        byte VecCtrlState,
        byte[] Payload,
        byte[] RawPacket);

    internal static class PacketOwnedMechanicRepeatSkillRuntime
    {
        private static readonly char[] Sg88MismatchByteListSeparators = { ',', ';', '|', ' ', '/', '+' };
        private static readonly Regex Sg88MismatchPairRegex = new(
            @"byte\s*(?<index>\d+)\s*:\s*0x(?<observed>[0-9A-Fa-f]{1,2})\s*->\s*0x(?<rebuilt>[0-9A-Fa-f]{1,2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Sg88MismatchByteListAssignmentRegex = new(
            @"[""']?(?<label>mismatch[\s_\-]*bytes|mismatch[\s_\-]*byte[\s_\-]*indices|byte[\s_\-]*indices)[""']?\s*[:=]\s*(?<value>\[[^\]]*\]|\{[^}]*\}|\([^\)]*\)|<[^>]*>|[^\s;\)]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchSingleByteAssignmentRegex = new(
            @"[""']?(?<label>mismatch[\s_\-]*byte|mismatch[\s_\-]*byte[\s_\-]*index|byte[\s_\-]*index)[""']?\s*[:=]\s*(?<value>[^\s;\),|]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchFieldListAssignmentRegex = new(
            @"[""']?(?<label>mismatch[\s_\-]*fields|mismatch[\s_\-]*field|mismatch[\s_\-]*field[\s_\-]*names|mismatch[\s_\-]*field[\s_\-]*name|field[\s_\-]*names|field[\s_\-]*name|fields|field)[""']?\s*[:=]\s*(?<value>\[[^\]]*\]|\{[^}]*\}|\([^\)]*\)|<[^>]*>|[^;\)\r\n]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MoveActionMismatchClassAssignmentRegex = new(
            @"[""']?(?<label>move[\s_\-]*action[\s_\-]*(?:mismatch|diff|parity)|move[\s_\-]*mismatch)[""']?\s*[:=]\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_\- ]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MoveActionFieldValueRegex = new(
            @"[""']?(?<label>(?:raw[\s_\-]*)?move[\s_\-]*action)[""']?\s*[:=]\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_\- ]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public const int RepeatSkillModeEndAckPacketType = 1020;
        public const int Sg88ManualAttackConfirmPacketType = 1021;
        public const int SkillEffectRequestOpcode = 71;
        public const int Sg88FirstUseSummonOpcode = 103;
        public const int Sg88SkillId = 35121003;
        public const int Sg88FirstUseMoveActionByteIndex = sizeof(ushort) + (sizeof(int) * 2) + 1 + (sizeof(short) * 2);
        public const int Sg88FirstUseVecCtrlByteIndex = Sg88FirstUseMoveActionByteIndex + 1;

        public static bool TryEncodeSkillEffectRequestPayload(
            int skillId,
            int skillLevel,
            bool sendLocal,
            out byte[] payload,
            out string error)
        {
            payload = Array.Empty<byte>();
            error = "Skill-effect request requires a positive skill id.";
            if (skillId <= 0)
            {
                return false;
            }

            if (skillLevel < byte.MinValue || skillLevel > byte.MaxValue)
            {
                error = "Skill-effect request skill level must fit in one byte.";
                return false;
            }

            payload = new byte[sizeof(int) + 2];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), skillId);
            payload[sizeof(int)] = (byte)skillLevel;
            payload[sizeof(int) + 1] = sendLocal ? (byte)1 : (byte)0;
            error = null;
            return true;
        }

        public static bool TryCreateSkillEffectRequest(
            int skillId,
            int skillLevel,
            bool sendLocal,
            out PacketOwnedSkillEffectRequest request,
            out string error)
        {
            request = default;
            if (!TryEncodeSkillEffectRequestPayload(
                    skillId,
                    skillLevel,
                    sendLocal,
                    out byte[] payload,
                    out error))
            {
                return false;
            }

            request = new PacketOwnedSkillEffectRequest(
                SkillEffectRequestOpcode,
                skillId,
                skillLevel,
                sendLocal,
                payload);
            return true;
        }

        public static bool TryEncodeSg88FirstUseRequestPayload(
            int requestTime,
            int skillLevel,
            short x,
            short y,
            byte moveActionLowBit,
            byte vecCtrlState,
            out byte[] payload,
            out string error)
        {
            payload = Array.Empty<byte>();
            error = "SG-88 first-use request skill level must fit in one byte.";
            if (skillLevel < byte.MinValue || skillLevel > byte.MaxValue)
            {
                return false;
            }

            payload = new byte[(sizeof(int) * 2) + 1 + (sizeof(short) * 2) + 2];
            int offset = 0;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), requestTime);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), Sg88SkillId);
            offset += sizeof(int);
            payload[offset++] = (byte)skillLevel;
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset, sizeof(short)), x);
            offset += sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset, sizeof(short)), y);
            offset += sizeof(short);
            payload[offset++] = (byte)(moveActionLowBit & 1);
            payload[offset] = vecCtrlState;
            error = null;
            return true;
        }

        public static bool TryCreateSg88FirstUseRequest(
            int requestTime,
            int skillLevel,
            short x,
            short y,
            byte moveActionLowBit,
            byte vecCtrlState,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            request = default;
            if (!TryEncodeSg88FirstUseRequestPayload(
                    requestTime,
                    skillLevel,
                    x,
                    y,
                    moveActionLowBit,
                    vecCtrlState,
                    out byte[] payload,
                    out error))
            {
                return false;
            }

            byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)), Sg88FirstUseSummonOpcode);
            payload.CopyTo(rawPacket, sizeof(ushort));
            request = new PacketOwnedSg88FirstUseRequest(
                Sg88FirstUseSummonOpcode,
                Sg88SkillId,
                skillLevel,
                requestTime,
                x,
                y,
                (byte)(moveActionLowBit & 1),
                (byte)(moveActionLowBit & 1),
                vecCtrlState,
                payload,
                rawPacket);
            return true;
        }

        public static bool TryDecodeSg88FirstUseRequestPayload(
            byte[] payload,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            return TryDecodeSg88FirstUseRequestPayload(
                payload,
                requireCanonicalMoveActionLowBit: true,
                out request,
                out error);
        }

        public static bool TryDecodeSg88FirstUseRawPacket(
            byte[] rawPacket,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            return TryDecodeSg88FirstUseRawPacketCore(
                rawPacket,
                requireCanonicalMoveActionLowBit: true,
                out request,
                out error);
        }

        public static bool TryDecodeSg88FirstUseRawPacketWithReplayParity(
            byte[] rawPacket,
            out PacketOwnedSg88FirstUseRequest request,
            out bool replayParityMatched,
            out string error)
        {
            request = default;
            replayParityMatched = false;
            if (!TryDecodeSg88FirstUseRawPacketCore(
                    rawPacket,
                    requireCanonicalMoveActionLowBit: false,
                    out PacketOwnedSg88FirstUseRequest decoded,
                    out error))
            {
                return false;
            }

            if (!TryCreateSg88FirstUseRequest(
                    decoded.RequestTime,
                    decoded.SkillLevel,
                    decoded.X,
                    decoded.Y,
                    decoded.MoveActionLowBit,
                    decoded.VecCtrlState,
                    out PacketOwnedSg88FirstUseRequest rebuilt,
                    out string rebuildError))
            {
                error = $"SG-88 first-use replay parity failed to rebuild from decoded fields: {rebuildError}";
                return false;
            }

            replayParityMatched = rawPacket.AsSpan().SequenceEqual(rebuilt.RawPacket);
            request = decoded;
            error = replayParityMatched
                ? null
                : BuildSg88FirstUseReplayParityMismatchDetail(rawPacket, rebuilt.RawPacket);
            return true;
        }

        public static bool TryCreateSg88FirstUseReplayTemplatePacket(
            PacketOwnedSg88FirstUseRequest request,
            out byte[] templateRawPacket,
            out string error)
        {
            templateRawPacket = Array.Empty<byte>();
            if (!TryCreateSg88FirstUseRequest(
                    requestTime: 0,
                    request.SkillLevel,
                    x: 0,
                    y: 0,
                    request.MoveActionLowBit,
                    request.VecCtrlState,
                    out PacketOwnedSg88FirstUseRequest template,
                    out error))
            {
                return false;
            }

            templateRawPacket = template.RawPacket;
            return true;
        }

        private static bool TryDecodeSg88FirstUseRawPacketCore(
            byte[] rawPacket,
            bool requireCanonicalMoveActionLowBit,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            request = default;
            error = "SG-88 first-use raw packet is missing.";
            int minimumLength = sizeof(ushort) + ((sizeof(int) * 2) + 1 + (sizeof(short) * 2) + 2);
            if (rawPacket == null || rawPacket.Length != minimumLength)
            {
                return false;
            }

            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)));
            if (opcode != Sg88FirstUseSummonOpcode)
            {
                error = $"SG-88 first-use raw packet opcode must be {Sg88FirstUseSummonOpcode}, got {opcode}.";
                return false;
            }

            byte[] payload = new byte[rawPacket.Length - sizeof(ushort)];
            Buffer.BlockCopy(rawPacket, sizeof(ushort), payload, 0, payload.Length);
            if (!TryDecodeSg88FirstUseRequestPayload(
                    payload,
                    requireCanonicalMoveActionLowBit,
                    out PacketOwnedSg88FirstUseRequest decoded,
                    out error))
            {
                return false;
            }

            request = decoded with { RawPacket = (byte[])rawPacket.Clone() };
            return true;
        }

        private static bool TryDecodeSg88FirstUseRequestPayload(
            byte[] payload,
            bool requireCanonicalMoveActionLowBit,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            request = default;
            error = "SG-88 first-use request payload is missing.";
            if (payload == null || payload.Length != ((sizeof(int) * 2) + 1 + (sizeof(short) * 2) + 2))
            {
                return false;
            }

            try
            {
                int offset = 0;
                int requestTime = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                offset += sizeof(int);
                int skillId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                offset += sizeof(int);
                byte skillLevelByte = payload[offset++];
                short x = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, sizeof(short)));
                offset += sizeof(short);
                short y = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, sizeof(short)));
                offset += sizeof(short);
                byte rawMoveActionByte = payload[offset++];
                byte moveActionLowBit = (byte)(rawMoveActionByte & 1);
                byte vecCtrlState = payload[offset];

                if (skillId != Sg88SkillId)
                {
                    error = $"SG-88 first-use payload skill id must be {Sg88SkillId}, got {skillId}.";
                    return false;
                }

                if (requireCanonicalMoveActionLowBit && (rawMoveActionByte & 0xFE) != 0)
                {
                    error = "SG-88 first-use payload move-action flag must keep only the low bit.";
                    return false;
                }

                byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
                BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)), Sg88FirstUseSummonOpcode);
                payload.CopyTo(rawPacket, sizeof(ushort));
                request = new PacketOwnedSg88FirstUseRequest(
                    Sg88FirstUseSummonOpcode,
                    skillId,
                    skillLevelByte,
                    requestTime,
                    x,
                    y,
                    moveActionLowBit,
                    rawMoveActionByte,
                    vecCtrlState,
                    (byte[])payload.Clone(),
                    rawPacket);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException)
            {
                error = $"SG-88 first-use payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static string BuildSg88FirstUseReplayParityMismatchDetail(byte[] observedRawPacket, byte[] rebuiltRawPacket)
        {
            if (observedRawPacket == null || rebuiltRawPacket == null)
            {
                return "SG-88 first-use replay parity mismatch between observed raw packet and rebuilt request packet.";
            }

            int comparedLength = Math.Min(observedRawPacket.Length, rebuiltRawPacket.Length);
            int firstMismatchByteIndex = -1;
            List<string> mismatchPairs = new();
            List<int> mismatchByteIndices = new();
            for (int i = 0; i < comparedLength; i++)
            {
                if (observedRawPacket[i] == rebuiltRawPacket[i])
                {
                    continue;
                }

                if (firstMismatchByteIndex < 0)
                {
                    firstMismatchByteIndex = i;
                }

                mismatchByteIndices.Add(i);
                mismatchPairs.Add($"byte{i}:0x{observedRawPacket[i]:X2}->0x{rebuiltRawPacket[i]:X2}");
            }

            if (mismatchPairs.Count > 0)
            {
                return $"SG-88 first-use replay parity mismatch at byteIndex={firstMismatchByteIndex} observed=0x{observedRawPacket[firstMismatchByteIndex]:X2} rebuilt=0x{rebuiltRawPacket[firstMismatchByteIndex]:X2}; mismatchBytes=[{string.Join(",", mismatchByteIndices)}]; mismatchPairs=[{string.Join(",", mismatchPairs)}].";
            }

            return $"SG-88 first-use replay parity length mismatch observedLen={observedRawPacket.Length} rebuiltLen={rebuiltRawPacket.Length}.";
        }

        public static bool TryExtractSg88ReplayParityMismatchByteIndex(string decodeDetail, out int byteIndex)
        {
            byteIndex = -1;
            if (!TryExtractSg88ReplayParityMismatchByteIndices(decodeDetail, out int[] byteIndices)
                || byteIndices.Length == 0)
            {
                return false;
            }

            byteIndex = byteIndices[0];
            return true;
        }

        public static bool TryExtractSg88ReplayParityMismatchByteIndices(string decodeDetail, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            static bool TryExtractWrappedByteList(string decodeDetail, string fieldLabel, out int[] byteIndices)
            {
                byteIndices = Array.Empty<int>();
                return TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                           decodeDetail,
                           $"{fieldLabel}=[",
                           requireClosingBracket: true,
                           out byteIndices)
                       || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                           decodeDetail,
                           $"{fieldLabel}={{",
                           requireClosingBracket: true,
                           out byteIndices)
                       || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                           decodeDetail,
                           $"{fieldLabel}=(",
                           requireClosingBracket: true,
                           out byteIndices)
                       || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                           decodeDetail,
                           $"{fieldLabel}=<",
                           requireClosingBracket: true,
                           out byteIndices);
            }

            if (TryExtractWrappedByteList(decodeDetail, "mismatchBytes", out int[] bracketByteIndices)
                || TryExtractWrappedByteList(decodeDetail, "mismatchByteIndices", out bracketByteIndices)
                || TryExtractWrappedByteList(decodeDetail, "byteIndices", out bracketByteIndices))
            {
                byteIndices = bracketByteIndices;
                return true;
            }

            if (TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                    decodeDetail,
                    "mismatchBytes=",
                    requireClosingBracket: false,
                    out int[] compactByteIndices)
                || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                    decodeDetail,
                    "mismatchBytes:",
                    requireClosingBracket: false,
                    out compactByteIndices)
                || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                    decodeDetail,
                    "mismatchByteIndices=",
                    requireClosingBracket: false,
                    out compactByteIndices)
                || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                    decodeDetail,
                    "mismatchByteIndices:",
                    requireClosingBracket: false,
                    out compactByteIndices)
                || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                    decodeDetail,
                    "byteIndices=",
                    requireClosingBracket: false,
                    out compactByteIndices)
                || TryExtractSg88ReplayParityMismatchByteIndicesSegment(
                    decodeDetail,
                    "byteIndices:",
                    requireClosingBracket: false,
                    out compactByteIndices))
            {
                byteIndices = compactByteIndices;
                return true;
            }

            if (TryExtractSg88ReplayParityMismatchByteIndicesByRegex(decodeDetail, out int[] regexByteIndices))
            {
                byteIndices = regexByteIndices;
                return true;
            }

            if (TryExtractSg88ReplayParityMismatchSingleByteValue(decodeDetail, "mismatchByte=", out int mismatchByteIndex)
                || TryExtractSg88ReplayParityMismatchSingleByteValue(decodeDetail, "mismatchByte:", out mismatchByteIndex)
                || TryExtractSg88ReplayParityMismatchSingleByteValue(decodeDetail, "mismatchByteIndex=", out mismatchByteIndex)
                || TryExtractSg88ReplayParityMismatchSingleByteValue(decodeDetail, "mismatchByteIndex:", out mismatchByteIndex))
            {
                byteIndices = new[] { mismatchByteIndex };
                return true;
            }

            if (TryExtractSg88ReplayParityMismatchSingleByteValueByRegex(decodeDetail, out int regexMismatchByteIndex))
            {
                byteIndices = new[] { regexMismatchByteIndex };
                return true;
            }

            if (TryExtractSg88ReplayParityMismatchPairs(decodeDetail, out string[] mismatchPairs)
                && mismatchPairs.Length > 0)
            {
                int[] parsed = mismatchPairs
                    .Select(pair => TryParseSg88ReplayParityMismatchPair(pair, out int index, out _) ? index : -1)
                    .Where(index => index >= 0)
                    .Distinct()
                    .OrderBy(index => index)
                    .ToArray();
                if (parsed.Length > 0)
                {
                    byteIndices = parsed;
                    return true;
                }
            }

            if (TryExtractSg88ReplayParityMismatchFieldIndicesByRegex(decodeDetail, out int[] mismatchFieldByteIndices))
            {
                byteIndices = mismatchFieldByteIndices;
                return true;
            }

            const string legacyMarker = "byteIndex=";
            if (!TryExtractSg88ReplayParityMismatchSingleByteValue(decodeDetail, legacyMarker, out int legacyByteIndex))
            {
                return false;
            }

            byteIndices = new[] { legacyByteIndex };
            return true;
        }

        private static bool TryExtractSg88ReplayParityMismatchByteIndicesSegment(
            string decodeDetail,
            string marker,
            bool requireClosingBracket,
            out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            int markerIndex = decodeDetail.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return false;
            }

            int valueStart = markerIndex + marker.Length;
            int valueEnd = valueStart;
            if (requireClosingBracket)
            {
                char closingMarker = ']';
                if (marker.Length > 0
                    && TryResolveSg88MismatchListClosingMarker(marker[^1], out char resolvedClosingMarker))
                {
                    closingMarker = resolvedClosingMarker;
                }

                valueEnd = decodeDetail.IndexOf(closingMarker, valueStart);
                if (valueEnd <= valueStart)
                {
                    return false;
                }
            }
            else
            {
                if (valueStart < decodeDetail.Length
                    && TryResolveSg88MismatchListClosingMarker(decodeDetail[valueStart], out char closingMarker))
                {
                    valueStart++;
                    valueEnd = decodeDetail.IndexOf(closingMarker, valueStart);
                    if (valueEnd <= valueStart)
                    {
                        return false;
                    }
                }
                else
                {
                    while (valueEnd < decodeDetail.Length)
                    {
                        char token = decodeDetail[valueEnd];
                        if (char.IsWhiteSpace(token) || token is ')' or ';')
                        {
                            break;
                        }

                        valueEnd++;
                    }

                    if (valueEnd <= valueStart)
                    {
                        return false;
                    }
                }
            }

            List<int> parsedByteIndices = ParseSg88ReplayParityMismatchByteList(
                decodeDetail.Substring(valueStart, valueEnd - valueStart));
            if (parsedByteIndices.Count == 0)
            {
                return false;
            }

            byteIndices = parsedByteIndices
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            return true;
        }

        private static bool TryExtractSg88ReplayParityMismatchSingleByteValue(
            string decodeDetail,
            string marker,
            out int byteIndex)
        {
            byteIndex = -1;
            int markerIndex = decodeDetail.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return false;
            }

            int valueStart = markerIndex + marker.Length;
            int valueEnd = valueStart;
            while (valueEnd < decodeDetail.Length)
            {
                char token = decodeDetail[valueEnd];
                if (char.IsWhiteSpace(token) || token is ')' or ';' or ',' or '|')
                {
                    break;
                }

                valueEnd++;
            }

            if (valueEnd <= valueStart
                || !TryParseSg88MismatchByteIndexToken(
                    decodeDetail.Substring(valueStart, valueEnd - valueStart),
                    out int parsedByteIndex))
            {
                return false;
            }

            byteIndex = parsedByteIndex;
            return true;
        }

        private static bool TryExtractSg88ReplayParityMismatchByteIndicesByRegex(string decodeDetail, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            MatchCollection matches = Sg88MismatchByteListAssignmentRegex.Matches(decodeDetail);
            foreach (Match match in matches.Cast<Match>())
            {
                Group valueGroup = match.Groups["value"];
                if (!valueGroup.Success)
                {
                    continue;
                }

                List<int> parsedByteIndices = ParseSg88ReplayParityMismatchByteList(valueGroup.Value);
                if (parsedByteIndices.Count == 0)
                {
                    continue;
                }

                byteIndices = parsedByteIndices
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
                return true;
            }

            return false;
        }

        private static bool TryExtractSg88ReplayParityMismatchSingleByteValueByRegex(string decodeDetail, out int byteIndex)
        {
            byteIndex = -1;
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            Match match = Sg88MismatchSingleByteAssignmentRegex.Match(decodeDetail);
            if (!match.Success)
            {
                return false;
            }

            Group valueGroup = match.Groups["value"];
            if (!valueGroup.Success
                || !TryParseSg88MismatchByteIndexToken(valueGroup.Value, out int parsedByteIndex))
            {
                return false;
            }

            byteIndex = parsedByteIndex;
            return true;
        }

        private static bool TryExtractSg88ReplayParityMismatchFieldIndicesByRegex(string decodeDetail, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            MatchCollection matches = Sg88MismatchFieldListAssignmentRegex.Matches(decodeDetail);
            foreach (Match match in matches.Cast<Match>())
            {
                Group valueGroup = match.Groups["value"];
                if (!valueGroup.Success)
                {
                    continue;
                }

                int[] parsedByteIndices = ParseSg88ReplayParityMismatchFieldList(valueGroup.Value);
                if (parsedByteIndices.Length == 0)
                {
                    continue;
                }

                byteIndices = parsedByteIndices;
                return true;
            }

            return false;
        }

        internal static bool TryExtractSg88ReplayParityMoveActionMismatchClass(
            string decodeDetail,
            out string mismatchClass)
        {
            mismatchClass = null;
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            if (TryExtractSg88ReplayParityMoveActionMismatchClassByRegex(
                    decodeDetail,
                    Sg88MoveActionMismatchClassAssignmentRegex,
                    out mismatchClass)
                || TryExtractSg88ReplayParityMoveActionMismatchClassByRegex(
                    decodeDetail,
                    Sg88MoveActionFieldValueRegex,
                    out mismatchClass))
            {
                return true;
            }

            if (decodeDetail.IndexOf("moveActionHighBitsOnly", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mismatchClass = "highBitsOnly";
                return true;
            }

            if (decodeDetail.IndexOf("lowBitChanged", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mismatchClass = "lowBitChanged";
                return true;
            }

            return false;
        }

        private static bool TryExtractSg88ReplayParityMoveActionMismatchClassByRegex(
            string decodeDetail,
            Regex matcher,
            out string mismatchClass)
        {
            mismatchClass = null;
            MatchCollection matches = matcher.Matches(decodeDetail);
            foreach (Match match in matches.Cast<Match>())
            {
                Group valueGroup = match.Groups["value"];
                if (!valueGroup.Success
                    || !TryNormalizeSg88MoveActionMismatchClassToken(valueGroup.Value, out string normalizedClass))
                {
                    continue;
                }

                mismatchClass = normalizedClass;
                return true;
            }

            return false;
        }

        private static bool TryNormalizeSg88MoveActionMismatchClassToken(string token, out string mismatchClass)
        {
            mismatchClass = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim()
                .Trim('"', '\'')
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            switch (normalized)
            {
                case "highbitsonly":
                case "moveactionhighbitsonly":
                case "rawhighbitsonly":
                case "samelowbit":
                case "samelowbits":
                    mismatchClass = "highBitsOnly";
                    return true;
                case "lowbitchanged":
                case "differentlowbit":
                case "differentlowbits":
                case "changedlowbit":
                case "changedlowbits":
                case "moveactionlowbitchanged":
                    mismatchClass = "lowBitChanged";
                    return true;
                default:
                    return false;
            }
        }

        private static List<int> ParseSg88ReplayParityMismatchByteList(string rawSegment)
        {
            List<int> parsedByteIndices = new();
            if (string.IsNullOrWhiteSpace(rawSegment))
            {
                return parsedByteIndices;
            }

            string normalizedSegment = NormalizeSg88MismatchByteListSegment(rawSegment);
            if (string.IsNullOrWhiteSpace(normalizedSegment))
            {
                return parsedByteIndices;
            }

            string[] tokens = normalizedSegment.Split(
                Sg88MismatchByteListSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string rawToken in tokens)
            {
                if (string.IsNullOrWhiteSpace(rawToken))
                {
                    continue;
                }

                string token = rawToken.Trim();
                if (TryExtractSg88MismatchByteRange(token, out int start, out int end))
                {
                    int clampedStart = Math.Min(start, end);
                    int clampedEnd = Math.Max(start, end);
                    for (int i = clampedStart; i <= clampedEnd; i++)
                    {
                        parsedByteIndices.Add(i);
                    }

                    continue;
                }

                if (TryParseSg88MismatchByteIndexToken(token, out int parsedByteIndex))
                {
                    parsedByteIndices.Add(parsedByteIndex);
                }
            }

            return parsedByteIndices;
        }

        private static int[] ParseSg88ReplayParityMismatchFieldList(string rawSegment)
        {
            HashSet<int> parsedByteIndices = new();
            if (string.IsNullOrWhiteSpace(rawSegment))
            {
                return Array.Empty<int>();
            }

            string normalizedSegment = NormalizeSg88MismatchByteListSegment(rawSegment);
            if (string.IsNullOrWhiteSpace(normalizedSegment))
            {
                return Array.Empty<int>();
            }

            string[] tokens = normalizedSegment.Split(
                Sg88MismatchByteListSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string rawToken in tokens)
            {
                if (string.IsNullOrWhiteSpace(rawToken))
                {
                    continue;
                }

                string token = NormalizeSg88MismatchByteToken(rawToken).Trim().Trim('"', '\'');
                if (token.StartsWith("field", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring("field".Length).TrimStart(':', '=', '_', '-').Trim();
                }

                if (TryParseSg88ReplayParityMismatchFieldToken(token, out int[] fieldByteIndices))
                {
                    foreach (int byteIndex in fieldByteIndices)
                    {
                        if (byteIndex >= 0)
                        {
                            parsedByteIndices.Add(byteIndex);
                        }
                    }
                }
            }

            return parsedByteIndices
                .OrderBy(index => index)
                .ToArray();
        }

        private static bool TryParseSg88ReplayParityMismatchFieldToken(string token, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string[] candidateTokens = BuildSg88MismatchFieldTokenCandidates(token);
            for (int i = 0; i < candidateTokens.Length; i++)
            {
                if (!TryMapSg88MismatchFieldTokenToByteIndices(candidateTokens[i], out int[] mappedByteIndices))
                {
                    continue;
                }

                byteIndices = mappedByteIndices;
                return true;
            }

            return false;
        }

        private static string[] BuildSg88MismatchFieldTokenCandidates(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Array.Empty<string>();
            }

            string trimmedToken = token.Trim();
            List<string> candidates = new()
            {
                trimmedToken
            };
            int separatorIndex = trimmedToken.IndexOfAny(new[] { ':', '=' });
            if (separatorIndex > 0 && separatorIndex < trimmedToken.Length - 1)
            {
                candidates.Add(trimmedToken.Substring(0, separatorIndex).Trim());
                candidates.Add(trimmedToken.Substring(separatorIndex + 1).Trim());
            }

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool TryMapSg88MismatchFieldTokenToByteIndices(string token, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim()
                .Trim('"', '\'')
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            if (normalized.StartsWith("payload", StringComparison.Ordinal))
            {
                normalized = normalized.Substring("payload".Length);
            }

            switch (normalized)
            {
                case "opcode":
                    byteIndices = new[] { 0, 1 };
                    return true;
                case "requesttime":
                case "requesttick":
                case "requestat":
                case "requesttimestamp":
                case "tick":
                    byteIndices = new[] { 2, 3, 4, 5 };
                    return true;
                case "skillid":
                case "skill":
                    byteIndices = new[] { 6, 7, 8, 9 };
                    return true;
                case "skilllevel":
                case "skilllvl":
                case "level":
                    byteIndices = new[] { 10 };
                    return true;
                case "x":
                case "xpos":
                case "positionx":
                    byteIndices = new[] { 11, 12 };
                    return true;
                case "y":
                case "ypos":
                case "positiony":
                    byteIndices = new[] { 13, 14 };
                    return true;
                case "moveaction":
                case "move":
                case "moveactionbyte":
                case "moveactionflag":
                case "rawmoveaction":
                case "moveactionlowbit":
                    byteIndices = new[] { Sg88FirstUseMoveActionByteIndex };
                    return true;
                case "vecctrl":
                case "vecctrlbyte":
                case "vecctrlowner":
                case "vecctrlstate":
                case "vectorctrl":
                case "vectorcontrol":
                    byteIndices = new[] { Sg88FirstUseVecCtrlByteIndex };
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeSg88MismatchByteListSegment(string rawSegment)
        {
            if (string.IsNullOrWhiteSpace(rawSegment))
            {
                return string.Empty;
            }

            string normalized = rawSegment.Trim()
                .TrimStart(':', '=')
                .Trim()
                .TrimEnd('.', ',', ';');
            if (normalized.StartsWith("bytes", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("bytes".Length);
            }
            else if (normalized.StartsWith("byte", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("byte".Length);
            }

            normalized = normalized.TrimStart(':', '=').Trim();
            while (normalized.Length >= 2
                   && TryResolveSg88MismatchTokenWrapper(normalized[0], out char closingWrapper)
                   && normalized[^1] == closingWrapper)
            {
                normalized = normalized.Substring(1, normalized.Length - 2).Trim();
            }

            return normalized;
        }

        private static bool TryExtractSg88MismatchByteRange(string token, out int start, out int end)
        {
            start = -1;
            end = -1;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalizedToken = token.Trim().Replace("..", "-", StringComparison.Ordinal);
            int dashIndex = normalizedToken.IndexOf('-');
            if (dashIndex <= 0 || dashIndex >= normalizedToken.Length - 1)
            {
                return false;
            }

            string leftToken = normalizedToken.Substring(0, dashIndex);
            string rightToken = normalizedToken.Substring(dashIndex + 1);
            if (!TryParseSg88MismatchByteIndexToken(leftToken, out start)
                || !TryParseSg88MismatchByteIndexToken(rightToken, out end))
            {
                start = -1;
                end = -1;
                return false;
            }

            return true;
        }

        private static bool TryParseSg88MismatchByteIndexToken(string token, out int byteIndex)
        {
            byteIndex = -1;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = NormalizeSg88MismatchByteToken(token);
            if (normalized.StartsWith("bytes", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("bytes".Length);
            }
            if (normalized.StartsWith("byte", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("byte".Length);
            }

            normalized = normalized.Trim();
            while (normalized.Length >= 2
                   && TryResolveSg88MismatchTokenWrapper(normalized[0], out char closingWrapper)
                   && normalized[^1] == closingWrapper)
            {
                normalized = normalized.Substring(1, normalized.Length - 2).Trim();
            }

            normalized = normalized.TrimStart(':', '=').Trim();
            if (normalized.StartsWith("+", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1).TrimStart();
            }

            if (normalized.EndsWith("h", StringComparison.OrdinalIgnoreCase)
                && normalized.Length > 1
                && IsSg88HexDigitsOnly(normalized.AsSpan(0, normalized.Length - 1)))
            {
                normalized = $"0x{normalized.Substring(0, normalized.Length - 1)}";
            }

            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(
                        normalized.Substring(2),
                        System.Globalization.NumberStyles.HexNumber,
                        null,
                        out int parsedHexByteIndex)
                    || parsedHexByteIndex < 0)
                {
                    return false;
                }

                byteIndex = parsedHexByteIndex;
                return true;
            }

            if (!int.TryParse(normalized, out int parsedByteIndex) || parsedByteIndex < 0)
            {
                return false;
            }

            byteIndex = parsedByteIndex;
            return true;
        }

        private static string NormalizeSg88MismatchByteToken(string token)
        {
            string normalized = token.Trim().TrimEnd('.', ',', ';');
            while (normalized.Length > 0
                   && TryResolveSg88MismatchTokenWrapper(normalized[0], out _))
            {
                normalized = normalized.Substring(1).TrimStart();
            }

            while (normalized.Length > 0
                   && TryResolveSg88MismatchClosingTokenWrapper(normalized[^1]))
            {
                normalized = normalized.Substring(0, normalized.Length - 1).TrimEnd();
            }

            return normalized;
        }

        private static bool TryResolveSg88MismatchClosingTokenWrapper(char closingWrapper)
        {
            return closingWrapper is ']' or '}' or ')' or '>';
        }

        private static bool IsSg88HexDigitsOnly(ReadOnlySpan<char> token)
        {
            if (token.IsEmpty)
            {
                return false;
            }

            foreach (char c in token)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveSg88MismatchListClosingMarker(char openingMarker, out char closingMarker)
        {
            switch (openingMarker)
            {
                case '[':
                    closingMarker = ']';
                    return true;
                case '{':
                    closingMarker = '}';
                    return true;
                case '(':
                    closingMarker = ')';
                    return true;
                case '<':
                    closingMarker = '>';
                    return true;
                default:
                    closingMarker = '\0';
                    return false;
            }
        }

        private static bool TryResolveSg88MismatchTokenWrapper(char openingWrapper, out char closingWrapper)
        {
            return TryResolveSg88MismatchListClosingMarker(openingWrapper, out closingWrapper);
        }

        public static bool TryExtractSg88ReplayParityMismatchPairs(string decodeDetail, out string[] mismatchPairs)
        {
            mismatchPairs = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            string rawPairSegment = null;
            const string marker = "mismatchPairs=[";
            int markerIndex = decodeDetail.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                int valueStart = markerIndex + marker.Length;
                int valueEnd = decodeDetail.IndexOf(']', valueStart);
                if (valueEnd > valueStart)
                {
                    rawPairSegment = decodeDetail.Substring(valueStart, valueEnd - valueStart);
                }
            }

            if (string.IsNullOrWhiteSpace(rawPairSegment))
            {
                rawPairSegment = decodeDetail;
            }

            MatchCollection matches = Sg88MismatchPairRegex.Matches(rawPairSegment);
            if (matches.Count == 0)
            {
                return false;
            }

            Dictionary<int, string> normalizedByByte = new();
            foreach (Match match in matches.Cast<Match>())
            {
                if (!TryParseSg88ReplayParityMismatchPair(match.Value, out int byteIndex, out string normalizedPair))
                {
                    continue;
                }

                normalizedByByte[byteIndex] = normalizedPair;
            }

            if (normalizedByByte.Count == 0)
            {
                return false;
            }

            mismatchPairs = normalizedByByte
                .OrderBy(entry => entry.Key)
                .Select(entry => entry.Value)
                .ToArray();
            return true;
        }

        internal static bool TryParseSg88ReplayParityMismatchPair(string token, out int byteIndex, out string normalizedPair)
        {
            byteIndex = -1;
            normalizedPair = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            Match match = Sg88MismatchPairRegex.Match(token);
            if (!match.Success
                || !int.TryParse(match.Groups["index"].Value, out int parsedByteIndex)
                || parsedByteIndex < 0
                || !byte.TryParse(match.Groups["observed"].Value, System.Globalization.NumberStyles.HexNumber, null, out byte observedByte)
                || !byte.TryParse(match.Groups["rebuilt"].Value, System.Globalization.NumberStyles.HexNumber, null, out byte rebuiltByte))
            {
                return false;
            }

            byteIndex = parsedByteIndex;
            normalizedPair = $"byte{parsedByteIndex}:0x{observedByte:X2}->0x{rebuiltByte:X2}";
            return true;
        }

        public static bool TryDecodeRepeatSkillModeEndAck(
            byte[] payload,
            out PacketOwnedRepeatSkillModeEndAck ack,
            out string error)
        {
            ack = default;
            error = "Repeat-skill mode-end ack payload is missing.";
            if (payload == null || payload.Length < (sizeof(int) * 3))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int skillId = reader.ReadInt32();
                int returnSkillId = reader.ReadInt32();
                int requestedAt = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"Repeat-skill mode-end ack payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                if (skillId <= 0)
                {
                    error = "Repeat-skill mode-end ack payload must include a positive skill id.";
                    return false;
                }

                if (returnSkillId <= 0)
                {
                    error = "Repeat-skill mode-end ack payload must include a positive return skill id.";
                    return false;
                }

                if (requestedAt == int.MinValue)
                {
                    error = "Repeat-skill mode-end ack payload must include the original request tick.";
                    return false;
                }

                ack = new PacketOwnedRepeatSkillModeEndAck(skillId, returnSkillId, requestedAt);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = $"Repeat-skill mode-end ack payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryDecodeSg88ManualAttackConfirm(
            byte[] payload,
            out PacketOwnedSg88ManualAttackConfirm confirm,
            out string error)
        {
            confirm = default;
            error = "SG-88 manual-attack confirm payload is missing.";
            if (payload == null || payload.Length < (sizeof(int) * 2))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int summonObjectId = reader.ReadInt32();
                int requestedAt = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"SG-88 manual-attack confirm payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                if (summonObjectId <= 0)
                {
                    error = "SG-88 manual-attack confirm payload must include a positive summon object id.";
                    return false;
                }

                if (requestedAt == int.MinValue)
                {
                    error = "SG-88 manual-attack confirm payload must include the original request tick.";
                    return false;
                }

                confirm = new PacketOwnedSg88ManualAttackConfirm(summonObjectId, requestedAt);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = $"SG-88 manual-attack confirm payload could not be decoded: {ex.Message}";
                return false;
            }
        }
    }
}
