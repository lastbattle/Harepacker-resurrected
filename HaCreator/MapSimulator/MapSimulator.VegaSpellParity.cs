using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
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
        private const string VegaResultPreludeLoopSoundOwnerImage = "UI.img";
        private const string VegaResultPreludeLoopSoundFallback = "Sound/UI.img/EnchantDelay";
        private const byte VegaPacketOwnedSuccessPreludeCode = 68;
        private const byte VegaPacketOwnedSuccessTerminalCode = 69;
        private const byte VegaPacketOwnedFailPreludeCode = 73;
        private const byte VegaPacketOwnedFailTerminalCode = 71;
        private const int VegaOwnerRequestPayloadLength = (sizeof(int) * 8) + sizeof(short);
        private const int VegaConsumeCashLaunchPayloadPrefixLength = sizeof(int) + sizeof(short) + sizeof(int);
        private const int VegaConsumeCashLaunchPayloadLength = VegaConsumeCashLaunchPayloadPrefixLength + (sizeof(int) * 3);
        private const string VegaResultLoopSoundKeyPrefix = "PacketOwnedSound:VegaLoop";
        private ActiveVegaModifierSelectionState _activeVegaModifierSelection;
        private bool _vegaExclusiveRequestSent;
        private bool _vegaResultLoopSoundActive;
        private string _vegaResultLoopSoundInstanceKey = string.Empty;
        private int _vegaExclusiveRequestSentTick = int.MinValue;
        private readonly Dictionary<EquipSlot, ObservedVegaEquipItemTokenState> _vegaObservedEquipItemTokensBySlot = new();
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
            public int? ObservedClientItemToken { get; init; }
            public string Source { get; init; } = string.Empty;
        }

        private sealed class PendingVegaLaunchState
        {
            public int ModifierItemId { get; init; }
            public InventoryType ModifierInventoryType { get; init; }
            public int ModifierSlotIndex { get; init; } = -1;
            public int? ObservedClientItemToken { get; init; }
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
            public bool OutcomeResolved { get; set; }
            public bool? ResolvedSuccess { get; set; }
            public ItemUpgradeUI.ItemUpgradeAttemptResult Result { get; set; }
            public byte PrimaryResultCode { get; init; }
            public byte SecondaryResultCode { get; init; }
            public byte? PacketOwnedPreludeCode { get; set; }
            public byte? PacketOwnedTerminalCode { get; set; }
            public bool PacketOwnedResultObserved { get; set; }
            public bool? PacketOwnedPreludeSuccess { get; set; }
            public bool? PacketOwnedTerminalSuccess { get; set; }
            public int? PacketOwnedPreludeStartedAtTick { get; set; }
            public int PacketOwnedPreludeDurationMs { get; set; }
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

        private sealed class ObservedVegaEquipItemTokenState
        {
            public int ItemId { get; init; }
            public int ItemToken { get; init; }
        }

        private enum VegaEquippedItemTokenSource
        {
            None = 0,
            PreferredRequest,
            ObservedSlot,
            ObservedInventory,
            CharacterPart,
            Synthetic
        }

        private readonly record struct VegaEquippedItemTokenResolution(
            int ItemToken,
            VegaEquippedItemTokenSource Source)
        {
            public bool IsClientAuthored => Source != VegaEquippedItemTokenSource.None
                && Source != VegaEquippedItemTokenSource.Synthetic;
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

        private void QueueVegaSpellWindowLaunch(
            int itemId,
            InventoryType inventoryType,
            int slotIndex,
            string source,
            int? observedClientItemToken = null)
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
                ObservedClientItemToken = observedClientItemToken,
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
            int modifierItemToken = ResolveLiveVegaModifierItemToken(inventoryType, slotIndex, itemId);
            byte[] payload = BuildVegaConsumeCashLaunchPayload(
                slotPosition,
                itemId,
                currTickCount,
                inventoryType,
                slotIndex,
                modifierItemToken);
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
                ObservedClientItemToken = modifierItemToken != 0 ? modifierItemToken : null,
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
            int modifierItemToken = ResolveLiveVegaModifierItemToken(inventoryType, slotIndex, itemId);
            byte[] payload = BuildVegaConsumeCashLaunchPayload(
                slotPosition,
                itemId,
                currTickCount,
                inventoryType,
                slotIndex,
                modifierItemToken);
            _activeVegaModifierSelection = new ActiveVegaModifierSelectionState
            {
                ModifierItemId = itemId,
                InventoryType = inventoryType,
                SlotIndex = slotIndex,
                ObservedClientItemToken = modifierItemToken != 0 ? modifierItemToken : null,
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
                ObservedClientItemToken = launchState.ObservedClientItemToken,
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

            if (_pendingVegaCastState.OutcomeResolved)
            {
                string applyError = null;
                if (!_pendingVegaCastState.ResolvedSuccess.HasValue ||
                    !TryApplyPendingVegaPacketOwnedResult(
                        _pendingVegaCastState.ResolvedSuccess.Value,
                        out ItemUpgradeUI.ItemUpgradeAttemptResult appliedResult,
                        out applyError))
                {
                    ItemUpgradeUI.ItemUpgradeAttemptResult failureResult = new(
                        success: null,
                        string.IsNullOrWhiteSpace(applyError)
                            ? VegaOwnerStringPoolText.GetUnexpectedResultNotice()
                            : applyError,
                        _pendingVegaCastState.Request.ScrollItemId,
                        _pendingVegaCastState.Request.ModifierItemId);
                    _pendingVegaCastState = null;
                    ClearVegaExclusiveRequestState(currTickCount);
                    ShowUtilityFeedbackMessage(failureResult.StatusMessage);
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI failedWindow)
                    {
                        failedWindow.SetOwnerStatusMessage(failureResult.StatusMessage);
                    }

                    return;
                }

                appliedResult = RewriteVegaOwnerResultMessage(appliedResult, _pendingVegaCastState.UseWhiteScroll);
                _pendingVegaCastState.ResultApplied = true;
                _pendingVegaCastState.Result = appliedResult;

                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI resolvedWindow)
                {
                    resolvedWindow.ApplyResolvedSpellResult(appliedResult);
                }

                return;
            }

            ItemUpgradeUI.ItemUpgradeAttemptResult result = itemUpgradeWindow.TryResolvePreparedUpgradeOutcomeAtSlots(
                _pendingVegaCastState.ScrollInventoryType,
                _pendingVegaCastState.ScrollSlotIndex,
                _pendingVegaCastState.ModifierInventoryType,
                _pendingVegaCastState.ModifierSlotIndex);
            if (!result.Success.HasValue)
            {
                ItemUpgradeUI.ItemUpgradeAttemptResult failureResult = new(
                    success: null,
                    VegaOwnerStringPoolText.GetUnexpectedResultNotice(),
                    _pendingVegaCastState.Request.ScrollItemId,
                    _pendingVegaCastState.Request.ModifierItemId);
                _pendingVegaCastState = null;
                ClearVegaExclusiveRequestState(currTickCount);
                ShowUtilityFeedbackMessage(failureResult.StatusMessage);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI failedWindow)
                {
                    failedWindow.SetOwnerStatusMessage(failureResult.StatusMessage);
                }

                return;
            }

            result = RewriteVegaOwnerResultMessage(result, _pendingVegaCastState.UseWhiteScroll);
            _pendingVegaCastState.OutcomeResolved = true;
            _pendingVegaCastState.ResolvedSuccess = result.Success;
            _pendingVegaCastState.Result = result;
            _pendingVegaCastState.ResultReadyAtTick = currTickCount + ResolveVegaDeferredTerminalApplyDelayMs(
                uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaWindow
                    ? vegaWindow.GetResultPreludeDurationMs()
                    : 0);

            ApplyVegaResultPrelude(result, allowSoundWithoutWindow: false);
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

            RememberObservedVegaEquipItemToken(
                request.Slot,
                request.EquipItemId,
                requestContext.EquipItemToken,
                requestContext.IsEquipItemTokenClientAuthored);
            ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);

            _pendingVegaPromptState = null;
            int requestTick = currTickCount;
            byte[] encodedPayload = BuildVegaRequestPayload(
                modifierSlotPosition: requestContext.ModifierSlotIndex + 1,
                modifierItemId: request.ModifierItemId,
                equipItemToken: requestContext.EquipItemToken,
                equipSlotPosition: requestContext.EncodedEquipPosition,
                scrollItemToken: requestContext.ScrollItemToken,
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
            int equipItemToken,
            int equipSlotPosition,
            int scrollItemToken,
            int scrollSlotPosition,
            bool useWhiteScroll,
            int updateTick)
        {
            byte[] payload = new byte[VegaOwnerRequestPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), updateTick);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(sizeof(int), sizeof(short)), (short)Math.Max(0, modifierSlotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(short), sizeof(int)), modifierItemId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 2) + sizeof(short), sizeof(int)), equipItemToken);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 3) + sizeof(short), sizeof(int)), equipSlotPosition);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 4) + sizeof(short), sizeof(int)), scrollItemToken);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 5) + sizeof(short), sizeof(int)), scrollSlotPosition);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 6) + sizeof(short), sizeof(int)), useWhiteScroll ? 1 : 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan((sizeof(int) * 7) + sizeof(short), sizeof(int)), updateTick);
            return payload;
        }

        private static byte[] BuildVegaConsumeCashLaunchPayload(
            int modifierSlotPosition,
            int modifierItemId,
            int updateTick,
            InventoryType inventoryType,
            int slotIndex,
            int modifierItemToken)
        {
            byte[] payload = new byte[VegaConsumeCashLaunchPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), updateTick);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(sizeof(int), sizeof(short)), (short)Math.Max(0, modifierSlotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(short), sizeof(int)), modifierItemId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(VegaConsumeCashLaunchPayloadPrefixLength, sizeof(int)), (int)inventoryType);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(VegaConsumeCashLaunchPayloadPrefixLength + sizeof(int), sizeof(int)), slotIndex);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(VegaConsumeCashLaunchPayloadPrefixLength + (sizeof(int) * 2), sizeof(int)), modifierItemToken);
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
            TryConsumePacketOwnedQuestResultStartQuestLatchFromSharedExclusiveReset();
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

            TryStampObservedVegaModifierItemToken(
                launchPayload.InventoryType,
                launchPayload.SlotIndex,
                launchPayload.ModifierItemId,
                launchPayload.ModifierItemToken);
            QueueVegaSpellWindowLaunch(
                launchPayload.ModifierItemId,
                launchPayload.InventoryType,
                launchPayload.SlotIndex,
                launchPayload.Source,
                launchPayload.ModifierItemToken != 0 ? launchPayload.ModifierItemToken : null);
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
                ItemUpgradeUI.ItemUpgradeAttemptResult result = BuildPacketOwnedVegaPreludeResult(
                    _pendingVegaCastState.Request,
                    success);
                result = RewriteVegaOwnerResultMessage(result, _pendingVegaCastState.UseWhiteScroll);
                int preludeDurationMs = ResolveCurrentVegaResultPreludeDurationMs();
                _pendingVegaCastState.PacketOwnedPreludeCode = resultCode;
                _pendingVegaCastState.PacketOwnedResultObserved = true;
                _pendingVegaCastState.PacketOwnedPreludeSuccess = success;
                _pendingVegaCastState.PacketOwnedPreludeStartedAtTick = currTickCount;
                _pendingVegaCastState.PacketOwnedPreludeDurationMs = preludeDurationMs;
                _pendingVegaCastState.OutcomeResolved = true;
                _pendingVegaCastState.ResolvedSuccess = success;
                _pendingVegaCastState.Result = result;
                _pendingVegaCastState.ResultReadyAtTick = currTickCount + VegaOwnerExternalResultFallbackDelayMs;
                ApplyVegaResultPrelude(result, allowSoundWithoutWindow: true);
                message = $"Observed packet-owned Vega prelude result code {resultCode} through CUIVega::OnVegaResult ownership and deferred equipment mutation until the terminal result packet.";
                return true;
            }

            if (TryMapPacketOwnedVegaTerminalCode(resultCode, out bool terminalSuccess))
            {
                if (_pendingVegaCastState.PacketOwnedPreludeSuccess.HasValue)
                {
                    terminalSuccess = _pendingVegaCastState.PacketOwnedPreludeSuccess.Value;
                }

                if (!_pendingVegaCastState.OutcomeResolved)
                {
                    ItemUpgradeUI.ItemUpgradeAttemptResult result = BuildPacketOwnedVegaPreludeResult(
                        _pendingVegaCastState.Request,
                        terminalSuccess);
                    result = RewriteVegaOwnerResultMessage(result, _pendingVegaCastState.UseWhiteScroll);
                    int preludeDurationMs = ResolveCurrentVegaResultPreludeDurationMs();
                    _pendingVegaCastState.PacketOwnedPreludeStartedAtTick = currTickCount;
                    _pendingVegaCastState.PacketOwnedPreludeDurationMs = preludeDurationMs;
                    _pendingVegaCastState.PacketOwnedPreludeSuccess = terminalSuccess;
                    _pendingVegaCastState.OutcomeResolved = true;
                    _pendingVegaCastState.ResolvedSuccess = terminalSuccess;
                    _pendingVegaCastState.Result = result;
                    ApplyVegaResultPrelude(result, allowSoundWithoutWindow: true);
                }

                _pendingVegaCastState.PacketOwnedTerminalCode = resultCode;
                _pendingVegaCastState.PacketOwnedResultObserved = true;
                _pendingVegaCastState.PacketOwnedTerminalSuccess = terminalSuccess;
                _pendingVegaCastState.ResolvedSuccess = terminalSuccess;
                _pendingVegaCastState.ResultReadyAtTick = ResolveVegaPacketOwnedTerminalApplyReadyTick(
                    currTickCount,
                    _pendingVegaCastState.PacketOwnedPreludeStartedAtTick ?? currTickCount,
                    _pendingVegaCastState.PacketOwnedPreludeDurationMs);
                message = $"Observed packet-owned Vega terminal result code {resultCode} ({(terminalSuccess ? "success" : "failure")}) and deferred equipment mutation until the recovered prelude handoff.";
                return true;
            }

            ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.FormatUnknownResultNotice(resultCode));
            HandleUnknownPacketOwnedVegaResult();
            message = $"Observed unknown packet-owned Vega result code {resultCode}.";
            return true;
        }

        private void HandleUnknownPacketOwnedVegaResult()
        {
            _pendingVegaCastState = null;
            _pendingVegaPromptState = null;
            ClearVegaExclusiveRequestState(currTickCount);
            StopVegaResultLoopSound();
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
            {
                vegaSpellWindow.Hide();
            }
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

            CharacterPart equippedPart = null;
            _playerManager?.Player?.Build?.Equipment?.TryGetValue(request.Slot, out equippedPart);
            int observedInventoryEquipItemToken = ResolveVegaEquippedInventoryItemToken(
                inventoryWindow,
                request.Slot,
                request.EquipItemId,
                encodedEquipPosition);
            VegaEquippedItemTokenResolution equipItemTokenResolution = ResolveVegaEquippedItemToken(
                request.EquipItemToken,
                ResolveObservedVegaEquipItemToken(request.Slot, request.EquipItemId),
                observedInventoryEquipItemToken,
                request.Slot,
                request.EquipItemId,
                encodedEquipPosition,
                equippedPart);
            if (!TryResolveVegaModifierSlotPosition(
                    inventoryWindow,
                    request.ModifierItemId,
                    out InventoryType modifierInventoryType,
                    out int modifierSlotIndex,
                    out InventorySlotData modifierSlot)
                || !TryResolveInventorySlotIndex(
                    inventoryWindow,
                    request.ScrollItemId,
                    preferredInventoryType: InventoryType.USE,
                    out InventoryType scrollInventoryType,
                    out int scrollSlotIndex,
                    out InventorySlotData scrollSlot))
            {
                failureMessage = VegaOwnerStringPoolText.GetMissingSelectionNotice();
                return false;
            }

            context = new VegaRequestContext(
                encodedEquipPosition,
                equipItemTokenResolution.ItemToken,
                equipItemTokenResolution.IsClientAuthored,
                modifierInventoryType,
                modifierSlotIndex,
                ResolveVegaModifierRequestItemToken(modifierInventoryType, modifierSlotIndex, request.ModifierItemId, modifierSlot),
                scrollInventoryType,
                scrollSlotIndex,
                BuildVegaInventoryItemToken(scrollInventoryType, scrollSlotIndex, request.ScrollItemId, scrollSlot));
            return true;
        }

        private int ResolveVegaModifierRequestItemToken(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            return ResolveVegaModifierRequestItemToken(
                _activeVegaModifierSelection?.ObservedClientItemToken,
                _activeVegaModifierSelection?.ModifierItemId ?? 0,
                _activeVegaModifierSelection?.InventoryType ?? InventoryType.NONE,
                _activeVegaModifierSelection?.SlotIndex ?? -1,
                inventoryType,
                slotIndex,
                itemId,
                slot);
        }

        private bool TryResolveVegaModifierSlotPosition(
            UI.IInventoryRuntime inventoryWindow,
            int modifierItemId,
            out InventoryType inventoryType,
            out int slotIndex,
            out InventorySlotData slot)
        {
            inventoryType = InventoryType.NONE;
            slotIndex = -1;
            slot = null;

            if (_activeVegaModifierSelection != null &&
                _activeVegaModifierSelection.ModifierItemId == modifierItemId &&
                TryGetInventorySlot(
                    inventoryWindow,
                    _activeVegaModifierSelection.InventoryType,
                    _activeVegaModifierSelection.SlotIndex,
                    modifierItemId,
                    out InventorySlotData activeSlot))
            {
                inventoryType = _activeVegaModifierSelection.InventoryType;
                slotIndex = _activeVegaModifierSelection.SlotIndex;
                slot = activeSlot;
                return true;
            }

            return TryResolveInventorySlotIndex(
                inventoryWindow,
                modifierItemId,
                preferredInventoryType: InventoryType.CASH,
                out inventoryType,
                out slotIndex,
                out slot);
        }

        private int ResolveLiveVegaModifierItemToken(InventoryType inventoryType, int slotIndex, int itemId)
        {
            InventorySlotData slot = null;
            if (uiWindowManager?.InventoryWindow is UI.IInventoryRuntime inventoryWindow)
            {
                TryGetInventorySlot(inventoryWindow, inventoryType, slotIndex, itemId, out slot);
            }

            return BuildVegaModifierItemToken(inventoryType, slotIndex, itemId, slot);
        }

        private static bool TryResolveInventorySlotIndex(
            UI.IInventoryRuntime inventoryWindow,
            int itemId,
            InventoryType preferredInventoryType,
            out InventoryType inventoryType,
            out int slotIndex,
            out InventorySlotData slot)
        {
            inventoryType = InventoryType.NONE;
            slotIndex = -1;
            slot = null;
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
                    InventorySlotData candidate = slots[i];
                    if (candidate == null ||
                        candidate.IsDisabled ||
                        candidate.ItemId != itemId ||
                        Math.Max(0, candidate.Quantity) <= 0)
                    {
                        continue;
                    }

                    inventoryType = currentType;
                    slotIndex = i;
                    slot = candidate.Clone();
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInventorySlot(
            UI.IInventoryRuntime inventoryWindow,
            InventoryType inventoryType,
            int slotIndex,
            int expectedItemId,
            out InventorySlotData slot)
        {
            slot = null;
            if (inventoryWindow == null || slotIndex < 0)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            if (slots == null || slotIndex >= slots.Count)
            {
                return false;
            }

            InventorySlotData candidate = slots[slotIndex];
            if (candidate == null
                || candidate.IsDisabled
                || candidate.ItemId != expectedItemId
                || Math.Max(0, candidate.Quantity) <= 0)
            {
                return false;
            }

            slot = candidate.Clone();
            return true;
        }

        private void TryStampObservedVegaModifierItemToken(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            int observedToken)
        {
            if (observedToken == 0 || uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventoryWindow)
            {
                return;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            TryStampObservedVegaModifierItemToken(slots, slotIndex, itemId, observedToken);
        }

        private static bool TryStampObservedVegaModifierItemToken(
            IReadOnlyList<InventorySlotData> slots,
            int slotIndex,
            int itemId,
            int observedToken)
        {
            if (slots == null || observedToken == 0 || itemId <= 0)
            {
                return false;
            }

            if (TryStampObservedVegaModifierItemTokenAtSlot(slots, slotIndex, itemId, observedToken))
            {
                return true;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (TryStampObservedVegaModifierItemTokenAtSlot(slots, i, itemId, observedToken))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryStampObservedVegaModifierItemTokenAtSlot(
            IReadOnlyList<InventorySlotData> slots,
            int slotIndex,
            int itemId,
            int observedToken)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            InventorySlotData slot = slots[slotIndex];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId != itemId
                || Math.Max(0, slot.Quantity) <= 0)
            {
                return false;
            }

            slot.ClientItemToken = observedToken;
            return true;
        }

        private static (byte PrimaryCode, byte SecondaryCode) ResolveVegaResultCodes(bool success)
        {
            return success
                ? (VegaPacketOwnedSuccessPreludeCode, VegaPacketOwnedSuccessTerminalCode)
                : (VegaPacketOwnedFailPreludeCode, VegaPacketOwnedFailTerminalCode);
        }

        private static int BuildSyntheticVegaEquippedItemToken(
            EquipSlot slot,
            int itemId,
            int encodedEquipPosition,
            CharacterPart equippedPart)
        {
            int encodedPosition = encodedEquipPosition;
            if (encodedPosition == int.MinValue
                && !RepairDurabilityClientParity.TryEncodeEquippedPosition(slot, itemId, out encodedPosition))
            {
                encodedPosition = 0;
            }

            int normalizedPosition = Math.Abs(encodedPosition) & 0x3F;
            return BuildDeterministicVegaItemToken(
                itemId,
                (int)slot,
                normalizedPosition,
                equippedPart?.OwnerAccountId ?? 0,
                equippedPart?.OwnerCharacterId ?? 0,
                equippedPart?.UpgradeSlots ?? 0,
                equippedPart?.TotalUpgradeSlotCount ?? 0,
                equippedPart?.RemainingUpgradeSlotCount ?? 0,
                equippedPart?.EnhancementStarCount ?? 0,
                equippedPart?.TradeAvailable ?? 0,
                equippedPart?.IsCash == true ? 1 : 0,
                equippedPart?.IsCashOwnershipLocked == true ? 1 : 0);
        }

        private static VegaEquippedItemTokenResolution ResolveVegaEquippedItemToken(
            int preferredItemToken,
            int observedEquipItemToken,
            int observedInventoryEquipItemToken,
            EquipSlot slot,
            int itemId,
            int encodedEquipPosition,
            CharacterPart equippedPart)
        {
            if (preferredItemToken != 0)
            {
                return new VegaEquippedItemTokenResolution(
                    preferredItemToken,
                    VegaEquippedItemTokenSource.PreferredRequest);
            }

            if (observedEquipItemToken != 0)
            {
                return new VegaEquippedItemTokenResolution(
                    observedEquipItemToken,
                    VegaEquippedItemTokenSource.ObservedSlot);
            }

            if (observedInventoryEquipItemToken != 0)
            {
                return new VegaEquippedItemTokenResolution(
                    observedInventoryEquipItemToken,
                    VegaEquippedItemTokenSource.ObservedInventory);
            }

            if (TryResolveClientAuthoredVegaEquippedItemToken(equippedPart, out int itemToken))
            {
                return new VegaEquippedItemTokenResolution(
                    itemToken,
                    VegaEquippedItemTokenSource.CharacterPart);
            }

            return new VegaEquippedItemTokenResolution(
                BuildSyntheticVegaEquippedItemToken(slot, itemId, encodedEquipPosition, equippedPart),
                VegaEquippedItemTokenSource.Synthetic);
        }

        private static int BuildVegaEquippedItemToken(
            int preferredItemToken,
            int observedEquipItemToken,
            int observedInventoryEquipItemToken,
            EquipSlot slot,
            int itemId,
            int encodedEquipPosition,
            CharacterPart equippedPart)
        {
            return ResolveVegaEquippedItemToken(
                preferredItemToken,
                observedEquipItemToken,
                observedInventoryEquipItemToken,
                slot,
                itemId,
                encodedEquipPosition,
                equippedPart).ItemToken;
        }

        private static int ResolveVegaEquippedInventoryItemToken(
            UI.IInventoryRuntime inventoryWindow,
            EquipSlot equipSlot,
            int equipItemId,
            int encodedEquipPosition)
        {
            if (inventoryWindow == null || equipItemId <= 0)
            {
                return 0;
            }

            IReadOnlyList<InventorySlotData> equipSlots = inventoryWindow.GetSlots(InventoryType.EQUIP);
            if (equipSlots == null || equipSlots.Count == 0)
            {
                return 0;
            }

            int fallbackToken = 0;
            int bestScore = int.MinValue;
            int bestToken = 0;
            for (int i = 0; i < equipSlots.Count; i++)
            {
                InventorySlotData candidate = equipSlots[i];
                if (candidate == null
                    || candidate.IsDisabled
                    || candidate.ItemId != equipItemId
                    || Math.Max(0, candidate.Quantity) <= 0
                    || !TryResolveClientAuthoredVegaInventoryItemToken(candidate, out int token))
                {
                    continue;
                }

                if (fallbackToken == 0)
                {
                    fallbackToken = token;
                }

                int score = 0;
                if (candidate.IsEquipped)
                {
                    score += 10;
                }

                CharacterPart tooltipPart = candidate.TooltipPart;
                if (tooltipPart?.Slot == equipSlot)
                {
                    score += 8;
                }

                if (tooltipPart?.ItemId == equipItemId)
                {
                    score += 4;
                }

                if (tooltipPart != null
                    && RepairDurabilityClientParity.TryEncodeEquippedPosition(
                        tooltipPart.Slot,
                        equipItemId,
                        out int tooltipEncodedPosition)
                    && tooltipEncodedPosition == encodedEquipPosition)
                {
                    score += 6;
                }

                score -= Math.Min(i, 32);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestToken = token;
                }
            }

            return bestToken != 0 ? bestToken : fallbackToken;
        }

        private int ResolveObservedVegaEquipItemToken(EquipSlot slot, int itemId)
        {
            if (!_vegaObservedEquipItemTokensBySlot.TryGetValue(slot, out ObservedVegaEquipItemTokenState observedState)
                || observedState == null
                || observedState.ItemToken == 0
                || observedState.ItemId <= 0
                || observedState.ItemId != itemId)
            {
                return 0;
            }

            return observedState.ItemToken;
        }

        private void RememberObservedVegaEquipItemToken(
            EquipSlot slot,
            int itemId,
            int equipItemToken,
            bool isClientAuthored)
        {
            if (slot == EquipSlot.None || itemId <= 0 || equipItemToken == 0)
            {
                return;
            }

            if (!isClientAuthored)
            {
                return;
            }

            _vegaObservedEquipItemTokensBySlot[slot] = new ObservedVegaEquipItemTokenState
            {
                ItemId = itemId,
                ItemToken = equipItemToken
            };
        }

        private static int BuildSyntheticVegaInventoryItemToken(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            long cashSerialNumber = slot?.CashItemSerialNumber ?? 0L;
            return BuildDeterministicVegaItemToken(
                itemId,
                (int)inventoryType,
                Math.Max(slotIndex, 0) + 1,
                slot?.Quantity ?? 0,
                slot?.MaxStackSize ?? 0,
                slot?.PendingRequestId ?? 0,
                slot?.OwnerCharacterId ?? 0,
                slot?.OwnerAccountId ?? 0,
                slot?.IsCashOwnershipLocked == true ? 1 : 0,
                slot?.IsEquipped == true ? 1 : 0,
                slot?.TooltipPart?.ItemId ?? 0,
                slot?.TooltipPart?.UpgradeSlots ?? 0,
                slot?.TooltipPart?.TotalUpgradeSlotCount ?? 0,
                slot?.TooltipPart?.RemainingUpgradeSlotCount ?? 0,
                slot?.TooltipPart?.EnhancementStarCount ?? 0,
                FoldVegaSerialNumberToInt(cashSerialNumber),
                unchecked((int)(cashSerialNumber >> 32)));
        }

        private static int BuildVegaInventoryItemToken(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            if (TryResolveClientAuthoredVegaInventoryItemToken(slot, out int itemToken))
            {
                return itemToken;
            }

            return BuildSyntheticVegaInventoryItemToken(inventoryType, slotIndex, itemId, slot);
        }

        private static int BuildVegaModifierItemToken(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            return BuildVegaInventoryItemToken(inventoryType, slotIndex, itemId, slot);
        }

        private static int ResolveVegaModifierRequestItemToken(
            int? observedClientItemToken,
            int observedItemId,
            InventoryType observedInventoryType,
            int observedSlotIndex,
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            if (observedClientItemToken.GetValueOrDefault() != 0
                && observedItemId == itemId
                && observedInventoryType == inventoryType
                && observedSlotIndex == slotIndex)
            {
                return observedClientItemToken.Value;
            }

            if (observedClientItemToken.GetValueOrDefault() != 0
                && observedItemId == itemId
                && !TryResolveClientAuthoredVegaInventoryItemToken(slot, out _))
            {
                return observedClientItemToken.Value;
            }

            return BuildVegaInventoryItemToken(inventoryType, slotIndex, itemId, slot);
        }

        private static bool TryResolveClientAuthoredVegaInventoryItemToken(InventorySlotData slot, out int itemToken)
        {
            itemToken = 0;
            if (slot == null)
            {
                return false;
            }

            if (slot.ClientItemToken.GetValueOrDefault() != 0)
            {
                itemToken = slot.ClientItemToken.Value;
                return true;
            }

            if (slot.TooltipPart?.ClientItemToken.GetValueOrDefault() != 0)
            {
                itemToken = slot.TooltipPart.ClientItemToken.Value;
                return true;
            }

            if (slot.CashItemSerialNumber is long cashItemSerialNumber)
            {
                itemToken = FoldVegaSerialNumberToInt(cashItemSerialNumber);
                return itemToken != 0;
            }

            if (slot.PendingRequestId != 0)
            {
                itemToken = slot.PendingRequestId;
                return true;
            }

            return false;
        }

        private static int FoldVegaSerialNumberToInt(long serialNumber)
        {
            int folded = unchecked((int)(serialNumber ^ (serialNumber >> 32)));
            if (folded == 0 && serialNumber != 0)
            {
                folded = unchecked((int)serialNumber);
            }

            return folded;
        }

        private static int BuildDeterministicVegaItemToken(params int[] components)
        {
            unchecked
            {
                const uint fnvOffset = 2166136261;
                const uint fnvPrime = 16777619;
                uint hash = fnvOffset;
                for (int i = 0; i < components.Length; i++)
                {
                    uint component = (uint)components[i];
                    hash ^= component;
                    hash *= fnvPrime;
                    hash ^= component >> 16;
                    hash *= fnvPrime;
                }

                int token = (int)(hash & 0x7FFFFFFF);
                return token == 0 ? 1 : token;
            }
        }

        private static bool TryResolveClientAuthoredVegaEquippedItemToken(CharacterPart equippedPart, out int itemToken)
        {
            itemToken = 0;
            if (equippedPart?.ClientItemToken.GetValueOrDefault() != 0)
            {
                itemToken = equippedPart.ClientItemToken.Value;
                return true;
            }

            return false;
        }

        internal static byte[] BuildVegaRequestPayloadForTests(
            int modifierSlotPosition,
            int modifierItemId,
            int equipItemToken,
            int equipSlotPosition,
            int scrollItemToken,
            int scrollSlotPosition,
            bool useWhiteScroll,
            int updateTick)
        {
            return BuildVegaRequestPayload(
                modifierSlotPosition,
                modifierItemId,
                equipItemToken,
                equipSlotPosition,
                scrollItemToken,
                scrollSlotPosition,
                useWhiteScroll,
                updateTick);
        }

        internal static byte[] BuildVegaConsumeCashLaunchPayloadForTests(
            int modifierSlotPosition,
            int modifierItemId,
            int updateTick,
            InventoryType inventoryType,
            int slotIndex,
            int modifierItemToken)
        {
            return BuildVegaConsumeCashLaunchPayload(
                modifierSlotPosition,
                modifierItemId,
                updateTick,
                inventoryType,
                slotIndex,
                modifierItemToken);
        }

        internal static byte[] BuildVegaConsumeCashLaunchPayloadWithSlotForTests(
            int modifierSlotPosition,
            int modifierItemId,
            int updateTick,
            InventoryType inventoryType,
            int slotIndex,
            InventorySlotData slot)
        {
            return BuildVegaConsumeCashLaunchPayload(
                modifierSlotPosition,
                modifierItemId,
                updateTick,
                inventoryType,
                slotIndex,
                BuildVegaModifierItemToken(inventoryType, slotIndex, modifierItemId, slot));
        }

        internal static (byte PrimaryCode, byte SecondaryCode) ResolveVegaResultCodesForTests(bool success)
        {
            return ResolveVegaResultCodes(success);
        }

        internal static int BuildSyntheticVegaEquippedItemTokenForTests(
            EquipSlot slot,
            int itemId,
            int encodedEquipPosition,
            CharacterPart equippedPart = null)
        {
            return BuildSyntheticVegaEquippedItemToken(slot, itemId, encodedEquipPosition, equippedPart);
        }

        internal static int BuildVegaEquippedItemTokenForTests(
            int preferredItemToken,
            int observedEquipItemToken,
            int observedInventoryEquipItemToken,
            EquipSlot slot,
            int itemId,
            int encodedEquipPosition,
            CharacterPart equippedPart = null)
        {
            return BuildVegaEquippedItemToken(
                preferredItemToken,
                observedEquipItemToken,
                observedInventoryEquipItemToken,
                slot,
                itemId,
                encodedEquipPosition,
                equippedPart);
        }

        internal static bool IsClientAuthoredVegaEquippedItemTokenForTests(
            int preferredItemToken,
            int observedEquipItemToken,
            int observedInventoryEquipItemToken,
            EquipSlot slot,
            int itemId,
            int encodedEquipPosition,
            CharacterPart equippedPart = null)
        {
            return ResolveVegaEquippedItemToken(
                preferredItemToken,
                observedEquipItemToken,
                observedInventoryEquipItemToken,
                slot,
                itemId,
                encodedEquipPosition,
                equippedPart).IsClientAuthored;
        }

        internal static int BuildSyntheticVegaInventoryItemTokenForTests(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            return BuildSyntheticVegaInventoryItemToken(inventoryType, slotIndex, itemId, slot);
        }

        internal static int BuildVegaInventoryItemTokenForTests(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            return BuildVegaInventoryItemToken(inventoryType, slotIndex, itemId, slot);
        }

        internal static int ResolveVegaModifierRequestItemTokenForTests(
            int? observedClientItemToken,
            int observedItemId,
            InventoryType observedInventoryType,
            int observedSlotIndex,
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            return ResolveVegaModifierRequestItemToken(
                observedClientItemToken,
                observedItemId,
                observedInventoryType,
                observedSlotIndex,
                inventoryType,
                slotIndex,
                itemId,
                slot);
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
            return TryDecodeVegaLaunchPayloadForTests(
                payload,
                fallbackModifierItemId,
                fallbackInventoryType,
                fallbackSlotIndex,
                out modifierItemId,
                out inventoryType,
                out slotIndex,
                out hasExplicitSlot,
                out _);
        }

        internal static bool TryDecodeVegaLaunchPayloadForTests(
            byte[] payload,
            int fallbackModifierItemId,
            InventoryType fallbackInventoryType,
            int fallbackSlotIndex,
            out int modifierItemId,
            out InventoryType inventoryType,
            out int slotIndex,
            out bool hasExplicitSlot,
            out int modifierItemToken)
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
            modifierItemToken = launchPayload.ModifierItemToken;
            return decoded;
        }

        internal static bool TryDecodeVegaResultPayloadForTests(byte[] payload, out byte resultCode)
        {
            return TryDecodeVegaResultPayload(payload, out resultCode);
        }

        internal static bool TryStampObservedVegaModifierItemTokenForTests(
            IReadOnlyList<InventorySlotData> slots,
            int slotIndex,
            int itemId,
            int observedToken)
        {
            return TryStampObservedVegaModifierItemToken(
                slots,
                slotIndex,
                itemId,
                observedToken);
        }

        internal static bool IsVegaExclusiveRequestBlockedForTests(bool requestSent, int lastRequestTick, int currentTick)
        {
            return IsVegaExclusiveRequestBlocked(requestSent, lastRequestTick, currentTick);
        }

        internal static int ResolveVegaDeferredTerminalApplyDelayMsForTests(int resultPreludeDurationMs)
        {
            return ResolveVegaDeferredTerminalApplyDelayMs(resultPreludeDurationMs);
        }

        internal static int ResolveVegaPacketOwnedTerminalApplyReadyTickForTests(
            int currentTick,
            int preludeStartedTick,
            int resultPreludeDurationMs)
        {
            return ResolveVegaPacketOwnedTerminalApplyReadyTick(
                currentTick,
                preludeStartedTick,
                resultPreludeDurationMs);
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

        private static int ResolveVegaDeferredTerminalApplyDelayMs(int resultPreludeDurationMs)
        {
            return Math.Max(VegaOwnerResultDelayMs, resultPreludeDurationMs);
        }

        private static int ResolveVegaPacketOwnedTerminalApplyReadyTick(
            int currentTick,
            int preludeStartedTick,
            int resultPreludeDurationMs)
        {
            int preludeReadyTick = preludeStartedTick + ResolveVegaDeferredTerminalApplyDelayMs(resultPreludeDurationMs);
            return unchecked(currentTick - preludeReadyTick) >= 0
                ? currentTick
                : preludeReadyTick;
        }

        private readonly record struct VegaRequestContext(
            int EncodedEquipPosition,
            int EquipItemToken,
            bool IsEquipItemTokenClientAuthored,
            InventoryType ModifierInventoryType,
            int ModifierSlotIndex,
            int ModifierItemToken,
            InventoryType ScrollInventoryType,
            int ScrollSlotIndex,
            int ScrollItemToken);

        private readonly record struct VegaLaunchPayload(
            int ModifierItemId,
            InventoryType InventoryType,
            int SlotIndex,
            bool HasExplicitSlot,
            int ModifierItemToken,
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
                _pendingVegaCastState.ModifierSlotIndex,
                forcedSuccess: success);
            if (!result.Success.HasValue)
            {
                message = VegaOwnerStringPoolText.GetUnexpectedResultNotice();
                return false;
            }

            return true;
        }

        private static ItemUpgradeUI.ItemUpgradeAttemptResult BuildPacketOwnedVegaPreludeResult(
            VegaSpellUI.VegaOwnerRequest request,
            bool success)
        {
            string statusMessage = success
                ? $"{request.EquipName} is responding to Vega's Spell."
                : $"{request.EquipName} is resisting Vega's Spell.";
            return new ItemUpgradeUI.ItemUpgradeAttemptResult(
                success,
                statusMessage,
                request.ScrollItemId,
                request.ModifierItemId);
        }

        private void ApplyVegaResultPrelude(
            ItemUpgradeUI.ItemUpgradeAttemptResult result,
            bool allowSoundWithoutWindow)
        {
            bool appliedToWindow = false;
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
            {
                vegaSpellWindow.ApplyPacketOwnedResultPrelude(result);
                appliedToWindow = true;
            }

            if (appliedToWindow || allowSoundWithoutWindow)
            {
                EnsureVegaResultLoopSoundPlaying();
            }
        }

        internal static string BuildPacketOwnedVegaPreludeResultMessageForTests(
            string equipName,
            bool success)
        {
            var request = new VegaSpellUI.VegaOwnerRequest(
                EquipSlot.Weapon,
                1302000,
                0x2468ACE,
                equipName,
                5610000,
                "Vega's Spell(10%)",
                2040000,
                "Scroll",
                1,
                10,
                30,
                requiresWhiteScrollPrompt: false);
            return BuildPacketOwnedVegaPreludeResult(request, success).StatusMessage;
        }

        private void EnsureVegaResultLoopSoundPlaying()
        {
            if (_vegaResultLoopSoundActive || _soundManager == null)
            {
                return;
            }

            string descriptor = ResolveVegaResultLoopSoundDescriptor();
            if (!TryResolvePacketOwnedWzSound(
                    descriptor,
                    "UI.img",
                    out MapleLib.WzLib.WzProperties.WzBinaryProperty soundProperty,
                    out string resolvedDescriptor,
                    false))
            {
                string fallbackDescriptor = NormalizeVegaResultLoopSoundDescriptor(
                    VegaResultPreludeLoopSoundFallback,
                    VegaResultPreludeLoopSoundFallback);
                if (string.Equals(descriptor, fallbackDescriptor, StringComparison.OrdinalIgnoreCase)
                    || !TryResolvePacketOwnedWzSound(
                        fallbackDescriptor,
                        "UI.img",
                        out soundProperty,
                        out resolvedDescriptor,
                        false))
                {
                    return;
                }
            }

            string soundKey = BuildVegaResultLoopSoundKey(resolvedDescriptor);
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
                VegaOwnerStringPoolText.GetResultLoopSoundDescriptor(),
                MapleStoryStringPool.GetOrFallback(
                    VegaResultPreludeLoopSoundStringPoolId,
                    VegaResultPreludeLoopSoundFallback));
        }

        internal static string BuildVegaResultLoopSoundKeyForTests(string resolvedDescriptor)
        {
            return BuildVegaResultLoopSoundKey(resolvedDescriptor);
        }

        private static string BuildVegaResultLoopSoundKey(string resolvedDescriptor)
        {
            string normalizedDescriptor = NormalizeVegaResultLoopSoundDescriptor(
                resolvedDescriptor,
                VegaResultPreludeLoopSoundFallback);
            return $"{VegaResultLoopSoundKeyPrefix}:{normalizedDescriptor}";
        }

        internal static string NormalizeVegaResultLoopSoundDescriptorForTests(string descriptor)
        {
            return NormalizeVegaResultLoopSoundDescriptor(
                descriptor,
                VegaResultPreludeLoopSoundFallback);
        }

        internal static string NormalizeVegaResultLoopSoundDescriptorForTests(string descriptor, string fallbackDescriptor)
        {
            return NormalizeVegaResultLoopSoundDescriptor(descriptor, fallbackDescriptor);
        }

        private static string NormalizeVegaResultLoopSoundDescriptor(string descriptor, string fallbackDescriptor)
        {
            string fallback = NormalizePacketOwnedClientSoundDescriptor(
                string.IsNullOrWhiteSpace(fallbackDescriptor)
                    ? VegaResultPreludeLoopSoundFallback
                    : fallbackDescriptor);
            if (string.IsNullOrWhiteSpace(fallback))
            {
                fallback = NormalizePacketOwnedClientSoundDescriptor(VegaResultPreludeLoopSoundFallback);
            }

            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            string aliasDescriptor = NormalizePacketOwnedClientSoundDescriptor(
                VegaOwnerStringPoolText.GetResultLoopSoundAliasDescriptor());
            if (string.Equals(normalized, aliasDescriptor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "VegaTwinkling", StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            if (!normalized.Contains('/', StringComparison.Ordinal))
            {
                return normalized;
            }

            if (!TrySplitPacketOwnedClientSoundDescriptor(normalized, out string imageName, out string propertyPath)
                || string.IsNullOrWhiteSpace(propertyPath)
                || propertyPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                || propertyPath.Contains(".img/", StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            if (string.Equals(propertyPath, "VegaTwinkling", StringComparison.OrdinalIgnoreCase)
                || (TrySplitPacketOwnedClientSoundDescriptor(aliasDescriptor, out _, out string aliasPropertyPath)
                    && string.Equals(propertyPath, aliasPropertyPath, StringComparison.OrdinalIgnoreCase)))
            {
                return fallback;
            }

            // CUIVega::OnVegaResult resolves this through play_ui_sound_loop, so keep
            // the descriptor on the UI sound owner even when string-pool text drifts.
            if (!string.Equals(imageName, VegaResultPreludeLoopSoundOwnerImage, StringComparison.OrdinalIgnoreCase))
            {
                return $"{VegaResultPreludeLoopSoundOwnerImage}/{propertyPath}";
            }

            return normalized;
        }

        private int ResolveCurrentVegaResultPreludeDurationMs()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow
                ? vegaSpellWindow.GetResultPreludeDurationMs()
                : 0;
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
            launchPayload = new VegaLaunchPayload(0, InventoryType.NONE, -1, false, 0, "packet-owned launch");

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
            int modifierItemToken = 0;
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
                int explicitSlotTailOffset = decodedConsumeCashPrefix
                    ? VegaConsumeCashLaunchPayloadPrefixLength
                    : nextOffset;
                if (payload.Length >= explicitSlotTailOffset + (sizeof(int) * 2))
                {
                    int encodedInventoryType = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(explicitSlotTailOffset, sizeof(int)));
                    int encodedSlotIndex = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(explicitSlotTailOffset + sizeof(int), sizeof(int)));
                    if (Enum.IsDefined(typeof(InventoryType), encodedInventoryType))
                    {
                        inventoryType = (InventoryType)encodedInventoryType;
                    }

                    if (encodedSlotIndex >= 0)
                    {
                        slotIndex = encodedSlotIndex;
                        hasExplicitSlot = true;
                    }

                    if (payload.Length >= explicitSlotTailOffset + (sizeof(int) * 3))
                    {
                        modifierItemToken = BinaryPrimitives.ReadInt32LittleEndian(
                            payload.AsSpan(explicitSlotTailOffset + (sizeof(int) * 2), sizeof(int)));
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
                modifierItemToken,
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
