using System;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal delegate bool BinaryPayloadArgumentParser(string argument, out byte[] payload, out string error);
    internal delegate bool HexByteDecoder(string hexBytes, out byte[] payload);

    internal static class MessengerCommandRouter
    {
        private const string PacketRawUsage = "Usage: /messenger packetraw <invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <hex bytes>";
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
            BinaryPayloadArgumentParser parseBinaryPayloadArgument)
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

                    return ChatCommandHandler.CommandResult.Ok(runtime.ReceiveInvitePacket(string.Join(" ", args.Skip(2))));
                case "accept":
                    return ChatCommandHandler.CommandResult.Ok(runtime.ResolvePendingInvitePacket(args.Length >= 3 ? string.Join(" ", args.Skip(2)) : null, accepted: true));
                case "reject":
                    return ChatCommandHandler.CommandResult.Ok(runtime.ResolvePendingInvitePacket(args.Length >= 3 ? string.Join(" ", args.Skip(2)) : null, accepted: false));
                case "leave":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error(LeaveUsage);
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: false));
                case "room":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error(RoomUsage);
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.ReceiveRoomMessage(args[2], string.Join(" ", args.Skip(3))));
                case "whisper":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error(WhisperUsage);
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.ReceiveRemoteWhisper(args[2], string.Join(" ", args.Skip(3))));
                case "member":
                case "memberinfo":
                case "presence":
                    if (!TryParsePacketPayload(args, 2, parseBinaryPayloadArgument, MemberUsage, out byte[] memberPayload, out string memberError))
                    {
                        return ChatCommandHandler.CommandResult.Error(memberError);
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.ApplyPacketPayload(MessengerPacketType.MemberInfo, memberPayload));
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

                    return ChatCommandHandler.CommandResult.Ok(runtime.ApplyPacketPayload(packetType, payload));
            }
        }

        internal static ChatCommandHandler.CommandResult HandlePacketRawCommand(
            string[] args,
            MessengerRuntime runtime,
            HexByteDecoder tryDecodeHexBytes)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
            }

            if (!TryParseMessengerPacketType(args[1], out MessengerPacketType packetType))
            {
                return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
            }

            if (!tryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] payload))
            {
                return ChatCommandHandler.CommandResult.Error(PacketRawUsage);
            }

            return ChatCommandHandler.CommandResult.Ok(runtime.ApplyPacketPayload(packetType, payload));
        }

        internal static ChatCommandHandler.CommandResult HandleRemoteCommand(string[] args, MessengerRuntime runtime)
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

                    return ChatCommandHandler.CommandResult.Ok(runtime.ReceiveInvitePacket(string.Join(" ", args.Skip(2))));
                case "accept":
                    return ChatCommandHandler.CommandResult.Ok(runtime.ResolvePendingInvite(true, packetDriven: true));
                case "reject":
                    if (args.Length >= 3)
                    {
                        return ChatCommandHandler.CommandResult.Ok(runtime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: true));
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.ResolvePendingInvite(false, packetDriven: true));
                case "leave":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote leave <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: false));
                case "room":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote room <name> <message>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.ReceiveRoomMessage(args[2], string.Join(" ", args.Skip(3))));
                case "whisper":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote whisper <name> <message>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(runtime.ReceiveRemoteWhisper(args[2], string.Join(" ", args.Skip(3))));
                default:
                    return ChatCommandHandler.CommandResult.Error(RemoteUsage);
            }
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
            return "Usage: /messenger packet <seed|clear|remove <name>|upsert <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>|invite <name>|accept [name]|reject [name]|leave <name>|room <name> <message>|whisper <name> <message>|member <payloadhex=..|payloadb64=..>|<invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <payloadhex=..|payloadb64=..>>";
        }
    }
}
