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
                "/newyearcard [status|sender|read [from to memo...]|draft <inventoryPosition> <itemId> <target> <memo...>|target <name>|memo <text...>|search [query...]|select <1-based index>|send [confirmempty]|readrequest <serial>|result <hex>|hide]",
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

                case "readrequest":
                    if (args.Length < 2
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int serialNumber))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard readrequest <serial>");
                    }

                    return TrySendNewYearCardReadRequest(serialNumber, out string readRequestMessage)
                        ? ChatCommandHandler.CommandResult.Ok(readRequestMessage)
                        : ChatCommandHandler.CommandResult.Error(readRequestMessage);

                case "result":
                case "packet":
                    string parseError = null;
                    if (args.Length < 2 || !TryParseNewYearCardHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] resultPayload, out parseError))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Usage: /newyearcard result <CWvsContext::OnNewYearCardRes payload hex>. {parseError}");
                    }

                    return TryApplyNewYearCardResultPayload(resultPayload, out string resultMessage)
                        ? ChatCommandHandler.CommandResult.Ok(resultMessage)
                        : ChatCommandHandler.CommandResult.Error(resultMessage);

                case "hide":
                case "close":
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.NewYearCardSender);
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.NewYearCardRead);
                    return ChatCommandHandler.CommandResult.Ok("Closed New Year Card sender/read dialog owners.");

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /newyearcard [status|sender|read [from to memo...]|draft <inventoryPosition> <itemId> <target> <memo...>|target <name>|memo <text...>|search [query...]|select <1-based index>|send [confirmempty]|readrequest <serial>|result <hex>|hide]");
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

        private bool TrySendNewYearCardReadRequest(int serialNumber, out string message)
        {
            if (!_newYearCardRuntime.TryBuildReadRequest(serialNumber, out NewYearCardReadRequest request, out message))
            {
                return false;
            }

            string dispatchStatus = DispatchNewYearCardReadRequest(request);
            _newYearCardRuntime.MarkReadRequestDispatched(request.SerialNumber, dispatchStatus);
            message = $"{message} {dispatchStatus}";
            return true;
        }

        private bool TryApplyNewYearCardResultPayload(byte[] payload, out string message)
        {
            if (!_newYearCardRuntime.TryApplyResultPayload(payload, out message))
            {
                return false;
            }

            NewYearCardResultSnapshot snapshot = _newYearCardRuntime.LastResultSnapshot;
            if (snapshot.Subtype == NewYearCardResultSubtype.ReceiveSuccess)
            {
                ShowNewYearCardReadWindow();
            }
            else if ((snapshot.Subtype == NewYearCardResultSubtype.ArrivalList
                    || snapshot.Subtype == NewYearCardResultSubtype.ArrivalSingle)
                && snapshot.Arrivals.Count > 0)
            {
                ShowNewYearCardArrivalPrompt(snapshot.Arrivals[0], snapshot.Arrivals.Count);
            }

            return true;
        }

        private string DispatchNewYearCardSendRequest(NewYearCardSendRequest request)
        {
            return DispatchPacketOwnedAccountMoreInfoClientRequest(
                request.Opcode,
                request.Payload,
                "CUINewYearCardSenderDlg::_SendNewYearCard");
        }

        private string DispatchNewYearCardReadRequest(NewYearCardReadRequest request)
        {
            return DispatchPacketOwnedAccountMoreInfoClientRequest(
                request.Opcode,
                request.Payload,
                "CUIFadeYesNo::CreateNewYearCardArrived accept");
        }

        private void ShowNewYearCardArrivalPrompt(NewYearCardArrivalPrompt prompt, int promptCount)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                return;
            }

            string senderName = string.IsNullOrWhiteSpace(prompt.SenderName)
                ? "Unknown"
                : prompt.SenderName;
            string body = promptCount > 1
                ? $"{promptCount} New Year Cards arrived."
                : "New Year Card arrived.";
            ConfigureInGameConfirmDialog(
                "New Year Card",
                body,
                string.Empty,
                onConfirm: () => TrySendNewYearCardReadRequest(prompt.SerialNumber, out _),
                onCancel: null,
                presentation: confirmDialogWindow.CreateParcelAlarmPresentation(),
                fadeYesNoType: SharedFadeYesNoModalType.NewYearCardArrived,
                fadeYesNoLifetimeMilliseconds: SharedFadeYesNoModalOwner.NativeInviteLifetimeMilliseconds,
                fadeYesNoPayloadFields: new SharedFadeYesNoPayloadFields(
                    RequesterName: senderName,
                    Message: body,
                    SerialNumber: prompt.SerialNumber));
            ShowWindow(
                MapSimulatorWindowNames.InGameConfirmDialog,
                confirmDialogWindow,
                trackDirectionModeOwner: true);
        }

        private void UpdateNewYearCardLocalSender()
        {
            _newYearCardRuntime.UpdateLocalSender(
                _playerManager?.Player?.Build?.Name
                ?? _loginCharacterRoster?.SelectedEntry?.Build?.Name);
        }

        private static bool TryParseNewYearCardHexBytes(string value, out byte[] bytes, out string error)
        {
            bytes = Array.Empty<byte>();
            error = null;
            string normalized = new((value ?? string.Empty)
                .Where(c => !char.IsWhiteSpace(c) && c != '-' && c != ':')
                .ToArray());
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(2);
            }

            if (normalized.Length == 0 || normalized.Length % 2 != 0)
            {
                error = "Payload hex must contain an even number of digits.";
                return false;
            }

            bytes = new byte[normalized.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(normalized.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                {
                    error = $"Invalid hex byte at offset {i}.";
                    bytes = Array.Empty<byte>();
                    return false;
                }
            }

            return true;
        }
    }
}
