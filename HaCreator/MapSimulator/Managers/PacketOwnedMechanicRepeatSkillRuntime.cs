using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using BinaryReader = MapleLib.PacketLib.PacketReader;
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
        private static readonly char[] Sg88MismatchFieldListSeparators = { ',', ';', '|', '/', '+' };
        private static readonly Regex Sg88MismatchPairRegex = new(
            @"byte[\s_\-]*(?<index>\d+)\s*(?::|=|\-)\s*(?<observed>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})\s*(?:->|=>|\bto\b|\-)\s*(?<rebuilt>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Sg88IndexedMismatchPairRegex = new(
            @"(?:byte[\s_\-]*index|byte[\s_\-]*offset|offset|index)?[\s_\-]*(?<index>\d+)\s*(?::|=)\s*(?<observed>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})\s*(?:->|=>|\bto\b|\-)\s*(?<rebuilt>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchFieldPairRegex = new(
            @"(?<field>(?:raw[\s_\-]*)?move[\s_\-]*action(?:[\s_\-]*(?:byte|flag|low[\s_\-]*bit))?|vec(?:tor)?[\s_\-]*(?:ctrl|control)(?:[\s_\-]*(?:owner|state|byte|flag))?|vec[\s_\-]*owner)\s*(?::|=)\s*(?<observed>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})\s*(?:->|=>|\bto\b|\-)\s*(?<rebuilt>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchPairValueRegex = new(
            @"(?<observed>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})\s*(?:->|=>|\bto\b|\-)\s*(?<rebuilt>0x[0-9A-Fa-f]{1,2}|\d{1,3}|[0-9A-Fa-f]{1,2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchByteListAssignmentRegex = new(
            @"[""']?(?<label>mismatch[\s_\-]*bytes|mismatch[\s_\-]*byte[\s_\-]*indices|byte[\s_\-]*indices)[""']?\s*[:=]\s*(?<value>\[[^\]]*\]|\{[^}]*\}|\([^\)]*\)|<[^>]*>|[^\s;\)]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchSingleByteAssignmentRegex = new(
            @"[""']?(?<label>mismatch[\s_\-]*byte|mismatch[\s_\-]*byte[\s_\-]*index|byte[\s_\-]*index)[""']?\s*[:=]\s*(?<value>[^\s;\),|]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MismatchFieldListAssignmentRegex = new(
            @"[""']?(?<label>mismatch[\s_\-]*fields[\s_\-]*list|mismatch[\s_\-]*field[\s_\-]*list|mismatch[\s_\-]*fields|mismatch[\s_\-]*field|mismatch[\s_\-]*field[\s_\-]*names|mismatch[\s_\-]*field[\s_\-]*name|field[\s_\-]*names|field[\s_\-]*name|fields[\s_\-]*list|field[\s_\-]*list|fields|field)[""']?\s*[:=]\s*(?<value>\[[^\]]*\]|\{[^}]*\}|\([^\)]*\)|<[^>]*>|[^;\)\r\n]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MoveActionMismatchClassAssignmentRegex = new(
            @"[""']?(?<label>move[\s_\-]*action[\s_\-]*(?:mismatch|diff|parity)|move[\s_\-]*mismatch)[""']?\s*[:=]\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_\- ]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88MoveActionFieldValueRegex = new(
            @"[""']?(?<label>(?:raw[\s_\-]*)?move[\s_\-]*action)[""']?\s*[:=]\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_\- ]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88VecCtrlMismatchClassAssignmentRegex = new(
            @"[""']?(?<label>(?:vec(?:tor)?[\s_\-]*)?(?:ctrl|control|owner|state)[\s_\-]*(?:mismatch|diff|parity)|vec[\s_\-]*mismatch)[""']?\s*[:=]\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_\- ]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sg88VecCtrlFieldValueRegex = new(
            @"[""']?(?<label>vec(?:tor)?[\s_\-]*(?:ctrl|control|owner|state)(?:[\s_\-]*byte)?|vec[\s_\-]*owner)[""']?\s*[:=]\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_\- ]*)",
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

        internal static bool TryExtractSg88ReplayParityVecCtrlMismatchClass(
            string decodeDetail,
            out string mismatchClass)
        {
            mismatchClass = null;
            if (string.IsNullOrWhiteSpace(decodeDetail))
            {
                return false;
            }

            if (TryExtractSg88ReplayParityVecCtrlMismatchClassByRegex(
                    decodeDetail,
                    Sg88VecCtrlMismatchClassAssignmentRegex,
                    out mismatchClass)
                || TryExtractSg88ReplayParityVecCtrlMismatchClassByRegex(
                    decodeDetail,
                    Sg88VecCtrlFieldValueRegex,
                    out mismatchClass))
            {
                return true;
            }

            if (decodeDetail.IndexOf("vecCtrlHighBitsOnly", StringComparison.OrdinalIgnoreCase) >= 0
                || decodeDetail.IndexOf("vectorControlHighBitsOnly", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mismatchClass = "highBitsOnly";
                return true;
            }

            if (decodeDetail.IndexOf("vecCtrlLowBitChanged", StringComparison.OrdinalIgnoreCase) >= 0
                || decodeDetail.IndexOf("vectorControlLowBitChanged", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mismatchClass = "lowBitChanged";
                return true;
            }

            if (decodeDetail.IndexOf("vecCtrlZeroToNonZero", StringComparison.OrdinalIgnoreCase) >= 0
                || decodeDetail.IndexOf("vectorControlZeroToNonZero", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mismatchClass = "zeroToNonZero";
                return true;
            }

            if (decodeDetail.IndexOf("vecCtrlNonZeroToZero", StringComparison.OrdinalIgnoreCase) >= 0
                || decodeDetail.IndexOf("vectorControlNonZeroToZero", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mismatchClass = "nonZeroToZero";
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

        private static bool TryExtractSg88ReplayParityVecCtrlMismatchClassByRegex(
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
                    || !TryNormalizeSg88VecCtrlMismatchClassToken(valueGroup.Value, out string normalizedClass))
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

        private static bool TryNormalizeSg88VecCtrlMismatchClassToken(string token, out string mismatchClass)
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
                case "vecctrlhighbitsonly":
                case "vectorcontrolhighbitsonly":
                case "samelowbit":
                case "samelowbits":
                    mismatchClass = "highBitsOnly";
                    return true;
                case "lowbitchanged":
                case "differentlowbit":
                case "differentlowbits":
                case "changedlowbit":
                case "changedlowbits":
                case "vecctrllowbitchanged":
                case "vectorcontrollowbitchanged":
                    mismatchClass = "lowBitChanged";
                    return true;
                case "zerotononzero":
                case "fromzerotononzero":
                case "nonzerofromzero":
                    mismatchClass = "zeroToNonZero";
                    return true;
                case "nonzerotozero":
                case "fromnonzerotozero":
                case "zerofromnonzero":
                    mismatchClass = "nonZeroToZero";
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

            bool parsedJsonLike = TryParseSg88ReplayParityMismatchFieldListJsonLike(normalizedSegment, parsedByteIndices);
            if (parsedJsonLike)
            {
                return parsedByteIndices
                    .OrderBy(index => index)
                    .ToArray();
            }

            string[] tokens = normalizedSegment.Split(
                Sg88MismatchFieldListSeparators,
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

            if (parsedByteIndices.Count == 0
                && normalizedSegment.IndexOf(' ', StringComparison.Ordinal) >= 0)
            {
                string[] fallbackWhitespaceTokens = normalizedSegment.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string rawToken in fallbackWhitespaceTokens)
                {
                    string token = NormalizeSg88MismatchByteToken(rawToken).Trim().Trim('"', '\'');
                    if (token.StartsWith("field", StringComparison.OrdinalIgnoreCase))
                    {
                        token = token.Substring("field".Length).TrimStart(':', '=', '_', '-').Trim();
                    }

                    if (!TryParseSg88ReplayParityMismatchFieldToken(token, out int[] fieldByteIndices))
                    {
                        continue;
                    }

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

        private static bool TryParseSg88ReplayParityMismatchFieldListJsonLike(string normalizedSegment, ISet<int> parsedByteIndices)
        {
            if (string.IsNullOrWhiteSpace(normalizedSegment)
                || parsedByteIndices == null
                || normalizedSegment.Length == 0)
            {
                return false;
            }

            char first = normalizedSegment[0];
            if (first != '{' && first != '[')
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(normalizedSegment);
                CollectSg88ReplayParityMismatchFieldIndicesFromJsonElement(document.RootElement, parsedByteIndices);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void CollectSg88ReplayParityMismatchFieldIndicesFromJsonElement(
            JsonElement element,
            ISet<int> parsedByteIndices)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        string propertyName = property.Name;
                        if (string.IsNullOrWhiteSpace(propertyName))
                        {
                            CollectSg88ReplayParityMismatchFieldIndicesFromJsonElement(property.Value, parsedByteIndices);
                            continue;
                        }

                        if (TryMapSg88MismatchFieldTokenToByteIndices(propertyName, out int[] mappedFromName)
                            && IsSg88MismatchAffirmativeJsonValue(property.Value))
                        {
                            foreach (int index in mappedFromName)
                            {
                                if (index >= 0)
                                {
                                    parsedByteIndices.Add(index);
                                }
                            }
                        }

                        if (IsSg88MismatchFieldValueLabel(propertyName)
                            && TryExtractSg88ReplayParityMismatchFieldIndicesFromJsonValue(property.Value, out int[] mappedFromValue))
                        {
                            foreach (int index in mappedFromValue)
                            {
                                if (index >= 0)
                                {
                                    parsedByteIndices.Add(index);
                                }
                            }
                        }

                        CollectSg88ReplayParityMismatchFieldIndicesFromJsonElement(property.Value, parsedByteIndices);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        CollectSg88ReplayParityMismatchFieldIndicesFromJsonElement(item, parsedByteIndices);
                    }

                    break;
                case JsonValueKind.String:
                    if (TryExtractSg88ReplayParityMismatchFieldIndicesFromTextToken(element.GetString(), out int[] mapped))
                    {
                        foreach (int index in mapped)
                        {
                            if (index >= 0)
                            {
                                parsedByteIndices.Add(index);
                            }
                        }
                    }

                    break;
            }
        }

        private static bool TryExtractSg88ReplayParityMismatchFieldIndicesFromJsonValue(
            JsonElement value,
            out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return TryExtractSg88ReplayParityMismatchFieldIndicesFromTextToken(value.GetString(), out byteIndices);
                case JsonValueKind.Array:
                {
                    HashSet<int> parsedIndices = new();
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        if (TryExtractSg88ReplayParityMismatchFieldIndicesFromJsonValue(item, out int[] nestedIndices))
                        {
                            foreach (int index in nestedIndices)
                            {
                                if (index >= 0)
                                {
                                    parsedIndices.Add(index);
                                }
                            }
                        }
                    }

                    if (parsedIndices.Count == 0)
                    {
                        return false;
                    }

                    byteIndices = parsedIndices.OrderBy(index => index).ToArray();
                    return true;
                }
                case JsonValueKind.Object:
                {
                    HashSet<int> parsedIndices = new();
                    CollectSg88ReplayParityMismatchFieldIndicesFromJsonElement(value, parsedIndices);
                    if (parsedIndices.Count == 0)
                    {
                        return false;
                    }

                    byteIndices = parsedIndices.OrderBy(index => index).ToArray();
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool TryExtractSg88ReplayParityMismatchFieldIndicesFromTextToken(
            string token,
            out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (TryParseSg88ReplayParityMismatchFieldToken(token, out int[] mapped))
            {
                byteIndices = mapped;
                return true;
            }

            string normalizedToken = NormalizeSg88MismatchByteToken(token).Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return false;
            }

            string[] delimitedTokens = normalizedToken.Split(
                Sg88MismatchFieldListSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            HashSet<int> parsedIndices = new();
            foreach (string delimitedToken in delimitedTokens)
            {
                if (!TryParseSg88ReplayParityMismatchFieldToken(delimitedToken, out int[] parsed))
                {
                    continue;
                }

                foreach (int index in parsed)
                {
                    if (index >= 0)
                    {
                        parsedIndices.Add(index);
                    }
                }
            }

            if (parsedIndices.Count == 0
                && normalizedToken.IndexOf(' ', StringComparison.Ordinal) >= 0)
            {
                string[] whitespaceTokens = normalizedToken.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string whitespaceToken in whitespaceTokens)
                {
                    if (!TryParseSg88ReplayParityMismatchFieldToken(whitespaceToken, out int[] parsed))
                    {
                        continue;
                    }

                    foreach (int index in parsed)
                    {
                        if (index >= 0)
                        {
                            parsedIndices.Add(index);
                        }
                    }
                }
            }

            if (parsedIndices.Count == 0)
            {
                return false;
            }

            byteIndices = parsedIndices.OrderBy(index => index).ToArray();
            return true;
        }

        private static bool IsSg88MismatchFieldValueLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            string normalized = label.Trim()
                .Trim('"', '\'')
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            return normalized is
                "field" or "fields"
                or "fieldname" or "fieldnames"
                or "fieldlist" or "fieldslist"
                or "mismatchfield" or "mismatchfields"
                or "mismatchfieldname" or "mismatchfieldnames"
                or "mismatchfieldlist" or "mismatchfieldslist";
        }

        private static bool IsSg88MismatchAffirmativeJsonValue(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return false;
                case JsonValueKind.Number:
                    if (value.TryGetInt64(out long integral))
                    {
                        return integral != 0;
                    }

                    return value.TryGetDouble(out double floating) && Math.Abs(floating) > double.Epsilon;
                case JsonValueKind.String:
                    return !IsSg88MismatchFalseLikeValueToken(value.GetString());
                case JsonValueKind.Array:
                    return value.EnumerateArray().Any(IsSg88MismatchAffirmativeJsonValue);
                case JsonValueKind.Object:
                    return TryResolveSg88MismatchAffirmativeJsonObject(value, out bool objectAffirmative)
                        && objectAffirmative;
                default:
                    return false;
            }
        }

        private static bool TryResolveSg88MismatchAffirmativeJsonObject(JsonElement objectValue, out bool affirmative)
        {
            affirmative = false;
            bool sawSignal = false;
            foreach (JsonProperty property in objectValue.EnumerateObject())
            {
                if (!TryNormalizeSg88MismatchSignalName(property.Name, out string signalName))
                {
                    continue;
                }

                sawSignal = true;
                if (!TryEvaluateSg88MismatchSignalValue(signalName, property.Value, out bool signalAffirmative))
                {
                    continue;
                }

                if (signalAffirmative)
                {
                    affirmative = true;
                    return true;
                }
            }

            return sawSignal;
        }

        private static bool TryNormalizeSg88MismatchSignalName(string signalName, out string normalizedSignalName)
        {
            normalizedSignalName = null;
            if (string.IsNullOrWhiteSpace(signalName))
            {
                return false;
            }

            string normalized = signalName.Trim()
                .Trim('"', '\'')
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace("/", string.Empty, StringComparison.Ordinal)
                .Replace("\\", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            switch (normalized)
            {
                case "mismatch":
                case "ismismatch":
                case "different":
                case "isdifferent":
                case "diff":
                case "changed":
                case "ischanged":
                case "change":
                case "delta":
                case "unequal":
                case "isunequal":
                case "notequal":
                    normalizedSignalName = "mismatch";
                    return true;
                case "match":
                case "matched":
                case "ismatch":
                case "equal":
                case "isequal":
                case "same":
                case "issame":
                    normalizedSignalName = "matched";
                    return true;
                case "parity":
                case "status":
                case "result":
                case "state":
                    normalizedSignalName = "status";
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryEvaluateSg88MismatchSignalValue(
            string signalName,
            JsonElement signalValue,
            out bool affirmative)
        {
            affirmative = false;
            switch (signalName)
            {
                case "mismatch":
                    affirmative = IsSg88MismatchAffirmativeJsonValue(signalValue);
                    return true;
                case "matched":
                    if (!TryResolveSg88MatchedJsonValue(signalValue, out bool matchedValue))
                    {
                        return false;
                    }

                    affirmative = !matchedValue;
                    return true;
                case "status":
                    if (TryResolveSg88MismatchStatusJsonValue(signalValue, out bool mismatchStatus))
                    {
                        affirmative = mismatchStatus;
                    }
                    else
                    {
                        affirmative = IsSg88MismatchAffirmativeJsonValue(signalValue);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static bool TryResolveSg88MismatchStatusJsonValue(JsonElement statusValue, out bool mismatchStatus)
        {
            mismatchStatus = false;
            switch (statusValue.ValueKind)
            {
                case JsonValueKind.String:
                    return TryResolveSg88MismatchStatusToken(statusValue.GetString(), out mismatchStatus);
                case JsonValueKind.Number:
                    if (statusValue.TryGetInt64(out long integral))
                    {
                        mismatchStatus = integral != 0;
                        return true;
                    }

                    if (statusValue.TryGetDouble(out double floating))
                    {
                        mismatchStatus = Math.Abs(floating) > double.Epsilon;
                        return true;
                    }

                    return false;
                case JsonValueKind.True:
                    mismatchStatus = true;
                    return true;
                case JsonValueKind.False:
                    mismatchStatus = false;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryResolveSg88MatchedJsonValue(JsonElement matchedValue, out bool matched)
        {
            matched = false;
            switch (matchedValue.ValueKind)
            {
                case JsonValueKind.True:
                    matched = true;
                    return true;
                case JsonValueKind.False:
                    matched = false;
                    return true;
                case JsonValueKind.Number:
                    if (matchedValue.TryGetInt64(out long integral))
                    {
                        matched = integral != 0;
                        return true;
                    }

                    if (matchedValue.TryGetDouble(out double floating))
                    {
                        matched = Math.Abs(floating) > double.Epsilon;
                        return true;
                    }

                    return false;
                case JsonValueKind.String:
                {
                    if (TryResolveSg88MismatchStatusToken(matchedValue.GetString(), out bool mismatchStatus))
                    {
                        matched = !mismatchStatus;
                        return true;
                    }

                    return false;
                }
                case JsonValueKind.Object:
                    if (TryResolveSg88MismatchAffirmativeJsonObject(matchedValue, out bool objectAffirmative))
                    {
                        matched = !objectAffirmative;
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static bool TryResolveSg88MismatchStatusToken(string token, out bool mismatchStatus)
        {
            mismatchStatus = false;
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
                case "mismatch":
                case "different":
                case "diff":
                case "changed":
                case "unmatched":
                case "notmatched":
                case "notequal":
                case "unequal":
                    mismatchStatus = true;
                    return true;
                case "match":
                case "matched":
                case "equal":
                case "same":
                case "identical":
                    mismatchStatus = false;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseSg88ReplayParityMismatchFieldToken(string token, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (TryParseSg88ReplayParityMismatchFieldAssignmentToken(token, out int[] assignmentByteIndices))
            {
                byteIndices = assignmentByteIndices;
                return true;
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

        private static bool TryParseSg88ReplayParityMismatchFieldAssignmentToken(string token, out int[] byteIndices)
        {
            byteIndices = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string trimmedToken = token.Trim();
            int separatorIndex = trimmedToken.IndexOfAny(new[] { ':', '=' });
            if (separatorIndex <= 0 || separatorIndex >= trimmedToken.Length - 1)
            {
                return false;
            }

            string left = trimmedToken.Substring(0, separatorIndex).Trim();
            string right = trimmedToken.Substring(separatorIndex + 1).Trim();
            if (IsSg88MismatchFalseLikeValueToken(right))
            {
                return false;
            }

            if (TryMapSg88MismatchFieldTokenToByteIndices(left, out int[] mappedLeft))
            {
                byteIndices = mappedLeft;
                return true;
            }

            if (TryMapSg88MismatchFieldTokenToByteIndices(right, out int[] mappedRight))
            {
                byteIndices = mappedRight;
                return true;
            }

            return false;
        }

        private static bool IsSg88MismatchFalseLikeValueToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            string normalized = token.Trim()
                .Trim('"', '\'')
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            return normalized is "0" or "false" or "off" or "disabled"
                or "no" or "none" or "null" or "na" or "n/a"
                or "nomismatch" or "matched" or "equal" or "same";
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
                .Replace("/", string.Empty, StringComparison.Ordinal)
                .Replace("\\", string.Empty, StringComparison.Ordinal)
                .Replace("[", string.Empty, StringComparison.Ordinal)
                .Replace("]", string.Empty, StringComparison.Ordinal)
                .Replace("(", string.Empty, StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal)
                .Replace("{", string.Empty, StringComparison.Ordinal)
                .Replace("}", string.Empty, StringComparison.Ordinal)
                .Replace("<", string.Empty, StringComparison.Ordinal)
                .Replace(">", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            if (normalized.StartsWith("payload", StringComparison.Ordinal))
            {
                normalized = normalized.Substring("payload".Length);
            }

            string[] prefixedAliases =
            {
                "mismatchfieldnames",
                "mismatchfieldslist",
                "mismatchfieldname",
                "mismatchfieldlist",
                "mismatchfields",
                "mismatchfield",
                "fieldnames",
                "fieldslist",
                "fieldname",
                "fieldlist",
                "fields",
                "field"
            };
            bool trimmedPrefix;
            do
            {
                trimmedPrefix = false;
                for (int i = 0; i < prefixedAliases.Length; i++)
                {
                    string alias = prefixedAliases[i];
                    if (!normalized.StartsWith(alias, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    normalized = normalized.Substring(alias.Length);
                    trimmedPrefix = true;
                    break;
                }
            } while (trimmedPrefix && normalized.Length > 0);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
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
                case "requesttimems":
                case "requestms":
                case "requestedat":
                case "requestedtime":
                case "requestedtick":
                case "reqms":
                case "reqat":
                case "reqtime":
                case "tick":
                    byteIndices = new[] { 2, 3, 4, 5 };
                    return true;
                case "skillid":
                case "skill":
                    byteIndices = new[] { 6, 7, 8, 9 };
                    return true;
                case "skilllevel":
                case "skilllvl":
                case "skilllv":
                case "slv":
                case "lv":
                case "level":
                case "skilllevelbyte":
                case "skilllvbyte":
                    byteIndices = new[] { 10 };
                    return true;
                case "x":
                case "xpos":
                case "posx":
                case "coordx":
                case "positionx":
                case "xposition":
                    byteIndices = new[] { 11, 12 };
                    return true;
                case "y":
                case "ypos":
                case "posy":
                case "coordy":
                case "positiony":
                case "yposition":
                    byteIndices = new[] { 13, 14 };
                    return true;
                case "moveaction":
                case "move":
                case "moveactionbyte":
                case "moveactionflag":
                case "rawmoveaction":
                case "rawmove":
                case "movebyte":
                case "moveactionlowbit":
                case "rawmoveactionbyte":
                case "rawmovebyte":
                case "moveactionmismatch":
                case "moveactiondiff":
                case "moveactionparity":
                case "rawmoveactionmismatch":
                case "rawmoveactiondiff":
                case "rawmoveactionparity":
                case "moveactionbytemismatch":
                case "moveactionbyteparity":
                case "moveactionbytediff":
                    byteIndices = new[] { Sg88FirstUseMoveActionByteIndex };
                    return true;
                case "vecctrl":
                case "vecctrlbyte":
                case "vecctrlowner":
                case "vecctrlstate":
                case "vectorctrl":
                case "vectorcontrol":
                case "vectorcontrolowner":
                case "vectorcontrolstate":
                case "vectorcontrolbyte":
                case "vec":
                case "vecowner":
                case "vecctrlflag":
                case "vecctrlownerbyte":
                case "vecctrlmismatch":
                case "vecctrldiff":
                case "vecctrlparity":
                case "vecctrlbytemismatch":
                case "vecctrlbyteparity":
                case "vecctrlbytediff":
                case "vecctrlownermismatch":
                case "vecctrlownerdiff":
                case "vecctrlownerparity":
                case "vectorcontrolmismatch":
                case "vectorcontroldiff":
                case "vectorcontrolparity":
                case "vectorcontrolbytemismatch":
                case "vectorcontrolbyteparity":
                case "vectorcontrolbytediff":
                case "vectorcontrolownermismatch":
                case "vectorcontrolownerdiff":
                case "vectorcontrolownerparity":
                case "vecmismatch":
                case "vecdiff":
                case "vecparity":
                case "vecownermismatch":
                case "vecownerdiff":
                case "vecownerparity":
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

            Dictionary<int, string> normalizedByByte = new();
            MatchCollection byteMatches = Sg88MismatchPairRegex.Matches(rawPairSegment);
            foreach (Match match in byteMatches.Cast<Match>())
            {
                if (!TryParseSg88ReplayParityMismatchPair(match.Value, out int byteIndex, out string normalizedPair))
                {
                    continue;
                }

                normalizedByByte[byteIndex] = normalizedPair;
            }

            MatchCollection indexedMatches = Sg88IndexedMismatchPairRegex.Matches(rawPairSegment);
            foreach (Match match in indexedMatches.Cast<Match>())
            {
                if (!TryParseSg88ReplayParityMismatchPair(match.Value, out int byteIndex, out string normalizedPair))
                {
                    continue;
                }

                normalizedByByte[byteIndex] = normalizedPair;
            }

            MatchCollection fieldMatches = Sg88MismatchFieldPairRegex.Matches(rawPairSegment);
            foreach (Match match in fieldMatches.Cast<Match>())
            {
                if (!TryParseSg88ReplayParityMismatchFieldPair(match, out int byteIndex, out string normalizedPair))
                {
                    continue;
                }

                normalizedByByte[byteIndex] = normalizedPair;
            }

            if (normalizedByByte.Count == 0)
            {
                TryExtractSg88ReplayParityMismatchPairsJsonLike(rawPairSegment, normalizedByByte);
                if (normalizedByByte.Count == 0
                    && !ReferenceEquals(rawPairSegment, decodeDetail))
                {
                    TryExtractSg88ReplayParityMismatchPairsJsonLike(decodeDetail, normalizedByByte);
                }

                if (normalizedByByte.Count == 0)
                {
                    return false;
                }
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

        private static bool TryExtractSg88ReplayParityMismatchPairsJsonLike(
            string rawSegment,
            IDictionary<int, string> normalizedByByte)
        {
            if (string.IsNullOrWhiteSpace(rawSegment)
                || normalizedByByte == null)
            {
                return false;
            }

            string normalizedSegment = rawSegment.Trim();
            if (normalizedSegment.Length == 0)
            {
                return false;
            }

            int jsonStart = normalizedSegment.IndexOfAny(new[] { '{', '[' });
            if (jsonStart < 0)
            {
                return false;
            }

            normalizedSegment = normalizedSegment.Substring(jsonStart);
            try
            {
                using JsonDocument document = JsonDocument.Parse(normalizedSegment);
                int before = normalizedByByte.Count;
                CollectSg88ReplayParityMismatchPairsFromJsonElement(
                    document.RootElement,
                    normalizedByByte,
                    insidePairContainer: false);
                return normalizedByByte.Count > before;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void CollectSg88ReplayParityMismatchPairsFromJsonElement(
            JsonElement element,
            IDictionary<int, string> normalizedByByte,
            bool insidePairContainer)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (TryResolveSg88ReplayParityMismatchPairJsonObject(
                        element,
                        out int byteIndex,
                        out string normalizedPair))
                    {
                        normalizedByByte[byteIndex] = normalizedPair;
                    }

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (TryParseSg88MismatchPairPropertyByteIndex(property.Name, out int propertyByteIndex)
                            && TryResolveSg88ReplayParityMismatchPairJsonValueWithByteIndex(
                                property.Value,
                                propertyByteIndex,
                                out string propertyPair))
                        {
                            normalizedByByte[propertyByteIndex] = propertyPair;
                        }
                        else if (TryResolveSg88MismatchPairPropertyFieldByteIndex(property.Name, out int propertyFieldByteIndex)
                                 && TryResolveSg88ReplayParityMismatchPairJsonValueWithByteIndex(
                                     property.Value,
                                     propertyFieldByteIndex,
                                     out string propertyFieldPair))
                        {
                            normalizedByByte[propertyFieldByteIndex] = propertyFieldPair;
                        }

                        bool childPairContainer = insidePairContainer
                            || IsSg88MismatchPairJsonLabel(property.Name);
                        CollectSg88ReplayParityMismatchPairsFromJsonElement(
                            property.Value,
                            normalizedByByte,
                            childPairContainer);
                    }

                    break;
                case JsonValueKind.Array:
                    if (insidePairContainer
                        && TryResolveSg88ReplayParityMismatchPairJsonArray(
                            element,
                            out int arrayByteIndex,
                            out string arrayPair))
                    {
                        normalizedByByte[arrayByteIndex] = arrayPair;
                    }

                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        CollectSg88ReplayParityMismatchPairsFromJsonElement(
                            item,
                            normalizedByByte,
                            insidePairContainer);
                    }

                    break;
                case JsonValueKind.String:
                    if (insidePairContainer)
                    {
                        string rawString = element.GetString() ?? string.Empty;
                        MatchCollection byteMatches = Sg88MismatchPairRegex.Matches(rawString);
                        foreach (Match match in byteMatches.Cast<Match>())
                        {
                            if (TryParseSg88ReplayParityMismatchPair(
                                match.Value,
                                out int parsedByteIndex,
                                out string parsedPair))
                            {
                                normalizedByByte[parsedByteIndex] = parsedPair;
                            }
                        }

                        MatchCollection indexedMatches = Sg88IndexedMismatchPairRegex.Matches(rawString);
                        foreach (Match match in indexedMatches.Cast<Match>())
                        {
                            if (TryParseSg88ReplayParityMismatchPair(
                                match.Value,
                                out int parsedByteIndex,
                                out string parsedPair))
                            {
                                normalizedByByte[parsedByteIndex] = parsedPair;
                            }
                        }

                        MatchCollection fieldMatches = Sg88MismatchFieldPairRegex.Matches(rawString);
                        foreach (Match match in fieldMatches.Cast<Match>())
                        {
                            if (TryParseSg88ReplayParityMismatchFieldPair(
                                match,
                                out int parsedByteIndex,
                                out string parsedPair))
                            {
                                normalizedByByte[parsedByteIndex] = parsedPair;
                            }
                        }
                    }

                    break;
            }
        }

        private static bool TryResolveSg88ReplayParityMismatchPairJsonObject(
            JsonElement element,
            out int byteIndex,
            out string normalizedPair)
        {
            byteIndex = -1;
            normalizedPair = null;
            byte? observed = null;
            byte? rebuilt = null;

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (TryNormalizeSg88MismatchPairJsonPropertyName(property.Name, out string normalizedName))
                {
                    switch (normalizedName)
                    {
                        case "byte":
                            if (TryParseSg88MismatchPairJsonByteIndex(property.Value, out int parsedByteIndex))
                            {
                                byteIndex = parsedByteIndex;
                            }
                            break;
                        case "field":
                            if (property.Value.ValueKind == JsonValueKind.String
                                && TryResolveSg88MismatchPairPropertyFieldByteIndex(
                                    property.Value.GetString(),
                                    out int parsedFieldByteIndex))
                            {
                                byteIndex = parsedFieldByteIndex;
                            }
                            break;
                        case "observed":
                            if (TryParseSg88MismatchPairJsonByteValue(property.Value, out byte observedByte))
                            {
                                observed = observedByte;
                            }
                            break;
                        case "rebuilt":
                            if (TryParseSg88MismatchPairJsonByteValue(property.Value, out byte rebuiltByte))
                            {
                                rebuilt = rebuiltByte;
                            }
                            break;
                    }
                }

                if (byteIndex < 0
                    && TryParseSg88MismatchPairPropertyByteIndex(property.Name, out int byteIndexFromName)
                    && property.Value.ValueKind == JsonValueKind.Object)
                {
                    byteIndex = byteIndexFromName;
                }
            }

            if (byteIndex < 0 || !observed.HasValue || !rebuilt.HasValue)
            {
                return false;
            }

            normalizedPair = $"byte{byteIndex}:0x{observed.Value:X2}->0x{rebuilt.Value:X2}";
            return true;
        }

        private static bool TryResolveSg88ReplayParityMismatchPairJsonValueWithByteIndex(
            JsonElement value,
            int byteIndex,
            out string normalizedPair)
        {
            normalizedPair = null;
            if (byteIndex < 0)
            {
                return false;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => TryResolveSg88ReplayParityMismatchPairJsonObjectWithByteIndex(
                    value,
                    byteIndex,
                    out normalizedPair),
                JsonValueKind.String => TryResolveSg88ReplayParityMismatchPairJsonStringWithByteIndex(
                    value.GetString(),
                    byteIndex,
                    out normalizedPair),
                JsonValueKind.Array => TryResolveSg88ReplayParityMismatchPairJsonArrayWithByteIndex(
                    value,
                    byteIndex,
                    out normalizedPair),
                _ => false
            };
        }

        private static bool TryResolveSg88ReplayParityMismatchPairJsonObjectWithByteIndex(
            JsonElement element,
            int byteIndex,
            out string normalizedPair)
        {
            normalizedPair = null;
            if (byteIndex < 0)
            {
                return false;
            }

            byte? observed = null;
            byte? rebuilt = null;
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!TryNormalizeSg88MismatchPairJsonPropertyName(property.Name, out string normalizedName))
                {
                    continue;
                }

                switch (normalizedName)
                {
                    case "observed":
                        if (TryParseSg88MismatchPairJsonByteValue(property.Value, out byte observedByte))
                        {
                            observed = observedByte;
                        }
                        break;
                    case "rebuilt":
                        if (TryParseSg88MismatchPairJsonByteValue(property.Value, out byte rebuiltByte))
                        {
                            rebuilt = rebuiltByte;
                        }
                        break;
                }
            }

            if (!observed.HasValue || !rebuilt.HasValue)
            {
                return false;
            }

            normalizedPair = $"byte{byteIndex}:0x{observed.Value:X2}->0x{rebuilt.Value:X2}";
            return true;
        }

        private static bool TryResolveSg88ReplayParityMismatchPairJsonArray(
            JsonElement element,
            out int byteIndex,
            out string normalizedPair)
        {
            byteIndex = -1;
            normalizedPair = null;
            if (element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            JsonElement[] items = element.EnumerateArray().Take(3).ToArray();
            if (items.Length < 3
                || !TryParseSg88MismatchPairJsonByteOrFieldIndex(items[0], out byteIndex)
                || !TryParseSg88MismatchPairJsonByteValue(items[1], out byte observed)
                || !TryParseSg88MismatchPairJsonByteValue(items[2], out byte rebuilt))
            {
                byteIndex = -1;
                return false;
            }

            normalizedPair = $"byte{byteIndex}:0x{observed:X2}->0x{rebuilt:X2}";
            return true;
        }

        private static bool TryResolveSg88ReplayParityMismatchPairJsonArrayWithByteIndex(
            JsonElement element,
            int byteIndex,
            out string normalizedPair)
        {
            normalizedPair = null;
            if (byteIndex < 0 || element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            JsonElement[] items = element.EnumerateArray().Take(2).ToArray();
            if (items.Length < 2
                || !TryParseSg88MismatchPairJsonByteValue(items[0], out byte observed)
                || !TryParseSg88MismatchPairJsonByteValue(items[1], out byte rebuilt))
            {
                return false;
            }

            normalizedPair = $"byte{byteIndex}:0x{observed:X2}->0x{rebuilt:X2}";
            return true;
        }

        private static bool TryResolveSg88ReplayParityMismatchPairJsonStringWithByteIndex(
            string value,
            int byteIndex,
            out string normalizedPair)
        {
            normalizedPair = null;
            if (byteIndex < 0
                || !TryParseSg88MismatchPairDeltaText(value, out byte observed, out byte rebuilt))
            {
                return false;
            }

            normalizedPair = $"byte{byteIndex}:0x{observed:X2}->0x{rebuilt:X2}";
            return true;
        }

        private static bool TryNormalizeSg88MismatchPairJsonPropertyName(string propertyName, out string normalizedName)
        {
            normalizedName = null;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string normalized = propertyName.Trim()
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            switch (normalized)
            {
                case "byte":
                case "byteindex":
                case "byteoffset":
                case "offset":
                case "index":
                    normalizedName = "byte";
                    return true;
                case "field":
                case "fieldname":
                case "fieldpath":
                    normalizedName = "field";
                    return true;
                case "observed":
                case "observedbyte":
                case "actual":
                case "raw":
                case "captured":
                case "official":
                case "client":
                case "from":
                case "before":
                case "left":
                    normalizedName = "observed";
                    return true;
                case "rebuilt":
                case "rebuiltbyte":
                case "expected":
                case "replay":
                case "replayed":
                case "simulator":
                case "simulated":
                case "generated":
                case "to":
                case "after":
                case "right":
                    normalizedName = "rebuilt";
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSg88MismatchPairJsonLabel(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string normalized = propertyName.Trim()
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            return normalized is "mismatchpairs"
                or "replaymismatchpairs"
                or "replayparitymismatchpairs"
                or "pairs"
                or "bytepairs";
        }

        private static bool TryParseSg88MismatchPairPropertyByteIndex(string propertyName, out int byteIndex)
        {
            byteIndex = -1;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string trimmedName = propertyName.Trim().Trim('"', '\'');
            if (int.TryParse(trimmedName, out byteIndex) && byteIndex >= 0)
            {
                return true;
            }

            Match match = Regex.Match(
                propertyName,
                @"^(?:byte|byte[\s_\-]*index|byte[\s_\-]*offset|offset|index)[\s_\-]*(?<index>\d+)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            return match.Success
                   && int.TryParse(match.Groups["index"].Value, out byteIndex)
                   && byteIndex >= 0;
        }

        private static bool TryResolveSg88MismatchPairPropertyFieldByteIndex(string propertyName, out int byteIndex)
        {
            byteIndex = -1;
            if (string.IsNullOrWhiteSpace(propertyName)
                || !TryMapSg88MismatchFieldTokenToByteIndices(propertyName, out int[] mappedByteIndices)
                || mappedByteIndices.Length != 1)
            {
                return false;
            }

            byteIndex = mappedByteIndices[0];
            return byteIndex >= 0;
        }

        private static bool TryParseSg88MismatchPairJsonByteIndex(JsonElement value, out int byteIndex)
        {
            byteIndex = -1;
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    return value.TryGetInt32(out byteIndex) && byteIndex >= 0;
                case JsonValueKind.String:
                    return TryParseSg88MismatchByteIndexToken(value.GetString(), out byteIndex);
                default:
                    return false;
            }
        }

        private static bool TryParseSg88MismatchPairJsonByteOrFieldIndex(JsonElement value, out int byteIndex)
        {
            byteIndex = -1;
            if (TryParseSg88MismatchPairJsonByteIndex(value, out byteIndex))
            {
                return true;
            }

            return value.ValueKind == JsonValueKind.String
                && TryResolveSg88MismatchPairPropertyFieldByteIndex(value.GetString(), out byteIndex);
        }

        private static bool TryParseSg88MismatchPairJsonByteValue(JsonElement value, out byte byteValue)
        {
            byteValue = 0;
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (!value.TryGetInt32(out int intValue) || intValue < byte.MinValue || intValue > byte.MaxValue)
                    {
                        return false;
                    }

                    byteValue = (byte)intValue;
                    return true;
                case JsonValueKind.String:
                    return TryParseSg88MismatchPairByteValue(value.GetString(), out byteValue);
                default:
                    return false;
            }
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
            if (!match.Success)
            {
                match = Sg88IndexedMismatchPairRegex.Match(token);
            }

            if (!match.Success
                || !int.TryParse(match.Groups["index"].Value, out int parsedByteIndex)
                || parsedByteIndex < 0
                || !TryParseSg88MismatchPairByteValue(match.Groups["observed"].Value, out byte observedByte)
                || !TryParseSg88MismatchPairByteValue(match.Groups["rebuilt"].Value, out byte rebuiltByte))
            {
                return false;
            }

            byteIndex = parsedByteIndex;
            normalizedPair = $"byte{parsedByteIndex}:0x{observedByte:X2}->0x{rebuiltByte:X2}";
            return true;
        }

        private static bool TryParseSg88ReplayParityMismatchFieldPair(Match match, out int byteIndex, out string normalizedPair)
        {
            byteIndex = -1;
            normalizedPair = null;
            if (match == null
                || !match.Success
                || !TryResolveSg88MismatchPairPropertyFieldByteIndex(match.Groups["field"].Value, out int parsedByteIndex)
                || !TryParseSg88MismatchPairByteValue(match.Groups["observed"].Value, out byte observedByte)
                || !TryParseSg88MismatchPairByteValue(match.Groups["rebuilt"].Value, out byte rebuiltByte))
            {
                return false;
            }

            byteIndex = parsedByteIndex;
            normalizedPair = $"byte{parsedByteIndex}:0x{observedByte:X2}->0x{rebuiltByte:X2}";
            return true;
        }

        private static bool TryParseSg88MismatchPairByteValue(string token, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim()
                .Trim('"', '\'');
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.TryParse(
                    normalized[2..],
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out value);
            }

            bool hasHexAlpha = normalized.Any(ch => (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'));
            if (hasHexAlpha)
            {
                return byte.TryParse(
                    normalized,
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out value);
            }

            return byte.TryParse(
                normalized,
                System.Globalization.NumberStyles.Integer,
                null,
                out value);
        }

        private static bool TryParseSg88MismatchPairDeltaText(
            string token,
            out byte observed,
            out byte rebuilt)
        {
            observed = 0;
            rebuilt = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            Match match = Sg88MismatchPairValueRegex.Match(token);
            return match.Success
                   && TryParseSg88MismatchPairByteValue(match.Groups["observed"].Value, out observed)
                   && TryParseSg88MismatchPairByteValue(match.Groups["rebuilt"].Value, out rebuilt);
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
