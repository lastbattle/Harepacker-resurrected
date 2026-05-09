using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly NewYearCardRuntime _newYearCardRuntime = new();

        private void RegisterNewYearCardChatCommands()
        {
            _chat.CommandHandler.RegisterCommand(
                "newyearcard",
                "Inspect or drive CUINewYearCardDlg and CUINewYearCardSenderDlg parity",
                "/newyearcard [status|sender|read [from to memo...]|draft <inventoryPosition> <itemId> <target> <memo...>|target <name>|memo <text...>|search [query...]|select <1-based index>|send [confirmempty]|hide]",
                HandleNewYearCardCommand);
        }

        private ChatCommandHandler.CommandResult HandleNewYearCardCommand(string[] args)
        {
            UpdateNewYearCardLocalSender();

            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_newYearCardRuntime.DescribeStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "sender":
                case "open":
                case "compose":
                    ShowNewYearCardSenderWindow();
                    return ChatCommandHandler.CommandResult.Ok(_newYearCardRuntime.DescribeStatus());

                case "read":
                    if (args.Length >= 4)
                    {
                        string memo = string.Join(' ', args.Skip(3));
                        _newYearCardRuntime.ConfigureReadView(args[1], args[2], memo);
                    }

                    ShowNewYearCardReadWindow();
                    return ChatCommandHandler.CommandResult.Ok(_newYearCardRuntime.DescribeStatus());

                case "draft":
                    if (args.Length < 5
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int inventoryPosition)
                        || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard draft <inventoryPosition> <itemId> <target> <memo...>");
                    }

                    string draftMemo = string.Join(' ', args.Skip(4));
                    string draftMessage = _newYearCardRuntime.ConfigureDraft(inventoryPosition, itemId, args[3], draftMemo);
                    ShowNewYearCardSenderWindow();
                    return ChatCommandHandler.CommandResult.Ok(draftMessage);

                case "target":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard target <name>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_newYearCardRuntime.SetTarget(args[1]));

                case "memo":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard memo <text...>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(_newYearCardRuntime.SetMemo(string.Join(' ', args.Skip(1))));

                case "search":
                    string query = args.Length >= 2 ? string.Join(' ', args.Skip(1)) : string.Empty;
                    ShowNewYearCardSenderWindow();
                    return ChatCommandHandler.CommandResult.Ok(_newYearCardRuntime.Search(query));

                case "select":
                    if (args.Length < 2
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int selectedIndex))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard select <1-based index>");
                    }

                    return _newYearCardRuntime.TrySelectSearchResult(selectedIndex - 1, out string selectMessage)
                        ? ChatCommandHandler.CommandResult.Ok(selectMessage)
                        : ChatCommandHandler.CommandResult.Error(selectMessage);

                case "send":
                    bool confirmedEmptyMemo = args.Length >= 2
                        && string.Equals(args[1], "confirmempty", StringComparison.OrdinalIgnoreCase);
                    return TrySendNewYearCard(confirmedEmptyMemo, out string sendMessage)
                        ? ChatCommandHandler.CommandResult.Ok(sendMessage)
                        : ChatCommandHandler.CommandResult.Error(sendMessage);

                case "hide":
                case "close":
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.NewYearCardSender);
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.NewYearCardRead);
                    return ChatCommandHandler.CommandResult.Ok("Closed New Year Card sender/read dialog owners.");

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard [status|sender|read [from to memo...]|draft <inventoryPosition> <itemId> <target> <memo...>|target <name>|memo <text...>|search [query...]|select <1-based index>|send [confirmempty]|hide]");
            }
        }

        private void ShowNewYearCardSenderWindow()
        {
            UpdateNewYearCardLocalSender();
            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.NewYearCardSender);
            WireNewYearCardSenderWindow();
        }

        private void ShowNewYearCardReadWindow()
        {
            UpdateNewYearCardLocalSender();
            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.NewYearCardRead);
            WireNewYearCardReadWindow();
        }

        private void WireNewYearCardSenderWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.NewYearCardSender) is not NewYearCardSenderWindow window)
            {
                return;
            }

            window.SetSnapshotProvider(_newYearCardRuntime.GetSenderSnapshot);
            window.SetActions(
                () => _newYearCardRuntime.Search(_newYearCardRuntime.GetSenderSnapshot().TargetName),
                () => TrySendNewYearCard(confirmedEmptyMemo: false, out string message) ? message : message,
                () => _newYearCardRuntime.MarkSendRejected("CUINewYearCardSenderDlg was cancelled before _SendNewYearCard."));
        }

        private void WireNewYearCardReadWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.NewYearCardRead) is NewYearCardReadWindow window)
            {
                window.SetSnapshotProvider(_newYearCardRuntime.GetReadSnapshot);
            }
        }

        private bool TrySendNewYearCard(bool confirmedEmptyMemo, out string message)
        {
            if (!_newYearCardRuntime.TryBuildSendRequest(out NewYearCardSendRequest request, out message, confirmedEmptyMemo))
            {
                return false;
            }

            string dispatchStatus = DispatchNewYearCardSendRequest(request);
            _newYearCardRuntime.MarkSendDispatched(dispatchStatus);
            message = $"{message} {dispatchStatus}";
            return true;
        }

        private string DispatchNewYearCardSendRequest(NewYearCardSendRequest request)
        {
            return DispatchPacketOwnedAccountMoreInfoClientRequest(
                request.Opcode,
                request.Payload,
                "CUINewYearCardSenderDlg::_SendNewYearCard");
        }

        private void UpdateNewYearCardLocalSender()
        {
            _newYearCardRuntime.UpdateLocalSender(
                _playerManager?.Player?.Build?.Name
                ?? _loginCharacterRoster?.SelectedEntry?.Build?.Name);
        }
    }
}
