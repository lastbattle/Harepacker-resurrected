using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;
using System.Buffers.Binary;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int ItemUpgradeOwnerResultFallbackDelayMs = 750;
        private const int ItemUpgradeOwnerResultApplyDelayMs = 50;
        private const int ItemUpgradeOwnerExclusiveRequestCooldownMs = 500;
        private const int ItemUpgradeOwnerRequestPayloadLength = sizeof(int) * 3;
        private const byte ItemUpgradePacketResultCodeFail = 0;
        private const byte ItemUpgradePacketResultCodeSuccess = 1;
        private const byte ItemUpgradePacketResultCodeClientNoUpgradeSlot = 65;
        private const byte ItemUpgradePacketResultCodeClientRejected = 66;

        private bool _itemUpgradeOwnerRequestSent;
        private int _itemUpgradeOwnerRequestSentTick = int.MinValue;
        private PendingItemUpgradeOwnerRequestState _pendingItemUpgradeOwnerRequest;

        private sealed class PendingItemUpgradeOwnerRequestState
        {
            public ItemUpgradeUI.ItemUpgradeOwnerRequest Request { get; init; }
            public int RequestedAtTick { get; init; }
            public int ResultReadyAtTick { get; set; }
            public bool? ForcedSuccess { get; set; }
            public bool SuppressUpgradeApply { get; set; }
            public string PacketOwnedStatusMessage { get; set; }
            public bool PacketOwnedResultObserved { get; set; }
            public byte? PacketOwnedResultCode { get; set; }
            public byte[] EncodedRequestPayload { get; init; } = Array.Empty<byte>();
            public int RequestItemToken { get; init; }
            public int RequestSlotPosition { get; init; }
        }

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
            if (HasActiveItemUpgradeOwnerRequestBlock(currTickCount))
            {
                string busyNotice = ResolveItemUpgradeBusyNotice();
                ShowUtilityFeedbackMessage(busyNotice);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                {
                    itemUpgradeWindow.SetOwnerStatusMessage(busyNotice, success: false);
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

            int requestItemToken = ResolveItemUpgradeRequestItemToken(request);
            int requestSlotPosition = Math.Max(0, request.ConsumableSlotIndex + 1);
            byte[] encodedRequestPayload = BuildItemUpgradeRequestPayload(
                requestItemToken,
                requestSlotPosition,
                currTickCount);

            _pendingItemUpgradeOwnerRequest = new PendingItemUpgradeOwnerRequestState
            {
                Request = request,
                RequestedAtTick = currTickCount,
                ResultReadyAtTick = currTickCount + ItemUpgradeOwnerResultFallbackDelayMs,
                ForcedSuccess = null,
                EncodedRequestPayload = encodedRequestPayload,
                RequestItemToken = requestItemToken,
                RequestSlotPosition = requestSlotPosition
            };
            _itemUpgradeOwnerRequestSent = true;
            StampPacketOwnedUtilityRequestState();

            string payloadHex = Convert.ToHexString(encodedRequestPayload);
            string statusMessage = $"Enhancement request sent for {request.EquipName} with {request.ConsumableName}.";
            ShowUtilityFeedbackMessage(statusMessage);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindowVisible)
            {
                itemUpgradeWindowVisible.SetPacketOwnedRequestPending(
                    $"{statusMessage} Waiting for packet-owned result. Encoded request body (itemTI, slot, tick): {payloadHex}.");
            }

            return true;
        }

        private void UpdateItemUpgradeOwnerState()
        {
            if (_pendingItemUpgradeOwnerRequest == null ||
                unchecked(currTickCount - _pendingItemUpgradeOwnerRequest.ResultReadyAtTick) < 0)
            {
                return;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is not ItemUpgradeUI itemUpgradeWindow)
            {
                return;
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
                statusMessage = result.StatusMessage;
                success = result.Success;
            }

            ShowUtilityFeedbackMessage(statusMessage);
            _itemUpgradeOwnerRequestSent = false;
            _itemUpgradeOwnerRequestSentTick = currTickCount;
        }

        private bool TryApplyPacketOwnedItemUpgradeResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (_pendingItemUpgradeOwnerRequest == null)
            {
                message = "No pending item-upgrade request is waiting for a packet-owned result.";
                return false;
            }

            if (!TryDecodeItemUpgradeResultPayload(payload, out byte resultCode))
            {
                message = "Packet-owned item-upgrade result payload is empty.";
                return false;
            }

            if (TryResolveItemUpgradePacketOwnedNoticeOnlyResult(payload, resultCode, out string noticeMessage))
            {
                _pendingItemUpgradeOwnerRequest.ForcedSuccess = null;
                _pendingItemUpgradeOwnerRequest.SuppressUpgradeApply = true;
                _pendingItemUpgradeOwnerRequest.PacketOwnedStatusMessage = noticeMessage;
                _pendingItemUpgradeOwnerRequest.PacketOwnedResultObserved = true;
                _pendingItemUpgradeOwnerRequest.PacketOwnedResultCode = resultCode;
                _pendingItemUpgradeOwnerRequest.ResultReadyAtTick = currTickCount + ItemUpgradeOwnerResultApplyDelayMs;
                message = $"Queued packet-owned item-upgrade notice result code {resultCode}.";
                return true;
            }

            if (!TryMapItemUpgradeResultCode(resultCode, out bool success))
            {
                message = $"Unsupported packet-owned item-upgrade result code {resultCode}.";
                return false;
            }

            _pendingItemUpgradeOwnerRequest.ForcedSuccess = success;
            _pendingItemUpgradeOwnerRequest.SuppressUpgradeApply = false;
            _pendingItemUpgradeOwnerRequest.PacketOwnedStatusMessage = null;
            _pendingItemUpgradeOwnerRequest.PacketOwnedResultObserved = true;
            _pendingItemUpgradeOwnerRequest.PacketOwnedResultCode = resultCode;
            _pendingItemUpgradeOwnerRequest.ResultReadyAtTick = currTickCount + ItemUpgradeOwnerResultApplyDelayMs;
            message = success
                ? $"Queued packet-owned item-upgrade success result code {resultCode}."
                : $"Queued packet-owned item-upgrade fail result code {resultCode}.";
            return true;
        }

        private bool HasActiveItemUpgradeOwnerRequestBlock(int currentTick)
        {
            return IsItemUpgradeOwnerRequestBlocked(
                _itemUpgradeOwnerRequestSent || _pendingItemUpgradeOwnerRequest != null,
                _itemUpgradeOwnerRequestSentTick,
                currentTick);
        }

        internal static bool IsItemUpgradeOwnerRequestBlockedForTests(bool requestSent, int lastRequestTick, int currentTick)
        {
            return IsItemUpgradeOwnerRequestBlocked(requestSent, lastRequestTick, currentTick);
        }

        private static bool IsItemUpgradeOwnerRequestBlocked(bool requestSent, int lastRequestTick, int currentTick)
        {
            if (requestSent)
            {
                return true;
            }

            return lastRequestTick != int.MinValue &&
                   unchecked(currentTick - lastRequestTick) < ItemUpgradeOwnerExclusiveRequestCooldownMs;
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
            if (payload == null || payload.Length < (sizeof(byte) + sizeof(int)))
            {
                return false;
            }

            reasonCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(byte), sizeof(int)));
            return true;
        }

        private int ResolveItemUpgradeRequestItemToken(ItemUpgradeUI.ItemUpgradeOwnerRequest request)
        {
            if (request.EquipItemToken > 0)
            {
                return request.EquipItemToken;
            }

            if (uiWindowManager?.InventoryWindow is IInventoryRuntime inventoryRuntime)
            {
                var slots = inventoryRuntime.GetSlots(request.ConsumableInventoryType);
                if (slots != null
                    && request.ConsumableSlotIndex >= 0
                    && request.ConsumableSlotIndex < slots.Count)
                {
                    InventorySlotData slot = slots[request.ConsumableSlotIndex];
                    if (slot != null
                        && slot.ItemId == request.ConsumableItemId
                        && slot.ClientItemToken.GetValueOrDefault() > 0)
                    {
                        return slot.ClientItemToken.Value;
                    }
                }
            }

            return request.ConsumableItemId;
        }

        private static byte[] BuildItemUpgradeRequestPayload(int itemToken, int slotPosition, int updateTick)
        {
            byte[] payload = new byte[ItemUpgradeOwnerRequestPayloadLength];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), itemToken);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)), Math.Max(0, slotPosition));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) * 2, sizeof(int)), updateTick);
            return payload;
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
            if (payload == null || payload.Length < ItemUpgradeOwnerRequestPayloadLength)
            {
                return false;
            }

            itemToken = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            slotPosition = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)));
            updateTick = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int) * 2, sizeof(int)));
            return true;
        }

        internal static bool TryDecodeItemUpgradeResultPayloadForTests(byte[] payload, out byte resultCode)
        {
            return TryDecodeItemUpgradeResultPayload(payload, out resultCode);
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

        private static bool TryMapItemUpgradeResultCode(byte resultCode, out bool success)
        {
            success = resultCode == ItemUpgradePacketResultCodeSuccess;
            return resultCode == ItemUpgradePacketResultCodeSuccess || resultCode == ItemUpgradePacketResultCodeFail;
        }

        internal static bool TryMapItemUpgradeResultCodeForTests(byte resultCode, out bool success)
        {
            return TryMapItemUpgradeResultCode(resultCode, out success);
        }

        private static bool TryResolveItemUpgradePacketOwnedNoticeOnlyResult(
            byte[] payload,
            byte resultCode,
            out string message)
        {
            message = null;
            if (resultCode == ItemUpgradePacketResultCodeClientNoUpgradeSlot)
            {
                if (TryDecodeItemUpgradeResultReasonCode(payload, out int noSlotReasonCode) && noSlotReasonCode != 0)
                {
                    message = ResolveItemUpgradeBusyNotice();
                }
                else
                {
                    message = ResolveItemUpgradeNoUpgradeSlotNotice();
                }

                return true;
            }

            if (resultCode != ItemUpgradePacketResultCodeClientRejected)
            {
                return false;
            }

            if (!TryDecodeItemUpgradeResultReasonCode(payload, out int reasonCode))
            {
                message = ResolveItemUpgradeBlockedStateNotice();
                return true;
            }

            message = reasonCode switch
            {
                1 => ResolveItemUpgradeSelectionRequiredNotice(),
                2 => ResolveItemUpgradeIncompatibleSelectionNotice(),
                3 => ResolveItemUpgradeNoUpgradeSlotNotice(),
                _ => ResolveItemUpgradeBusyNotice()
            };
            return true;
        }

        internal static bool TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(
            byte[] payload,
            byte resultCode,
            out string message)
        {
            return TryResolveItemUpgradePacketOwnedNoticeOnlyResult(payload, resultCode, out message);
        }

        private static string ResolveItemUpgradeBusyNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x1A86, "An enhancement request is already in progress.");
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

        private static string ResolveItemUpgradeNoUpgradeSlotNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x13D1, "The selected item has no upgrade slots remaining.");
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
    }
}
