using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly RockPaperScissorsPacketInboxManager _rockPaperScissorsPacketInbox = new();

        private void RegisterRockPaperScissorsChatCommands()
        {
            _chat.CommandHandler.RegisterCommand(
                "rps",
                "Drive the CRPSGameDlg Rock-Paper-Scissors runtime",
                "/rps <open|close|choose|main|status|packet|packetraw|inbox> [...]",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /rps <open|close|choose|main|status|packet|packetraw|inbox> [...]");
                    }

                    RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
                    string action = args[0].ToLowerInvariant();
                    int currTickCount = Environment.TickCount;

                    switch (action)
                    {
                        case "open":
                        {
                            uint entryValue = 0;
                            if (args.Length >= 2
                                && !uint.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out entryValue))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps open [entryValue]");
                            }

                            byte[] payload = BitConverter.GetBytes(entryValue);
                            return TryApplyRockPaperScissorsPacket(8, payload, currTickCount, out string openMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(openMessage);
                        }

                        case "close":
                        case "destroy":
                            return TryApplyRockPaperScissorsPacket(13, Array.Empty<byte>(), currTickCount, out string destroyMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(destroyMessage);

                        case "choose":
                        case "select":
                        {
                            if (args.Length < 2 || !TryParseRockPaperScissorsChoiceToken(args[1], out RockPaperScissorsChoice choice))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps choose <rock|paper|scissor>");
                            }

                            return field.TrySelectChoice(choice, out string choiceMessage)
                                ? ChatCommandHandler.CommandResult.Ok(choiceMessage)
                                : ChatCommandHandler.CommandResult.Error(choiceMessage);
                        }

                        case "main":
                        case "start":
                        case "continue":
                        case "retry":
                            return field.TryActivateMainButton(currTickCount, out string mainMessage)
                                ? ChatCommandHandler.CommandResult.Ok(mainMessage)
                                : ChatCommandHandler.CommandResult.Error(mainMessage);

                        case "status":
                            return ChatCommandHandler.CommandResult.Info($"{field.DescribeStatus()} | inbox={_rockPaperScissorsPacketInbox.LastStatus}");

                        case "packet":
                        {
                            if (args.Length < 2 || !RockPaperScissorsField.TryParsePacketType(args[1], out int packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps packet <open|destroy|win|lose|start|forceresult|result|continue|reset> [...]");
                            }

                            byte[] payload = Array.Empty<byte>();
                            switch (packetType)
                            {
                                case 8:
                                {
                                    uint entryValue = 0;
                                    if (args.Length >= 3
                                        && !uint.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out entryValue))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps packet open [entryValue]");
                                    }

                                    payload = BitConverter.GetBytes(entryValue);
                                    break;
                                }

                                case 11:
                                {
                                    if (args.Length < 4
                                        || !TryParseRockPaperScissorsChoiceToken(args[2], out RockPaperScissorsChoice npcChoice)
                                        || !sbyte.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte streak))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps packet result <rock|paper|scissor> <straightVictoryCount>");
                                    }

                                    payload = new[] { (byte)npcChoice, unchecked((byte)streak) };
                                    break;
                                }
                            }

                            return TryApplyRockPaperScissorsPacket(packetType, payload, currTickCount, out string packetMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(packetMessage);
                        }

                        case "packetraw":
                        {
                            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] rawPacket))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps packetraw <opcode-wrapped hex bytes>");
                            }

                            if (!RockPaperScissorsPacketInboxManager.TryParsePacketLine($"packetraw {Convert.ToHexString(rawPacket)}", out int packetType, out byte[] payload, out string packetParseError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetParseError ?? "Usage: /rps packetraw <opcode-wrapped hex bytes>");
                            }

                            return TryApplyRockPaperScissorsPacket(packetType, payload, currTickCount, out string packetRawMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(packetRawMessage);
                        }

                        case "inbox":
                        {
                            string inboxAction = args.Length >= 2 ? args[1].ToLowerInvariant() : "status";
                            switch (inboxAction)
                            {
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(_rockPaperScissorsPacketInbox.LastStatus);
                                case "start":
                                {
                                    int port = RockPaperScissorsPacketInboxManager.DefaultPort;
                                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps inbox start [port]");
                                    }

                                    _rockPaperScissorsPacketInbox.Start(port);
                                    return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsPacketInbox.LastStatus);
                                }
                                case "stop":
                                    _rockPaperScissorsPacketInbox.Stop();
                                    return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsPacketInbox.LastStatus);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /rps inbox [status|start [port]|stop]");
                            }
                        }

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /rps <open|close|choose|main|status|packet|packetraw|inbox> [...]");
                    }
                });
        }

        private void SyncRockPaperScissorsPacketInboxState()
        {
            _specialFieldRuntime.Minigames.RockPaperScissors.SetUniqueModelessOwnerConflictEvaluator(HasRockPaperScissorsUniqueModelessConflict);

            if (_gameState.IsLoginMap)
            {
                _rockPaperScissorsPacketInbox.Stop();
                return;
            }

            _rockPaperScissorsPacketInbox.Start();
        }

        private void DrainRockPaperScissorsPacketInbox(int currentTickCount)
        {
            RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
            while (_rockPaperScissorsPacketInbox.TryDequeue(out RockPaperScissorsPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = field.TryApplyRawPacket(message.PacketType, message.Payload, currentTickCount, out string resultMessage);
                if (applied)
                {
                    DrainRockPaperScissorsPendingNotice(field);
                }

                _rockPaperScissorsPacketInbox.RecordDispatchResult(
                    message.Source,
                    message.PacketType,
                    applied,
                    applied ? field.DescribeStatus() : resultMessage);
            }
        }

        private bool TryApplyRockPaperScissorsPacket(int packetType, byte[] payload, int currentTickCount, out string message)
        {
            RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
            bool applied = field.TryApplyRawPacket(packetType, payload, currentTickCount, out message);
            if (!applied)
            {
                return false;
            }

            DrainRockPaperScissorsPendingNotice(field);
            message = field.DescribeStatus();
            return true;
        }

        private void DrainRockPaperScissorsPendingNotice(RockPaperScissorsField field)
        {
            while (field.TryConsumePendingNotice(out _, out string notice))
            {
                ShowPacketOwnedNoticeDialog(notice);
            }
        }

        private bool HasRockPaperScissorsUniqueModelessConflict()
        {
            return !string.IsNullOrWhiteSpace(GetVisibleUniqueModelessOwner(null))
                || (_npcInteractionOverlay?.IsVisible == true)
                || _specialFieldRuntime.Minigames.MemoryGame.IsVisible
                || _specialFieldRuntime.Minigames.Tournament.MatchTableDialog.IsVisible
                || _specialFieldRuntime.Minigames.RockPaperScissors.IsVisible;
        }

        private static bool TryParseRockPaperScissorsChoiceToken(string token, out RockPaperScissorsChoice choice)
        {
            choice = RockPaperScissorsChoice.None;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "rock":
                case "r":
                case "0":
                    choice = RockPaperScissorsChoice.Rock;
                    return true;
                case "paper":
                case "p":
                case "1":
                    choice = RockPaperScissorsChoice.Paper;
                    return true;
                case "scissor":
                case "scissors":
                case "s":
                case "2":
                    choice = RockPaperScissorsChoice.Scissor;
                    return true;
                default:
                    return false;
            }
        }
    }
}
