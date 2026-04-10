using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const short VegaOwnerRequestOpcode = 0x55;
        private const int VegaOwnerLaunchDelayMs = 50;
        private const int VegaOwnerResultDelayMs = 50;
        private const int VegaOwnerExclusiveRequestCooldownMs = 500;
        private const int VegaOwnerExternalResultFallbackDelayMs = 3000;
        private const int VegaResultPreludeLoopSoundStringPoolId = 0x1534;
        private const string VegaResultPreludeLoopSoundFallback = "Sound/UI.img/EnchantDelay";
        private const byte VegaPacketOwnedSuccessPreludeCode = 68;
        private const byte VegaPacketOwnedSuccessTerminalCode = 69;
        private const byte VegaPacketOwnedFailPreludeCode = 73;
        private const byte VegaPacketOwnedFailTerminalCode = 71;
        private const int VegaConsumeCashLaunchPayloadLength = sizeof(int) + sizeof(short) + sizeof(int);
        private const string VegaResultLoopSoundKey = "PacketOwnedSound:Sound/UI.img/EnchantDelay:VegaLoop";
        private ActiveVegaModifierSelectionState _activeVegaModifierSelection;
        private bool _vegaExclusiveRequestSent;
        private bool _vegaResultLoopSoundActive;
        private string _vegaResultLoopSoundInstanceKey = string.Empty;
        private int _vegaExclusiveRequestSentTick = int.MinValue;
        private PendingVegaLaunchState _pendingVegaLaunchState;
        private PendingVegaLoopbackLaunchState _pendingVegaLoopbackLaunchState;
        private PendingVegaCastState _pendingVegaCastState;
        private PendingVegaPromptState _pendingVegaPromptState;
        private Action _inGameConfirmAcceptedAction;
        private Action _inGameConfirmCancelledAction;

        private sealed class ActiveVegaModifierSelectionState
        {
            public int ModifierItemId { get; init; }
            public InventoryType InventoryType { get; init; }
            public int SlotIndex { get; init; }
            public string Source { get; init; } = string.Empty;
        }

        private sealed class PendingVegaLaunchState
        {
            public int ModifierItemId { get; init; }
            public InventoryType ModifierInventoryType { get; init; }
            public int ModifierSlotIndex { get; init; } = -1;
            public string Source { get; init; } = string.Empty;
            public int RequestedAtTick { get; init; }
            public int ReadyAtTick { get; init; }
        }

        private sealed class PendingVegaCastState
        {
            public VegaSpellUI.VegaOwnerRequest Request { get; init; }
            public bool UseWhiteScroll { get; init; }
            public InventoryType ModifierInventoryType { get; init; }
            public int ModifierSlotIndex { get; init; } = -1;
            public InventoryType ScrollInventoryType { get; init; }
            public int ScrollSlotIndex { get; init; } = -1;
            public int EncodedEquipPosition { get; init; }
            public int RequestedAtTick { get; init; }
            public int ResultReadyAtTick { get; set; }
            public byte[] EncodedPayload { get; init; } = Array.Empty<byte>();
            public string RequestDispatchSummary { get; init; } = string.Empty;
            public bool ResultApplied { get; set; }
            public ItemUpgradeUI.ItemUpgradeAttemptResult Result { get; set; }
            public byte PrimaryResultCode { get; init; }
            public byte SecondaryResultCode { get; init; }
            public byte? PacketOwnedPreludeCode { get; set; }
            public byte? PacketOwnedTerminalCode { get; set; }
            public bool PacketOwnedResultObserved { get; set; }
        }

        private sealed class PendingVegaPromptState
        {
            public VegaSpellUI.VegaOwnerRequest Request { get; init; }
            public int RequestedAtTick { get; init; }
        }

        private sealed class PendingVegaLoopbackLaunchState
        {
            public byte[] Payload { get; init; } = Array.Empty<byte>();
            public string Source { get; init; } = string.Empty;
            public int ReadyAtTick { get; init; }
        }

        private void WireVegaSpellWindowOwnerCallbacks(VegaSpellUI vegaSpellWindow)
        {
            if (vegaSpellWindow == null)
            {
                return;
            }

            vegaSpellWindow.StartSpellCastRequested = HandleVegaSpellCastRequested;
            vegaSpellWindow.ValidationFailed = HandleVegaSpellValidationFailed;
            vegaSpellWindow.ResultAcknowledged = HandleVegaSpellResultAcknowledged;
            vegaSpellWindow.ResultPreludeStarted = HandleVegaSpellResultPreludeStarted;
            vegaSpellWindow.ResultPopupStarted = HandleVegaSpellResultPopupStarted;
        }

        private void QueueVegaSpellWindowLaunch(int itemId, InventoryType inventoryType, int slotIndex, string source)
        {
            if (!ItemUpgradeUI.IsVegaSpellConsumable(itemId))
            {
                return;
            }

            StampPacketOwnedUtilityRequestState();
            _pendingVegaLaunchState = new PendingVegaLaunchState
            {
                ModifierItemId = itemId,
                ModifierInventoryType = inventoryType,
                ModifierSlotIndex = slotIndex,
                Source = string.IsNullOrWhiteSpace(source) ? "inventory-use" : source.Trim(),
                RequestedAtTick = currTickCount,
                ReadyAtTick = currTickCount + VegaOwnerLaunchDelayMs
            };
        }

        private void QueuePacketOwnedVegaSpellWindowLaunch(int itemId, InventoryType inventoryType, int slotIndex, string source)
        {
            if (!ItemUpgradeUI.IsVegaSpellConsumable(itemId))
            {
                return;
            }

            int slotPosition = slotIndex >= 0 ? slotIndex + 1 : 0;
            byte[] payload = BuildVegaConsumeCashLaunchPayload(slotPosition, itemId, currTickCount);
            _localUtilityPacketInbox.EnqueueLocal(
                LocalUtilityPacketInboxManager.ConsumeCashItemUseRequestPacketType,
                payload,
                string.IsNullOrWhiteSpace(source)
                    ? "CWvsContext::SendConsumeCashItemUseRequest"
                    : source.Trim());
            _activeVegaModifierSelection = new ActiveVegaModifierSelectionState
            {
                ModifierItemId = itemId,
                InventoryType = inventoryType,
                SlotIndex = slotIndex,
                Source = string.IsNullOrWhiteSpace(source) ? "consume-cash-item request" : source.Trim()
            };
            StampPacketOwnedUtilityRequestState();
        }

        private void DispatchVegaConsumableUseRequest(int itemId, InventoryType inventoryType, int slotIndex, string source)
        {
            if (!ItemUpgradeUI.IsVegaSpellConsumable(itemId))
            {
                return;
            }

            int slotPosition = slotIndex >= 0 ? slotIndex + 1 : 0;
            byte[] payload = BuildVegaConsumeCashLaunchPayload(slotPosition, itemId, currTickCount);
            _activeVegaModifierSelection = new ActiveVegaModifierSelectionState
            {
                ModifierItemId = itemId,
                InventoryType = inventoryType,
                SlotIndex = slotIndex,
                Source = string.IsNullOrWhiteSpace(source) ? "consume-cash-item request" : source.Trim()
            };
            StampPacketOwnedUtilityRequestState();

            string requestSummary = BuildVegaConsumableLaunchDispatchLabel(payload, out bool requiresOfflineLoopback);
            ShowUtilityFeedbackMessage(requestSummary);

            if (!requiresOfflineLoopback)
            {
                _pendingVegaLoopbackLaunchState = null;
                return;
            }

            _pendingVegaLoopbackLaunchState = new PendingVegaLoopbackLaunchState
            {
                Payload = payload,
                Source = string.IsNullOrWhiteSpace(source)
                    ? "consume-cash-item request"
                    : source.Trim(),
                ReadyAtTick = currTickCount + VegaOwnerLaunchDelayMs
            };
        }

        private void UpdateVegaSpellOwnerState()
        {
            ProcessPendingVegaLoopbackLaunchState();
            ProcessPendingVegaLaunchState();
            ProcessPendingVegaCastState();
        }

        private void ProcessPendingVegaLoopbackLaunchState()
        {
            if (_pendingVegaLoopbackLaunchState == null ||
                unchecked(currTickCount - _pendingVegaLoopbackLaunchState.ReadyAtTick) < 0)
            {
                return;
            }

            PendingVegaLoopbackLaunchState loopbackLaunchState = _pendingVegaLoopbackLaunchState;
            _pendingVegaLoopbackLaunchState = null;
            _localUtilityPacketInbox.EnqueueLocal(
                LocalUtilityPacketInboxManager.ConsumeCashItemUseRequestPacketType,
                loopbackLaunchState.Payload,
                $"{loopbackLaunchState.Source} offline packet-owned loopback");
        }

        private void ProcessPendingVegaLaunchState()
        {
            if (_pendingVegaLaunchState == null ||
                unchecked(currTickCount - _pendingVegaLaunchState.ReadyAtTick) < 0)
            {
                return;
            }

            PendingVegaLaunchState launchState = _pendingVegaLaunchState;
            _pendingVegaLaunchState = null;

            if (!TryShowVegaSpellWindow(out VegaSpellUI vegaSpellWindow))
            {
                return;
            }

            _activeVegaModifierSelection = new ActiveVegaModifierSelectionState
            {
                ModifierItemId = launchState.ModifierItemId,
                InventoryType = launchState.ModifierInventoryType,
                SlotIndex = launchState.ModifierSlotIndex,
                Source = launchState.Source
            };
            vegaSpellWindow.PrepareModifierSelection(launchState.ModifierItemId);
            string slotLabel = launchState.ModifierSlotIndex >= 0
                ? $" slot #{launchState.ModifierSlotIndex + 1}"
                : string.Empty;
            string inventoryLabel = launchState.ModifierInventoryType != InventoryType.NONE
                ? launchState.ModifierInventoryType.ToString()
                : "cash";
            vegaSpellWindow.SetOwnerStatusMessage(
                $"Vega's Spell opened from {inventoryLabel}{slotLabel}. Select equipment and a compatible scroll.");
        }

        private void ProcessPendingVegaCastState()
        {
            if (_pendingVegaCastState == null ||
                _pendingVegaCastState.ResultApplied ||
                unchecked(currTickCount - _pendingVegaCastState.ResultReadyAtTick) < 0 ||
                uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is not ItemUpgradeUI itemUpgradeWindow)
            {
                return;
            }

            itemUpgradeWindow.PrepareEquipmentSelection(_pendingVegaCastState.Request.Slot);
            itemUpgradeWindow.PrepareConsumableSelection(_pendingVegaCastState.Request.ModifierItemId);

            ItemUpgradeUI.ItemUpgradeAttemptResult result = itemUpgradeWindow.TryApplyPreparedUpgradeAtSlots(
                _pendingVegaCastState.ScrollInventoryType,
                _pendingVegaCastState.ScrollSlotIndex,
                _pendingVegaCastState.ModifierInventoryType,
                _pendingVegaCastState.ModifierSlotIndex);
            if (!result.Success.HasValue)
            {
                result = new ItemUpgradeUI.ItemUpgradeAttemptResult(
                    success: null,
                    VegaOwnerStringPoolText.GetUnexpectedResultNotice(),
                    _pendingVegaCastState.Request.ScrollItemId,
                    _pendingVegaCastState.Request.ModifierItemId);
                _pendingVegaCastState = null;
                ClearVegaExclusiveRequestState(currTickCount);
                ShowUtilityFeedbackMessage(result.StatusMessage);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI failedWindow)
                {
                    failedWindow.SetOwnerStatusMessage(result.StatusMessage);
                }
                return;
            }

            result = RewriteVegaOwnerResultMessage(result, _pendingVegaCastState.UseWhiteScroll);

            _pendingVegaCastState.ResultApplied = true;
            _pendingVegaCastState.Result = result;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
            {
                vegaSpellWindow.ApplyResolvedSpellResult(result);
            }
        }

        private bool HandleVegaSpellCastRequested(VegaSpellUI.VegaOwnerRequest request)
        {
            if (_pendingVegaCastState != null || HasActiveVegaExclusiveRequestBlock(currTickCount))
            {
                ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI busyWindow)
                {
                    busyWindow.SetOwnerStatusMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                }

                return true;
            }

            if (_pendingEquipmentChangeRequests.Count > 0)
            {
                ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI equipmentBlockedWindow)
                {
                    equipmentBlockedWindow.SetOwnerStatusMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                }

                return true;
            }

            if (_gameState.PendingMapChange || _playerManager?.Player?.Build == null)
            {
                ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetBlockedStateNotice());
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI blockedWindow)
                {
                    blockedWindow.SetOwnerStatusMessage(VegaOwnerStringPoolText.GetBlockedStateNotice());
                }

                return true;
            }

            if (request.RequiresWhiteScrollPrompt)
            {
                _pendingVegaPromptState = new PendingVegaPromptState
                {
                    Request = request,
                    RequestedAtTick = currTickCount
                };
                ShowVegaWhiteScrollPrompt(request);
                return true;
            }

            DispatchVegaSpellRequest(request, useWhiteScroll: false);
            return true;
        }

        private void HandleVegaSpellValidationFailed(VegaSpellUI.VegaOwnerValidationFailure failure)
        {
            string message = failure switch
            {
                VegaSpellUI.VegaOwnerValidationFailure.IncompatiblePair => VegaOwnerStringPoolText.GetIncompatiblePairNotice(),
                _ => VegaOwnerStringPoolText.GetMissingSelectionNotice()
            };

            ShowUtilityFeedbackMessage(message);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
            {
                vegaSpellWindow.SetOwnerStatusMessage(message);
            }
        }

        private void HandleVegaSpellResultPreludeStarted()
        {
            EnsureVegaResultLoopSoundPlaying();
        }

        private void HandleVegaSpellResultPopupStarted()
        {
            StopVegaResultLoopSound();
        }

        private void HandleVegaSpellResultAcknowledged()
        {
            _pendingVegaCastState = null;
            _pendingVegaPromptState = null;
            ClearVegaExclusiveRequestState(currTickCount);
            StopVegaResultLoopSound();
        }

        private void ShowVegaWhiteScrollPrompt(VegaSpellUI.VegaOwnerRequest request)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                DispatchVegaSpellRequest(request, useWhiteScroll: false);
                return;
            }

            ConfigureInGameConfirmDialog(
                "Vega's Spell",
                VegaOwnerStringPoolText.GetWhiteScrollPrompt(),
                $"Recovered CUIVega::OnButtonClicked white-scroll branch for {request.ScrollName}.",
                onConfirm: () =>
                {
                    ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetWhiteScrollUsedNotice());
                    DispatchVegaSpellRequest(request, useWhiteScroll: true);
                },
                onCancel: () =>
                {
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
                    {
                        vegaSpellWindow.RestoreReadyStatusMessage();
                    }

                    _pendingVegaPromptState = null;
                });

            ShowWindow(
                MapSimulatorWindowNames.InGameConfirmDialog,
                confirmDialogWindow,
                trackDirectionModeOwner: true);
        }

        private void DispatchVegaSpellRequest(VegaSpellUI.VegaOwnerRequest request, bool useWhiteScroll)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is not VegaSpellUI vegaSpellWindow)
            {
                return;
            }

            if (!TryResolveVegaRequestContext(request, out VegaRequestContext requestContext, out string contextFailure))
            {
                ShowUtilityFeedbackMessage(contextFailure);
                vegaSpellWindow.SetOwnerStatusMessage(contextFailure);
                return;
            }

            _pendingVegaPromptState = null;
            int requestTick = currTickCount;
            byte[] encodedPayload = BuildVegaRequestPayload(
                modifierSlotPosition: requestContext.ModifierSlotIndex + 1,
                modifierItemId: request.ModifierItemId,
                equipItemId: request.EquipItemId,
                equipSlotPosition: requestContext.EncodedEquipPosition,
                scrollItemId: request.ScrollItemId,
                scrollSlotPosition: requestContext.ScrollSlotIndex + 1,
                useWhiteScroll: useWhiteScroll,
                updateTick: requestTick);
            string requestDispatchSummary = BuildVegaOutboundRequestLabel(
                VegaOwnerRequestOpcode,
                encodedPayload,
                "Mirrored CUIVega::OnButtonClicked through the dedicated Vega owner seam.",
                out int responseDelayMs);
            int readyAtTick = requestTick + Math.Max(
                vegaSpellWindow.GetCastingDurationMs(),
                Math.Max(VegaOwnerResultDelayMs, responseDelayMs));
            (byte primaryResultCode, byte secondaryResultCode) = ResolveVegaResultCodes(success: true);
            if (!_playerManager?.Player?.Build?.Equipment?.ContainsKey(request.Slot) ?? true)
            {
                primaryResultCode = 0;
                secondaryResultCode = 0;
            }

            _pendingVegaCastState = new PendingVegaCastState
            {
                Request = request,
                UseWhiteScroll = useWhiteScroll,
                ModifierInventoryType = requestContext.ModifierInventoryType,
                ModifierSlotIndex = requestContext.ModifierSlotIndex,
                ScrollInventoryType = requestContext.ScrollInventoryType,
                ScrollSlotIndex = requestContext.ScrollSlotIndex,
                EncodedEquipPosition = requestContext.EncodedEquipPosition,
                RequestedAtTick = requestTick,
                ResultReadyAtTick = readyAtTick,
                EncodedPayload = encodedPayload,
                RequestDispatchSummary = requestDispatchSummary,
                PrimaryResultCode = primaryResultCode,
                SecondaryResultCode = secondaryResultCode
            };
            MarkVegaExclusiveRequestSent(requestTick);
            StampPacketOwnedUtilityRequestState();
            ShowUtilityFeedbackMessage(requestDispatchSummary);

            string status = useWhiteScroll
                ? $"{request.ModifierName} sent with White Scroll protection."
                : $"Sent {request.ModifierName} request for {request.EquipName}.";
            vegaSpellWindow.StartSpellCast(status);
        }

        private static byte[] BuildVegaRequestPayload(
            int modifierSlotPosition,
            int modifierItemId,
            int equipItemId,
            int equipSlotPosition,
            int scrollItemId,
            int scrollSlotPosition,
            bool useWhiteScroll,
            int updateTick)
        {
            byte[] payload = new byte[(sizeof(int) * 7) + sizeof(short)];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), updateTick);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(sizeof(int), sizeof(short)), (short)Math.Max(0, modifierSlotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(short), sizeof(int)), modifierItemId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 2) + sizeof(short), sizeof(int)), equipItemId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 3) + sizeof(short), sizeof(int)), equipSlotPosition);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 4) + sizeof(short), sizeof(int)), scrollItemId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 5) + sizeof(short), sizeof(int)), scrollSlotPosition);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 6) + sizeof(short), sizeof(int)), useWhiteScroll ? 1 : 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 7) + sizeof(short), sizeof(int)), updateTick);
            return payload;
        }

        private static byte[] BuildVegaConsumeCashLaunchPayload(
            int modifierSlotPosition,
            int modifierItemId,
            int updateTick)
        {
            byte[] payload = new byte[VegaConsumeCashLaunchPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), updateTick);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(sizeof(int), sizeof(short)), (short)Math.Max(0, modifierSlotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(short), sizeof(int)), modifierItemId);
            return payload;
        }

        private bool HasActiveVegaExclusiveRequestBlock(int currentTick)
        {
            return IsVegaExclusiveRequestBlocked(_vegaExclusiveRequestSent, _vegaExclusiveRequestSentTick, currentTick);
        }

        private static ItemUpgradeUI.ItemUpgradeAttemptResult RewriteVegaOwnerResultMessage(
            ItemUpgradeUI.ItemUpgradeAttemptResult result,
            bool usedWhiteScroll)
        {
            if (!usedWhiteScroll || !result.Success.HasValue)
            {
                return result;
            }

            string message = result.Success.Value
                ? VegaOwnerStringPoolText.GetWhiteScrollSuccessNotice()
                : VegaOwnerStringPoolText.GetWhiteScrollProtectedFailureNotice();
            return new ItemUpgradeUI.ItemUpgradeAttemptResult(
                result.Success,
                message,
                result.ConsumableItemId,
                result.ModifierItemId);
        }

        private void ConfigureInGameConfirmDialog(
            string title,
            string body,
            string footer,
            Action onConfirm,
            Action onCancel,
            InGameConfirmDialogPresentation presentation = null)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                onConfirm?.Invoke();
                return;
            }

            _inGameConfirmAcceptedAction = onConfirm;
            _inGameConfirmCancelledAction = onCancel;
            confirmDialogWindow.Configure(title, body, footer, presentation);
        }

        private void ClearInGameConfirmDialogActions()
        {
            _inGameConfirmAcceptedAction = null;
            _inGameConfirmCancelledAction = null;
        }

        private void MarkVegaExclusiveRequestSent(int currentTick)
        {
            _vegaExclusiveRequestSent = true;
            _vegaExclusiveRequestSentTick = currentTick;
        }

        private void ClearVegaExclusiveRequestState(int currentTick)
        {
            _vegaExclusiveRequestSent = false;
            _vegaExclusiveRequestSentTick = currentTick;
        }

        private bool TryQueueVegaOutboundPacket(
            short opcode,
            byte[] payload,
            out string dispatchSummary)
        {
            string payloadHex = payload.Length > 0 ? Convert.ToHexString(payload) : "<empty>";
            string dispatchStatus = "live bridge unavailable";
            string outboxStatus = "packet outbox unavailable";

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, payload, out dispatchStatus))
            {
                dispatchSummary = $"Mirrored opcode {opcode} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
                return true;
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(opcode, payload, out outboxStatus))
            {
                dispatchSummary = $"Mirrored opcode {opcode} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
                return true;
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, payload, out string queuedBridgeStatus))
            {
                dispatchSummary = $"Queued opcode {opcode} [{payloadHex}] for deferred official-session injection after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
                return true;
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, payload, out string queuedOutboxStatus))
            {
                dispatchSummary = $"Queued opcode {opcode} [{payloadHex}] for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
                return true;
            }

            dispatchSummary = $"Kept opcode {opcode} [{payloadHex}] simulator-local because neither the live bridge nor the generic outbox nor either deferred queue accepted the Vega owner request. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            return false;
        }

        private string BuildVegaOutboundRequestLabel(
            short opcode,
            byte[] payload,
            string localMessage,
            out int responseDelayMs)
        {
            responseDelayMs = VegaOwnerResultDelayMs;
            bool queuedExternally = TryQueueVegaOutboundPacket(opcode, payload, out string dispatchSummary);
            if (queuedExternally)
            {
                responseDelayMs = VegaOwnerExternalResultFallbackDelayMs;
            }

            return string.IsNullOrWhiteSpace(localMessage)
                ? dispatchSummary
                : $"{localMessage} {dispatchSummary}";
        }

        private string BuildVegaConsumableLaunchDispatchLabel(
            byte[] payload,
            out bool requiresOfflineLoopback)
        {
            bool awaitingPacketOwnedLaunch = HasLiveVegaLaunchIngressRoute();
            bool queuedExternally = TryQueueVegaOutboundPacket(VegaOwnerRequestOpcode, payload, out string dispatchSummary);
            requiresOfflineLoopback = !awaitingPacketOwnedLaunch;

            string localMessage = awaitingPacketOwnedLaunch
                ? "Mirrored CWvsContext::SendConsumeCashItemUseRequest through the Vega owner seam and waiting for packet-owned launch traffic."
                : "Mirrored CWvsContext::SendConsumeCashItemUseRequest through the Vega owner seam and scheduled offline packet-owned launch loopback because no live Vega transport is attached.";

            if (!queuedExternally && awaitingPacketOwnedLaunch)
            {
                requiresOfflineLoopback = true;
                localMessage =
                    "Mirrored CWvsContext::SendConsumeCashItemUseRequest through the Vega owner seam and fell back to offline packet-owned launch loopback because no live Vega transport accepted the outbound request.";
            }

            return string.IsNullOrWhiteSpace(dispatchSummary)
                ? localMessage
                : $"{localMessage} {dispatchSummary}";
        }

        private bool TryApplyPacketOwnedVegaLaunchPayload(byte[] payload, out string message)
        {
            message = null;

            if (!TryDecodeVegaLaunchPayload(
                    payload,
                    _activeVegaModifierSelection?.ModifierItemId ?? 0,
                    _activeVegaModifierSelection?.InventoryType ?? InventoryType.NONE,
                    _activeVegaModifierSelection?.SlotIndex ?? -1,
                    out VegaLaunchPayload launchPayload))
            {
                message = "Vega launch payload did not expose a supported modifier item.";
                return false;
            }

            QueueVegaSpellWindowLaunch(
                launchPayload.ModifierItemId,
                launchPayload.InventoryType,
                launchPayload.SlotIndex,
                launchPayload.Source);
            message = launchPayload.HasExplicitSlot
                ? $"Queued packet-owned Vega launch for modifier {launchPayload.ModifierItemId} from {launchPayload.InventoryType} slot #{launchPayload.SlotIndex + 1}."
                : $"Queued packet-owned Vega launch for modifier {launchPayload.ModifierItemId}.";
            return true;
        }

        private bool TryApplyPacketOwnedVegaResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodeVegaResultPayload(payload, out byte resultCode))
            {
                message = "Vega result payload must contain at least the leading result byte from CUIVega::OnVegaResult.";
                return false;
            }

            if (_pendingVegaCastState == null)
            {
                message = $"Vega result code {resultCode} arrived without a pending Vega request.";
                return false;
            }

            if (TryMapPacketOwnedVegaPreludeCode(resultCode, out bool success))
            {
                if (!TryApplyPendingVegaPacketOwnedResult(success, out ItemUpgradeUI.ItemUpgradeAttemptResult result, out string error))
                {
                    message = error;
                    return false;
                }

                result = RewriteVegaOwnerResultMessage(result, _pendingVegaCastState.UseWhiteScroll);
                _pendingVegaCastState.PacketOwnedPreludeCode = resultCode;
                _pendingVegaCastState.PacketOwnedResultObserved = true;
                _pendingVegaCastState.ResultApplied = true;
                _pendingVegaCastState.Result = result;
                _pendingVegaCastState.ResultReadyAtTick = currTickCount;
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
                {
                    vegaSpellWindow.ApplyPacketOwnedResultPrelude(result);
                }

                EnsureVegaResultLoopSoundPlaying();
                message = $"Applied packet-owned Vega prelude result code {resultCode} through CUIVega::OnVegaResult ownership.";
                return true;
            }

            if (TryMapPacketOwnedVegaTerminalCode(resultCode, out bool terminalSuccess))
            {
                _pendingVegaCastState.PacketOwnedTerminalCode = resultCode;
                _pendingVegaCastState.PacketOwnedResultObserved = true;
                message = $"Observed packet-owned Vega terminal result code {resultCode} ({(terminalSuccess ? "success" : "failure")}).";
                return true;
            }

            ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.FormatUnknownResultNotice(resultCode));
            message = $"Observed unknown packet-owned Vega result code {resultCode}.";
            return true;
        }

        private bool TryResolveVegaRequestContext(
            VegaSpellUI.VegaOwnerRequest request,
            out VegaRequestContext context,
            out string failureMessage)
        {
            context = default;
            failureMessage = VegaOwnerStringPoolText.GetBlockedStateNotice();

            if (uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventoryWindow)
            {
                return false;
            }

            if (!RepairDurabilityClientParity.TryEncodeEquippedPosition(request.Slot, request.EquipItemId, out int encodedEquipPosition))
            {
                return false;
            }

            if (!TryResolveVegaModifierSlotPosition(
                    inventoryWindow,
                    request.ModifierItemId,
                    out InventoryType modifierInventoryType,
                    out int modifierSlotIndex)
                || !TryResolveInventorySlotIndex(
                    inventoryWindow,
                    request.ScrollItemId,
                    preferredInventoryType: InventoryType.USE,
                    out InventoryType scrollInventoryType,
                    out int scrollSlotIndex))
            {
                failureMessage = VegaOwnerStringPoolText.GetMissingSelectionNotice();
                return false;
            }

            context = new VegaRequestContext(
                encodedEquipPosition,
                modifierInventoryType,
                modifierSlotIndex,
                scrollInventoryType,
                scrollSlotIndex);
            return true;
        }

        private bool TryResolveVegaModifierSlotPosition(
            UI.IInventoryRuntime inventoryWindow,
            int modifierItemId,
            out InventoryType inventoryType,
            out int slotIndex)
        {
            inventoryType = InventoryType.NONE;
            slotIndex = -1;

            if (_activeVegaModifierSelection != null &&
                _activeVegaModifierSelection.ModifierItemId == modifierItemId &&
                TryGetInventorySlot(
                    inventoryWindow,
                    _activeVegaModifierSelection.InventoryType,
                    _activeVegaModifierSelection.SlotIndex,
                    modifierItemId))
            {
                inventoryType = _activeVegaModifierSelection.InventoryType;
                slotIndex = _activeVegaModifierSelection.SlotIndex;
                return true;
            }

            return TryResolveInventorySlotIndex(
                inventoryWindow,
                modifierItemId,
                preferredInventoryType: InventoryType.CASH,
                out inventoryType,
                out slotIndex);
        }

        private static bool TryResolveInventorySlotIndex(
            UI.IInventoryRuntime inventoryWindow,
            int itemId,
            InventoryType preferredInventoryType,
            out InventoryType inventoryType,
            out int slotIndex)
        {
            inventoryType = InventoryType.NONE;
            slotIndex = -1;
            if (inventoryWindow == null || itemId <= 0)
            {
                return false;
            }

            List<InventoryType> searchOrder = new List<InventoryType>(2);
            if (preferredInventoryType == InventoryType.USE || preferredInventoryType == InventoryType.CASH)
            {
                searchOrder.Add(preferredInventoryType);
            }

            if (!searchOrder.Contains(InventoryType.USE))
            {
                searchOrder.Add(InventoryType.USE);
            }

            if (!searchOrder.Contains(InventoryType.CASH))
            {
                searchOrder.Add(InventoryType.CASH);
            }

            for (int typeIndex = 0; typeIndex < searchOrder.Count; typeIndex++)
            {
                InventoryType currentType = searchOrder[typeIndex];
                IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(currentType);
                if (slots == null)
                {
                    continue;
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    InventorySlotData slot = slots[i];
                    if (slot == null ||
                        slot.IsDisabled ||
                        slot.ItemId != itemId ||
                        Math.Max(0, slot.Quantity) <= 0)
                    {
                        continue;
                    }

                    inventoryType = currentType;
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInventorySlot(
            UI.IInventoryRuntime inventoryWindow,
            InventoryType inventoryType,
            int slotIndex,
            int expectedItemId)
        {
            if (inventoryWindow == null || slotIndex < 0)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            if (slots == null || slotIndex >= slots.Count)
            {
                return false;
            }

            InventorySlotData slot = slots[slotIndex];
            return slot != null
                && !slot.IsDisabled
                && slot.ItemId == expectedItemId
                && Math.Max(0, slot.Quantity) > 0;
        }

        private static (byte PrimaryCode, byte SecondaryCode) ResolveVegaResultCodes(bool success)
        {
            return success
                ? (VegaPacketOwnedSuccessPreludeCode, VegaPacketOwnedSuccessTerminalCode)
                : (VegaPacketOwnedFailPreludeCode, VegaPacketOwnedFailTerminalCode);
        }

        internal static byte[] BuildVegaRequestPayloadForTests(
            int modifierSlotPosition,
            int modifierItemId,
            int equipItemId,
            int equipSlotPosition,
            int scrollItemId,
            int scrollSlotPosition,
            bool useWhiteScroll,
            int updateTick)
        {
            return BuildVegaRequestPayload(
                modifierSlotPosition,
                modifierItemId,
                equipItemId,
                equipSlotPosition,
                scrollItemId,
                scrollSlotPosition,
                useWhiteScroll,
                updateTick);
        }

        internal static byte[] BuildVegaConsumeCashLaunchPayloadForTests(
            int modifierSlotPosition,
            int modifierItemId,
            int updateTick)
        {
            return BuildVegaConsumeCashLaunchPayload(modifierSlotPosition, modifierItemId, updateTick);
        }

        internal static (byte PrimaryCode, byte SecondaryCode) ResolveVegaResultCodesForTests(bool success)
        {
            return ResolveVegaResultCodes(success);
        }

        internal static bool TryDecodeVegaLaunchPayloadForTests(
            byte[] payload,
            int fallbackModifierItemId,
            InventoryType fallbackInventoryType,
            int fallbackSlotIndex,
            out int modifierItemId,
            out InventoryType inventoryType,
            out int slotIndex,
            out bool hasExplicitSlot)
        {
            bool decoded = TryDecodeVegaLaunchPayload(
                payload,
                fallbackModifierItemId,
                fallbackInventoryType,
                fallbackSlotIndex,
                out VegaLaunchPayload launchPayload);
            modifierItemId = launchPayload.ModifierItemId;
            inventoryType = launchPayload.InventoryType;
            slotIndex = launchPayload.SlotIndex;
            hasExplicitSlot = launchPayload.HasExplicitSlot;
            return decoded;
        }

        internal static bool TryDecodeVegaResultPayloadForTests(byte[] payload, out byte resultCode)
        {
            return TryDecodeVegaResultPayload(payload, out resultCode);
        }

        internal static bool IsVegaExclusiveRequestBlockedForTests(bool requestSent, int lastRequestTick, int currentTick)
        {
            return IsVegaExclusiveRequestBlocked(requestSent, lastRequestTick, currentTick);
        }

        private static bool IsVegaExclusiveRequestBlocked(bool requestSent, int lastRequestTick, int currentTick)
        {
            if (requestSent)
            {
                return true;
            }

            return lastRequestTick != int.MinValue
                && unchecked(currentTick - lastRequestTick) < VegaOwnerExclusiveRequestCooldownMs;
        }

        private readonly record struct VegaRequestContext(
            int EncodedEquipPosition,
            InventoryType ModifierInventoryType,
            int ModifierSlotIndex,
            InventoryType ScrollInventoryType,
            int ScrollSlotIndex);

        private readonly record struct VegaLaunchPayload(
            int ModifierItemId,
            InventoryType InventoryType,
            int SlotIndex,
            bool HasExplicitSlot,
            string Source);

        private bool TryApplyPendingVegaPacketOwnedResult(
            bool success,
            out ItemUpgradeUI.ItemUpgradeAttemptResult result,
            out string message)
        {
            result = default;
            message = null;

            if (_pendingVegaCastState == null)
            {
                message = "No pending Vega request is available for packet-owned result application.";
                return false;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is not ItemUpgradeUI itemUpgradeWindow)
            {
                message = "Item-upgrade backend is unavailable for packet-owned Vega result application.";
                return false;
            }

            itemUpgradeWindow.PrepareEquipmentSelection(_pendingVegaCastState.Request.Slot);
            itemUpgradeWindow.PrepareConsumableSelection(_pendingVegaCastState.Request.ModifierItemId);
            result = itemUpgradeWindow.TryApplyPreparedUpgradeAtSlots(
                _pendingVegaCastState.ScrollInventoryType,
                _pendingVegaCastState.ScrollSlotIndex,
                _pendingVegaCastState.ModifierInventoryType,
                _pendingVegaCastState.ModifierSlotIndex);
            if (!result.Success.HasValue)
            {
                message = VegaOwnerStringPoolText.GetUnexpectedResultNotice();
                return false;
            }

            if (result.Success.Value != success)
            {
                result = new ItemUpgradeUI.ItemUpgradeAttemptResult(
                    success,
                    success
                        ? "Packet-owned Vega result promoted a success outcome."
                        : "Packet-owned Vega result promoted a failure outcome.",
                    result.ConsumableItemId,
                    result.ModifierItemId);
            }

            return true;
        }

        private void EnsureVegaResultLoopSoundPlaying()
        {
            if (_vegaResultLoopSoundActive || _soundManager == null)
            {
                return;
            }

            string descriptor = ResolveVegaResultLoopSoundDescriptor();

            if (!TryResolvePacketOwnedWzSound(descriptor, "UI.img", out MapleLib.WzLib.WzProperties.WzBinaryProperty soundProperty, out string resolvedDescriptor))
            {
                return;
            }

            string soundKey = $"{VegaResultLoopSoundKey}:{resolvedDescriptor}";
            _soundManager.RegisterSound(soundKey, soundProperty);
            _soundManager.PlayLoopingSound(soundKey);
            _vegaResultLoopSoundActive = true;
            _vegaResultLoopSoundInstanceKey = soundKey;
        }

        private void StopVegaResultLoopSound()
        {
            if (!_vegaResultLoopSoundActive || _soundManager == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_vegaResultLoopSoundInstanceKey))
            {
                _soundManager.StopLoopingSound(_vegaResultLoopSoundInstanceKey);
            }

            _vegaResultLoopSoundActive = false;
            _vegaResultLoopSoundInstanceKey = string.Empty;
        }

        private static string ResolveVegaResultLoopSoundDescriptor()
        {
            return NormalizeVegaResultLoopSoundDescriptor(
                VegaOwnerStringPoolText.GetResultLoopSoundDescriptor());
        }

        internal static string NormalizeVegaResultLoopSoundDescriptorForTests(string descriptor)
        {
            return NormalizeVegaResultLoopSoundDescriptor(descriptor);
        }

        private static string NormalizeVegaResultLoopSoundDescriptor(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return VegaResultPreludeLoopSoundFallback;
            }

            string normalized = descriptor.Trim();
            if (normalized.StartsWith("UI/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Etc/", StringComparison.OrdinalIgnoreCase))
            {
                return VegaResultPreludeLoopSoundFallback;
            }

            return normalized;
        }

        private static bool TryDecodeVegaResultPayload(byte[] payload, out byte resultCode)
        {
            resultCode = 0;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            resultCode = payload[0];
            return true;
        }

        private static bool TryDecodeVegaLaunchPayload(
            byte[] payload,
            int fallbackModifierItemId,
            InventoryType fallbackInventoryType,
            int fallbackSlotIndex,
            out VegaLaunchPayload launchPayload)
        {
            launchPayload = new VegaLaunchPayload(0, InventoryType.NONE, -1, false, "packet-owned launch");

            int modifierItemId = 0;
            int modifierOffset = -1;
            if (payload != null)
            {
                for (int offset = 0; offset <= payload.Length - sizeof(int); offset++)
                {
                    int candidate = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                    if (ItemUpgradeUI.IsVegaSpellConsumable(candidate))
                    {
                        modifierItemId = candidate;
                        modifierOffset = offset;
                        break;
                    }
                }
            }

            if (!ItemUpgradeUI.IsVegaSpellConsumable(modifierItemId))
            {
                modifierItemId = fallbackModifierItemId;
            }

            if (!ItemUpgradeUI.IsVegaSpellConsumable(modifierItemId))
            {
                return false;
            }

            InventoryType inventoryType = fallbackInventoryType;
            int slotIndex = fallbackSlotIndex;
            bool hasExplicitSlot = false;
            bool decodedConsumeCashPrefix = false;
            if (payload != null && modifierOffset >= 0)
            {
                if (modifierOffset >= sizeof(short))
                {
                    short precedingSlotPosition = BinaryPrimitives.ReadInt16LittleEndian(
                        payload.AsSpan(modifierOffset - sizeof(short), sizeof(short)));
                    if (precedingSlotPosition > 0)
                    {
                        slotIndex = precedingSlotPosition - 1;
                        hasExplicitSlot = true;
                        if (inventoryType == InventoryType.NONE)
                        {
                            inventoryType = InventoryType.CASH;
                        }

                        decodedConsumeCashPrefix = modifierOffset == sizeof(int) + sizeof(short);
                    }
                }

                int nextOffset = modifierOffset + sizeof(int);
                if (!decodedConsumeCashPrefix && payload.Length >= nextOffset + (sizeof(int) * 2))
                {
                    int encodedInventoryType = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(nextOffset, sizeof(int)));
                    int encodedSlotIndex = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(nextOffset + sizeof(int), sizeof(int)));
                    if (Enum.IsDefined(typeof(InventoryType), encodedInventoryType))
                    {
                        inventoryType = (InventoryType)encodedInventoryType;
                    }

                    if (encodedSlotIndex >= 0)
                    {
                        slotIndex = encodedSlotIndex;
                        hasExplicitSlot = true;
                    }
                }
                else if (!decodedConsumeCashPrefix && payload.Length >= nextOffset + sizeof(short))
                {
                    short encodedSlotIndex = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(nextOffset, sizeof(short)));
                    if (encodedSlotIndex >= 0)
                    {
                        slotIndex = encodedSlotIndex;
                        hasExplicitSlot = true;
                    }
                }
            }

            launchPayload = new VegaLaunchPayload(
                modifierItemId,
                inventoryType,
                slotIndex,
                hasExplicitSlot,
                "packet-owned launch");
            return true;
        }

        private static bool TryMapPacketOwnedVegaPreludeCode(byte resultCode, out bool success)
        {
            success = resultCode == VegaPacketOwnedSuccessPreludeCode;
            return resultCode == VegaPacketOwnedSuccessPreludeCode || resultCode == VegaPacketOwnedFailPreludeCode;
        }

        private static bool TryMapPacketOwnedVegaTerminalCode(byte resultCode, out bool success)
        {
            success = resultCode == VegaPacketOwnedSuccessTerminalCode;
            return resultCode == VegaPacketOwnedSuccessTerminalCode || resultCode == VegaPacketOwnedFailTerminalCode;
        }

        private bool HasLiveVegaLaunchIngressRoute()
        {
            return HasLiveVegaLaunchIngressRoute(
                _localUtilityOfficialSessionBridge?.HasConnectedSession == true,
                _localUtilityPacketOutbox?.HasConnectedClients == true);
        }

        internal static bool HasLiveVegaLaunchIngressRouteForTests(bool hasConnectedSession, bool hasConnectedOutboxClient)
        {
            return HasLiveVegaLaunchIngressRoute(hasConnectedSession, hasConnectedOutboxClient);
        }

        private static bool HasLiveVegaLaunchIngressRoute(bool hasConnectedSession, bool hasConnectedOutboxClient)
        {
            return hasConnectedSession || hasConnectedOutboxClient;
        }
    }
}
