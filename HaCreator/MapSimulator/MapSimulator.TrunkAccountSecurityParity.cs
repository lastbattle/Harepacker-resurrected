using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private void WireTrunkSecurityWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is not TrunkUI trunkWindow)
            {
                return;
            }

            trunkWindow.AccountSecurityPromptRequested = HandleTrunkAccountSecurityPromptRequested;
            trunkWindow.CloseRequested = HandleTrunkCloseRequested;
            trunkWindow.WindowHidden = HandleTrunkWindowHidden;
            trunkWindow.PacketOwnedGetItemRequested = HandlePacketOwnedTrunkGetItemRequested;
            trunkWindow.PacketOwnedPutItemRequested = HandlePacketOwnedTrunkPutItemRequested;
        }

        private TrunkUI.PacketOwnedTrunkRequestResult HandlePacketOwnedTrunkGetItemRequested(
            InventoryType inventoryType,
            int ownerRowIndex,
            InventorySlotData slotData)
        {
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = GetPacketOwnedSocialUtilityDialogDispatcher();
            if (!dispatcher.IsPacketOwnedTrunkDialogOpen)
            {
                return TrunkUI.PacketOwnedTrunkRequestResult.Failure(
                    "Packet-owned trunk owner is not open; falling back to local trunk inventory mutation.");
            }

            if (ShowPacketOwnedTrunkGetItemConfirmPrompt(inventoryType, ownerRowIndex, slotData))
            {
                return TrunkUI.PacketOwnedTrunkRequestResult.Success(
                    "CTrunkDlg::SendGetItemRequest opened the recovered CUtilDlg::YesNo confirmation owner; opcode 67 [04] waits for acceptance.");
            }

            return DispatchPacketOwnedTrunkGetItemRequest(inventoryType, ownerRowIndex, slotData);
        }

        private TrunkUI.PacketOwnedTrunkRequestResult DispatchPacketOwnedTrunkGetItemRequest(
            InventoryType inventoryType,
            int ownerRowIndex,
            InventorySlotData slotData)
        {
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = GetPacketOwnedSocialUtilityDialogDispatcher();
            bool built = dispatcher.TryBuildTrunkGetItemOutboundRequest(
                inventoryType,
                ownerRowIndex,
                slotData,
                out PacketOwnedNpcUtilityOutboundRequest request,
                out string message);
            if (!built)
            {
                return TrunkUI.PacketOwnedTrunkRequestResult.Success(message);
            }

            _ = TryDispatchPacketOwnedTrunkOutboundRequest(request, out string dispatchStatus);
            string status = string.IsNullOrWhiteSpace(dispatchStatus)
                ? message
                : $"{message} {dispatchStatus}";
            return TrunkUI.PacketOwnedTrunkRequestResult.Success(status);
        }

        private bool ShowPacketOwnedTrunkGetItemConfirmPrompt(
            InventoryType inventoryType,
            int ownerRowIndex,
            InventorySlotData slotData)
        {
            if (slotData == null ||
                uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                return false;
            }

            string body = TrunkDialogClientParityText.BuildSendGetConfirmationBody(slotData, mesoCost: 0);
            ConfigureInGameConfirmDialog(
                "Storage",
                body,
                "Recovered CTrunkDlg::SendGetItemRequest CUtilDlg::YesNo confirmation owner.",
                onConfirm: () =>
                {
                    TrunkUI.PacketOwnedTrunkRequestResult result = DispatchPacketOwnedTrunkGetItemRequest(
                        inventoryType,
                        ownerRowIndex,
                        slotData);
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is TrunkUI trunkWindow)
                    {
                        trunkWindow.RefreshSecurityStatus(result.Message);
                    }
                },
                onCancel: () =>
                {
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is TrunkUI trunkWindow)
                    {
                        trunkWindow.RefreshSecurityStatus(
                            "CTrunkDlg::SendGetItemRequest confirmation was cancelled before opcode 67 [04].");
                    }
                });
            ShowWindow(
                MapSimulatorWindowNames.InGameConfirmDialog,
                confirmDialogWindow,
                trackDirectionModeOwner: true);
            return true;
        }

        private TrunkUI.PacketOwnedTrunkRequestResult HandlePacketOwnedTrunkPutItemRequested(
            InventoryType inventoryType,
            int inventoryRowIndex,
            InventorySlotData slotData,
            int requestedQuantity)
        {
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = GetPacketOwnedSocialUtilityDialogDispatcher();
            if (!dispatcher.IsPacketOwnedTrunkDialogOpen)
            {
                return TrunkUI.PacketOwnedTrunkRequestResult.Failure(
                    "Packet-owned trunk owner is not open; falling back to local trunk inventory mutation.");
            }

            bool built = dispatcher.TryBuildTrunkPutItemOutboundRequest(
                inventoryType,
                inventoryRowIndex,
                slotData,
                requestedQuantity,
                out PacketOwnedNpcUtilityOutboundRequest request,
                out string message);
            if (!built)
            {
                return TrunkUI.PacketOwnedTrunkRequestResult.Success(message);
            }

            _ = TryDispatchPacketOwnedTrunkOutboundRequest(request, out string dispatchStatus);
            string status = string.IsNullOrWhiteSpace(dispatchStatus)
                ? message
                : $"{message} {dispatchStatus}";
            return TrunkUI.PacketOwnedTrunkRequestResult.Success(status);
        }

        private bool TryDispatchPacketOwnedTrunkOutboundRequest(
            PacketOwnedNpcUtilityOutboundRequest request,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out string bridgeStatus))
            {
                statusMessage = $"Mirrored opcode {request.Opcode} through the live official-session bridge. {bridgeStatus}";
                return true;
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out string outboxStatus))
            {
                statusMessage = $"Mirrored opcode {request.Opcode} through the packet outbox after the bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
                return true;
            }

            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out string queuedBridgeStatus))
            {
                statusMessage = $"Queued opcode {request.Opcode} for deferred official-session injection. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
                return true;
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out string queuedOutboxStatus))
            {
                statusMessage = $"Queued opcode {request.Opcode} for deferred packet outbox delivery. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
                return true;
            }

            statusMessage = $"Opcode {request.Opcode} remained simulator-owned because bridge/outbox dispatch was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            return false;
        }

        private bool HandleTrunkAccountSecurityPromptRequested(TrunkAccountSecurityPromptKind promptKind)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is not TrunkUI trunkWindow ||
                !trunkWindow.IsVisible)
            {
                return false;
            }

            string body;
            string inputLabel;
            string inputPlaceholder;
            int inputMaxLength;
            SoftKeyboardKeyboardType keyboardType;
            LoginUtilityDialogAction action;

            switch (promptKind)
            {
                case TrunkAccountSecurityPromptKind.VerifyPic:
                    body = "Enter the configured PIC to unlock this trunk session.";
                    inputLabel = "PIC";
                    inputPlaceholder = "Enter PIC";
                    inputMaxLength = 8;
                    keyboardType = SoftKeyboardKeyboardType.NumericOnlyAlt;
                    action = LoginUtilityDialogAction.VerifyTrunkPic;
                    break;
                case TrunkAccountSecurityPromptKind.VerifySecondaryPassword:
                    body = "Enter the configured secondary password to unlock this trunk session.";
                    inputLabel = "Secondary Password";
                    inputPlaceholder = "Enter secondary password";
                    inputMaxLength = 16;
                    keyboardType = SoftKeyboardKeyboardType.AlphaNumeric;
                    action = LoginUtilityDialogAction.VerifyTrunkSpw;
                    break;
                default:
                    return false;
            }

            ShowLoginUtilityDialog(
                "Login Utility",
                body,
                LoginUtilityDialogButtonLayout.YesNo,
                action,
                primaryLabel: "Verify",
                secondaryLabel: "Cancel",
                inputLabel: inputLabel,
                inputPlaceholder: inputPlaceholder,
                inputMasked: true,
                inputMaxLength: inputMaxLength,
                softKeyboardType: keyboardType,
                inputBoundsOverride: CreateLoginUtilityInputBoundsOverride(),
                trackDirectionModeOwner: true);
            return true;
        }

        private bool TryHandleTrunkAccountSecurityPrimaryRequested()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is not TrunkUI trunkWindow)
            {
                return false;
            }

            switch (_loginUtilityDialogAction)
            {
                case LoginUtilityDialogAction.VerifyTrunkPic:
                    if (!TryGetLoginUtilityDialogInput(out string trunkPicInput))
                    {
                        _loginTitleStatusMessage = "Enter the PIC before unlocking trunk access.";
                        return true;
                    }

                    if ((trunkWindow.StorageRuntime?.TryVerifyAccountPic(trunkPicInput)).GetValueOrDefault())
                    {
                        HideLoginUtilityDialog();
                        _loginTitleStatusMessage = "Verified the simulator account PIC for trunk access.";
                        trunkWindow.ResumeSecurityUnlockFlow();
                        return true;
                    }

                    ShowLoginUtilityDialog(
                        "Login Utility",
                        "PIC verification failed.",
                        LoginUtilityDialogButtonLayout.Ok,
                        LoginUtilityDialogAction.RetryTrunkPic,
                        noticeTextIndex: 15,
                        trackDirectionModeOwner: true);
                    _loginTitleStatusMessage = "Trunk access rejected the simulator account PIC.";
                    trunkWindow.RefreshSecurityStatus();
                    return true;
                case LoginUtilityDialogAction.VerifyTrunkSpw:
                    if (!TryGetLoginUtilityDialogInput(out string trunkSpwInput))
                    {
                        _loginTitleStatusMessage = "Enter the secondary password before unlocking trunk access.";
                        return true;
                    }

                    if ((trunkWindow.StorageRuntime?.TryVerifyAccountSecondaryPassword(trunkSpwInput)).GetValueOrDefault())
                    {
                        HideLoginUtilityDialog();
                        _loginTitleStatusMessage = "Verified the simulator account secondary password for trunk access.";
                        trunkWindow.ResumeSecurityUnlockFlow();
                        return true;
                    }

                    ShowLoginUtilityDialog(
                        "Login Utility",
                        "Secondary password verification failed.",
                        LoginUtilityDialogButtonLayout.Ok,
                        LoginUtilityDialogAction.RetryTrunkSpw,
                        noticeTextIndex: 93,
                        trackDirectionModeOwner: true);
                    _loginTitleStatusMessage = "Trunk access rejected the simulator account secondary password.";
                    trunkWindow.RefreshSecurityStatus();
                    return true;
                case LoginUtilityDialogAction.RetryTrunkPic:
                    return HandleTrunkAccountSecurityPromptRequested(TrunkAccountSecurityPromptKind.VerifyPic);
                case LoginUtilityDialogAction.RetryTrunkSpw:
                    return HandleTrunkAccountSecurityPromptRequested(TrunkAccountSecurityPromptKind.VerifySecondaryPassword);
                default:
                    return false;
            }
        }

        private bool TryHandleTrunkAccountSecuritySecondaryRequested()
        {
            switch (_loginUtilityDialogAction)
            {
                case LoginUtilityDialogAction.VerifyTrunkPic:
                case LoginUtilityDialogAction.VerifyTrunkSpw:
                case LoginUtilityDialogAction.RetryTrunkPic:
                case LoginUtilityDialogAction.RetryTrunkSpw:
                    HideLoginUtilityDialog();
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is TrunkUI trunkWindow)
                    {
                        trunkWindow.RefreshSecurityStatus();
                    }

                    _loginTitleStatusMessage = "Cancelled the simulator trunk account-authentication prompt.";
                    SyncLoginTitleWindow();
                    SyncLoginEntryDialogs();
                    return true;
                default:
                    return false;
            }
        }

        private void HandleTrunkWindowHidden(TrunkUI _)
        {
            if (IsTrunkAccountSecurityDialogAction(_loginUtilityDialogAction))
            {
                HideLoginUtilityDialog();
            }
        }

        private bool HandleTrunkCloseRequested()
        {
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = GetPacketOwnedSocialUtilityDialogDispatcher();
            if (!dispatcher.TryBuildTrunkCloseOutboundRequest(out PacketOwnedNpcUtilityOutboundRequest request, out _))
            {
                return true;
            }

            TryDispatchPacketOwnedTrunkOutboundRequest(request, out _);
            return true;
        }

        private static bool IsTrunkAccountSecurityDialogAction(LoginUtilityDialogAction action)
        {
            return action == LoginUtilityDialogAction.VerifyTrunkPic ||
                   action == LoginUtilityDialogAction.VerifyTrunkSpw ||
                   action == LoginUtilityDialogAction.RetryTrunkPic ||
                   action == LoginUtilityDialogAction.RetryTrunkSpw;
        }
    }
}
