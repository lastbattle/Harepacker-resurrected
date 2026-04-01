using HaCreator.MapSimulator.Interaction;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private ChatCommandHandler.CommandResult HandleMessengerPacketCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error(GetMessengerPacketUsage());
            }

            switch (args[1].ToLowerInvariant())
            {
                case "seed":
                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.SeedPacketProfiles());
                case "clear":
                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ClearPacketProfiles());
                case "remove":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger packet remove <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.RemovePacketProfile(string.Join(" ", args.Skip(2))));
                case "upsert":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger packet upsert <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.UpsertPacketProfile(string.Join(" ", args.Skip(2))));
                case "invite":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger packet invite <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveInvitePacket(string.Join(" ", args.Skip(2))));
                case "accept":
                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ResolvePendingInvitePacket(args.Length >= 3 ? string.Join(" ", args.Skip(2)) : null, accepted: true));
                case "reject":
                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ResolvePendingInvitePacket(args.Length >= 3 ? string.Join(" ", args.Skip(2)) : null, accepted: false));
                case "leave":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger packet leave <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: false));
                case "room":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger packet room <name> <message>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveRoomMessage(args[2], string.Join(" ", args.Skip(3))));
                case "whisper":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger packet whisper <name> <message>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveRemoteWhisper(args[2], string.Join(" ", args.Skip(3))));
                case "member":
                case "memberinfo":
                case "presence":
                    string memberError = null;
                    if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out byte[] memberPayload, out memberError))
                    {
                        return ChatCommandHandler.CommandResult.Error(memberError ?? "Usage: /messenger packet member <payloadhex=..|payloadb64=..>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ApplyPacketPayload(MessengerPacketType.MemberInfo, memberPayload));
                default:
                    if (TryParseMessengerPacketType(args[1], out MessengerPacketType packetType))
                    {
                        string payloadError = null;
                        if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out byte[] payload, out payloadError))
                        {
                            return ChatCommandHandler.CommandResult.Error(payloadError ?? $"Usage: /messenger packet {args[1]} <payloadhex=..|payloadb64=..>");
                        }

                        return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ApplyPacketPayload(packetType, payload));
                    }

                    return ChatCommandHandler.CommandResult.Error(GetMessengerPacketUsage());
            }
        }

        private ChatCommandHandler.CommandResult HandleMessengerPacketRawCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /messenger packetraw <invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <hex bytes>");
            }

            if (!TryParseMessengerPacketType(args[1], out MessengerPacketType packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /messenger packetraw <invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <hex bytes>");
            }

            if (!TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] payload))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /messenger packetraw <invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <hex bytes>");
            }

            return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ApplyPacketPayload(packetType, payload));
        }

        private ChatCommandHandler.CommandResult HandleMessengerRemoteCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote <invite|accept|reject|leave|room|whisper> ...");
            }

            switch (args[1].ToLowerInvariant())
            {
                case "invite":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote invite <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveInvitePacket(string.Join(" ", args.Skip(2))));
                case "accept":
                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ResolvePendingInvite(true, packetDriven: true));
                case "reject":
                    if (args.Length >= 3)
                    {
                        return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: true));
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ResolvePendingInvite(false, packetDriven: true));
                case "leave":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote leave <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: false));
                case "room":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote room <name> <message>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveRoomMessage(args[2], string.Join(" ", args.Skip(3))));
                case "whisper":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote whisper <name> <message>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveRemoteWhisper(args[2], string.Join(" ", args.Skip(3))));
                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote <invite|accept|reject|leave|room|whisper> ...");
            }
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

        private static string GetMessengerPacketUsage()
        {
            return "Usage: /messenger packet <seed|clear|remove <name>|upsert <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>|invite <name>|accept [name]|reject [name]|leave <name>|room <name> <message>|whisper <name> <message>|member <payloadhex=..|payloadb64=..>|<invite|accept|reject|leave|room|whisper|member|blocked|avatar|enter|inviteresult|migrated|selfenterresult> <payloadhex=..|payloadb64=..>>";
        }
    }
}
