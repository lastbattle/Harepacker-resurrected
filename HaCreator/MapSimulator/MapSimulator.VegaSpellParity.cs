using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using MapleLib.PacketLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const short VegaOwnerRequestOpcode = 0x55;
        private const int VegaOwnerLaunchDelayMs = 50;
        private const int VegaOwnerResultDelayMs = 50;
        private const int VegaOwnerExclusiveRequestCooldownMs = 500;
        private const int VegaOwnerExternalResultFallbackDelayMs = 3000;
        private const int VegaResultPreludeLoopSoundPrefixStringPoolId = 0x8B9;
        private const string VegaResultPreludeLoopSoundOwnerImage = "UI.img";
        private const string VegaResultPreludeLoopSoundFallback = "Sound/UI.img/EnchantDelay";
        private const byte VegaPacketOwnedSuccessPreludeCode = 68;
        private const byte VegaPacketOwnedSuccessTerminalCode = 69;
        private const byte VegaPacketOwnedFailPreludeCode = 73;
        private const byte VegaPacketOwnedFailTerminalCode = 71;
        private const int VegaOwnerRequestPayloadLength = (sizeof(int) * 8) + sizeof(short);
        private const int VegaConsumeCashLaunchPayloadPrefixLength = sizeof(int) + sizeof(short) + sizeof(int);
        private const int VegaConsumeCashLaunchPayloadLength = VegaConsumeCashLaunchPayloadPrefixLength + (sizeof(int) * 3);
        private const int VegaPacketOwnedEquipSnapshotMarker = 0x56514753; // "VGQS"
        private const int VegaPacketOwnedEquipSnapshotIntCount = 19;
        private const int VegaClientEquipSnapshotStatFieldCount = 15;
        private const int VegaClientEquipSnapshotLegacyStatFieldCount = 14;
        private const byte VegaClientInventoryOperationEquipType = (byte)InventoryType.EQUIP;
        private const byte VegaClientInventoryOperationCashType = (byte)InventoryType.CASH;
        private const byte VegaClientInventoryOperationSlotTypeEquip = 1;
        private const byte VegaClientInventoryOperationSlotTypeBundle = 2;
        private const byte VegaClientInventoryOperationSlotTypePet = 3;
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
            public bool PacketOwnedOutcomeSuccessObserved { get; set; }
            public bool PacketOwnedOutcomeSuccess { get; set; }
            public bool PacketOwnedUpgradeStateObserved { get; set; }
            public int PacketOwnedUpgradeState { get; set; } = int.MinValue;
            public bool PacketOwnedEquipItemTokenObserved { get; set; }
            public int PacketOwnedEquipItemToken { get; set; }
            public bool PacketOwnedEquipSnapshotObserved { get; set; }
            public bool PacketOwnedEquipSnapshotFromInventoryOperation { get; set; }
            public VegaPacketOwnedEquipSnapshot PacketOwnedEquipSnapshot { get; set; }
            public bool PacketOwnedScrollCountObserved { get; set; }
            public int PacketOwnedScrollFinalCount { get; set; }
            public bool PacketOwnedModifierCountObserved { get; set; }
            public int PacketOwnedModifierFinalCount { get; set; }
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
            CharacterPart
        }

        private readonly record struct VegaEquippedItemTokenResolution(
            int ItemToken,
            VegaEquippedItemTokenSource Source)
        {
            public bool IsClientAuthored => Source != VegaEquippedItemTokenSource.None;
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
                (_pendingVegaCastState.PacketOwnedTerminalCode.HasValue &&
                    !_pendingVegaCastState.PacketOwnedPreludeCode.HasValue) ||
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
            ClearVegaExclusiveRequestState(currTickCount);
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
            using PacketWriter writer = new();
            writer.WriteInt(updateTick);
            writer.Write((short)Math.Max(0, modifierSlotPosition));
            writer.WriteInt(modifierItemId);
            writer.WriteInt(equipItemToken);
            writer.WriteInt(equipSlotPosition);
            writer.WriteInt(scrollItemToken);
            writer.WriteInt(scrollSlotPosition);
            writer.WriteInt(useWhiteScroll ? 1 : 0);
            writer.WriteInt(updateTick);
            return writer.ToArray();
        }

        private static byte[] BuildVegaConsumeCashLaunchPayload(
            int modifierSlotPosition,
            int modifierItemId,
            int updateTick,
            InventoryType inventoryType,
            int slotIndex,
            int modifierItemToken)
        {
            using PacketWriter writer = new();
            writer.WriteInt(updateTick);
            writer.Write((short)Math.Max(0, modifierSlotPosition));
            writer.WriteInt(modifierItemId);
            writer.WriteInt((int)inventoryType);
            writer.WriteInt(slotIndex);
            writer.WriteInt(modifierItemToken);
            return writer.ToArray();
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
            InGameConfirmDialogPresentation presentation = null,
            SharedFadeYesNoModalType fadeYesNoType = SharedFadeYesNoModalType.Generic,
            int fadeYesNoLifetimeMilliseconds = -1,
            int fadeYesNoStackIndex = 0,
            bool fadeYesNoQuickDelivery = false,
            SharedFadeYesNoPayloadFields fadeYesNoPayloadFields = null)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                onConfirm?.Invoke();
                return;
            }

            _inGameConfirmAcceptedAction = onConfirm;
            _inGameConfirmCancelledAction = onCancel;
            confirmDialogWindow.ConfigureSharedFadeYesNo(
                new SharedFadeYesNoModalRequest(
                    fadeYesNoType,
                    title,
                    body,
                    footer,
                    fadeYesNoLifetimeMilliseconds,
                    fadeYesNoStackIndex,
                    fadeYesNoQuickDelivery,
                    presentation,
                    fadeYesNoPayloadFields,
                    onConfirm,
                    onCancel),
                presentation);
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
            if (!TryDecodeVegaResultPayloadState(payload, out VegaResultDecodeState decodeState, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            byte resultCode = decodeState.ResultCode;
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
                RecordPacketOwnedVegaOutcomeState(decodeState);
                _pendingVegaCastState.OutcomeResolved = true;
                _pendingVegaCastState.ResolvedSuccess = success;
                _pendingVegaCastState.Result = result;
                if (_pendingVegaCastState.PacketOwnedTerminalCode.HasValue)
                {
                    if (TryResolveVegaTerminalCompletionSuccess(
                            success,
                            _pendingVegaCastState.PacketOwnedTerminalCode.Value,
                            out bool completionSuccess,
                            out _))
                    {
                        _pendingVegaCastState.PacketOwnedTerminalSuccess = completionSuccess;
                        _pendingVegaCastState.ResolvedSuccess = completionSuccess;
                        _pendingVegaCastState.ResultReadyAtTick = ResolveVegaPacketOwnedTerminalApplyReadyTick(
                            currTickCount,
                            _pendingVegaCastState.PacketOwnedPreludeStartedAtTick ?? currTickCount,
                            _pendingVegaCastState.PacketOwnedPreludeDurationMs);
                    }
                    else
                    {
                        _pendingVegaCastState.ResultReadyAtTick = currTickCount + VegaOwnerExternalResultFallbackDelayMs;
                    }
                }
                else
                {
                    _pendingVegaCastState.ResultReadyAtTick = currTickCount + VegaOwnerExternalResultFallbackDelayMs;
                }

                ApplyVegaResultPrelude(result, allowSoundWithoutWindow: true);
                string stateNote = BuildPacketOwnedVegaResultStateNote(decodeState);
                message = _pendingVegaCastState.PacketOwnedTerminalCode.HasValue
                    ? $"Observed packet-owned Vega prelude result code {resultCode}{stateNote} after the terminal packet; replayed the recovered CUIVega::OnVegaResult prelude before terminal equipment mutation."
                    : $"Observed packet-owned Vega prelude result code {resultCode}{stateNote} through CUIVega::OnVegaResult ownership and deferred equipment mutation until the terminal result packet.";
                return true;
            }

            if (TryMapPacketOwnedVegaTerminalCode(resultCode, out bool terminalSuccess))
            {
                if (_pendingVegaCastState.PacketOwnedPreludeSuccess.HasValue)
                {
                    bool preludeSuccess = _pendingVegaCastState.PacketOwnedPreludeSuccess.Value;
                    TryResolveVegaTerminalCompletionSuccess(
                        preludeSuccess,
                        resultCode,
                        out terminalSuccess,
                        out _);
                }

                if (!_pendingVegaCastState.OutcomeResolved)
                {
                    RecordPacketOwnedVegaOutcomeState(decodeState);
                }
                else
                {
                    RecordPacketOwnedVegaOutcomeState(decodeState);
                }

                _pendingVegaCastState.PacketOwnedTerminalCode = resultCode;
                _pendingVegaCastState.PacketOwnedResultObserved = true;
                _pendingVegaCastState.PacketOwnedTerminalSuccess = terminalSuccess;
                if (_pendingVegaCastState.PacketOwnedPreludeCode.HasValue)
                {
                    _pendingVegaCastState.ResolvedSuccess = terminalSuccess;
                    _pendingVegaCastState.ResultReadyAtTick = ResolveVegaPacketOwnedTerminalApplyReadyTick(
                        currTickCount,
                        _pendingVegaCastState.PacketOwnedPreludeStartedAtTick ?? currTickCount,
                        _pendingVegaCastState.PacketOwnedPreludeDurationMs);
                }

                string stateNote = BuildPacketOwnedVegaResultStateNote(decodeState);
                message = _pendingVegaCastState.PacketOwnedPreludeCode.HasValue
                    ? $"Observed packet-owned Vega terminal result code {resultCode} ({(terminalSuccess ? "success" : "failure")}){stateNote} and deferred equipment mutation until the recovered prelude handoff."
                    : $"Observed packet-owned Vega terminal result code {resultCode} ({(terminalSuccess ? "success" : "failure")}){stateNote} before the recovered prelude; stored m_nRet2-style state without starting prelude playback.";
                return true;
            }

            ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.FormatUnknownResultNotice(resultCode));
            HandleUnknownPacketOwnedVegaResult();
            message = $"Observed unknown packet-owned Vega result code {resultCode}.";
            return true;
        }

        private void RecordPacketOwnedVegaOutcomeState(VegaResultDecodeState decodeState)
        {
            if (_pendingVegaCastState == null || !decodeState.HasOutcomeState)
            {
                return;
            }

            if (TryResolvePacketOwnedVegaOutcomeSuccess(decodeState.OutcomeResultValue, out bool outcomeSuccess))
            {
                _pendingVegaCastState.PacketOwnedOutcomeSuccessObserved = true;
                _pendingVegaCastState.PacketOwnedOutcomeSuccess = outcomeSuccess;
            }

            if (decodeState.HasEquipItemToken)
            {
                _pendingVegaCastState.PacketOwnedEquipItemTokenObserved = true;
                _pendingVegaCastState.PacketOwnedEquipItemToken = decodeState.EquipItemToken;
            }

            _pendingVegaCastState.PacketOwnedUpgradeStateObserved = true;
            _pendingVegaCastState.PacketOwnedUpgradeState = decodeState.OutcomeUpgradeState;
            if (decodeState.HasEquipSnapshot)
            {
                if (!_pendingVegaCastState.PacketOwnedEquipSnapshotFromInventoryOperation)
                {
                    _pendingVegaCastState.PacketOwnedEquipSnapshotObserved = true;
                    _pendingVegaCastState.PacketOwnedEquipSnapshot = decodeState.EquipSnapshot;
                }
            }
        }

        private bool TryApplyPendingVegaInventoryOperationAuthorityPayload(byte[] payload, out string message)
        {
            message = null;
            if (_pendingVegaCastState == null || payload == null || payload.Length == 0)
            {
                return false;
            }

            if (!TryDecodeVegaClientInventoryOperationState(
                    payload,
                    _pendingVegaCastState.Request.EquipItemId,
                    _pendingVegaCastState.EncodedEquipPosition,
                    _pendingVegaCastState.Request.ScrollItemId,
                    _pendingVegaCastState.ScrollInventoryType,
                    _pendingVegaCastState.ScrollSlotIndex,
                    _pendingVegaCastState.Request.ModifierItemId,
                    _pendingVegaCastState.ModifierInventoryType,
                    _pendingVegaCastState.ModifierSlotIndex,
                    out VegaClientInventoryOperationState operationState,
                    out string rejectReason))
            {
                message = rejectReason;
                return false;
            }

            VegaPacketOwnedEquipSnapshot snapshot = operationState.EquipSnapshot;
            _pendingVegaCastState.PacketOwnedEquipSnapshotObserved = true;
            _pendingVegaCastState.PacketOwnedEquipSnapshotFromInventoryOperation = true;
            _pendingVegaCastState.PacketOwnedEquipSnapshot = snapshot;
            if (operationState.ScrollCountObserved)
            {
                _pendingVegaCastState.PacketOwnedScrollCountObserved = true;
                _pendingVegaCastState.PacketOwnedScrollFinalCount = operationState.ScrollFinalCount;
            }

            if (operationState.ModifierCountObserved)
            {
                _pendingVegaCastState.PacketOwnedModifierCountObserved = true;
                _pendingVegaCastState.PacketOwnedModifierFinalCount = operationState.ModifierFinalCount;
            }

            if (snapshot.EquipItemToken != 0)
            {
                _pendingVegaCastState.PacketOwnedEquipItemTokenObserved = true;
                _pendingVegaCastState.PacketOwnedEquipItemToken = snapshot.EquipItemToken;
            }

            _pendingVegaCastState.PacketOwnedUpgradeStateObserved = true;
            _pendingVegaCastState.PacketOwnedUpgradeState = snapshot.RemainingSlots;
            if (operationState.ClearsExclusiveRequest)
            {
                ClearVegaExclusiveRequestState(currTickCount);
            }

            string resetNote = operationState.ClearsExclusiveRequest
                ? " and consumed the client bExclRequestSent reset marker"
                : string.Empty;
            string countNote = operationState.ScrollCountObserved || operationState.ModifierCountObserved
                ? " plus packet-authored staged item counts"
                : string.Empty;
            message = $"Captured packet-owned Vega equip state from CWvsContext::OnInventoryOperation for item {snapshot.ItemId}{countNote}{resetNote}; the pending Vega terminal handoff will apply this client-authored equipment snapshot after the recovered result prelude.";
            return true;
        }

        private static string BuildPacketOwnedVegaResultStateNote(VegaResultDecodeState decodeState)
        {
            if (!decodeState.HasOutcomeState)
            {
                return string.Empty;
            }

            string tokenNote = decodeState.HasEquipItemToken
                ? $" and equip TI {decodeState.EquipItemToken}"
                : string.Empty;
            string snapshotNote = decodeState.HasEquipSnapshot
                ? " and packet-authored equip snapshot"
                : string.Empty;
            return $" with packet-authored upgrade state {decodeState.OutcomeUpgradeState}{tokenNote}{snapshotNote}";
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
                0,
                VegaEquippedItemTokenSource.None);
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

        private static int BuildVegaInventoryItemToken(
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            InventorySlotData slot)
        {
            _ = inventoryType;
            _ = slotIndex;
            _ = itemId;
            if (TryResolveClientAuthoredVegaInventoryItemToken(slot, out int itemToken))
            {
                return itemToken;
            }

            return 0;
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

        internal static bool TryDecodeVegaResultPayloadStateForTests(
            byte[] payload,
            out byte resultCode,
            out bool hasOutcomeState,
            out int outcomeResultValue,
            out int outcomeUpgradeState)
        {
            bool decoded = TryDecodeVegaResultPayloadState(payload, out VegaResultDecodeState decodeState, out _);
            resultCode = decodeState.ResultCode;
            hasOutcomeState = decodeState.HasOutcomeState;
            outcomeResultValue = decodeState.OutcomeResultValue;
            outcomeUpgradeState = decodeState.OutcomeUpgradeState;
            return decoded;
        }

        internal static bool TryDecodeVegaResultPayloadStateForTests(
            byte[] payload,
            out byte resultCode,
            out bool hasEquipItemToken,
            out int equipItemToken,
            out bool hasOutcomeState,
            out int outcomeResultValue,
            out int outcomeUpgradeState)
        {
            bool decoded = TryDecodeVegaResultPayloadState(payload, out VegaResultDecodeState decodeState, out _);
            resultCode = decodeState.ResultCode;
            hasEquipItemToken = decodeState.HasEquipItemToken;
            equipItemToken = decodeState.EquipItemToken;
            hasOutcomeState = decodeState.HasOutcomeState;
            outcomeResultValue = decodeState.OutcomeResultValue;
            outcomeUpgradeState = decodeState.OutcomeUpgradeState;
            return decoded;
        }

        internal static bool TryDecodeVegaResultPayloadEquipSnapshotForTests(
            byte[] payload,
            out byte resultCode,
            out bool hasEquipSnapshot,
            out int equipItemToken,
            out int itemId,
            out int totalSlots,
            out int remainingSlots,
            out int successCount,
            out int bonusStr,
            out int bonusWeaponAttack)
        {
            bool decoded = TryDecodeVegaResultPayloadState(payload, out VegaResultDecodeState decodeState, out _);
            resultCode = decodeState.ResultCode;
            hasEquipSnapshot = decodeState.HasEquipSnapshot;
            equipItemToken = decodeState.EquipSnapshot.EquipItemToken;
            itemId = decodeState.EquipSnapshot.ItemId;
            totalSlots = decodeState.EquipSnapshot.TotalSlots;
            remainingSlots = decodeState.EquipSnapshot.RemainingSlots;
            successCount = decodeState.EquipSnapshot.SuccessCount;
            bonusStr = decodeState.EquipSnapshot.BonusSTR;
            bonusWeaponAttack = decodeState.EquipSnapshot.BonusWeaponAttack;
            return decoded;
        }

        internal static bool TryDecodeVegaClientInventoryOperationEquipSnapshotForTests(
            byte[] payload,
            int expectedItemId,
            int expectedEncodedEquipPosition,
            out bool clearsExclusiveRequest,
            out int equipItemToken,
            out int totalSlots,
            out int remainingSlots,
            out int successCount,
            out int bonusStr,
            out int bonusWeaponAttack,
            out string rejectReason)
        {
            bool decoded = TryDecodeVegaClientInventoryOperationState(
                payload,
                expectedItemId,
                expectedEncodedEquipPosition,
                expectedScrollItemId: 0,
                expectedScrollInventoryType: InventoryType.NONE,
                expectedScrollSlotIndex: -1,
                expectedModifierItemId: 0,
                expectedModifierInventoryType: InventoryType.NONE,
                expectedModifierSlotIndex: -1,
                out VegaClientInventoryOperationState operationState,
                out rejectReason);
            VegaPacketOwnedEquipSnapshot snapshot = operationState.EquipSnapshot;
            clearsExclusiveRequest = operationState.ClearsExclusiveRequest;
            equipItemToken = snapshot.EquipItemToken;
            totalSlots = snapshot.TotalSlots;
            remainingSlots = snapshot.RemainingSlots;
            successCount = snapshot.SuccessCount;
            bonusStr = snapshot.BonusSTR;
            bonusWeaponAttack = snapshot.BonusWeaponAttack;
            return decoded;
        }

        internal static bool TryDecodeVegaClientInventoryOperationCountsForTests(
            byte[] payload,
            int expectedItemId,
            int expectedEncodedEquipPosition,
            int expectedScrollItemId,
            InventoryType expectedScrollInventoryType,
            int expectedScrollSlotIndex,
            int expectedModifierItemId,
            InventoryType expectedModifierInventoryType,
            int expectedModifierSlotIndex,
            out bool scrollCountObserved,
            out int scrollFinalCount,
            out bool modifierCountObserved,
            out int modifierFinalCount,
            out string rejectReason)
        {
            bool decoded = TryDecodeVegaClientInventoryOperationState(
                payload,
                expectedItemId,
                expectedEncodedEquipPosition,
                expectedScrollItemId,
                expectedScrollInventoryType,
                expectedScrollSlotIndex,
                expectedModifierItemId,
                expectedModifierInventoryType,
                expectedModifierSlotIndex,
                out VegaClientInventoryOperationState operationState,
                out rejectReason);
            scrollCountObserved = operationState.ScrollCountObserved;
            scrollFinalCount = operationState.ScrollFinalCount;
            modifierCountObserved = operationState.ModifierCountObserved;
            modifierFinalCount = operationState.ModifierFinalCount;
            return decoded;
        }

        internal static bool TryApplyPacketOwnedVegaEquipSnapshotStateForTests(
            CharacterPart equippedPart,
            int expectedItemId,
            int equipItemToken,
            int itemId,
            int totalSlots,
            int remainingSlots,
            int successCount,
            int bonusStr,
            int bonusWeaponAttack)
        {
            return TryApplyPacketOwnedVegaEquipSnapshotState(
                equippedPart,
                expectedItemId,
                new VegaPacketOwnedEquipSnapshot(
                    equipItemToken,
                    itemId,
                    totalSlots,
                    remainingSlots,
                    successCount,
                    bonusStr,
                    0,
                    0,
                    0,
                    0,
                    0,
                    bonusWeaponAttack,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0));
        }

        internal static int VegaPacketOwnedEquipSnapshotMarkerForTests => VegaPacketOwnedEquipSnapshotMarker;

        internal static int VegaPacketOwnedEquipSnapshotIntCountForTests => VegaPacketOwnedEquipSnapshotIntCount;

        internal static bool TryDecodeVegaResultPayloadStateForTests(
            byte[] payload,
            out byte resultCode,
            out bool hasOutcomeState,
            out int outcomeResultValue,
            out int outcomeUpgradeState,
            out string decodeError)
        {
            bool decoded = TryDecodeVegaResultPayloadState(payload, out VegaResultDecodeState decodeState, out decodeError);
            resultCode = decodeState.ResultCode;
            hasOutcomeState = decodeState.HasOutcomeState;
            outcomeResultValue = decodeState.OutcomeResultValue;
            outcomeUpgradeState = decodeState.OutcomeUpgradeState;
            return decoded;
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
            bool mutationSuccess = ResolvePacketOwnedVegaMutationSuccess(
                success,
                _pendingVegaCastState.PacketOwnedOutcomeSuccessObserved,
                _pendingVegaCastState.PacketOwnedOutcomeSuccess);
            if (_pendingVegaCastState.PacketOwnedEquipSnapshotObserved &&
                _pendingVegaCastState.PacketOwnedEquipSnapshotFromInventoryOperation)
            {
                result = itemUpgradeWindow.ApplyPacketOwnedPreparedUpgradeInventoryOperationCounts(
                    _pendingVegaCastState.ScrollInventoryType,
                    _pendingVegaCastState.ScrollSlotIndex,
                    _pendingVegaCastState.PacketOwnedScrollCountObserved,
                    _pendingVegaCastState.PacketOwnedScrollFinalCount,
                    _pendingVegaCastState.ModifierInventoryType,
                    _pendingVegaCastState.ModifierSlotIndex,
                    _pendingVegaCastState.PacketOwnedModifierCountObserved,
                    _pendingVegaCastState.PacketOwnedModifierFinalCount,
                    mutationSuccess);
            }
            else if (_pendingVegaCastState.PacketOwnedEquipSnapshotObserved)
            {
                result = itemUpgradeWindow.ConsumePacketOwnedPreparedUpgradeItemsAtPacketCounts(
                    _pendingVegaCastState.ScrollInventoryType,
                    _pendingVegaCastState.ScrollSlotIndex,
                    _pendingVegaCastState.PacketOwnedScrollCountObserved
                        ? _pendingVegaCastState.PacketOwnedScrollFinalCount
                        : null,
                    _pendingVegaCastState.ModifierInventoryType,
                    _pendingVegaCastState.ModifierSlotIndex,
                    _pendingVegaCastState.PacketOwnedModifierCountObserved
                        ? _pendingVegaCastState.PacketOwnedModifierFinalCount
                        : null,
                    mutationSuccess);
            }
            else
            {
                result = itemUpgradeWindow.TryApplyPreparedUpgradeAtSlots(
                    _pendingVegaCastState.ScrollInventoryType,
                    _pendingVegaCastState.ScrollSlotIndex,
                    _pendingVegaCastState.ModifierInventoryType,
                    _pendingVegaCastState.ModifierSlotIndex,
                    forcedSuccess: mutationSuccess);
            }
            if (!result.Success.HasValue)
            {
                message = VegaOwnerStringPoolText.GetUnexpectedResultNotice();
                return false;
            }

            if (_pendingVegaCastState.PacketOwnedUpgradeStateObserved)
            {
                itemUpgradeWindow.ApplyPacketOwnedUpgradeSlotState(
                    _pendingVegaCastState.Request.Slot,
                    _pendingVegaCastState.PacketOwnedUpgradeState);
            }

            if (_pendingVegaCastState.PacketOwnedEquipItemTokenObserved)
            {
                ApplyPacketOwnedVegaEquipItemTokenState(
                    _pendingVegaCastState.Request.Slot,
                    _pendingVegaCastState.Request.EquipItemId,
                    _pendingVegaCastState.PacketOwnedEquipItemToken);
            }

            if (_pendingVegaCastState.PacketOwnedEquipSnapshotObserved)
            {
                ApplyPacketOwnedVegaEquipSnapshotState(
                    _pendingVegaCastState.Request.Slot,
                    _pendingVegaCastState.Request.EquipItemId,
                    _pendingVegaCastState.PacketOwnedEquipSnapshot);
            }

            return true;
        }

        private void ApplyPacketOwnedVegaEquipItemTokenState(EquipSlot slot, int itemId, int itemToken)
        {
            if (slot == EquipSlot.None || itemId <= 0 || itemToken == 0)
            {
                return;
            }

            if (_playerManager?.Player?.Build?.Equipment == null ||
                !_playerManager.Player.Build.Equipment.TryGetValue(slot, out CharacterPart equippedPart) ||
                equippedPart == null ||
                equippedPart.ItemId != itemId)
            {
                return;
            }

            equippedPart.ClientItemToken = itemToken;
            RememberObservedVegaEquipItemToken(slot, itemId, itemToken, isClientAuthored: true);
        }

        private void ApplyPacketOwnedVegaEquipSnapshotState(
            EquipSlot slot,
            int expectedItemId,
            VegaPacketOwnedEquipSnapshot snapshot)
        {
            if (_playerManager?.Player?.Build?.Equipment == null ||
                !_playerManager.Player.Build.Equipment.TryGetValue(slot, out CharacterPart equippedPart) ||
                equippedPart == null ||
                !TryApplyPacketOwnedVegaEquipSnapshotState(equippedPart, expectedItemId, snapshot))
            {
                return;
            }

            RememberObservedVegaEquipItemToken(slot, expectedItemId, snapshot.EquipItemToken, isClientAuthored: true);
        }

        private static bool TryApplyPacketOwnedVegaEquipSnapshotState(
            CharacterPart equippedPart,
            int expectedItemId,
            VegaPacketOwnedEquipSnapshot snapshot)
        {
            if (equippedPart == null ||
                expectedItemId <= 0 ||
                snapshot.ItemId != expectedItemId ||
                equippedPart.ItemId != expectedItemId)
            {
                return false;
            }

            if (snapshot.EquipItemToken != 0)
            {
                equippedPart.ClientItemToken = snapshot.EquipItemToken;
            }

            equippedPart.TotalUpgradeSlotCount = Math.Max(0, snapshot.TotalSlots);
            equippedPart.RemainingUpgradeSlotCount = Math.Clamp(snapshot.RemainingSlots, 0, Math.Max(0, snapshot.TotalSlots));
            equippedPart.UpgradeSlots = equippedPart.RemainingUpgradeSlotCount.Value;
            equippedPart.EnhancementStarCount = Math.Max(0, snapshot.SuccessCount);
            equippedPart.BonusSTR = snapshot.BonusSTR;
            equippedPart.BonusDEX = snapshot.BonusDEX;
            equippedPart.BonusINT = snapshot.BonusINT;
            equippedPart.BonusLUK = snapshot.BonusLUK;
            equippedPart.BonusHP = snapshot.BonusHP;
            equippedPart.BonusMP = snapshot.BonusMP;
            equippedPart.BonusWeaponAttack = snapshot.BonusWeaponAttack;
            equippedPart.BonusMagicAttack = snapshot.BonusMagicAttack;
            equippedPart.BonusWeaponDefense = snapshot.BonusWeaponDefense;
            equippedPart.BonusMagicDefense = snapshot.BonusMagicDefense;
            equippedPart.BonusAccuracy = snapshot.BonusAccuracy;
            equippedPart.BonusAvoidability = snapshot.BonusAvoidability;
            equippedPart.BonusSpeed = snapshot.BonusSpeed;
            equippedPart.BonusJump = snapshot.BonusJump;
            return true;
        }

        private static bool TryDecodeVegaClientInventoryOperationState(
            byte[] payload,
            int expectedItemId,
            int expectedEncodedEquipPosition,
            int expectedScrollItemId,
            InventoryType expectedScrollInventoryType,
            int expectedScrollSlotIndex,
            int expectedModifierItemId,
            InventoryType expectedModifierInventoryType,
            int expectedModifierSlotIndex,
            out VegaClientInventoryOperationState operationState,
            out string rejectReason)
        {
            operationState = default;
            rejectReason = null;
            if (payload == null || payload.Length < sizeof(byte) * 2)
            {
                rejectReason = "Inventory-operation payload is missing the exclusive-reset and operation-count bytes.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                bool clearsExclusiveRequest = reader.ReadByte() != 0;
                int operationCount = reader.ReadByte();
                if (operationCount <= 0)
                {
                    rejectReason = "Inventory-operation payload did not include any Vega equipment operation.";
                    return false;
                }

                for (int i = 0; i < operationCount; i++)
                {
                    if (!TryEnsureVegaInventoryOperationRemaining(stream, sizeof(byte) * 2 + sizeof(short), out rejectReason))
                    {
                        return false;
                    }

                    byte operationMode = reader.ReadByte();
                    byte inventoryType = reader.ReadByte();
                    short fromPosition = reader.ReadInt16();
                    switch (operationMode)
                    {
                        case 0:
                            if (TryReadVegaClientInventoryOperationEquipAddEntry(
                                    reader,
                                    inventoryType,
                                    fromPosition,
                                    expectedItemId,
                                    expectedEncodedEquipPosition,
                                    out VegaPacketOwnedEquipSnapshot decodedSnapshot,
                                    out rejectReason))
                            {
                                operationState = operationState with
                                {
                                    ClearsExclusiveRequest = clearsExclusiveRequest,
                                    HasEquipSnapshot = true,
                                    EquipSnapshot = decodedSnapshot
                                };
                                break;
                            }

                            if (!string.IsNullOrWhiteSpace(rejectReason))
                            {
                                return false;
                            }

                            rejectReason = "Inventory-operation payload included a mode-0 entry before the matching Vega equipment add entry could be decoded.";
                            return false;
                        case 1:
                            if (!TryEnsureVegaInventoryOperationRemaining(stream, sizeof(short), out rejectReason))
                            {
                                return false;
                            }

                            int newCount = Math.Max(0, (int)reader.ReadInt16());
                            operationState = ObserveVegaClientInventoryOperationCount(
                                operationState,
                                inventoryType,
                                fromPosition,
                                newCount,
                                expectedScrollItemId,
                                expectedScrollInventoryType,
                                expectedScrollSlotIndex,
                                expectedModifierItemId,
                                expectedModifierInventoryType,
                                expectedModifierSlotIndex);
                            break;
                        case 2:
                            if (!TryEnsureVegaInventoryOperationRemaining(stream, sizeof(short), out rejectReason))
                            {
                                return false;
                            }

                            _ = reader.ReadInt16();
                            break;
                        case 3:
                            operationState = ObserveVegaClientInventoryOperationCount(
                                operationState,
                                inventoryType,
                                fromPosition,
                                0,
                                expectedScrollItemId,
                                expectedScrollInventoryType,
                                expectedScrollSlotIndex,
                                expectedModifierItemId,
                                expectedModifierInventoryType,
                                expectedModifierSlotIndex);
                            break;
                        case 4:
                            if (!TryEnsureVegaInventoryOperationRemaining(stream, sizeof(int), out rejectReason))
                            {
                                return false;
                            }

                            _ = reader.ReadInt32();
                            break;
                        default:
                            // CWvsContext::OnInventoryOperation falls through unknown modes after
                            // consuming the shared mode/type/position header, so keep scanning for
                            // the Vega-owned equip add entry instead of rejecting the packet.
                            break;
                    }
                }

                if (!operationState.HasEquipSnapshot)
                {
                    rejectReason = "Inventory-operation payload did not include a matching Vega GW_ItemSlotEquip add entry.";
                    return false;
                }

                operationState = operationState with { ClearsExclusiveRequest = clearsExclusiveRequest };
                return true;
            }
            catch (Exception ex)
            {
                rejectReason = $"Inventory-operation payload could not be decoded for Vega equipment state: {ex.Message}";
                return false;
            }
        }

        private static VegaClientInventoryOperationState ObserveVegaClientInventoryOperationCount(
            VegaClientInventoryOperationState operationState,
            byte inventoryType,
            short position,
            int finalCount,
            int expectedScrollItemId,
            InventoryType expectedScrollInventoryType,
            int expectedScrollSlotIndex,
            int expectedModifierItemId,
            InventoryType expectedModifierInventoryType,
            int expectedModifierSlotIndex)
        {
            if (MatchesVegaClientInventoryOperationSlot(
                    inventoryType,
                    position,
                    expectedScrollItemId,
                    expectedScrollInventoryType,
                    expectedScrollSlotIndex))
            {
                operationState = operationState with
                {
                    ScrollCountObserved = true,
                    ScrollFinalCount = finalCount
                };
            }

            if (MatchesVegaClientInventoryOperationSlot(
                    inventoryType,
                    position,
                    expectedModifierItemId,
                    expectedModifierInventoryType,
                    expectedModifierSlotIndex))
            {
                operationState = operationState with
                {
                    ModifierCountObserved = true,
                    ModifierFinalCount = finalCount
                };
            }

            return operationState;
        }

        private static bool MatchesVegaClientInventoryOperationSlot(
            byte inventoryType,
            short position,
            int expectedItemId,
            InventoryType expectedInventoryType,
            int expectedSlotIndex)
        {
            if (expectedItemId <= 0 ||
                expectedInventoryType == InventoryType.NONE ||
                expectedSlotIndex < 0 ||
                !Enum.IsDefined(typeof(InventoryType), (int)inventoryType) ||
                (InventoryType)inventoryType != expectedInventoryType)
            {
                return false;
            }

            return position == expectedSlotIndex + 1;
        }

        private static bool TryReadVegaClientInventoryOperationEquipAddEntry(
            BinaryReader reader,
            byte inventoryType,
            short targetPosition,
            int expectedItemId,
            int expectedEncodedEquipPosition,
            out VegaPacketOwnedEquipSnapshot snapshot,
            out string rejectReason)
        {
            snapshot = default;
            rejectReason = null;
            if (inventoryType != VegaClientInventoryOperationEquipType &&
                inventoryType != VegaClientInventoryOperationCashType)
            {
                return false;
            }

            if (targetPosition != expectedEncodedEquipPosition)
            {
                return false;
            }

            if (!TryEnsureVegaInventoryOperationRemaining(reader.BaseStream, sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long), out rejectReason))
            {
                return false;
            }

            byte slotType = reader.ReadByte();
            int itemId = reader.ReadInt32();
            bool hasCashSerial = reader.ReadByte() != 0;
            long cashItemSerialNumber = 0;
            if (hasCashSerial)
            {
                if (!TryEnsureVegaInventoryOperationRemaining(reader.BaseStream, sizeof(long), out rejectReason))
                {
                    return false;
                }

                cashItemSerialNumber = reader.ReadInt64();
            }

            long itemTokenSource = 0;
            if (!TryEnsureVegaInventoryOperationRemaining(reader.BaseStream, sizeof(long), out rejectReason))
            {
                return false;
            }

            itemTokenSource = reader.ReadInt64();
            if (slotType != VegaClientInventoryOperationSlotTypeEquip || itemId != expectedItemId)
            {
                rejectReason = $"Inventory-operation Vega add entry did not match the pending equip item; slot type {slotType}, item {itemId}.";
                return false;
            }

            long bodyStart = reader.BaseStream?.CanSeek == true ? reader.BaseStream.Position : -1;
            if (TryReadVegaClientEquipSnapshotBody(
                    reader,
                    hasCashSerial,
                    itemId,
                    cashItemSerialNumber,
                    itemTokenSource,
                    VegaClientEquipSnapshotStatFieldCount,
                    out snapshot,
                    out rejectReason))
            {
                return true;
            }

            if (bodyStart >= 0)
            {
                reader.BaseStream.Position = bodyStart;
                return TryReadVegaClientEquipSnapshotBody(
                    reader,
                    hasCashSerial,
                    itemId,
                    cashItemSerialNumber,
                    itemTokenSource,
                    VegaClientEquipSnapshotLegacyStatFieldCount,
                    out snapshot,
                    out rejectReason);
            }

            return false;
        }

        private static bool TryReadVegaClientEquipSnapshotBody(
            BinaryReader reader,
            bool hasCashSerial,
            int itemId,
            long cashItemSerialNumber,
            long itemTokenSource,
            int statFieldCount,
            out VegaPacketOwnedEquipSnapshot snapshot,
            out string rejectReason)
        {
            snapshot = default;
            rejectReason = null;
            if (!TryEnsureVegaInventoryOperationRemaining(
                    reader.BaseStream,
                    sizeof(byte) * 2 + (sizeof(short) * statFieldCount),
                    out rejectReason))
            {
                return false;
            }

            int remainingSlots = reader.ReadByte();
            int successCount = reader.ReadByte();
            Span<short> stats = stackalloc short[VegaClientEquipSnapshotStatFieldCount];
            for (int i = 0; i < statFieldCount; i++)
            {
                stats[i] = reader.ReadInt16();
            }

            if (!TryReadVegaClientMapleString(reader, out rejectReason))
            {
                return false;
            }

            const int equipTailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (!TryEnsureVegaInventoryOperationRemaining(
                    reader.BaseStream,
                    equipTailLength + (hasCashSerial ? 0 : sizeof(long)) + sizeof(long) + sizeof(int),
                    out rejectReason))
            {
                return false;
            }

            _ = reader.ReadInt16();
            _ = reader.ReadByte();
            _ = reader.ReadByte();
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadByte();
            _ = reader.ReadByte();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            if (!hasCashSerial)
            {
                _ = reader.ReadInt64();
            }

            _ = reader.ReadInt64();
            _ = reader.ReadInt32();

            int equipItemToken = 0;
            if (itemTokenSource != 0)
            {
                equipItemToken = FoldVegaSerialNumberToInt(itemTokenSource);
            }
            else if (cashItemSerialNumber != 0)
            {
                equipItemToken = FoldVegaSerialNumberToInt(cashItemSerialNumber);
            }

            snapshot = new VegaPacketOwnedEquipSnapshot(
                equipItemToken,
                itemId,
                Math.Max(0, remainingSlots + successCount),
                Math.Max(0, remainingSlots),
                Math.Max(0, successCount),
                stats[0],
                stats[1],
                stats[2],
                stats[3],
                stats[4],
                stats[5],
                stats[6],
                stats[7],
                stats[8],
                stats[9],
                stats[10],
                stats[11],
                stats[13],
                stats[14]);
            return true;
        }

        private static bool TryReadVegaClientMapleString(BinaryReader reader, out string rejectReason)
        {
            rejectReason = null;
            if (!TryEnsureVegaInventoryOperationRemaining(reader.BaseStream, sizeof(short), out rejectReason))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0)
            {
                rejectReason = "Inventory-operation equip title length was negative.";
                return false;
            }

            if (!TryEnsureVegaInventoryOperationRemaining(reader.BaseStream, length, out rejectReason))
            {
                return false;
            }

            _ = reader.ReadBytes(length);
            return true;
        }

        private static bool TryEnsureVegaInventoryOperationRemaining(Stream stream, int byteCount, out string rejectReason)
        {
            rejectReason = null;
            if (stream == null || !stream.CanSeek)
            {
                rejectReason = "Inventory-operation stream is unavailable.";
                return false;
            }

            if (byteCount < 0 || stream.Length - stream.Position < byteCount)
            {
                rejectReason = "Inventory-operation payload ended before the Vega equipment state could be decoded.";
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

            string descriptor = ResolveVegaResultLoopSoundClientPlaybackDescriptor();
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                descriptor = ResolveVegaResultLoopSoundDescriptor();
            }

            if (!TryResolvePacketOwnedWzSound(
                    descriptor,
                    VegaResultPreludeLoopSoundOwnerImage,
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
                        VegaResultPreludeLoopSoundOwnerImage,
                        out soundProperty,
                        out resolvedDescriptor,
                        false))
                {
                    return;
                }
            }

            string soundKey = BuildVegaResultLoopSoundKey(resolvedDescriptor, soundProperty);
            if (_soundManager.TryPlayClientSoundEffect(
                    soundKey,
                    soundProperty,
                    startVolumeScale: 1f,
                    loop: true,
                    suppressWhileActive: false,
                    out _,
                    out _))
            {
                _vegaResultLoopSoundActive = true;
                _vegaResultLoopSoundInstanceKey = soundKey;
            }
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
                VegaOwnerStringPoolText.GetResultLoopSoundFallbackDescriptor());
        }

        private static string ResolveVegaResultLoopSoundClientPlaybackDescriptor()
        {
            return NormalizeVegaResultLoopSoundDescriptor(
                BuildVegaPlayUiSoundLoopDescriptor(
                    VegaOwnerStringPoolText.GetResultLoopSoundAliasDescriptor()),
                VegaResultPreludeLoopSoundFallback);
        }

        internal static string BuildVegaPlayUiSoundLoopDescriptorForTests(string soundName)
        {
            return BuildVegaPlayUiSoundLoopDescriptor(soundName);
        }

        internal static string ResolveVegaResultLoopSoundClientPlaybackDescriptorForTests()
        {
            return ResolveVegaResultLoopSoundClientPlaybackDescriptor();
        }

        private static string BuildVegaPlayUiSoundLoopDescriptor(string soundName)
        {
            string normalizedSoundName = NormalizePacketOwnedClientSoundDescriptor(soundName);
            if (string.IsNullOrWhiteSpace(normalizedSoundName))
            {
                return string.Empty;
            }

            string prefix = MapleStoryStringPool.GetOrFallback(
                VegaResultPreludeLoopSoundPrefixStringPoolId,
                "Sound/UI.img/");
            string normalizedPrefix = NormalizePacketOwnedClientSoundDescriptor(prefix);
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                return normalizedSoundName;
            }

            return NormalizePacketOwnedClientSoundDescriptor(
                $"{normalizedPrefix.TrimEnd('/')}/{normalizedSoundName.TrimStart('/')}");
        }

        internal static string BuildVegaResultLoopSoundKeyForTests(string resolvedDescriptor)
        {
            return BuildVegaResultLoopSoundKey(resolvedDescriptor, soundProperty: null);
        }

        private static string BuildVegaResultLoopSoundKey(
            string resolvedDescriptor,
            WzBinaryProperty soundProperty)
        {
            string normalizedDescriptor = NormalizeVegaResultLoopSoundDescriptor(
                resolvedDescriptor,
                VegaResultPreludeLoopSoundFallback);
            return SoundManager.BuildPacketOwnedClientSoundKey(normalizedDescriptor, soundProperty);
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

        private readonly record struct VegaResultDecodeState(
            byte ResultCode,
            bool HasEquipItemToken,
            int EquipItemToken,
            bool HasOutcomeState,
            int OutcomeResultValue,
            int OutcomeUpgradeState,
            bool HasEquipSnapshot,
            VegaPacketOwnedEquipSnapshot EquipSnapshot);

        private readonly record struct VegaPacketOwnedEquipSnapshot(
            int EquipItemToken,
            int ItemId,
            int TotalSlots,
            int RemainingSlots,
            int SuccessCount,
            int BonusSTR,
            int BonusDEX,
            int BonusINT,
            int BonusLUK,
            int BonusHP,
            int BonusMP,
            int BonusWeaponAttack,
            int BonusMagicAttack,
            int BonusWeaponDefense,
            int BonusMagicDefense,
            int BonusAccuracy,
            int BonusAvoidability,
            int BonusSpeed,
            int BonusJump);

        private readonly record struct VegaClientInventoryOperationState(
            bool ClearsExclusiveRequest,
            bool HasEquipSnapshot,
            VegaPacketOwnedEquipSnapshot EquipSnapshot,
            bool ScrollCountObserved,
            int ScrollFinalCount,
            bool ModifierCountObserved,
            int ModifierFinalCount);

        private static bool TryDecodeVegaResultPayload(byte[] payload, out byte resultCode)
        {
            bool decoded = TryDecodeVegaResultPayloadState(payload, out VegaResultDecodeState decodeState, out _);
            resultCode = decodeState.ResultCode;
            return decoded;
        }

        private static bool TryDecodeVegaResultPayloadState(
            byte[] payload,
            out VegaResultDecodeState decodeState,
            out string decodeError)
        {
            decodeState = default;
            decodeError = null;
            if (payload == null || payload.Length == 0)
            {
                decodeError = "Vega result payload must contain the leading result byte from CUIVega::OnVegaResult.";
                return false;
            }

            byte resultCode = payload[0];
            int outcomePayloadLength = sizeof(byte) + (sizeof(int) * 2);
            int equipTokenAndOutcomePayloadLength = sizeof(byte) + (sizeof(int) * 3);
            int equipSnapshotPayloadLength = equipTokenAndOutcomePayloadLength + sizeof(int) + (sizeof(int) * VegaPacketOwnedEquipSnapshotIntCount);
            if (payload.Length == equipSnapshotPayloadLength)
            {
                int equipItemToken = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte), sizeof(int)));
                int outcomeResultValue = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte) + sizeof(int), sizeof(int)));
                int outcomeUpgradeState = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte) + (sizeof(int) * 2), sizeof(int)));
                int marker = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(equipTokenAndOutcomePayloadLength, sizeof(int)));
                if (marker != VegaPacketOwnedEquipSnapshotMarker)
                {
                    decodeState = new VegaResultDecodeState(
                        resultCode,
                        HasEquipItemToken: equipItemToken != 0,
                        EquipItemToken: equipItemToken,
                        HasOutcomeState: true,
                        outcomeResultValue,
                        outcomeUpgradeState,
                        HasEquipSnapshot: false,
                        EquipSnapshot: default);
                    decodeError = $"Packet-owned Vega result code {resultCode} contains an equip-state tail with an unexpected marker.";
                    return false;
                }

                VegaPacketOwnedEquipSnapshot snapshot = DecodeVegaPacketOwnedEquipSnapshot(
                    payload.AsSpan(equipTokenAndOutcomePayloadLength + sizeof(int), sizeof(int) * VegaPacketOwnedEquipSnapshotIntCount));
                decodeState = new VegaResultDecodeState(
                    resultCode,
                    HasEquipItemToken: equipItemToken != 0,
                    EquipItemToken: equipItemToken,
                    HasOutcomeState: true,
                    outcomeResultValue,
                    outcomeUpgradeState,
                    HasEquipSnapshot: true,
                    snapshot);
                return true;
            }

            if (payload.Length == equipTokenAndOutcomePayloadLength)
            {
                int equipItemToken = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte), sizeof(int)));
                int outcomeResultValue = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte) + sizeof(int), sizeof(int)));
                int outcomeUpgradeState = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte) + (sizeof(int) * 2), sizeof(int)));
                decodeState = new VegaResultDecodeState(
                    resultCode,
                    HasEquipItemToken: equipItemToken != 0,
                    EquipItemToken: equipItemToken,
                    HasOutcomeState: true,
                    outcomeResultValue,
                    outcomeUpgradeState,
                    HasEquipSnapshot: false,
                    EquipSnapshot: default);
                return true;
            }

            if (payload.Length == outcomePayloadLength)
            {
                int outcomeResultValue = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte), sizeof(int)));
                int outcomeUpgradeState = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(sizeof(byte) + sizeof(int), sizeof(int)));
                decodeState = new VegaResultDecodeState(
                    resultCode,
                    HasEquipItemToken: false,
                    EquipItemToken: 0,
                    HasOutcomeState: true,
                    outcomeResultValue,
                    outcomeUpgradeState,
                    HasEquipSnapshot: false,
                    EquipSnapshot: default);
                return true;
            }

            if (payload.Length != sizeof(byte))
            {
                decodeState = new VegaResultDecodeState(
                    resultCode,
                    HasEquipItemToken: false,
                    EquipItemToken: 0,
                    HasOutcomeState: false,
                    OutcomeResultValue: 0,
                    OutcomeUpgradeState: int.MinValue,
                    HasEquipSnapshot: false,
                    EquipSnapshot: default);
                decodeError = payload.Length > equipTokenAndOutcomePayloadLength
                    ? $"Packet-owned Vega result code {resultCode} contains unexpected trailing bytes after the optional outcome-state payload fields."
                    : $"Packet-owned Vega result code {resultCode} must be either the result byte alone, result plus outcome-state payload fields, result plus equip-token and outcome-state payload fields, or result plus packet-authored equip-state snapshot fields.";
                return false;
            }

            decodeState = new VegaResultDecodeState(
                resultCode,
                HasEquipItemToken: false,
                EquipItemToken: 0,
                HasOutcomeState: false,
                OutcomeResultValue: 0,
                OutcomeUpgradeState: int.MinValue,
                HasEquipSnapshot: false,
                EquipSnapshot: default);
            return true;
        }

        private static VegaPacketOwnedEquipSnapshot DecodeVegaPacketOwnedEquipSnapshot(ReadOnlySpan<byte> payload)
        {
            Span<int> values = stackalloc int[VegaPacketOwnedEquipSnapshotIntCount];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.Slice(i * sizeof(int), sizeof(int)));
            }

            return new VegaPacketOwnedEquipSnapshot(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9],
                values[10],
                values[11],
                values[12],
                values[13],
                values[14],
                values[15],
                values[16],
                values[17],
                values[18]);
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

        private static bool DoesVegaTerminalMatchPrelude(bool preludeSuccess, byte terminalResultCode)
        {
            return preludeSuccess
                ? terminalResultCode == VegaPacketOwnedSuccessTerminalCode
                : terminalResultCode == VegaPacketOwnedFailTerminalCode;
        }

        private static bool TryResolveVegaTerminalCompletionSuccess(
            bool? preludeSuccess,
            byte terminalResultCode,
            out bool success,
            out bool terminalMatchesPrelude)
        {
            success = false;
            terminalMatchesPrelude = false;
            if (!TryMapPacketOwnedVegaTerminalCode(terminalResultCode, out bool terminalSuccess))
            {
                return false;
            }

            if (!preludeSuccess.HasValue)
            {
                success = terminalSuccess;
                terminalMatchesPrelude = true;
                return true;
            }

            success = preludeSuccess.Value;
            terminalMatchesPrelude = DoesVegaTerminalMatchPrelude(preludeSuccess.Value, terminalResultCode);
            return true;
        }

        internal static bool DoesVegaTerminalMatchPreludeForTests(bool preludeSuccess, byte terminalResultCode)
        {
            return DoesVegaTerminalMatchPrelude(preludeSuccess, terminalResultCode);
        }

        internal static bool TryResolveVegaTerminalCompletionSuccessForTests(
            bool? preludeSuccess,
            byte terminalResultCode,
            out bool success,
            out bool terminalMatchesPrelude)
        {
            return TryResolveVegaTerminalCompletionSuccess(
                preludeSuccess,
                terminalResultCode,
                out success,
                out terminalMatchesPrelude);
        }

        internal static bool TryResolvePacketOwnedVegaOutcomeSuccessForTests(int outcomeResultValue, out bool success)
        {
            return TryResolvePacketOwnedVegaOutcomeSuccess(outcomeResultValue, out success);
        }

        internal static bool ResolvePacketOwnedVegaMutationSuccessForTests(
            bool resultCodeSuccess,
            bool outcomeSuccessObserved,
            bool outcomeSuccess)
        {
            return ResolvePacketOwnedVegaMutationSuccess(
                resultCodeSuccess,
                outcomeSuccessObserved,
                outcomeSuccess);
        }

        private static bool ResolvePacketOwnedVegaMutationSuccess(
            bool resultCodeSuccess,
            bool outcomeSuccessObserved,
            bool outcomeSuccess)
        {
            return outcomeSuccessObserved ? outcomeSuccess : resultCodeSuccess;
        }

        private static bool TryResolvePacketOwnedVegaOutcomeSuccess(int outcomeResultValue, out bool success)
        {
            if (outcomeResultValue == 0)
            {
                success = false;
                return true;
            }

            if (outcomeResultValue == 1)
            {
                success = true;
                return true;
            }

            success = false;
            return false;
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
