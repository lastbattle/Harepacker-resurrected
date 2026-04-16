using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int ItemUpgradeOwnerResultFallbackDelayMs = 750;
        private const int ItemUpgradeOwnerResultApplyDelayMs = 50;
        private const int ItemUpgradeOwnerExclusiveRequestCooldownMs = 500;
        private const byte ItemUpgradePacketResultCodeFail = 0;
        private const byte ItemUpgradePacketResultCodeSuccess = 1;

        private bool _itemUpgradeOwnerRequestSent;
        private int _itemUpgradeOwnerRequestSentTick = int.MinValue;
        private PendingItemUpgradeOwnerRequestState _pendingItemUpgradeOwnerRequest;

        private sealed class PendingItemUpgradeOwnerRequestState
        {
            public ItemUpgradeUI.ItemUpgradeOwnerRequest Request { get; init; }
            public int RequestedAtTick { get; init; }
            public int ResultReadyAtTick { get; set; }
            public bool? ForcedSuccess { get; set; }
            public bool PacketOwnedResultObserved { get; set; }
            public byte? PacketOwnedResultCode { get; set; }
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
                    itemUpgradeWindow.SetPacketOwnedRequestPending(busyNotice);
                }

                return true;
            }

            if (_pendingEquipmentChangeRequests.Count > 0 || _gameState.PendingMapChange || _playerManager?.Player?.Build == null)
            {
                string blockedNotice = ResolveItemUpgradeBlockedStateNotice();
                ShowUtilityFeedbackMessage(blockedNotice);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow)
                {
                    itemUpgradeWindow.SetPacketOwnedRequestPending(blockedNotice);
                }

                return true;
            }

            _pendingItemUpgradeOwnerRequest = new PendingItemUpgradeOwnerRequestState
            {
                Request = request,
                RequestedAtTick = currTickCount,
                ResultReadyAtTick = currTickCount + ItemUpgradeOwnerResultFallbackDelayMs,
                ForcedSuccess = null
            };
            _itemUpgradeOwnerRequestSent = true;
            StampPacketOwnedUtilityRequestState();

            string statusMessage = $"Enhancement request sent for {request.EquipName} with {request.ConsumableName}.";
            ShowUtilityFeedbackMessage(statusMessage);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindowVisible)
            {
                itemUpgradeWindowVisible.SetPacketOwnedRequestPending(
                    $"{statusMessage} Waiting for packet-owned result.");
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

            ItemUpgradeUI.ItemUpgradeAttemptResult result =
                itemUpgradeWindow.ApplyPacketOwnedPreparedUpgradeResultAtSlots(
                    pendingRequest.Request.ConsumableInventoryType,
                    pendingRequest.Request.ConsumableSlotIndex,
                    pendingRequest.Request.ModifierInventoryType,
                    pendingRequest.Request.ModifierSlotIndex,
                    pendingRequest.ForcedSuccess);
            ShowUtilityFeedbackMessage(result.StatusMessage);
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

            if (!TryMapItemUpgradeResultCode(resultCode, out bool success))
            {
                message = $"Unsupported packet-owned item-upgrade result code {resultCode}.";
                return false;
            }

            _pendingItemUpgradeOwnerRequest.ForcedSuccess = success;
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

        internal static bool TryDecodeItemUpgradeResultPayloadForTests(byte[] payload, out byte resultCode)
        {
            return TryDecodeItemUpgradeResultPayload(payload, out resultCode);
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

        private static string ResolveItemUpgradeBusyNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x1A86, "An enhancement request is already in progress.");
        }

        private static string ResolveItemUpgradeBlockedStateNotice()
        {
            return ResolveItemUpgradeStringPoolNotice(0x136, "You cannot use item enhancement right now.");
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
