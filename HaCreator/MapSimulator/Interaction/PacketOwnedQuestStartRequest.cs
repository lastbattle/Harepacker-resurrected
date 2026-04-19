using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed record PacketOwnedQuestStartRequest(
        int Opcode,
        int RequestKind,
        int QuestId,
        int NpcTemplateId,
        int DeliveryItemPosition,
        short UserX,
        short UserY,
        bool IncludesUserPosition,
        IReadOnlyList<byte> Payload,
        string Summary)
    {
        // CQuest::StartQuest sends COutPacket(119), then subtype 1 for quest start.
        internal const int ClientOpcode = 119;
        internal const int StartRequestKind = 1;

        internal static PacketOwnedQuestStartRequest Create(
            int questId,
            int npcTemplateId,
            int deliveryItemPosition,
            short userX,
            short userY,
            bool includeUserPosition)
        {
            int normalizedQuestId = Math.Clamp(questId, ushort.MinValue, ushort.MaxValue);
            int normalizedNpcTemplateId = Math.Max(0, npcTemplateId);
            int normalizedDeliveryItemPosition = Math.Max(0, deliveryItemPosition);
            byte[] payload = BuildPayload(
                StartRequestKind,
                normalizedQuestId,
                normalizedNpcTemplateId,
                normalizedDeliveryItemPosition,
                userX,
                userY,
                includeUserPosition);

            string summary = includeUserPosition
                ? $"StartQuest request opcode {ClientOpcode} kind {StartRequestKind} quest {normalizedQuestId} npc {normalizedNpcTemplateId} itemPos {normalizedDeliveryItemPosition} userPos ({userX},{userY})"
                : $"StartQuest request opcode {ClientOpcode} kind {StartRequestKind} quest {normalizedQuestId} npc {normalizedNpcTemplateId} itemPos {normalizedDeliveryItemPosition}";
            return new PacketOwnedQuestStartRequest(
                ClientOpcode,
                StartRequestKind,
                normalizedQuestId,
                normalizedNpcTemplateId,
                normalizedDeliveryItemPosition,
                userX,
                userY,
                includeUserPosition,
                Array.AsReadOnly(payload),
                summary);
        }

        internal static bool ResolveIncludeUserPosition(bool isAutoAlertQuest)
        {
            // CQuest::StartQuest only encodes ptUserPos for subtype 1/2 when
            // CQuestMan::IsAutoAlertQuest returns false.
            return !isAutoAlertQuest;
        }

        internal static bool ResolveIsAutoCompletionAlertQuest(
            bool hasQuestInfoAutoComplete,
            bool hasQuestInfoAutoPreComplete)
        {
            // WZ QuestInfo.img publishes both autoComplete and autoPreComplete
            // completion-alert families that feed the client auto-alert owner.
            return hasQuestInfoAutoComplete || hasQuestInfoAutoPreComplete;
        }

        internal static bool ResolveShouldRegisterAutoCompletionAlertQuest(
            bool isAutoCompletionAlertQuestCandidate,
            bool hasCompletionDemandOutstanding)
        {
            // CWvsContext::TryRegisterAutoCompletionAlertQuest only keeps quests in
            // m_lAutoCompletionAlertQuest while completion demand is still unmet.
            return isAutoCompletionAlertQuestCandidate && hasCompletionDemandOutstanding;
        }

        internal static bool ResolveIsAutoCompletionAlertQuest(
            bool hasQuestInfoAutoComplete,
            bool hasQuestInfoAutoPreComplete,
            bool isRegisteredAutoCompletionAlertQuest)
        {
            bool isCandidate = ResolveIsAutoCompletionAlertQuest(
                hasQuestInfoAutoComplete,
                hasQuestInfoAutoPreComplete);
            return isCandidate && isRegisteredAutoCompletionAlertQuest;
        }

        internal static bool ResolveIsAutoAlertQuest(
            bool isAutoStartQuest,
            bool isAutoCompletionAlertQuest)
        {
            // CQuestMan::IsAutoAlertQuest = IsAutoStartQuest || IsAutoCompletionAlertQuest.
            return isAutoStartQuest || isAutoCompletionAlertQuest;
        }

        internal static bool TryDecodePayload(
            IReadOnlyList<byte> payload,
            out int requestKind,
            out int questId,
            out int npcTemplateId,
            out int deliveryItemPosition,
            out short userX,
            out short userY,
            out bool includesUserPosition,
            out string error)
        {
            requestKind = 0;
            questId = 0;
            npcTemplateId = 0;
            deliveryItemPosition = 0;
            userX = 0;
            userY = 0;
            includesUserPosition = false;
            error = null;

            if (payload == null)
            {
                error = "Quest-start payload is missing.";
                return false;
            }

            byte[] bytes = new byte[payload.Count];
            for (int i = 0; i < payload.Count; i++)
            {
                bytes[i] = payload[i];
            }

            const int baseLength = sizeof(byte) + sizeof(ushort) + sizeof(int) + sizeof(int);
            const int withPositionLength = baseLength + sizeof(short) + sizeof(short);
            if (bytes.Length != baseLength && bytes.Length != withPositionLength)
            {
                error = $"Quest-start payload must be {baseLength} or {withPositionLength} bytes.";
                return false;
            }

            using MemoryStream stream = new(bytes, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            requestKind = reader.ReadByte();
            questId = reader.ReadUInt16();
            npcTemplateId = reader.ReadInt32();
            deliveryItemPosition = reader.ReadInt32();
            if (stream.Position + sizeof(short) + sizeof(short) <= stream.Length)
            {
                userX = reader.ReadInt16();
                userY = reader.ReadInt16();
                includesUserPosition = true;
            }

            if (stream.Position != stream.Length)
            {
                error = $"Quest-start payload has {stream.Length - stream.Position} trailing byte(s).";
                return false;
            }

            return true;
        }

        private static byte[] BuildPayload(
            int requestKind,
            int questId,
            int npcTemplateId,
            int deliveryItemPosition,
            short userX,
            short userY,
            bool includeUserPosition)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)Math.Clamp(requestKind, byte.MinValue, byte.MaxValue));
            writer.Write((ushort)Math.Clamp(questId, ushort.MinValue, ushort.MaxValue));
            writer.Write(Math.Max(0, npcTemplateId));
            writer.Write(Math.Max(0, deliveryItemPosition));
            if (includeUserPosition)
            {
                writer.Write(userX);
                writer.Write(userY);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
