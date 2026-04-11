using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum MessengerPacketType
    {
        Invite = 0,
        InviteAccept = 1,
        InviteReject = 2,
        Leave = 3,
        RoomChat = 4,
        Whisper = 5,
        MemberInfo = 6,
        Blocked = 7,
        Avatar = 8,
        Enter = 9,
        InviteResult = 10,
        Migrated = 11,
        SelfEnterResult = 12
    }

    internal readonly record struct MessengerInvitePacket(
        string ContactName,
        byte InviteType,
        int InviteSequence,
        bool SkipBlacklistAutoReject);

    internal readonly record struct MessengerChatPacket(string ContactName, string Message);

    internal readonly record struct MessengerClientChatPacket(
        string ContactName,
        string Message,
        bool IsWhisper,
        bool IsActivityPulse,
        bool ActivityEnabled);

    internal readonly record struct MessengerClientInviteRequestPacket(string ContactName);

    internal readonly record struct MessengerClientAcceptInviteRequestPacket(int InviteSequence);

    internal readonly record struct MessengerClientClaimRequestPacket(
        string TargetCharacterName,
        byte ClaimType,
        string Context,
        string ChatLog,
        bool IncludesChatLog);

    internal readonly record struct MessengerClientBlockedAutoRejectPacket(
        string InviterName,
        string LocalCharacterName,
        bool Blocked);

    internal readonly record struct MessengerMemberInfoPacket(
        string ContactName,
        bool IsOnline,
        int Channel,
        int Level,
        string JobName,
        string LocationSummary,
        string StatusText,
        LoginAvatarLook AvatarLook);

    internal readonly record struct MessengerBlockedPacket(string ContactName, bool Blocked);

    internal readonly record struct MessengerAvatarPacket(int SlotIndex, LoginAvatarLook AvatarLook);

    internal readonly record struct MessengerEnterPacket(
        int SlotIndex,
        string ContactName,
        int Channel,
        bool IsNew,
        LoginAvatarLook AvatarLook)
    {
        public bool IsOnline => true;
    }

    internal readonly record struct MessengerInviteResultPacket(string ContactName, bool InviteSent);

    internal readonly record struct MessengerLeaveSlotPacket(int SlotIndex);

    internal readonly record struct MessengerSelfEnterResultPacket(int SlotIndex)
    {
        public bool Succeeded => SlotIndex >= 0;
    }

    internal readonly record struct MessengerMigratedParticipantPacket(
        int SlotIndex,
        byte State,
        string ContactName,
        int Channel,
        LoginAvatarLook AvatarLook)
    {
        public bool ClearSlot => State == 0;
        public bool PreserveSlot => State == 1;
        public bool Present => State >= 2;
        public bool IsOnline => Present;
    }

    internal readonly record struct MessengerMigratedPacket(
        MessengerMigratedParticipantPacket[] Participants);

    internal static class MessengerPacketCodec
    {
        internal const ushort ClientMessengerResultOpcode = 372;
        internal const ushort ClientMessengerRequestOpcode = 143;
        private const string ChatSeparator = " : ";

        public static bool TryParseClientOpcodePacket(ReadOnlySpan<byte> packet, out byte[] dispatchPayload, out string error)
        {
            dispatchPayload = Array.Empty<byte>();
            error = null;

            if (packet.Length < 3)
            {
                error = "Messenger client opcode packet must include a 2-byte opcode and subtype byte.";
                return false;
            }

            ushort opcode = (ushort)(packet[0] | (packet[1] << 8));
            if (opcode != ClientMessengerResultOpcode)
            {
                error = $"Unsupported Messenger client opcode {opcode}. Expected {ClientMessengerResultOpcode} for CUIMessenger::OnPacket.";
                return false;
            }

            dispatchPayload = packet[2..].ToArray();
            return true;
        }

        public static byte[] BuildBlockedAutoRejectPayload(string inviterName, string localCharacterName)
        {
            PacketWriter writer = new();
            writer.WriteByte(5);
            writer.WriteMapleString(inviterName ?? string.Empty);
            writer.WriteMapleString(localCharacterName ?? string.Empty);
            writer.WriteByte(1);
            return writer.ToArray();
        }

        public static byte[] BuildBlockedAutoRejectOutPacket(string inviterName, string localCharacterName)
        {
            PacketWriter writer = new();
            writer.WriteShort(ClientMessengerRequestOpcode);
            writer.WriteBytes(BuildBlockedAutoRejectPayload(inviterName, localCharacterName));
            return writer.ToArray();
        }

        public static byte[] BuildInviteRequestPayload(string contactName)
        {
            PacketWriter writer = new();
            writer.WriteByte(3);
            writer.WriteMapleString(NormalizeText(contactName));
            return writer.ToArray();
        }

        public static byte[] BuildAcceptInviteRequestPayload(int inviteSequence)
        {
            PacketWriter writer = new();
            writer.WriteByte(0);
            writer.WriteInt(inviteSequence);
            return writer.ToArray();
        }

        public static byte[] BuildLeaveRequestPayload()
        {
            PacketWriter writer = new();
            writer.WriteByte(2);
            return writer.ToArray();
        }

        public static byte[] BuildProcessChatRequestPayload(string localCharacterName, string message)
        {
            PacketWriter writer = new();
            writer.WriteByte(6);
            writer.WriteMapleString($"{NormalizeText(localCharacterName)}{ChatSeparator}{NormalizeText(message)}");
            return writer.ToArray();
        }

        public static byte[] BuildClaimRequestPayload(
            string targetCharacterName,
            byte claimType,
            string context,
            string chatLog = null)
        {
            PacketWriter writer = new();
            bool includeChatLog = !string.IsNullOrWhiteSpace(chatLog);
            writer.WriteByte(includeChatLog ? (byte)1 : (byte)0);
            writer.WriteMapleString(NormalizeText(targetCharacterName));
            writer.WriteByte(claimType);
            writer.WriteMapleString(NormalizeText(context));
            if (includeChatLog)
            {
                writer.WriteMapleString(NormalizeText(chatLog));
            }

            return writer.ToArray();
        }

        public static byte[] BuildInvitePayload(
            string contactName,
            byte inviteType = 0,
            int inviteSequence = 0,
            bool skipBlacklistAutoReject = false)
        {
            PacketWriter writer = new();
            writer.WriteMapleString(NormalizeText(contactName));
            writer.WriteByte(inviteType);
            writer.WriteInt(inviteSequence);
            writer.WriteByte(skipBlacklistAutoReject ? 1 : 0);
            return writer.ToArray();
        }

        public static byte[] BuildChatPayload(string contactName, string message)
        {
            PacketWriter writer = new();
            WriteString8(writer, contactName);
            writer.WriteMapleString(NormalizeText(message));
            return writer.ToArray();
        }

        public static byte[] BuildClientChatPayload(string contactName, string message)
        {
            PacketWriter writer = new();
            writer.WriteMapleString($"{NormalizeText(contactName)}{ChatSeparator}{NormalizeText(message)}");
            return writer.ToArray();
        }

        public static byte[] BuildMemberInfoPayload(
            string contactName,
            bool isOnline,
            int channel,
            int level,
            string jobName,
            string locationSummary,
            string statusText,
            LoginAvatarLook avatarLook = null)
        {
            PacketWriter writer = new();
            WriteString8(writer, contactName);
            writer.WriteByte(isOnline ? (byte)1 : (byte)0);
            writer.WriteByte((byte)Math.Clamp(channel, 1, byte.MaxValue));
            writer.WriteShort(Math.Clamp(level, 1, short.MaxValue));
            WriteString8(writer, jobName);
            WriteString8(writer, locationSummary);
            WriteString8(writer, statusText);
            WriteLengthPrefixedAvatarLook(writer, avatarLook);
            return writer.ToArray();
        }

        public static byte[] BuildAvatarPayload(int slotIndex, LoginAvatarLook avatarLook)
        {
            if (avatarLook == null)
            {
                throw new ArgumentNullException(nameof(avatarLook));
            }

            PacketWriter writer = new();
            writer.WriteByte((byte)Math.Clamp(slotIndex, 0, byte.MaxValue));
            writer.WriteBytes(LoginAvatarLookCodec.Encode(avatarLook));
            return writer.ToArray();
        }

        public static byte[] BuildEnterPayload(
            int slotIndex,
            string contactName,
            int channel,
            bool isNew,
            LoginAvatarLook avatarLook = null)
        {
            PacketWriter writer = new();
            writer.WriteByte((byte)Math.Clamp(slotIndex, 0, byte.MaxValue));
            WriteLengthPrefixedAvatarLook(writer, avatarLook);
            WriteString8(writer, contactName);
            writer.WriteByte((byte)Math.Clamp(channel, 1, byte.MaxValue));
            writer.WriteByte(isNew ? (byte)1 : (byte)0);
            return writer.ToArray();
        }

        public static byte[] BuildBlockedPayload(string contactName, bool blocked)
        {
            PacketWriter writer = new();
            writer.WriteMapleString(NormalizeText(contactName));
            writer.WriteByte(blocked ? (byte)1 : (byte)0);
            return writer.ToArray();
        }

        public static byte[] BuildInviteResultPayload(string contactName, bool inviteSent)
        {
            PacketWriter writer = new();
            writer.WriteMapleString(NormalizeText(contactName));
            writer.WriteByte(inviteSent ? (byte)1 : (byte)0);
            return writer.ToArray();
        }

        public static byte[] BuildLeaveSlotPayload(int slotIndex)
        {
            PacketWriter writer = new();
            writer.WriteByte((byte)Math.Clamp(slotIndex, 0, byte.MaxValue));
            return writer.ToArray();
        }

        public static byte[] BuildSelfEnterResultPayload(int slotIndex)
        {
            PacketWriter writer = new();
            writer.WriteByte((byte)Math.Clamp(slotIndex, sbyte.MinValue, byte.MaxValue));
            return writer.ToArray();
        }

        public static byte[] BuildMigratedPayload(IReadOnlyList<MessengerMigratedParticipantPacket> participants)
        {
            PacketWriter writer = new();
            for (int slotIndex = 0; slotIndex < 3; slotIndex++)
            {
                MessengerMigratedParticipantPacket participant = participants != null && slotIndex < participants.Count
                    ? participants[slotIndex]
                    : new MessengerMigratedParticipantPacket(slotIndex, 0, string.Empty, 1, null);
                writer.WriteByte(participant.State);
                if (!participant.Present)
                {
                    continue;
                }

                WriteLengthPrefixedAvatarLook(writer, participant.AvatarLook);
                WriteString8(writer, participant.ContactName);
                writer.WriteByte((byte)Math.Clamp(participant.Channel, 1, byte.MaxValue));
            }

            return writer.ToArray();
        }

        public static bool TryParseClientDispatch(ReadOnlySpan<byte> payload, out byte packetType, out byte[] body, out string error)
        {
            packetType = 0;
            body = Array.Empty<byte>();
            error = null;

            if (payload.Length == 0)
            {
                error = "Messenger client packet payload is empty.";
                return false;
            }

            packetType = payload[0];
            body = payload[1..].ToArray();
            return true;
        }

        public static bool TryDescribeClientRequest(
            int opcode,
            ReadOnlySpan<byte> payload,
            out string summary)
        {
            summary = null;
            switch (opcode)
            {
                case ClientMessengerRequestOpcode:
                    if (payload.Length == 0)
                    {
                        summary = "messenger request <empty>";
                        return true;
                    }

                    byte requestType = payload[0];
                    ReadOnlySpan<byte> requestBody = payload[1..];
                    switch (requestType)
                    {
                        case 0:
                            if (TryParseClientAcceptInviteRequest(requestBody, out MessengerClientAcceptInviteRequestPacket acceptPacket, out _))
                            {
                                summary = $"CUIMessenger::TryNew request inviteSequence={acceptPacket.InviteSequence}";
                                return true;
                            }

                            summary = "CUIMessenger::TryNew request";
                            return true;
                        case 3:
                            if (TryParseClientInviteRequest(requestBody, out MessengerClientInviteRequestPacket invitePacket, out _))
                            {
                                summary = $"CUIMessenger::SendInviteMsg request target={invitePacket.ContactName}";
                                return true;
                            }

                            summary = "CUIMessenger::SendInviteMsg request";
                            return true;
                        case 2:
                            summary = "CUIMessenger::OnDestroy leave request";
                            return true;
                        case 5:
                            if (TryParseClientBlockedAutoRejectRequest(requestBody, out MessengerClientBlockedAutoRejectPacket blockedPacket, out _))
                            {
                                summary = $"CUIMessenger::OnInvite blacklist auto-reject inviter={blockedPacket.InviterName} local={blockedPacket.LocalCharacterName} blocked={blockedPacket.Blocked}";
                                return true;
                            }

                            summary = "CUIMessenger::OnInvite blacklist auto-reject request";
                            return true;
                        case 6:
                            if (TryParseClientChatRequest(requestBody, out MessengerChatPacket chatPacket, out _))
                            {
                                string speaker = string.IsNullOrWhiteSpace(chatPacket.ContactName) ? "local" : chatPacket.ContactName;
                                summary = $"CUIMessenger::ProcessChat request speaker={speaker} message={chatPacket.Message}";
                                return true;
                            }

                            summary = "CUIMessenger::ProcessChat request";
                            return true;
                        default:
                            summary = $"CUIMessenger request subtype={requestType}";
                            return true;
                    }

                case 118:
                    if (TryParseClientClaimRequest(payload, out MessengerClientClaimRequestPacket claimPacket, out _))
                    {
                        summary = claimPacket.IncludesChatLog
                            ? $"CWvsContext::SendClaimRequest target={claimPacket.TargetCharacterName} type={claimPacket.ClaimType} context={claimPacket.Context} chatLog={claimPacket.ChatLog}"
                            : $"CWvsContext::SendClaimRequest target={claimPacket.TargetCharacterName} type={claimPacket.ClaimType} context={claimPacket.Context}";
                        return true;
                    }

                    summary = "CWvsContext::SendClaimRequest";
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryDescribeClientResult(
            int opcode,
            ReadOnlySpan<byte> payload,
            out string summary)
        {
            summary = null;
            if (opcode != ClientMessengerResultOpcode)
            {
                return false;
            }

            if (!TryParseClientDispatch(payload, out byte packetSubtype, out byte[] body, out _))
            {
                summary = "CUIMessenger::OnPacket <empty>";
                return true;
            }

            switch (packetSubtype)
            {
                case 0:
                    if (TryParseEnter(body, out MessengerEnterPacket enterPacket, out _))
                    {
                        summary = $"CUIMessenger::OnEnter slot={enterPacket.SlotIndex} name={enterPacket.ContactName} channel={enterPacket.Channel} isNew={enterPacket.IsNew}";
                        return true;
                    }

                    summary = "CUIMessenger::OnEnter";
                    return true;
                case 1:
                    if (TryParseSelfEnterResult(body, out MessengerSelfEnterResultPacket selfEnterResultPacket, out _))
                    {
                        summary = selfEnterResultPacket.Succeeded
                            ? $"CUIMessenger::OnSelfEnterResult slot={selfEnterResultPacket.SlotIndex}"
                            : "CUIMessenger::OnSelfEnterResult failed";
                        return true;
                    }

                    summary = "CUIMessenger::OnSelfEnterResult";
                    return true;
                case 2:
                    if (TryParseLeaveSlot(body, out MessengerLeaveSlotPacket leavePacket, out _))
                    {
                        summary = $"CUIMessenger::OnLeave slot={leavePacket.SlotIndex}";
                        return true;
                    }

                    summary = "CUIMessenger::OnLeave";
                    return true;
                case 3:
                    if (TryParseInvite(body, out MessengerInvitePacket invitePacket, out _))
                    {
                        summary = $"CUIMessenger::OnInvite from={invitePacket.ContactName} type={invitePacket.InviteType} sequence={invitePacket.InviteSequence} skipBlacklistAutoReject={invitePacket.SkipBlacklistAutoReject}";
                        return true;
                    }

                    summary = "CUIMessenger::OnInvite";
                    return true;
                case 4:
                    if (TryParseInviteResult(body, out MessengerInviteResultPacket inviteResultPacket, out _))
                    {
                        summary = $"CUIMessenger::OnInviteResult target={inviteResultPacket.ContactName} inviteSent={inviteResultPacket.InviteSent}";
                        return true;
                    }

                    summary = "CUIMessenger::OnInviteResult";
                    return true;
                case 5:
                    if (TryParseBlocked(body, out MessengerBlockedPacket blockedPacket, out _))
                    {
                        summary = $"CUIMessenger::OnBlocked target={blockedPacket.ContactName} blocked={blockedPacket.Blocked}";
                        return true;
                    }

                    summary = "CUIMessenger::OnBlocked";
                    return true;
                case 6:
                    if (TryParseClientChat(body, out MessengerClientChatPacket chatPacket, out _))
                    {
                        string chatKind = chatPacket.IsWhisper ? "whisper" : "room-chat";
                        string speaker = string.IsNullOrWhiteSpace(chatPacket.ContactName) ? "unknown" : chatPacket.ContactName;
                        summary = $"CUIMessenger::OnChat {chatKind} speaker={speaker} message={chatPacket.Message}";
                        return true;
                    }

                    summary = "CUIMessenger::OnChat";
                    return true;
                case 7:
                    if (TryParseAvatar(body, out MessengerAvatarPacket avatarPacket, out _))
                    {
                        summary = $"CUIMessenger::OnAvatar slot={avatarPacket.SlotIndex}";
                        return true;
                    }

                    summary = "CUIMessenger::OnAvatar";
                    return true;
                case 8:
                    if (TryParseMigrated(body, out MessengerMigratedPacket migratedPacket, out _))
                    {
                        int presentCount = 0;
                        for (int i = 0; i < migratedPacket.Participants.Length; i++)
                        {
                            if (migratedPacket.Participants[i].Present)
                            {
                                presentCount++;
                            }
                        }

                        summary = $"CUIMessenger::OnMigrated participants={presentCount}";
                        return true;
                    }

                    summary = "CUIMessenger::OnMigrated";
                    return true;
                default:
                    summary = $"CUIMessenger::OnPacket subtype={packetSubtype}";
                    return true;
            }
        }

        private static void WriteString8(PacketWriter writer, string value)
        {
            string normalized = NormalizeText(value);
            writer.WriteByte(Math.Min(byte.MaxValue, normalized.Length));
            if (normalized.Length > 0)
            {
                writer.WriteString(normalized[..Math.Min(byte.MaxValue, normalized.Length)]);
            }
        }

        private static void WriteLengthPrefixedAvatarLook(PacketWriter writer, LoginAvatarLook avatarLook)
        {
            if (avatarLook == null)
            {
                writer.WriteInt(0);
                return;
            }

            byte[] avatarLookPayload = LoginAvatarLookCodec.Encode(avatarLook);
            writer.WriteInt(avatarLookPayload.Length);
            writer.WriteBytes(avatarLookPayload);
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool TryParseInvite(ReadOnlySpan<byte> payload, out MessengerInvitePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString16();
                byte inviteType = reader.HasRemaining ? reader.ReadByte() : (byte)0;
                int inviteSequence = reader.HasRemaining ? reader.ReadInt32() : 0;
                bool skipBlacklistAutoReject = reader.HasRemaining && reader.ReadByte() != 0;

                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger invite packet contact name is empty.";
                    return false;
                }

                packet = new MessengerInvitePacket(
                    contactName.Trim(),
                    inviteType,
                    inviteSequence,
                    skipBlacklistAutoReject);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseChat(ReadOnlySpan<byte> payload, out MessengerChatPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString8();
                string message = reader.ReadString16();
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger chat packet contact name is empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    error = "Messenger chat packet message is empty.";
                    return false;
                }

                packet = new MessengerChatPacket(contactName.Trim(), message.Trim());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMemberInfo(ReadOnlySpan<byte> payload, out MessengerMemberInfoPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString8();
                bool isOnline = reader.ReadByte() != 0;
                int channel = Math.Max(1, (int)reader.ReadByte());
                int level = Math.Max(1, (int)reader.ReadInt16());
                string jobName = reader.ReadString8();
                string locationSummary = reader.ReadString8();
                string statusText = reader.ReadString8();
                LoginAvatarLook avatarLook = null;
                if (reader.TryReadAvatarLook(out LoginAvatarLook decodedAvatarLook, out string avatarError))
                {
                    avatarLook = decodedAvatarLook;
                }
                else
                {
                    error = avatarError;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger member-info packet contact name is empty.";
                    return false;
                }

                packet = new MessengerMemberInfoPacket(
                    contactName.Trim(),
                    isOnline,
                    channel,
                    level,
                    string.IsNullOrWhiteSpace(jobName) ? "Adventurer" : jobName.Trim(),
                    string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim(),
                    string.IsNullOrWhiteSpace(statusText) ? (isOnline ? "Online" : "Offline") : statusText.Trim(),
                    avatarLook);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseBlocked(ReadOnlySpan<byte> payload, out MessengerBlockedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString16();
                bool blocked = reader.ReadByte() != 0;
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger blocked packet contact name is empty.";
                    return false;
                }

                packet = new MessengerBlockedPacket(contactName.Trim(), blocked);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseAvatar(ReadOnlySpan<byte> payload, out MessengerAvatarPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                int slotIndex = reader.ReadByte();
                byte[] avatarPayload = reader.ReadRemainingBytes();
                if (avatarPayload.Length == 0)
                {
                    error = "Messenger avatar packet is missing AvatarLook bytes.";
                    return false;
                }

                if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out LoginAvatarLook avatarLook, out error))
                {
                    error ??= "Messenger avatar packet AvatarLook payload could not be decoded.";
                    return false;
                }

                packet = new MessengerAvatarPacket(slotIndex, avatarLook);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseEnter(ReadOnlySpan<byte> payload, out MessengerEnterPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                int slotIndex = reader.ReadByte();
                if (!reader.TryReadAvatarLook(out LoginAvatarLook avatarLook, out error))
                {
                    error ??= "Messenger enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                string contactName = reader.ReadString8();
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger enter packet contact name is empty.";
                    return false;
                }

                int channel = Math.Max(1, (int)reader.ReadByte());
                bool isNew = reader.ReadByte() != 0;

                packet = new MessengerEnterPacket(slotIndex, contactName.Trim(), channel, isNew, avatarLook);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseInviteResult(ReadOnlySpan<byte> payload, out MessengerInviteResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString16();
                bool inviteSent = reader.ReadByte() != 0;
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger invite-result packet contact name is empty.";
                    return false;
                }

                packet = new MessengerInviteResultPacket(contactName.Trim(), inviteSent);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseClientInviteRequest(ReadOnlySpan<byte> payload, out MessengerClientInviteRequestPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload.ToArray());
                string contactName = reader.ReadMapleString().Trim();
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger client invite request contact name is empty.";
                    return false;
                }

                packet = new MessengerClientInviteRequestPacket(contactName);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Messenger client invite request payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryParseClientAcceptInviteRequest(ReadOnlySpan<byte> payload, out MessengerClientAcceptInviteRequestPacket packet, out string error)
        {
            packet = default;
            error = null;

            if (payload.Length < sizeof(int))
            {
                error = "Messenger client accept-invite request payload is missing the invite sequence.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload.ToArray());
                packet = new MessengerClientAcceptInviteRequestPacket(reader.ReadInt());
                return true;
            }
            catch (Exception ex)
            {
                error = $"Messenger client accept-invite request payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryParseClientChatRequest(ReadOnlySpan<byte> payload, out MessengerChatPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload.ToArray());
                string value = reader.ReadMapleString().Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    error = "Messenger client chat request payload is empty.";
                    return false;
                }

                int separatorIndex = value.IndexOf(ChatSeparator, StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    packet = new MessengerChatPacket(string.Empty, value);
                    return true;
                }

                string contactName = value[..separatorIndex].Trim();
                string message = value[(separatorIndex + ChatSeparator.Length)..].Trim();
                packet = new MessengerChatPacket(contactName, message);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Messenger client chat request payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryParseClientClaimRequest(ReadOnlySpan<byte> payload, out MessengerClientClaimRequestPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload.ToArray());
                bool includesChatLog = reader.ReadByte() != 0;
                string targetCharacterName = reader.ReadMapleString().Trim();
                byte claimType = reader.ReadByte();
                string context = reader.ReadMapleString().Trim();
                string chatLog = includesChatLog ? reader.ReadMapleString().Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(targetCharacterName))
                {
                    error = "Messenger claim request target name is empty.";
                    return false;
                }

                packet = new MessengerClientClaimRequestPacket(
                    targetCharacterName,
                    claimType,
                    context,
                    chatLog,
                    includesChatLog);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Messenger claim request payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryParseClientBlockedAutoRejectRequest(ReadOnlySpan<byte> payload, out MessengerClientBlockedAutoRejectPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload.ToArray());
                string inviterName = reader.ReadMapleString().Trim();
                string localCharacterName = reader.ReadMapleString().Trim();
                bool blocked = reader.ReadByte() != 0;
                if (string.IsNullOrWhiteSpace(inviterName))
                {
                    error = "Messenger blocked-auto-reject inviter name is empty.";
                    return false;
                }

                packet = new MessengerClientBlockedAutoRejectPacket(inviterName, localCharacterName, blocked);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Messenger blocked-auto-reject request payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryParseLeaveSlot(ReadOnlySpan<byte> payload, out MessengerLeaveSlotPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                packet = new MessengerLeaveSlotPacket(reader.ReadByte());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseClientChat(ReadOnlySpan<byte> payload, out MessengerClientChatPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string chatText = reader.ReadString16();
                if (string.IsNullOrWhiteSpace(chatText))
                {
                    error = "Messenger client chat packet text is empty.";
                    return false;
                }

                int separatorIndex = chatText.IndexOf(ChatSeparator, StringComparison.Ordinal);
                if (separatorIndex > 0 && separatorIndex < chatText.Length - ChatSeparator.Length)
                {
                    string contactName = chatText[..separatorIndex].Trim();
                    string message = chatText[(separatorIndex + ChatSeparator.Length)..].Trim();
                    if (string.IsNullOrWhiteSpace(contactName))
                    {
                        error = "Messenger client chat packet speaker name is empty.";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        error = "Messenger client chat packet message is empty.";
                        return false;
                    }

                    packet = new MessengerClientChatPacket(contactName, message, IsWhisper: false, IsActivityPulse: false, ActivityEnabled: false);
                    return true;
                }

                if (chatText.Length < 2)
                {
                    error = "Messenger client chat packet is missing both the MapleStory 'name : message' separator and the activity marker.";
                    return false;
                }

                string activityName = chatText[..^1].Trim();
                char activityMarker = chatText[^1];
                if (string.IsNullOrWhiteSpace(activityName))
                {
                    error = "Messenger client chat packet speaker name is empty.";
                    return false;
                }

                if (activityMarker is not '0' and not '1')
                {
                    error = "Messenger client chat packet activity marker must be '0' or '1' when no room-chat text is present.";
                    return false;
                }

                packet = new MessengerClientChatPacket(
                    activityName,
                    string.Empty,
                    IsWhisper: false,
                    IsActivityPulse: true,
                    ActivityEnabled: activityMarker != '0');
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseSelfEnterResult(ReadOnlySpan<byte> payload, out MessengerSelfEnterResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                packet = new MessengerSelfEnterResultPacket((sbyte)reader.ReadByte());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMigrated(ReadOnlySpan<byte> payload, out MessengerMigratedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                MessengerMigratedParticipantPacket[] participants = new MessengerMigratedParticipantPacket[3];
                for (int i = 0; i < participants.Length; i++)
                {
                    byte state = reader.ReadByte();
                    if (state == 0)
                    {
                        participants[i] = new MessengerMigratedParticipantPacket(i, state, string.Empty, 1, null);
                        continue;
                    }

                    if (state == 1)
                    {
                        participants[i] = new MessengerMigratedParticipantPacket(i, state, string.Empty, 1, null);
                        continue;
                    }

                    if (!reader.TryReadAvatarLook(out LoginAvatarLook avatarLook, out error))
                    {
                        error ??= "Messenger migrated packet AvatarLook payload could not be decoded.";
                        return false;
                    }

                    string contactName = reader.ReadString8();
                    if (string.IsNullOrWhiteSpace(contactName))
                    {
                        error = "Messenger migrated packet contact name is empty.";
                        return false;
                    }

                    int channel = Math.Max(1, (int)reader.ReadByte());

                    participants[i] = new MessengerMigratedParticipantPacket(
                        i,
                        state,
                        contactName.Trim(),
                        channel,
                        avatarLook);
                }

                packet = new MessengerMigratedPacket(participants);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _payload;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> payload)
            {
                _payload = payload;
                _offset = 0;
            }

            public bool HasRemaining => _offset < _payload.Length;

            public byte ReadByte()
            {
                EnsureAvailable(sizeof(byte));
                return _payload[_offset++];
            }

            public short ReadInt16()
            {
                EnsureAvailable(sizeof(short));
                short value = (short)(_payload[_offset] | (_payload[_offset + 1] << 8));
                _offset += sizeof(short);
                return value;
            }

            public int ReadInt32()
            {
                EnsureAvailable(sizeof(int));
                int value = _payload[_offset]
                    | (_payload[_offset + 1] << 8)
                    | (_payload[_offset + 2] << 16)
                    | (_payload[_offset + 3] << 24);
                _offset += sizeof(int);
                return value;
            }

            public int ReadInt()
            {
                return ReadInt32();
            }

            public string ReadString8()
            {
                int length = ReadByte();
                return ReadString(length);
            }

            public string ReadString16()
            {
                int length = ReadInt16();
                return ReadString(length);
            }

            public string ReadMapleString()
            {
                return ReadString16();
            }

            public bool TryReadAvatarLook(out LoginAvatarLook avatarLook, out string error)
            {
                avatarLook = null;
                error = null;
                if (_offset >= _payload.Length)
                {
                    return true;
                }

                if (_payload.Length - _offset < sizeof(int))
                {
                    error = "Messenger member-info AvatarLook length is truncated.";
                    return false;
                }

                int avatarLookLength = ReadInt32();
                if (avatarLookLength <= 0)
                {
                    return true;
                }

                EnsureAvailable(avatarLookLength);
                byte[] avatarLookPayload = _payload.Slice(_offset, avatarLookLength).ToArray();
                _offset += avatarLookLength;
                if (!LoginAvatarLookCodec.TryDecode(avatarLookPayload, out avatarLook, out error))
                {
                    error ??= "Messenger member-info AvatarLook payload could not be decoded.";
                    return false;
                }

                return true;
            }

            public byte[] ReadRemainingBytes()
            {
                byte[] remaining = _payload.Slice(_offset).ToArray();
                _offset = _payload.Length;
                return remaining;
            }

            private string ReadString(int length)
            {
                if (length < 0)
                {
                    throw new InvalidOperationException($"Messenger packet string length {length} is invalid.");
                }

                EnsureAvailable(length);
                string value = System.Text.Encoding.ASCII.GetString(_payload.Slice(_offset, length));
                _offset += length;
                return value;
            }

            private void EnsureAvailable(int count)
            {
                if (count < 0 || _offset + count > _payload.Length)
                {
                    throw new InvalidOperationException("Messenger packet payload ended before all fields could be read.");
                }
            }
        }
    }
}
