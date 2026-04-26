using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using System;
using System.Buffers.Binary;
using System.Globalization;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const short ItemUpgradeOwnerRequestOpcode = 0x55;
        private const int ItemUpgradeOwnerResultFallbackDelayMs = 750;
        private const int ItemUpgradeOwnerResultApplyDelayMs = 50;
        private const int ItemUpgradeOwnerExternalResultFallbackDelayMs = 3000;
        private const int ItemUpgradeOwnerExclusiveRequestCooldownMs = 500;
        private const int ItemUpgradeOwnerResultAckViciousHammerDelayMs = 1000;
        private const int ItemUpgradeOwnerRequestPayloadLength = sizeof(int) * 3;
        private const int ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength = sizeof(int) + sizeof(short) + sizeof(int);
        private const int ItemUpgradeOwnerConsumeCashRequestPayloadLength = ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength + ItemUpgradeOwnerRequestPayloadLength;
        private const int ItemUpgradeOwnerResultAckPayloadLength = sizeof(int) * 2;
        private const int ItemUpgradeResultReasonPayloadLength = sizeof(byte) + sizeof(int);
        private const int ItemUpgradeResultOutcomePayloadLength = sizeof(byte) + (sizeof(int) * 2);
        private const byte ItemUpgradePacketResultCodeFail = 0;
        private const byte ItemUpgradePacketResultCodeSuccess = 1;
        private const byte ItemUpgradePacketResultCodeClientNoUpgradeSlot = 65;
        private const byte ItemUpgradePacketResultCodeClientRejected = 66;
        private const byte ItemUpgradePacketResultCodeViciousHammer = 61;
        private const int ItemUpgradePacketOutcomeStateFail = 0;
        private const int ItemUpgradePacketOutcomeStateSuccess = 1;
        private const short ItemUpgradeOwnerResultAckOpcode = 296;
        private const int ItemUpgradeClientDuplicateRequestBusyResultValue = 9;
        private const int ItemUpgradeClientInitialResultValue = -2;

        private bool _itemUpgradeOwnerRequestSent;
        private int _itemUpgradeOwnerRequestSentTick = int.MinValue;
        private int _itemUpgradeOwnerLastResultValue = ItemUpgradeClientInitialResultValue;
        private int _itemUpgradeOwnerLastUpgradeStateValue = int.MinValue;
        private int _itemUpgradeOwnerConsumeCashUseRequestTick = int.MinValue;
        private InventoryType _itemUpgradeOwnerConsumeCashUseInventoryType = InventoryType.USE;
        private int _itemUpgradeOwnerConsumeCashUseSlotIndex = -1;
        private int _itemUpgradeOwnerConsumeCashUseItemId;
        private int _itemUpgradeOwnerPendingResultAckReturnCode;
        private int _itemUpgradeOwnerPendingResultAckValue;
        private int _itemUpgradeOwnerPendingResultAckReadyTick = int.MinValue;
        private PendingItemUpgradeOwnerRequestState _pendingItemUpgradeOwnerRequest;

        private sealed class PendingItemUpgradeOwnerRequestState
        {
            public ItemUpgradeUI.ItemUpgradeOwnerRequest Request { get; init; }
            public int RequestedAtTick { get; init; }
            public int ResultReadyAtTick { get; set; }
            public bool? ForcedSuccess { get; set; }
            public bool SuppressUpgradeApply { get; set; }
            public string PacketOwnedStatusMessage { get; set; }
            public string PacketOwnedApplyStatusMessageOverride { get; set; }
            public bool PacketOwnedResultObserved { get; set; }
            public byte? PacketOwnedResultCode { get; set; }
            public byte[] EncodedRequestPayload { get; init; } = Array.Empty<byte>();
            public int RequestItemToken { get; init; }
            public int RequestSlotPosition { get; init; }
        }

        private readonly record struct ItemUpgradeResultDecodeState(
            byte ResultCode,
            bool HasReasonCode,
            int ReasonCode,
            bool HasOutcomeState,
            int OutcomeResultValue,
            int OutcomeUpgradeState);

        private void WireItemUpgradeOwnerCallbacks(ItemUpgradeUI itemUpgradeWindow)
        {
            if (itemUpgradeWindow == null)
            {
                return;
            }

            itemUpgradeWindow.StartUpgradeRequested = HandleItemUpgradeOwnerStartRequested;
        }

        private bool HandleItemUpgradeOwnerStartRequested(ItemUpgradeUI.ItemUpgradeOwnerRequest request)
        {
            if (IsItemUpgradeOwnerDuplicateSendBlocked(
                    _itemUpgradeOwnerRequestSent,
                    IsItemUpgradeOwnerRequestAwaitingPacketResult(_pendingItemUpgradeOwnerRequest)))
            {
                string busyNotice = ResolveItemUpgradeBusyNotice(ItemUpgradeClientDuplicateRequestBusyResultValue);
                ShowUtilityFeedbackMessage(busyNotice);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                {
                    itemUpgradeWindow.SetOwnerStatusMessage(busyNotice, success: false);
                }

                return true;
            }

            if (HasActiveItemUpgradeOwnerRequestBlock(currTickCount))
            {
                string blockedNotice = ResolveItemUpgradeBlockedStateNotice();
                ShowUtilityFeedbackMessage(blockedNotice);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                {
                    itemUpgradeWindow.SetOwnerStatusMessage(blockedNotice, success: false);
                }

                return true;
            }

            if (_pendingEquipmentChangeRequests.Count > 0 || _gameState.PendingMapChange || _playerManager?.Player?.Build == null)
            {
                string blockedNotice = ResolveItemUpgradeBlockedStateNotice();
                ShowUtilityFeedbackMessage(blockedNotice);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                {
                    itemUpgradeWindow.SetOwnerStatusMessage(blockedNotice, success: false);
                }

                return true;
            }

            ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);

            int requestItemToken = ResolveItemUpgradeRequestItemToken(request);
            int requestSlotPosition = Math.Max(0, request.ConsumableSlotIndex + 1);
            bool hasMatchedConsumeCashSeed = TryConsumeItemUpgradeConsumeCashUseRequestTick(
                request,
                currTickCount,
                out int consumeCashUseRequestTick);
            byte[] encodedRequestPayload = BuildItemUpgradeRequestPayload(
                requestItemToken,
                requestSlotPosition,
                currTickCount);
            byte[] encodedConsumeCashRequestPayload = BuildItemUpgradeConsumeCashRequestPayload(
                consumeCashUseRequestTick,
                requestSlotPosition,
                request.ConsumableItemId,
                requestItemToken,
                currTickCount);
            bool useConsumeCashRequestPayload = ShouldUseConsumeCashItemUseRequestPayload(
                request.ConsumableInventoryType,
                hasMatchedConsumeCashSeed);
            byte[] outboundPayload = useConsumeCashRequestPayload
                ? encodedConsumeCashRequestPayload
                : encodedRequestPayload;
            string requestDispatchSummary = BuildItemUpgradeOutboundRequestDispatchLabel(
                outboundPayload,
                useConsumeCashRequestPayload,
                out int responseDelayMs);

            _pendingItemUpgradeOwnerRequest = new PendingItemUpgradeOwnerRequestState
            {
                Request = request,
                RequestedAtTick = currTickCount,
                ResultReadyAtTick = currTickCount + responseDelayMs,
                ForcedSuccess = null,
                EncodedRequestPayload = encodedRequestPayload,
                RequestItemToken = requestItemToken,
                RequestSlotPosition = requestSlotPosition
            };
            MarkItemUpgradeOwnerRequestSent();
            StampPacketOwnedUtilityRequestState();

            string payloadHex = Convert.ToHexString(encodedRequestPayload);
            string statusMessage = $"Enhancement request sent for {request.EquipName} with {request.ConsumableName}.";
            ShowUtilityFeedbackMessage(statusMessage);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindowVisible)
            {
                itemUpgradeWindowVisible.SetPacketOwnedRequestPending(
                    $"{statusMessage} Waiting for packet-owned result. Encoded request body (itemTI, slot, tick): {payloadHex}. {requestDispatchSummary}");
            }

            return true;
        }

        private void UpdateItemUpgradeOwnerState()
        {
            TryDispatchPendingItemUpgradeResultAck(currTickCount);

            if (_pendingItemUpgradeOwnerRequest == null ||
                unchecked(currTickCount - _pendingItemUpgradeOwnerRequest.ResultReadyAtTick) < 0)
            {
                return;
            }

            TryCompletePendingItemUpgradeOwnerRequest();
        }

        private bool TryCompletePendingItemUpgradeOwnerRequest()
        {
            if (_pendingItemUpgradeOwnerRequest == null ||
                uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is not ItemUpgradeUI itemUpgradeWindow)
            {
                return false;
            }

            PendingItemUpgradeOwnerRequestState pendingRequest = _pendingItemUpgradeOwnerRequest;
            _pendingItemUpgradeOwnerRequest = null;
            itemUpgradeWindow.PrepareEquipmentSelection(pendingRequest.Request.Slot);
            itemUpgradeWindow.PrepareConsumableSelection(pendingRequest.Request.ConsumableItemId);

            string statusMessage;
            bool? success;
            if (pendingRequest.SuppressUpgradeApply)
            {
                statusMessage = string.IsNullOrWhiteSpace(pendingRequest.PacketOwnedStatusMessage)
                    ? ResolveItemUpgradeBlockedStateNotice()
                    : pendingRequest.PacketOwnedStatusMessage;
                success = false;
                itemUpgradeWindow.SetOwnerStatusMessage(statusMessage, success);
            }
            else
            {
                ItemUpgradeUI.ItemUpgradeAttemptResult result =
                    itemUpgradeWindow.ApplyPacketOwnedPreparedUpgradeResultAtSlots(
                        pendingRequest.Request.ConsumableInventoryType,
                        pendingRequest.Request.ConsumableSlotIndex,
                        pendingRequest.Request.ModifierInventoryType,
                        pendingRequest.Request.ModifierSlotIndex,
                        pendingRequest.ForcedSuccess);
                statusMessage = string.IsNullOrWhiteSpace(pendingRequest.PacketOwnedApplyStatusMessageOverride)
                    ? result.StatusMessage
                    : pendingRequest.PacketOwnedApplyStatusMessageOverride;
                success = result.Success;
                if (!string.IsNullOrWhiteSpace(pendingRequest.PacketOwnedApplyStatusMessageOverride))
                {
                    itemUpgradeWindow.SetOwnerStatusMessage(statusMessage, success);
                }
            }

            ShowUtilityFeedbackMessage(statusMessage);
            if (_itemUpgradeOwnerRequestSent)
            {
                ClearItemUpgradeOwnerRequestState(currTickCount);
            }

            return true;
        }

        private bool TryApplyPacketOwnedItemUpgradeResultPayload(byte[] payload, out string message)
        {
            message = null;

            // CUIItemUpgrade::OnItemUpgradeResult clears request-sent and stamps the
            // exclusive resend timer immediately when the result packet arrives,
            // before any Decode* branch handling.
            ClearItemUpgradeOwnerRequestState(currTickCount);
            bool consumedQuestStartLatch = TryConsumePacketOwnedQuestResultStartQuestLatchFromSharedExclusiveReset();

            if (!TryDecodeItemUpgradeResultPayloadState(payload, out ItemUpgradeResultDecodeState decodeState, out string decodeError))
            {
                message = consumedQuestStartLatch
                    ? $"{decodeError} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch."
                    : decodeError;
                return false;
            }

            if (decodeState.HasOutcomeState)
            {
                _itemUpgradeOwnerLastResultValue = decodeState.OutcomeResultValue;
                _itemUpgradeOwnerLastUpgradeStateValue = decodeState.OutcomeUpgradeState;
                StageItemUpgradeResultAck(decodeState.ResultCode, decodeState.OutcomeResultValue, currTickCount);
            }

            if (_pendingItemUpgradeOwnerRequest == null)
            {
                if (TryResolveItemUpgradePacketOwnedNoticeWithoutPendingRequest(
                        decodeState,
                        out string packetOwnedNoticeWithoutPendingRequest))
                {
                    ShowUtilityFeedbackMessage(packetOwnedNoticeWithoutPendingRequest);
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                    {
                        itemUpgradeWindow.SetOwnerStatusMessage(packetOwnedNoticeWithoutPendingRequest, success: false);
                    }

                    message = $"Applied packet-owned item-upgrade notice result code {decodeState.ResultCode} without a pending request.";
                    if (consumedQuestStartLatch)
                    {
                        message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
                    }

                    return true;
                }

                if (TryResolveItemUpgradePacketOwnedOutcomeWithoutPendingRequest(
                        decodeState,
                        out string packetOwnedOutcomeWithoutPendingRequest,
                        out bool? packetOwnedOutcomeSuccess))
                {
                    ShowUtilityFeedbackMessage(packetOwnedOutcomeWithoutPendingRequest);
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                    {
                        itemUpgradeWindow.SetOwnerStatusMessage(packetOwnedOutcomeWithoutPendingRequest, packetOwnedOutcomeSuccess);
                    }

                    message = $"Applied packet-owned item-upgrade outcome result code {decodeState.ResultCode} without a pending request.";
                    if (consumedQuestStartLatch)
                    {
                        message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
                    }

                    return true;
                }

                message = $"Observed packet-owned item-upgrade result code {decodeState.ResultCode}, but no pending request is waiting for it.";
                if (consumedQuestStartLatch)
                {
                    message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
                }

                return true;
            }

            if (decodeState.ResultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot &&
                decodeState.HasReasonCode &&
                decodeState.ReasonCode == 0)
            {
                int recoverySlotCountArgument = ResolveItemUpgradeRecoveredSlotCountArgument(_pendingItemUpgradeOwnerRequest.Request.Slot);
                _pendingItemUpgradeOwnerRequest.ForcedSuccess = true;
                _pendingItemUpgradeOwnerRequest.SuppressUpgradeApply = false;
                _pendingItemUpgradeOwnerRequest.PacketOwnedStatusMessage = null;
                _pendingItemUpgradeOwnerRequest.PacketOwnedApplyStatusMessageOverride =
                    ResolveItemUpgradeRecoveredSlotNotice(recoverySlotCountArgument);
                _pendingItemUpgradeOwnerRequest.PacketOwnedResultObserved = true;
                _pendingItemUpgradeOwnerRequest.PacketOwnedResultCode = decodeState.ResultCode;
                _pendingItemUpgradeOwnerRequest.ResultReadyAtTick = currTickCount + ResolveItemUpgradeResultReadyDelayMs(
                    decodeState.ResultCode,
                    decodeState.OutcomeResultValue);
                TryCompletePendingItemUpgradeOwnerRequest();
                message = $"Queued packet-owned item-upgrade recovery apply result code {decodeState.ResultCode}.";
                if (consumedQuestStartLatch)
                {
                    message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
                }

                return true;
            }

            if (TryResolveItemUpgradePacketOwnedNoticeOnlyResult(
                    decodeState.ResultCode,
                    decodeState.HasReasonCode ? decodeState.ReasonCode : (int?)null,
                    decodeState.HasOutcomeState ? decodeState.OutcomeResultValue : _itemUpgradeOwnerLastResultValue,
                    out string noticeMessage))
            {
                _pendingItemUpgradeOwnerRequest.ForcedSuccess = null;
                _pendingItemUpgradeOwnerRequest.SuppressUpgradeApply = true;
                _pendingItemUpgradeOwnerRequest.PacketOwnedStatusMessage = noticeMessage;
                _pendingItemUpgradeOwnerRequest.PacketOwnedApplyStatusMessageOverride = null;
                _pendingItemUpgradeOwnerRequest.PacketOwnedResultObserved = true;
                _pendingItemUpgradeOwnerRequest.PacketOwnedResultCode = decodeState.ResultCode;
                _pendingItemUpgradeOwnerRequest.ResultReadyAtTick = currTickCount + ResolveItemUpgradeResultReadyDelayMs(
                    decodeState.ResultCode,
                    outcomeResultValue: null);
                TryCompletePendingItemUpgradeOwnerRequest();
                message = $"Queued packet-owned item-upgrade notice result code {decodeState.ResultCode}.";
                if (consumedQuestStartLatch)
                {
                    message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
                }

                return true;
            }

            bool success;
            if (decodeState.HasOutcomeState)
            {
                if (!TryMapItemUpgradeOutcomeStateResult(decodeState.OutcomeResultValue, out bool outcomeSuccess))
                {
                    message = $"Unsupported packet-owned item-upgrade outcome result value {decodeState.OutcomeResultValue} for result code {decodeState.ResultCode}.";
                    return false;
                }

                success = outcomeSuccess;
            }
            else if (!TryMapItemUpgradeResultCode(decodeState.ResultCode, out success))
            {
                message = $"Unsupported packet-owned item-upgrade result code {decodeState.ResultCode}.";
                return false;
            }

            _pendingItemUpgradeOwnerRequest.ForcedSuccess = success;
            _pendingItemUpgradeOwnerRequest.SuppressUpgradeApply = false;
            _pendingItemUpgradeOwnerRequest.PacketOwnedStatusMessage = null;
            _pendingItemUpgradeOwnerRequest.PacketOwnedApplyStatusMessageOverride = null;
            _pendingItemUpgradeOwnerRequest.PacketOwnedResultObserved = true;
            _pendingItemUpgradeOwnerRequest.PacketOwnedResultCode = decodeState.ResultCode;
            _pendingItemUpgradeOwnerRequest.ResultReadyAtTick = currTickCount + ResolveItemUpgradeResultReadyDelayMs(
                decodeState.ResultCode,
                decodeState.HasOutcomeState ? decodeState.OutcomeResultValue : (int?)null);
            message = success
                ? $"Queued packet-owned item-upgrade success result code {decodeState.ResultCode}."
                : $"Queued packet-owned item-upgrade fail result code {decodeState.ResultCode}.";
            if (consumedQuestStartLatch)
            {
                message = $"{message} The same shared exclusive-reset event also cleared the packet-owned StartQuest follow-up latch.";
            }

            return true;
        }

        private bool TryResolveItemUpgradePacketOwnedNoticeWithoutPendingRequest(
            ItemUpgradeResultDecodeState decodeState,
            out string message)
        {
            int? reasonCode = decodeState.HasReasonCode ? decodeState.ReasonCode : (int?)null;
            int? resultValue = decodeState.HasOutcomeState ? decodeState.OutcomeResultValue : _itemUpgradeOwnerLastResultValue;
            int recoverySlotCountArgument = ResolveItemUpgradeRecoveredSlotCountArgumentWithoutPendingRequest();
            return TryResolveItemUpgradePacketOwnedNoticeWithoutPendingRequest(
                decodeState.ResultCode,
                reasonCode,
                resultValue,
                recoverySlotCountArgument,
                out message);
        }

        private static bool TryResolveItemUpgradePacketOwnedOutcomeWithoutPendingRequest(
            ItemUpgradeResultDecodeState decodeState,
            out string message,
            out bool? success)
        {
            return TryResolveItemUpgradePacketOwnedOutcomeWithoutPendingRequest(
                decodeState.ResultCode,
                decodeState.HasOutcomeState ? decodeState.OutcomeResultValue : (int?)null,
                out message,
                out success);
        }

        private static bool TryResolveItemUpgradePacketOwnedOutcomeWithoutPendingRequest(
            byte resultCode,
            int? resultValue,
            out string message,
            out bool? success)
        {
            message = null;
            success = null;

            if (resultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot ||
                resultCode == ItemUpgradePacketResultCodeClientRejected ||
                !resultValue.HasValue)
            {
                return false;
            }

            if (!TryMapItemUpgradeOutcomeStateResult(resultValue.Value, out bool mappedSuccess))
            {
                return false;
            }

            success = mappedSuccess;
            message = mappedSuccess
                ? "Packet-owned item enhancement succeeded."
                : "Packet-owned item enhancement failed.";
            return true;
        }

        private static int ResolveItemUpgradeResultReadyDelayMs(byte resultCode, int? outcomeResultValue)
        {
            // CUIItemUpgrade::OnItemUpgradeResult handles 65/66 branches in the same call
            // (Decode4 reason + immediate notice/recovery path), while non-65/66 outcomes
            // transition through the regular result-state show path.
            //
            // CUIItemUpgrade::ShowResult sets m_tEnd to now+1000 specifically when
            // m_nReturnResult==61 and m_nResult==0 (Vicious' Hammer fail), so keep
            // that result branch on the delayed apply lane before owner completion.
            return resultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot ||
                   resultCode == ItemUpgradePacketResultCodeClientRejected
                ? 0
                : resultCode == ItemUpgradePacketResultCodeViciousHammer &&
                  outcomeResultValue.GetValueOrDefault() == ItemUpgradePacketOutcomeStateFail
                    ? ItemUpgradeOwnerResultAckViciousHammerDelayMs
                : ItemUpgradeOwnerResultApplyDelayMs;
        }

        private int ResolveItemUpgradeRecoveredSlotCountArgument(EquipSlot slot)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
            {
                if (_itemUpgradeOwnerLastUpgradeStateValue != int.MinValue)
                {
                    int totalSlots = Math.Max(1, itemUpgradeWindow.ResolveProjectedRemainingUpgradeSlotCountAfterRecovery(slot));
                    return ResolveItemUpgradeRecoveredSlotCountArgumentFromPacketState(
                        totalSlots,
                        _itemUpgradeOwnerLastUpgradeStateValue);
                }

                return itemUpgradeWindow.ResolveProjectedRemainingUpgradeSlotCountAfterRecovery(slot);
            }

            return 1;
        }

        private int ResolveItemUpgradeRecoveredSlotCountArgumentWithoutPendingRequest()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
            {
                int totalSlotCount = itemUpgradeWindow.ResolveCurrentSelectionTotalUpgradeSlotCount();
                if (_itemUpgradeOwnerLastUpgradeStateValue != int.MinValue)
                {
                    return ResolveItemUpgradeRecoveredSlotCountArgumentFromPacketState(
                        totalSlotCount,
                        _itemUpgradeOwnerLastUpgradeStateValue);
                }

                return itemUpgradeWindow.ResolveProjectedRemainingUpgradeSlotCountAfterRecoveryFromCurrentSelection();
            }

            return 1;
        }

        private static int ResolveItemUpgradeRecoveredSlotCountArgumentFromPacketState(int totalSlotCount, int packetUpgradeState)
        {
            int totalSlots = Math.Max(0, totalSlotCount);
            int consumedSlotCount = packetUpgradeState & 0xFF;
            int recoveredSlotCount = (packetUpgradeState >> 8) & 0xFF;
            return Math.Max(0, totalSlots + recoveredSlotCount - consumedSlotCount);
        }

        private void MarkItemUpgradeOwnerRequestSent()
        {
            _itemUpgradeOwnerRequestSent = true;
        }

        private void ClearItemUpgradeOwnerRequestState(int currentTick)
        {
            _itemUpgradeOwnerRequestSent = false;
            _itemUpgradeOwnerRequestSentTick = currentTick;
            StampPacketOwnedUtilityRequestState();
        }

        private bool HasActiveItemUpgradeOwnerRequestBlock(int currentTick)
        {
            return IsItemUpgradeOwnerRequestBlocked(
                _pendingItemUpgradeOwnerRequest != null,
                _itemUpgradeOwnerRequestSentTick,
                _packetOwnedUtilityRequestTick,
                currentTick,
                IsLocalCharacterAliveForExclusiveRequest(_playerManager?.Player));
        }

        internal static bool IsItemUpgradeOwnerRequestBlockedForTests(bool requestSent, int lastRequestTick, int currentTick)
        {
            return IsItemUpgradeOwnerRequestBlocked(
                requestSent,
                lastRequestTick,
                sharedUtilityRequestTick: int.MinValue,
                currentTick,
                isLocalCharacterAlive: true);
        }

        internal static bool IsItemUpgradeOwnerRequestBlockedForTests(
            bool requestSent,
            int lastRequestTick,
            int sharedUtilityRequestTick,
            int currentTick,
            bool isLocalCharacterAlive)
        {
            return IsItemUpgradeOwnerRequestBlocked(
                requestSent,
                lastRequestTick,
                sharedUtilityRequestTick,
                currentTick,
                isLocalCharacterAlive);
        }

        internal static bool IsItemUpgradeOwnerRequestAwaitingPacketResultForTests(bool hasPendingRequest, bool packetOwnedResultObserved)
        {
            PendingItemUpgradeOwnerRequestState pendingRequest = hasPendingRequest
                ? new PendingItemUpgradeOwnerRequestState
                {
                    PacketOwnedResultObserved = packetOwnedResultObserved
                }
                : null;
            return IsItemUpgradeOwnerRequestAwaitingPacketResult(pendingRequest);
        }

        internal static bool IsItemUpgradeOwnerDuplicateSendBlockedForTests(bool requestSent, bool awaitingPacketResult)
        {
            return IsItemUpgradeOwnerDuplicateSendBlocked(requestSent, awaitingPacketResult);
        }

        private static bool IsItemUpgradeOwnerRequestBlocked(
            bool requestSent,
            int lastRequestTick,
            int sharedUtilityRequestTick,
            int currentTick,
            bool isLocalCharacterAlive)
        {
            // CUIItemUpgrade::OnButtonClicked denies upgrade send while the shared
            // exclusive-request latch is active or the local character is blocked
            // by the same dead-state gate used by CWvsContext::CanSendExclRequest.
            // Duplicate-send request-in-flight gating is handled separately by
            // the m_bRequestSent branch before this shared exclusive gate.
            if (requestSent)
            {
                return true;
            }

            if (!isLocalCharacterAlive)
            {
                return true;
            }

            if (lastRequestTick != int.MinValue &&
                unchecked(currentTick - lastRequestTick) < ItemUpgradeOwnerExclusiveRequestCooldownMs)
            {
                return true;
            }

            return sharedUtilityRequestTick != int.MinValue &&
                   unchecked(currentTick - sharedUtilityRequestTick) < ItemUpgradeOwnerExclusiveRequestCooldownMs;
        }

        private static bool IsItemUpgradeOwnerRequestAwaitingPacketResult(PendingItemUpgradeOwnerRequestState pendingRequest)
        {
            return pendingRequest != null && !pendingRequest.PacketOwnedResultObserved;
        }

        private static bool IsItemUpgradeOwnerDuplicateSendBlocked(
            bool requestSent,
            bool awaitingPacketResult)
        {
            return requestSent || awaitingPacketResult;
        }

        private static bool TryDecodeItemUpgradeResultPayload(byte[] payload, out byte resultCode)
        {
            resultCode = 0;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            resultCode = payload[0];
            return true;
        }

        private static bool TryDecodeItemUpgradeResultReasonCode(byte[] payload, out int reasonCode)
        {
            reasonCode = 0;
            if (payload == null || payload.Length < ItemUpgradeResultReasonPayloadLength)
            {
                return false;
            }

            reasonCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(byte), sizeof(int)));
            return true;
        }

        private static bool TryDecodeItemUpgradeResultOutcomeState(
            byte[] payload,
            out int packetResultValue,
            out int packetUpgradeState)
        {
            packetResultValue = 0;
            packetUpgradeState = 0;
            if (payload == null || payload.Length < ItemUpgradeResultOutcomePayloadLength)
            {
                return false;
            }

            packetResultValue = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(byte), sizeof(int)));
            packetUpgradeState = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(byte) + sizeof(int), sizeof(int)));
            return true;
        }

        private static bool TryDecodeItemUpgradeResultPayloadState(
            byte[] payload,
            out ItemUpgradeResultDecodeState decodeState,
            out string decodeError)
        {
            decodeState = default;
            decodeError = null;

            if (!TryDecodeItemUpgradeResultPayload(payload, out byte resultCode))
            {
                decodeError = "Packet-owned item-upgrade result payload is empty.";
                return false;
            }

            if (resultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot ||
                resultCode == ItemUpgradePacketResultCodeClientRejected)
            {
                if (payload.Length != ItemUpgradeResultReasonPayloadLength)
                {
                    decodeError = payload.Length > ItemUpgradeResultReasonPayloadLength
                        ? $"Packet-owned item-upgrade result code {resultCode} contains unexpected trailing bytes after the required reason payload field."
                        : $"Packet-owned item-upgrade result code {resultCode} requires a reason payload field.";
                    return false;
                }

                if (!TryDecodeItemUpgradeResultReasonCode(payload, out int reasonCode))
                {
                    decodeError = $"Packet-owned item-upgrade result code {resultCode} requires a reason payload field.";
                    return false;
                }

                decodeState = new ItemUpgradeResultDecodeState(
                    resultCode,
                    HasReasonCode: true,
                    ReasonCode: reasonCode,
                    HasOutcomeState: false,
                    OutcomeResultValue: 0,
                    OutcomeUpgradeState: 0);
                return true;
            }

            if (payload.Length != ItemUpgradeResultOutcomePayloadLength)
            {
                decodeError = payload.Length > ItemUpgradeResultOutcomePayloadLength
                    ? $"Packet-owned item-upgrade result code {resultCode} contains unexpected trailing bytes after the required outcome-state payload fields."
                    : $"Packet-owned item-upgrade result code {resultCode} requires outcome-state payload fields.";
                return false;
            }

            if (!TryDecodeItemUpgradeResultOutcomeState(payload, out int packetResultValue, out int packetUpgradeState))
            {
                decodeError = $"Packet-owned item-upgrade result code {resultCode} requires outcome-state payload fields.";
                return false;
            }

            decodeState = new ItemUpgradeResultDecodeState(
                resultCode,
                HasReasonCode: false,
                ReasonCode: 0,
                HasOutcomeState: true,
                OutcomeResultValue: packetResultValue,
                OutcomeUpgradeState: packetUpgradeState);
            return true;
        }

        private int ResolveItemUpgradeRequestItemToken(ItemUpgradeUI.ItemUpgradeOwnerRequest request)
        {
            if (request.EquipItemToken > 0)
            {
                return request.EquipItemToken;
            }

            return ResolveItemUpgradeRequestItemTokenFallback(request.EquipItemId, request.ConsumableItemId);
        }

        private static int ResolveItemUpgradeRequestItemTokenFallback(int equipItemId, int consumableItemId)
        {
            if (equipItemId > 0)
            {
                return equipItemId;
            }

            return consumableItemId;
        }

        private static byte[] BuildItemUpgradeRequestPayload(int itemToken, int slotPosition, int updateTick)
        {
            byte[] payload = new byte[ItemUpgradeOwnerRequestPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), itemToken);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)), Math.Max(0, slotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) * 2, sizeof(int)), updateTick);
            return payload;
        }

        private static byte[] BuildItemUpgradeConsumeCashRequestPayload(
            int useRequestTick,
            int consumableSlotPosition,
            int consumableItemId,
            int itemToken,
            int updateTick)
        {
            byte[] payload = new byte[ItemUpgradeOwnerConsumeCashRequestPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), useRequestTick);
            BinaryPrimitives.WriteInt16LittleEndian(
                payload.AsSpan(sizeof(int), sizeof(short)),
                (short)Math.Clamp(consumableSlotPosition, short.MinValue, short.MaxValue));
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(sizeof(int) + sizeof(short), sizeof(int)),
                consumableItemId);
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength, sizeof(int)),
                itemToken);
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength + sizeof(int), sizeof(int)),
                Math.Max(0, consumableSlotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength + (sizeof(int) * 2), sizeof(int)),
                updateTick);
            return payload;
        }

        private string BuildItemUpgradeOutboundRequestDispatchLabel(
            byte[] payload,
            bool useConsumeCashRequestPayload,
            out int responseDelayMs)
        {
            responseDelayMs = ItemUpgradeOwnerResultFallbackDelayMs;
            string payloadHex = payload?.Length > 0 ? Convert.ToHexString(payload) : "<empty>";
            string dispatchStatus = "live bridge unavailable";
            string outboxStatus = "packet outbox unavailable";
            string requestPathLabel = useConsumeCashRequestPayload
                ? "consume-cash-prefixed request body"
                : "CUIItemUpgrade::OnButtonClicked 3xDecode4 request body";

            if (payload != null &&
                _localUtilityOfficialSessionBridge.TrySendOutboundPacket(ItemUpgradeOwnerRequestOpcode, payload, out dispatchStatus))
            {
                responseDelayMs = ItemUpgradeOwnerExternalResultFallbackDelayMs;
                return $"Mirrored item-upgrade request opcode {ItemUpgradeOwnerRequestOpcode} [{payloadHex}] ({requestPathLabel}) through the live local-utility bridge. {dispatchStatus}";
            }

            if (payload != null &&
                _localUtilityPacketOutbox.TrySendOutboundPacket(ItemUpgradeOwnerRequestOpcode, payload, out outboxStatus))
            {
                responseDelayMs = ItemUpgradeOwnerExternalResultFallbackDelayMs;
                return $"Mirrored item-upgrade request opcode {ItemUpgradeOwnerRequestOpcode} [{payloadHex}] ({requestPathLabel}) through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            if (payload != null &&
                _localUtilityOfficialSessionBridge.IsRunning &&
                _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(ItemUpgradeOwnerRequestOpcode, payload, out string queuedBridgeStatus))
            {
                responseDelayMs = ItemUpgradeOwnerExternalResultFallbackDelayMs;
                return $"Queued item-upgrade request opcode {ItemUpgradeOwnerRequestOpcode} [{payloadHex}] ({requestPathLabel}) for deferred official-session injection after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
            }

            if (payload != null &&
                _localUtilityPacketOutbox.TryQueueOutboundPacket(ItemUpgradeOwnerRequestOpcode, payload, out string queuedOutboxStatus))
            {
                responseDelayMs = ItemUpgradeOwnerExternalResultFallbackDelayMs;
                return $"Queued item-upgrade request opcode {ItemUpgradeOwnerRequestOpcode} [{payloadHex}] ({requestPathLabel}) for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"The item-upgrade owner kept opcode {ItemUpgradeOwnerRequestOpcode} [{payloadHex}] ({requestPathLabel}) simulator-local because neither the live bridge nor the generic outbox nor either deferred queue accepted the request. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
        }

        private static bool TryDecodeItemUpgradeRequestPayload(
            byte[] payload,
            out int itemToken,
            out int slotPosition,
            out int updateTick)
        {
            itemToken = 0;
            slotPosition = 0;
            updateTick = 0;
            if (payload == null || payload.Length != ItemUpgradeOwnerRequestPayloadLength)
            {
                return false;
            }

            itemToken = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            slotPosition = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)));
            updateTick = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int) * 2, sizeof(int)));
            return true;
        }

        private void StageItemUpgradeResultAck(byte returnResultCode, int resultValue, int currentTick)
        {
            _itemUpgradeOwnerPendingResultAckReturnCode = returnResultCode;
            _itemUpgradeOwnerPendingResultAckValue = resultValue;
            _itemUpgradeOwnerPendingResultAckReadyTick = unchecked(currentTick + ResolveItemUpgradeResultAckDispatchDelayMs(returnResultCode, resultValue));
        }

        private void TryDispatchPendingItemUpgradeResultAck(int currentTick)
        {
            if (_itemUpgradeOwnerPendingResultAckReadyTick == int.MinValue ||
                unchecked(currentTick - _itemUpgradeOwnerPendingResultAckReadyTick) < 0)
            {
                return;
            }

            byte[] payload = BuildItemUpgradeResultAckPayload(
                _itemUpgradeOwnerPendingResultAckReturnCode,
                _itemUpgradeOwnerPendingResultAckValue);

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(ItemUpgradeOwnerResultAckOpcode, payload, out _))
            {
                _itemUpgradeOwnerPendingResultAckReadyTick = int.MinValue;
                return;
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(ItemUpgradeOwnerResultAckOpcode, payload, out _))
            {
                _itemUpgradeOwnerPendingResultAckReadyTick = int.MinValue;
                return;
            }

            if (_localUtilityOfficialSessionBridge.IsRunning &&
                _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(ItemUpgradeOwnerResultAckOpcode, payload, out _))
            {
                _itemUpgradeOwnerPendingResultAckReadyTick = int.MinValue;
                return;
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(ItemUpgradeOwnerResultAckOpcode, payload, out _))
            {
                _itemUpgradeOwnerPendingResultAckReadyTick = int.MinValue;
            }
        }

        private static int ResolveItemUpgradeResultAckDispatchDelayMs(byte returnResultCode, int resultValue)
        {
            return returnResultCode == ItemUpgradePacketResultCodeViciousHammer && resultValue == ItemUpgradePacketOutcomeStateFail
                ? ItemUpgradeOwnerResultAckViciousHammerDelayMs
                : 0;
        }

        private static byte[] BuildItemUpgradeResultAckPayload(int returnResultCode, int resultValue)
        {
            byte[] payload = new byte[ItemUpgradeOwnerResultAckPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), returnResultCode);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)), resultValue);
            return payload;
        }

        private static bool TryDecodeItemUpgradeResultAckPayload(
            byte[] payload,
            out int returnResultCode,
            out int resultValue)
        {
            returnResultCode = 0;
            resultValue = 0;
            if (payload == null || payload.Length != ItemUpgradeOwnerResultAckPayloadLength)
            {
                return false;
            }

            returnResultCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            resultValue = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)));
            return true;
        }

        private static bool TryDecodeItemUpgradeConsumeCashRequestPayload(
            byte[] payload,
            out int useRequestTick,
            out short consumeSlotPosition,
            out int consumeItemId,
            out int itemToken,
            out int slotPosition,
            out int updateTick)
        {
            useRequestTick = 0;
            consumeSlotPosition = 0;
            consumeItemId = 0;
            itemToken = 0;
            slotPosition = 0;
            updateTick = 0;
            if (payload == null || payload.Length != ItemUpgradeOwnerConsumeCashRequestPayloadLength)
            {
                return false;
            }

            useRequestTick = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            consumeSlotPosition = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(sizeof(int), sizeof(short)));
            consumeItemId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(short), sizeof(int)));
            itemToken = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength, sizeof(int)));
            slotPosition = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength + sizeof(int), sizeof(int)));
            updateTick = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(ItemUpgradeOwnerConsumeCashRequestPayloadPrefixLength + (sizeof(int) * 2), sizeof(int)));
            return true;
        }

        internal static bool TryDecodeItemUpgradeResultPayloadForTests(byte[] payload, out byte resultCode)
        {
            return TryDecodeItemUpgradeResultPayload(payload, out resultCode);
        }

        internal static bool TryDecodeItemUpgradeResultOutcomeStateForTests(
            byte[] payload,
            out int packetResultValue,
            out int packetUpgradeState)
        {
            return TryDecodeItemUpgradeResultOutcomeState(payload, out packetResultValue, out packetUpgradeState);
        }

        internal static byte[] BuildItemUpgradeRequestPayloadForTests(int itemToken, int slotPosition, int updateTick)
        {
            return BuildItemUpgradeRequestPayload(itemToken, slotPosition, updateTick);
        }

        internal static bool TryDecodeItemUpgradeRequestPayloadForTests(
            byte[] payload,
            out int itemToken,
            out int slotPosition,
            out int updateTick)
        {
            return TryDecodeItemUpgradeRequestPayload(payload, out itemToken, out slotPosition, out updateTick);
        }

        internal static int ResolveItemUpgradeRequestItemTokenFallbackForTests(int equipItemId, int consumableItemId)
        {
            return ResolveItemUpgradeRequestItemTokenFallback(equipItemId, consumableItemId);
        }

        internal static byte[] BuildItemUpgradeConsumeCashRequestPayloadForTests(
            int consumableSlotPosition,
            int consumableItemId,
            int itemToken,
            int updateTick)
        {
            return BuildItemUpgradeConsumeCashRequestPayload(updateTick, consumableSlotPosition, consumableItemId, itemToken, updateTick);
        }

        internal static byte[] BuildItemUpgradeConsumeCashRequestPayloadForTests(
            int useRequestTick,
            int consumableSlotPosition,
            int consumableItemId,
            int itemToken,
            int updateTick)
        {
            return BuildItemUpgradeConsumeCashRequestPayload(useRequestTick, consumableSlotPosition, consumableItemId, itemToken, updateTick);
        }

        internal static bool TryDecodeItemUpgradeConsumeCashRequestPayloadForTests(
            byte[] payload,
            out int useRequestTick,
            out short consumeSlotPosition,
            out int consumeItemId,
            out int itemToken,
            out int slotPosition,
            out int updateTick)
        {
            return TryDecodeItemUpgradeConsumeCashRequestPayload(
                payload,
                out useRequestTick,
                out consumeSlotPosition,
                out consumeItemId,
                out itemToken,
                out slotPosition,
                out updateTick);
        }

        internal static int ResolveItemUpgradeResultAckDispatchDelayMsForTests(byte returnResultCode, int resultValue)
        {
            return ResolveItemUpgradeResultAckDispatchDelayMs(returnResultCode, resultValue);
        }

        internal static byte[] BuildItemUpgradeResultAckPayloadForTests(int returnResultCode, int resultValue)
        {
            return BuildItemUpgradeResultAckPayload(returnResultCode, resultValue);
        }

        internal static bool TryDecodeItemUpgradeResultAckPayloadForTests(
            byte[] payload,
            out int returnResultCode,
            out int resultValue)
        {
            return TryDecodeItemUpgradeResultAckPayload(payload, out returnResultCode, out resultValue);
        }

        private static bool ShouldUseConsumeCashItemUseRequestPayload(
            InventoryType consumableInventoryType,
            bool hasMatchedConsumeCashSeed)
        {
            return hasMatchedConsumeCashSeed && consumableInventoryType == InventoryType.CASH;
        }

        internal static bool ShouldUseConsumeCashItemUseRequestPayloadForTests(
            InventoryType consumableInventoryType,
            bool hasMatchedConsumeCashSeed)
        {
            return ShouldUseConsumeCashItemUseRequestPayload(consumableInventoryType, hasMatchedConsumeCashSeed);
        }

        private static bool TryMapItemUpgradeResultCode(byte resultCode, out bool success)
        {
            success = resultCode == ItemUpgradePacketResultCodeSuccess;
            return resultCode == ItemUpgradePacketResultCodeSuccess || resultCode == ItemUpgradePacketResultCodeFail;
        }

        private static bool TryMapItemUpgradeOutcomeStateResult(int packetResultValue, out bool success)
        {
            // CUIItemUpgrade::OnItemUpgradeResult stores Decode4 directly into m_nResult,
            // and ShowResult gates by truthiness (if m_nResult) rather than a strict {0,1} domain.
            // Keep 0 as fail and treat any non-zero outcome as success-equivalent.
            success = packetResultValue != ItemUpgradePacketOutcomeStateFail;
            return true;
        }

        internal static bool TryMapItemUpgradeResultCodeForTests(byte resultCode, out bool success)
        {
            return TryMapItemUpgradeResultCode(resultCode, out success);
        }

        internal static bool TryMapItemUpgradeOutcomeStateResultForTests(int packetResultValue, out bool success)
        {
            return TryMapItemUpgradeOutcomeStateResult(packetResultValue, out success);
        }

        internal static int ResolveItemUpgradeResultReadyDelayMsForTests(byte resultCode)
        {
            return ResolveItemUpgradeResultReadyDelayMs(resultCode, outcomeResultValue: null);
        }

        internal static int ResolveItemUpgradeResultReadyDelayMsForTests(byte resultCode, int? outcomeResultValue)
        {
            return ResolveItemUpgradeResultReadyDelayMs(resultCode, outcomeResultValue);
        }

        internal static bool TryDecodeItemUpgradeResultPayloadStateForTests(
            byte[] payload,
            out byte resultCode,
            out bool hasReasonCode,
            out int reasonCode,
            out bool hasOutcomeState,
            out int outcomeResultValue,
            out int outcomeUpgradeState,
            out string decodeError)
        {
            bool decoded = TryDecodeItemUpgradeResultPayloadState(payload, out ItemUpgradeResultDecodeState decodeState, out decodeError);
            resultCode = decodeState.ResultCode;
            hasReasonCode = decodeState.HasReasonCode;
            reasonCode = decodeState.ReasonCode;
            hasOutcomeState = decodeState.HasOutcomeState;
            outcomeResultValue = decodeState.OutcomeResultValue;
            outcomeUpgradeState = decodeState.OutcomeUpgradeState;
            return decoded;
        }

        private static bool TryResolveItemUpgradePacketOwnedNoticeOnlyResult(
            byte[] payload,
            byte resultCode,
            out string message)
        {
            int? reasonCode = TryDecodeItemUpgradeResultReasonCode(payload, out int decodedReasonCode)
                ? decodedReasonCode
                : (int?)null;
            return TryResolveItemUpgradePacketOwnedNoticeOnlyResult(resultCode, reasonCode, resultValue: null, out message);
        }

        private static bool TryResolveItemUpgradePacketOwnedNoticeOnlyResult(
            byte resultCode,
            int? reasonCode,
            int? resultValue,
            out string message)
        {
            message = null;
            if (resultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot)
            {
                if (reasonCode.GetValueOrDefault() != 0)
                {
                    message = ResolveItemUpgradeBusyNotice(resultValue ?? ItemUpgradeClientInitialResultValue);
                    return true;
                }

                return false;
            }

            if (resultCode != ItemUpgradePacketResultCodeClientRejected)
            {
                return false;
            }

            if (!reasonCode.HasValue)
            {
                message = ResolveItemUpgradeBlockedStateNotice();
                return true;
            }

            message = reasonCode.Value switch
            {
                1 => ResolveItemUpgradeSelectionRequiredNotice(),
                2 => ResolveItemUpgradeIncompatibleSelectionNotice(),
                3 => ResolveItemUpgradeViciousHammerBlockedNotice(),
                _ => ResolveItemUpgradeBusyNotice(resultValue ?? ItemUpgradeClientInitialResultValue)
            };
            return true;
        }

        private static bool TryResolveItemUpgradePacketOwnedNoticeWithoutPendingRequest(
            byte resultCode,
            int? reasonCode,
            int? resultValue,
            int recoverySlotCountArgument,
            out string message)
        {
            message = null;
            if (resultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot &&
                reasonCode.HasValue &&
                reasonCode.Value == 0)
            {
                message = ResolveItemUpgradeRecoveredSlotNotice(Math.Max(0, recoverySlotCountArgument));
                return true;
            }

            return TryResolveItemUpgradePacketOwnedNoticeOnlyResult(resultCode, reasonCode, resultValue, out message);
        }

        internal static int ResolveItemUpgradeClientDuplicateRequestBusyResultValueForTests()
        {
            return ItemUpgradeClientDuplicateRequestBusyResultValue;
        }

        internal static int ResolveItemUpgradeClientInitialResultValueForTests()
        {
            return ItemUpgradeClientInitialResultValue;
        }

        internal static bool TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(
            byte[] payload,
            byte resultCode,
            out string message)
        {
            return TryResolveItemUpgradePacketOwnedNoticeOnlyResult(payload, resultCode, out message);
        }

        internal static bool TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(
            byte resultCode,
            int? reasonCode,
            int? resultValue,
            out string message)
        {
            return TryResolveItemUpgradePacketOwnedNoticeOnlyResult(resultCode, reasonCode, resultValue, out message);
        }

        internal static bool TryResolveItemUpgradePacketOwnedNoticeWithoutPendingRequestForTests(
            byte resultCode,
            int? reasonCode,
            int? resultValue,
            int recoverySlotCountArgument,
            out string message)
        {
            return TryResolveItemUpgradePacketOwnedNoticeWithoutPendingRequest(
                resultCode,
                reasonCode,
                resultValue,
                recoverySlotCountArgument,
                out message);
        }

        internal static bool TryResolveItemUpgradePacketOwnedOutcomeWithoutPendingRequestForTests(
            byte resultCode,
            int? resultValue,
            out string message,
            out bool? success)
        {
            return TryResolveItemUpgradePacketOwnedOutcomeWithoutPendingRequest(
                resultCode,
                resultValue,
                out message,
                out success);
        }

        internal static string ResolveItemUpgradeRecoveredSlotNoticeForTests(int remainingUpgradeCount)
        {
            return ResolveItemUpgradeRecoveredSlotNotice(remainingUpgradeCount);
        }

        internal static int ResolveItemUpgradeRecoveredSlotCountArgumentFromPacketStateForTests(int totalSlotCount, int packetUpgradeState)
        {
            return ResolveItemUpgradeRecoveredSlotCountArgumentFromPacketState(totalSlotCount, packetUpgradeState);
        }

        private static string ResolveItemUpgradeBusyNotice(int resultValue)
        {
            return ResolveItemUpgradeFormattedStringPoolNotice(
                0x1A86,
                resultValue,
                "An enhancement request is already in progress.");
        }

        private static string ResolveItemUpgradeRecoveredSlotNotice(int remainingUpgradeCount)
        {
            return ResolveItemUpgradeFormattedStringPoolNotice(
                0x13D0,
                Math.Max(0, remainingUpgradeCount),
                "Increased available upgrade by 1. 1 upgrades are left.");
        }

        private static string ResolveItemUpgradeBlockedStateNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x136, "You cannot use item enhancement right now.");
        }

        private static string ResolveItemUpgradeSelectionRequiredNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x13CE, "Please select an item to enhance.");
        }

        private static string ResolveItemUpgradeIncompatibleSelectionNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x13CF, "The selected item cannot be enhanced with this scroll.");
        }

        private static string ResolveItemUpgradeViciousHammerBlockedNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x13D1, "You can't use Vicious' Hammer on Horntail Necklace.");
        }

        private static string ResolveItemUpgradeStringPoolNotice(int stringPoolId, string fallback)
        {
            string text = MapleStoryStringPool.GetOrFallback(stringPoolId, fallback);
            if (string.IsNullOrWhiteSpace(text) || text.Contains('%'))
            {
                return fallback;
            }

            return text;
        }

        private static string ResolveItemUpgradeFormattedStringPoolNotice(
            int stringPoolId,
            int argument,
            string fallback)
        {
            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallback,
                maxPlaceholderCount: 1,
                out _);
            if (string.IsNullOrWhiteSpace(compositeFormat))
            {
                return fallback;
            }

            try
            {
                string resolved = string.Format(CultureInfo.InvariantCulture, compositeFormat, argument);
                return string.IsNullOrWhiteSpace(resolved) ? fallback : resolved;
            }
            catch (FormatException)
            {
                return fallback;
            }
        }

        private void StageItemUpgradeConsumeCashUseRequestSeed(int itemId, InventoryType inventoryType, int slotIndex, int updateTick)
        {
            _itemUpgradeOwnerConsumeCashUseRequestTick = updateTick;
            _itemUpgradeOwnerConsumeCashUseInventoryType = inventoryType;
            _itemUpgradeOwnerConsumeCashUseSlotIndex = slotIndex;
            _itemUpgradeOwnerConsumeCashUseItemId = itemId;
        }

        private bool TryConsumeItemUpgradeConsumeCashUseRequestTick(
            ItemUpgradeUI.ItemUpgradeOwnerRequest request,
            int fallbackTick,
            out int consumeCashUseRequestTick)
        {
            consumeCashUseRequestTick = fallbackTick;
            bool isMatchingConsumableSeed =
                _itemUpgradeOwnerConsumeCashUseRequestTick != int.MinValue &&
                request.ConsumableInventoryType == _itemUpgradeOwnerConsumeCashUseInventoryType &&
                request.ConsumableSlotIndex == _itemUpgradeOwnerConsumeCashUseSlotIndex &&
                request.ConsumableItemId == _itemUpgradeOwnerConsumeCashUseItemId;
            if (!isMatchingConsumableSeed)
            {
                return false;
            }

            consumeCashUseRequestTick = _itemUpgradeOwnerConsumeCashUseRequestTick;
            _itemUpgradeOwnerConsumeCashUseRequestTick = int.MinValue;
            _itemUpgradeOwnerConsumeCashUseSlotIndex = -1;
            _itemUpgradeOwnerConsumeCashUseItemId = 0;
            return true;
        }
    }
}
