using System;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal delegate bool BinaryPayloadArgumentParser(string argument, out byte[] payload, out string error);
    internal delegate bool HexByteDecoder(string hexBytes, out byte[] payload);
    internal delegate ChatCommandHandler.CommandResult MessengerPacketCommandApplier(MessengerPacketType packetType, byte[] payload);
    internal delegate ChatCommandHandler.CommandResult MessengerDispatchCommandApplier(byte[] payload);
    internal delegate bool MessengerInviteResolutionPayloadBuilder(bool accepted, string contactName, out byte[] payload, out string message);
    internal delegate bool MessengerAvatarPayloadBuilder(string participantToken, int? slotOverride, out byte[] payload, out string message);
    internal delegate bool MessengerEnterPayloadBuilder(string participantToken, int? slotOverride, int? channelOverride, bool? isNewOverride, out byte[] payload, out string message);
    internal delegate bool MessengerMigratedPayloadBuilder(out byte[] payload, out string message);
    internal delegate bool MessengerSelfEnterResultPayloadBuilder(int? slotOverride, out byte[] payload, out string message);

    internal static class MessengerCommandRouter
    {
        private const string PacketRawUsage = "Usage: /messenger packetraw <dispatch|invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <hex bytes>";
        private const string RemoteUsage = "Usage: /messenger remote <invite|accept|reject|leave|room|whisper> ...";
        private const string RemoveUsage = "Usage: /messenger packet remove <name>";
        private const string UpsertUsage = "Usage: /messenger packet upsert <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>";
        private const string InviteUsage = "Usage: /messenger packet invite <name>";
        private const string LeaveUsage = "Usage: /messenger packet leave <name>";
        private const string RoomUsage = "Usage: /messenger packet room <name> <message>";
        private const string WhisperUsage = "Usage: /messenger packet whisper <name> <message>";
        private const string MemberUsage = "Usage: /messenger packet member <payloadhex=..|payloadb64=..>";

        internal static ChatCommandHandler.CommandResult HandlePacketCommand(
            string[] args,
            MessengerRuntime runtime,
            BinaryPayloadArgumentParser parseBinaryPayloadArgument,
            MessengerPacketCommandApplier applyPacket,
            MessengerDispatchCommandApplier applyDispatch,
            MessengerInviteResolutionPayloadBuilder buildInviteResolutionPayload,
            MessengerAvatarPayloadBuilder buildAvatarPayload,
            MessengerEnterPayloadBuilder buildEnterPayload,
            MessengerMigratedPayloadBuilder buildMigratedPayload,
            MessengerSelfEnterResultPayloadBuilder buildSelfEnterResultPayload)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error(GetPacketUsage());
            }

            switch (args[1].ToLowerInvariant())
            {
                case "seed":
                    return ChatCommandHandler.CommandResult.Ok(runtime.SeedPacketProfiles());
                case "clear":
                    return ChatCommandHandler.CommandResult.Ok(runtime.ClearPacketProfiles());
                case "remove":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error(RemoveUsage);
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.RemovePacketProfile(string.Join(" ", args.Skip(2))));
                case "upsert":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error(UpsertUsage);
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.UpsertPacketProfile(string.Join(" ", args.Skip(2))));
                case "invite":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error(InviteUsage);
                    }

                    return applyPacket(MessengerPacketType.Invite, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                case "accept":
                    if (args.Length >= 3)
                    {
                        return applyPacket(MessengerPacketType.InviteAccept, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                    }

                    return BuildAndApplyInviteResolutionPayload(
                        accepted: true,
                        contactName: null,
                        buildInviteResolutionPayload,
                        applyPacket);
                case "reject":
                    if (args.Length >= 3)
                    {
                        return applyPacket(MessengerPacketType.InviteReject, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                    }

                    return BuildAndApplyInviteResolutionPayload(
                        accepted: false,
                        contactName: null,
                        buildInviteResolutionPayload,
                        applyPacket);
                case "leave":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error(LeaveUsage);
                    }

                    return applyPacket(MessengerPacketType.Leave, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                case "room":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error(RoomUsage);
                    }

                    return applyPacket(MessengerPacketType.RoomChat, MessengerPacketCodec.BuildChatPayload(args[2], string.Join(" ", args.Skip(3))));
                case "whisper":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error(WhisperUsage);
                    }

                    return applyPacket(MessengerPacketType.Whisper, MessengerPacketCodec.BuildChatPayload(args[2], string.Join(" ", args.Skip(3))));
                case "member":
                case "memberinfo":
                case "presence":
                    if (!TryParsePacketPayload(args, 2, parseBinaryPayloadArgument, MemberUsage, out byte[] memberPayload, out string memberError))
                    {
                        return ChatCommandHandler.CommandResult.Error(memberError);
                    }

                    return applyPacket(MessengerPacketType.MemberInfo, memberPayload);
                case "avatar":
                    if (!LooksLikeBinaryPayloadArgument(args, 2))
                    {
                        return BuildAndApplyAvatarPayload(args, buildAvatarPayload, applyPacket);
                    }

                    goto default;
                case "enter":
                    if (!LooksLikeBinaryPayloadArgument(args, 2))
                    {
                        return BuildAndApplyEnterPayload(args, buildEnterPayload, applyPacket);
                    }

                    goto default;
                case "migrated":
                    if (!LooksLikeBinaryPayloadArgument(args, 2))
                    {
                        return BuildAndApplyMigratedPayload(buildMigratedPayload, applyPacket);
                    }

                    goto default;
                case "selfenterresult":
                case "selfenter":
                    if (!LooksLikeBinaryPayloadArgument(args, 2))
                    {
                        return BuildAndApplySelfEnterResultPayload(args, buildSelfEnterResultPayload, applyPacket);
                    }

                    goto default;
                case "dispatch":
                case "onpacket":
                    string dispatchUsage = $"Usage: /messenger packet {args[1]} <payloadhex=..|payloadb64=..>";
                    if (!TryParsePacketPayload(args, 2, parseBinaryPayloadArgument, dispatchUsage, out byte[] dispatchPayload, out string dispatchError))
                    {
                        return ChatCommandHandler.CommandResult.Error(dispatchError);
                    }

                    return applyDispatch(dispatchPayload);
                default:
                    if (!TryParseMessengerPacketType(args[1], out MessengerPacketType packetType))
                    {
                        return ChatCommandHandler.CommandResult.Error(GetPacketUsage());
                    }

                    string payloadUsage = $"Usage: /messenger packet {args[1]} <payloadhex=..|payloadb64=..>";
                    if (!TryParsePacketPayload(args, 2, parseBinaryPayloadArgument, payloadUsage, out byte[] payload, out string payloadError))
                    {
                        return ChatCommandHandler.CommandResult.Error(payloadError);
                    }

                    return applyPacket(packetType, payload);
            }
        }

        internal static ChatCommandHandler.CommandResult HandlePacketRawCommand(
            string[] args,
            MessengerRuntime runtime,
            HexByteDecoder tryDecodeHexBytes,
            MessengerPacketCommandApplier applyPacket,
            MessengerDispatchCommandApplier applyDispatch)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
            }

            if (string.Equals(args[1], "dispatch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "onpacket", StringComparison.OrdinalIgnoreCase))
            {
                if (!tryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] dispatchPayload))
                {
                    return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
                }

                return applyDispatch(dispatchPayload);
            }

            if (!TryParseMessengerPacketType(args[1], out MessengerPacketType packetType))
            {
                return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
            }

            if (!tryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] payload))
            {
                return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
            }

            return applyPacket(packetType, payload);
        }

        internal static ChatCommandHandler.CommandResult HandleRemoteCommand(
            string[] args,
            MessengerRuntime runtime,
            MessengerPacketCommandApplier applyPacket,
            MessengerInviteResolutionPayloadBuilder buildInviteResolutionPayload,
            MessengerAvatarPayloadBuilder buildAvatarPayload,
            MessengerEnterPayloadBuilder buildEnterPayload,
            MessengerMigratedPayloadBuilder buildMigratedPayload,
            MessengerSelfEnterResultPayloadBuilder buildSelfEnterResultPayload)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error(RemoteUsage);
            }

            switch (args[1].ToLowerInvariant())
            {
                case "invite":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote invite <name>");
                    }

                    return applyPacket(MessengerPacketType.Invite, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                case "accept":
                    if (args.Length >= 3)
                    {
                        return applyPacket(MessengerPacketType.InviteAccept, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                    }

                    return BuildAndApplyInviteResolutionPayload(
                        accepted: true,
                        contactName: null,
                        buildInviteResolutionPayload,
                        applyPacket);
                case "reject":
                    if (args.Length >= 3)
                    {
                        return applyPacket(MessengerPacketType.InviteReject, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                    }

                    return BuildAndApplyInviteResolutionPayload(
                        accepted: false,
                        contactName: null,
                        buildInviteResolutionPayload,
                        applyPacket);
                case "leave":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote leave <name>");
                    }

                    return applyPacket(MessengerPacketType.Leave, MessengerPacketCodec.BuildInvitePayload(string.Join(" ", args.Skip(2))));
                case "room":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote room <name> <message>");
                    }

                    return applyPacket(MessengerPacketType.RoomChat, MessengerPacketCodec.BuildChatPayload(args[2], string.Join(" ", args.Skip(3))));
                case "whisper":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote whisper <name> <message>");
                    }

                    return applyPacket(MessengerPacketType.Whisper, MessengerPacketCodec.BuildChatPayload(args[2], string.Join(" ", args.Skip(3))));
                case "avatar":
                    return BuildAndApplyAvatarPayload(args, buildAvatarPayload, applyPacket, usage: "Usage: /messenger remote avatar <name|local> [slot]");
                case "enter":
                    return BuildAndApplyEnterPayload(args, buildEnterPayload, applyPacket, usage: "Usage: /messenger remote enter <name|local> [slot] [channel] [new|existing]");
                case "migrated":
                    return BuildAndApplyMigratedPayload(buildMigratedPayload, applyPacket);
                case "selfenterresult":
                case "selfenter":
                    return BuildAndApplySelfEnterResultPayload(args, buildSelfEnterResultPayload, applyPacket, usage: "Usage: /messenger remote selfenterresult [slot|fail]");
                default:
                    return ChatCommandHandler.CommandResult.Error(RemoteUsage);
            }
        }

        private static ChatCommandHandler.CommandResult BuildAndApplyInviteResolutionPayload(
            bool accepted,
            string contactName,
            MessengerInviteResolutionPayloadBuilder buildInviteResolutionPayload,
            MessengerPacketCommandApplier applyPacket)
        {
            if (!buildInviteResolutionPayload(accepted, contactName, out byte[] payload, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return applyPacket(
                accepted ? MessengerPacketType.InviteAccept : MessengerPacketType.InviteReject,
                payload);
        }

        private static ChatCommandHandler.CommandResult BuildAndApplyAvatarPayload(
            string[] args,
            MessengerAvatarPayloadBuilder buildAvatarPayload,
            MessengerPacketCommandApplier applyPacket,
            string usage = "Usage: /messenger packet avatar <name|local> [slot]")
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!TryParseOptionalInt(args, 3, usage, out int? slotOverride))
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!buildAvatarPayload(args[2], slotOverride, out byte[] payload, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return applyPacket(MessengerPacketType.Avatar, payload);
        }

        private static ChatCommandHandler.CommandResult BuildAndApplyEnterPayload(
            string[] args,
            MessengerEnterPayloadBuilder buildEnterPayload,
            MessengerPacketCommandApplier applyPacket,
            string usage = "Usage: /messenger packet enter <name|local> [slot] [channel] [new|existing]")
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!TryParseOptionalInt(args, 3, usage, out int? slotOverride)
                || !TryParseOptionalInt(args, 4, usage, out int? channelOverride)
                || !TryParseOptionalIsNewFlag(args, 5, usage, out bool? isNewOverride))
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!buildEnterPayload(args[2], slotOverride, channelOverride, isNewOverride, out byte[] payload, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return applyPacket(MessengerPacketType.Enter, payload);
        }

        private static ChatCommandHandler.CommandResult BuildAndApplyMigratedPayload(
            MessengerMigratedPayloadBuilder buildMigratedPayload,
            MessengerPacketCommandApplier applyPacket)
        {
            if (!buildMigratedPayload(out byte[] payload, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return applyPacket(MessengerPacketType.Migrated, payload);
        }

        private static ChatCommandHandler.CommandResult BuildAndApplySelfEnterResultPayload(
            string[] args,
            MessengerSelfEnterResultPayloadBuilder buildSelfEnterResultPayload,
            MessengerPacketCommandApplier applyPacket,
            string usage = "Usage: /messenger packet selfenterresult [slot|fail]")
        {
            if (!TryParseOptionalSelfEnterSlot(args, 2, usage, out int? slotOverride))
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!buildSelfEnterResultPayload(slotOverride, out byte[] payload, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return applyPacket(MessengerPacketType.SelfEnterResult, payload);
        }

        private static bool TryParsePacketPayload(
            string[] args,
            int payloadIndex,
            BinaryPayloadArgumentParser parseBinaryPayloadArgument,
            string usage,
            out byte[] payload,
            out string error)
        {
            payload = null;
            error = usage;
            if (args.Length <= payloadIndex)
            {
                return false;
            }

            if (!parseBinaryPayloadArgument(args[payloadIndex], out payload, out string parseError))
            {
                error = parseError ?? usage;
                return false;
            }

            error = null;
            return true;
        }

        private static bool LooksLikeBinaryPayloadArgument(string[] args, int index)
        {
            return args.Length > index
                && (args[index].StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase)
                    || args[index].StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryParseOptionalInt(string[] args, int index, string usage, out int? value)
        {
            value = null;
            if (args.Length <= index)
            {
                return true;
            }

            return int.TryParse(args[index], out int parsedValue)
                ? (value = parsedValue) is not null
                : false;
        }

        private static bool TryParseOptionalIsNewFlag(string[] args, int index, string usage, out bool? isNew)
        {
            isNew = null;
            if (args.Length <= index)
            {
                return true;
            }

            switch (args[index].Trim().ToLowerInvariant())
            {
                case "new":
                    isNew = true;
                    return true;
                case "existing":
                case "old":
                    isNew = false;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseOptionalSelfEnterSlot(string[] args, int index, string usage, out int? slotOverride)
        {
            slotOverride = null;
            if (args.Length <= index)
            {
                return true;
            }

            if (string.Equals(args[index], "fail", StringComparison.OrdinalIgnoreCase))
            {
                slotOverride = -1;
                return true;
            }

            return int.TryParse(args[index], out int parsedSlot)
                ? (slotOverride = parsedSlot) is not null
                : false;
        }

        private static bool TryParseMessengerPacketType(string token, out MessengerPacketType packetType)
        {
            packetType = MessengerPacketType.Invite;
            switch (token?.Trim().ToLowerInvariant())
            {
                case "invite":
                    packetType = MessengerPacketType.Invite;
                    return true;
                case "accept":
                case "inviteaccept":
                    packetType = MessengerPacketType.InviteAccept;
                    return true;
                case "reject":
                case "invitereject":
                    packetType = MessengerPacketType.InviteReject;
                    return true;
                case "leave":
                    packetType = MessengerPacketType.Leave;
                    return true;
                case "room":
                case "roomchat":
                    packetType = MessengerPacketType.RoomChat;
                    return true;
                case "whisper":
                    packetType = MessengerPacketType.Whisper;
                    return true;
                case "member":
                case "memberinfo":
                case "presence":
                    packetType = MessengerPacketType.MemberInfo;
                    return true;
                case "blocked":
                    packetType = MessengerPacketType.Blocked;
                    return true;
                case "avatar":
                    packetType = MessengerPacketType.Avatar;
                    return true;
                case "enter":
                    packetType = MessengerPacketType.Enter;
                    return true;
                case "inviteresult":
                case "result":
                    packetType = MessengerPacketType.InviteResult;
                    return true;
                case "migrated":
                    packetType = MessengerPacketType.Migrated;
                    return true;
                case "selfenterresult":
                case "selfenter":
                    packetType = MessengerPacketType.SelfEnterResult;
                    return true;
                default:
                    return false;
            }
        }

        private static string GetPacketUsage()
        {
            return "Usage: /messenger packet <seed|clear|remove <name>|upsert <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>|invite <name>|accept [name]|reject [name]|leave <name>|room <name> <message>|whisper <name> <message>|avatar <name|local> [slot]|enter <name|local> [slot] [channel] [new|existing]|migrated|selfenterresult [slot|fail]|member <payloadhex=..|payloadb64=..>|dispatch <payloadhex=..|payloadb64=..>|<invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <payloadhex=..|payloadb64=..>>";
        }
    }
}
