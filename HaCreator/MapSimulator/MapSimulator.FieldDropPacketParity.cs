using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int LocalItemDropFallbackCommitDelayMs = 600;
        private int _nextLocalItemDropRequestId = 1;
        private readonly Dictionary<int, PendingLocalItemDropRequest> _pendingLocalItemDropRequests = new();

        private sealed class PendingLocalItemDropRequest
        {
            public int RequestId { get; init; }
            public int RequestedAtTick { get; init; }
            public LocalFieldItemDropRequest Request { get; init; }
            public int SourceStackQuantity { get; init; }
            public int OwnerId { get; init; }
            public float DropX { get; init; }
            public float DropY { get; init; }
        }

        private bool TryQueuePendingLocalItemDropRequest(LocalFieldItemDropRequest request, InventorySlotData sourceSlotData)
        {
            if (request.ItemId <= 0
                || request.Quantity <= 0
                || sourceSlotData == null
                || uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return false;
            }

            int ownerId = _playerManager?.Player?.Build?.Id ?? 0;
            float dropX = _playerManager?.Player?.X ?? 0f;
            float dropY = _playerManager?.Player?.Y ?? 0f;
            int requestId = GetNextLocalItemDropRequestId();
            if (requestId <= 0
                || !inventoryWindow.TrySetPendingRequestState(request.InventoryType, request.SlotIndex, requestId, isPending: true))
            {
                return false;
            }

            _pendingLocalItemDropRequests[requestId] = new PendingLocalItemDropRequest
            {
                RequestId = requestId,
                RequestedAtTick = currTickCount,
                Request = request,
                SourceStackQuantity = Math.Max(1, sourceSlotData.Quantity),
                OwnerId = ownerId,
                DropX = dropX,
                DropY = dropY
            };

            if (!MirrorClientItemDropRequestEcho(request))
            {
                _ = TryFinalizePendingLocalItemDropRequestSuccess(
                    requestId,
                    source: "local fallback",
                    out _);
            }

            return true;
        }

        private bool TryApplyPendingLocalItemDropInventoryOperationPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0 || _pendingLocalItemDropRequests.Count == 0)
            {
                return false;
            }

            List<KeyValuePair<int, PendingLocalItemDropRequest>> pendingRequests = _pendingLocalItemDropRequests
                .OrderBy(entry => entry.Value?.RequestedAtTick ?? int.MaxValue)
                .ToList();
            for (int i = 0; i < pendingRequests.Count; i++)
            {
                PendingLocalItemDropRequest pending = pendingRequests[i].Value;
                if (pending == null)
                {
                    continue;
                }

                LocalItemDropInventoryOperationResultKind result = FieldDropRequestEvaluator.ResolveClientItemDropInventoryOperationResult(
                    pending.Request,
                    pending.SourceStackQuantity,
                    payload,
                    out string operationStatus);
                switch (result)
                {
                    case LocalItemDropInventoryOperationResultKind.Success:
                        if (TryFinalizePendingLocalItemDropRequestSuccess(
                                pending.RequestId,
                                source: "packet-owned inventory operation",
                                out string successMessage))
                        {
                            message = string.IsNullOrWhiteSpace(operationStatus)
                                ? successMessage
                                : $"{successMessage} {operationStatus}";
                            return true;
                        }

                        message = successMessage;
                        return false;

                    case LocalItemDropInventoryOperationResultKind.Reject:
                        if (TryFinalizePendingLocalItemDropRequestReject(
                                pending.RequestId,
                                source: "packet-owned inventory operation",
                                out string rejectMessage))
                        {
                            message = string.IsNullOrWhiteSpace(operationStatus)
                                ? rejectMessage
                                : $"{rejectMessage} {operationStatus}";
                            return true;
                        }

                        message = rejectMessage;
                        return false;

                    default:
                        break;
                }
            }

            return false;
        }

        private void FlushPendingLocalItemDropRequests(int currentTick)
        {
            if (_pendingLocalItemDropRequests.Count == 0)
            {
                return;
            }

            int[] timedOutRequestIds = _pendingLocalItemDropRequests.Values
                .Where(request => request != null && Math.Max(0, unchecked(currentTick - request.RequestedAtTick)) >= LocalItemDropFallbackCommitDelayMs)
                .Select(request => request.RequestId)
                .ToArray();
            for (int i = 0; i < timedOutRequestIds.Length; i++)
            {
                _ = TryFinalizePendingLocalItemDropRequestSuccess(
                    timedOutRequestIds[i],
                    source: "timeout fallback",
                    out _);
            }
        }

        private bool TryFinalizePendingLocalItemDropRequestSuccess(int requestId, string source, out string message)
        {
            message = null;
            if (requestId <= 0
                || !_pendingLocalItemDropRequests.TryGetValue(requestId, out PendingLocalItemDropRequest pending)
                || pending == null)
            {
                message = $"Local discard request {requestId} is not pending.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                _pendingLocalItemDropRequests.Remove(requestId);
                message = $"Local discard request {requestId} was cleared because the inventory runtime is unavailable.";
                return false;
            }

            inventoryWindow.TryClearPendingRequestState(requestId);
            if (!inventoryWindow.TryConsumeItemAtSlotForDropRequest(
                    pending.Request.InventoryType,
                    pending.Request.SlotIndex,
                    pending.Request.ItemId,
                    pending.Request.Quantity))
            {
                _pendingLocalItemDropRequests.Remove(requestId);
                message = $"Local discard request {requestId} could not consume the source inventory slot after {source}.";
                return false;
            }

            _dropPool?.SpawnItemDrop(
                pending.DropX,
                pending.DropY,
                pending.Request.ItemId.ToString(CultureInfo.InvariantCulture),
                pending.Request.Quantity,
                currTickCount,
                pending.OwnerId);
            PlayDropItemSE();
            _pendingLocalItemDropRequests.Remove(requestId);
            message = $"Committed local discard request {requestId} through {source}.";
            return true;
        }

        private bool TryFinalizePendingLocalItemDropRequestReject(int requestId, string source, out string message)
        {
            message = null;
            if (requestId <= 0
                || !_pendingLocalItemDropRequests.TryGetValue(requestId, out PendingLocalItemDropRequest pending)
                || pending == null)
            {
                message = $"Local discard request {requestId} is not pending.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow)
            {
                inventoryWindow.TryClearPendingRequestState(requestId);
            }

            _pendingLocalItemDropRequests.Remove(requestId);
            message = $"Rejected local discard request {requestId} through {source}.";
            return true;
        }

        private int GetNextLocalItemDropRequestId()
        {
            int requestId = _nextLocalItemDropRequestId++;
            if (requestId <= 0)
            {
                _nextLocalItemDropRequestId = 2;
                requestId = 1;
            }

            return requestId;
        }
    }
}
