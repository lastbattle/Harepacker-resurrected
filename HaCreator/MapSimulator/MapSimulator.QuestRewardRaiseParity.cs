using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private int _nextQuestRewardRaiseRequestId = 1;
        private readonly QuestRewardRaiseManagerRuntime _questRewardRaiseManager = new();

        private void WireQuestRewardRaiseWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is not QuestRewardRaiseWindow raiseWindow)
            {
                return;
            }

            raiseWindow.SetFont(_fontChat);
            raiseWindow.SetItemIconProvider(LoadInventoryItemIcon);
            raiseWindow.SelectionConfirmed -= HandleQuestRewardRaiseSelectionConfirmed;
            raiseWindow.SelectionConfirmed += HandleQuestRewardRaiseSelectionConfirmed;
            raiseWindow.PlacementConfirmed -= HandleQuestRewardRaisePlacementConfirmed;
            raiseWindow.PlacementConfirmed += HandleQuestRewardRaisePlacementConfirmed;
            raiseWindow.PieceDropRequested -= HandleQuestRewardRaisePieceDropRequested;
            raiseWindow.PieceDropRequested += HandleQuestRewardRaisePieceDropRequested;
            raiseWindow.PieceRemovalRequested -= HandleQuestRewardRaisePieceRemovalRequested;
            raiseWindow.PieceRemovalRequested += HandleQuestRewardRaisePieceRemovalRequested;
            raiseWindow.CancelRequested -= HandleQuestRewardRaiseCancelRequested;
            raiseWindow.CancelRequested += HandleQuestRewardRaiseCancelRequested;

            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (activeRaise != null && CanDisplayQuestRewardRaise(activeRaise))
            {
                raiseWindow.Configure(activeRaise);
            }
            else if (raiseWindow.IsVisible)
            {
                raiseWindow.DismissWithoutCancelling();
            }
        }

        private void OpenQuestRewardChoicePrompt(QuestRewardChoicePrompt prompt, QuestRewardRaiseSourceKind source)
        {
            QuestRewardRaiseWindowMode windowMode = prompt?.OwnerContext?.WindowMode ?? QuestRewardRaiseWindowMode.Selection;
            bool hasSelectionGroups = prompt?.Groups?.Count > 0;
            if (prompt == null || (!hasSelectionGroups && windowMode != QuestRewardRaiseWindowMode.PiecePlacement))
            {
                return;
            }

            Point defaultPosition = uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise)?.Position ?? Point.Zero;
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.Open(prompt, source, defaultPosition);
            if (activeRaise == null)
            {
                return;
            }

            ApplyQuestRewardRaiseQuestRecordContext(activeRaise);
            activeRaise.OpenDispatchSummary = BuildQuestRewardRaiseOpenSummary(activeRaise);

            if (windowMode != QuestRewardRaiseWindowMode.PiecePlacement && hasSelectionGroups)
            {
                activeRaise.DisplayMode = QuestRewardRaiseWindowMode.Selection;
            }

            if (source == QuestRewardRaiseSourceKind.NpcOverlay)
            {
                _npcInteractionOverlay?.Close();
            }

            ShowActiveQuestRewardRaiseGroup();
        }

        private void ShowActiveQuestRewardRaiseGroup()
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (activeRaise?.Prompt == null)
            {
                return;
            }

            if (activeRaise.DisplayMode != QuestRewardRaiseWindowMode.PiecePlacement
                && activeRaise.GroupIndex >= activeRaise.Prompt.Groups.Count)
            {
                ResolveQuestRewardRaise();
                return;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is not QuestRewardRaiseWindow raiseWindow)
            {
                return;
            }

            raiseWindow.Configure(activeRaise);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.QuestRewardRaise);
        }

        private void HandleQuestRewardRaiseSelectionConfirmed(int selectedItemId)
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (activeRaise?.Prompt?.Groups == null ||
                activeRaise.DisplayMode != QuestRewardRaiseWindowMode.Selection ||
                activeRaise.GroupIndex < 0 ||
                activeRaise.GroupIndex >= activeRaise.Prompt.Groups.Count)
            {
                DismissQuestRewardRaise(clearState: true, restorePlacedPieces: true);
                return;
            }

            QuestRewardChoiceGroup group = activeRaise.Prompt.Groups[activeRaise.GroupIndex];
            if (selectedItemId <= 0 || group.Options == null || !group.Options.Any(option => option.ItemId == selectedItemId))
            {
                _chat?.AddSystemMessage("That quest reward choice is no longer available.", currTickCount);
                DismissQuestRewardRaise(clearState: true, restorePlacedPieces: true);
                return;
            }

            activeRaise.SelectedItemsByGroup[group.GroupKey] = selectedItemId;
            activeRaise.GroupIndex++;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is QuestRewardRaiseWindow raiseWindow)
            {
                raiseWindow.DismissWithoutCancelling();
            }

            ShowActiveQuestRewardRaiseGroup();
        }

        private void HandleQuestRewardRaisePlacementConfirmed()
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (activeRaise == null || activeRaise.DisplayMode != QuestRewardRaiseWindowMode.PiecePlacement)
            {
                return;
            }

            if (activeRaise.Prompt?.Groups?.Count > 0)
            {
                activeRaise.DisplayMode = QuestRewardRaiseWindowMode.Selection;
                activeRaise.GroupIndex = 0;
                RefreshQuestRewardRaiseWindow();
                return;
            }

            ResolveQuestRewardRaise();
        }

        private void HandleQuestRewardRaisePieceDropRequested(QuestRewardRaisePieceDropRequest request)
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (activeRaise == null
                || activeRaise.DisplayMode != QuestRewardRaiseWindowMode.PiecePlacement
                || request.SlotData == null
                || request.SourceInventoryType == InventoryType.NONE
                || request.SourceSlotIndex < 0)
            {
                return;
            }

            if ((activeRaise.PlacedPieces?.Count ?? 0) >= activeRaise.MaxDropCount)
            {
                _chat?.AddSystemMessage("The raise window has no free piece slots.", currTickCount);
                return;
            }

            if (activeRaise.PlacedPieces.Any(piece =>
                    piece.InventoryType == request.SourceInventoryType &&
                    piece.SlotIndex == request.SourceSlotIndex))
            {
                _chat?.AddSystemMessage("That inventory slot is already queued in the raise window.", currTickCount);
                return;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return;
            }

            int requestId = GetNextQuestRewardRaiseRequestId();
            if (!inventoryWindow.TrySetPendingRequestState(request.SourceInventoryType, request.SourceSlotIndex, requestId, isPending: true))
            {
                _chat?.AddSystemMessage("The client could not reserve that inventory slot for the raise owner.", currTickCount);
                return;
            }

            activeRaise.PlacedPieces.Add(new QuestRewardRaisePlacedPiece
            {
                RequestId = requestId,
                InventoryType = request.SourceInventoryType,
                SlotIndex = request.SourceSlotIndex,
                ItemId = request.SlotData.ItemId,
                Quantity = 1,
                LifecycleState = QuestRewardRaisePieceLifecycleState.PendingAddAck,
                Label = string.IsNullOrWhiteSpace(request.SlotData.ItemName)
                    ? ResolveQuestRewardRaiseItemName(request.SlotData.ItemId)
                    : request.SlotData.ItemName.Trim()
            });

            QuestRewardRaisePlacedPiece placedPiece = activeRaise.PlacedPieces[^1];
            DispatchQuestRewardRaisePieceRequest(
                activeRaise,
                placedPiece,
                QuestRewardRaiseOutboundRequest.CreatePutItemAdd(activeRaise, placedPiece),
                out string dispatchSummary);
            activeRaise.OpenDispatchSummary = dispatchSummary;

            _chat?.AddSystemMessage(
                $"Queued raise PutItem request #{requestId} for {ResolveQuestRewardRaiseItemName(request.SlotData.ItemId)} on {request.SourceInventoryType} slot {request.SourceSlotIndex + 1}. {dispatchSummary}",
                currTickCount);
            RefreshQuestRewardRaiseWindow();
        }

        private void HandleQuestRewardRaisePieceRemovalRequested(int requestId)
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (activeRaise?.PlacedPieces == null || requestId <= 0)
            {
                return;
            }

            QuestRewardRaisePlacedPiece placedPiece = activeRaise.PlacedPieces.FirstOrDefault(piece => piece.RequestId == requestId);
            if (placedPiece == null || placedPiece.LifecycleState == QuestRewardRaisePieceLifecycleState.PendingReleaseAck)
            {
                return;
            }

            placedPiece.LifecycleState = QuestRewardRaisePieceLifecycleState.PendingReleaseAck;
            DispatchQuestRewardRaisePieceRequest(
                activeRaise,
                placedPiece,
                QuestRewardRaiseOutboundRequest.CreatePutItemRelease(activeRaise, placedPiece),
                out string dispatchSummary);
            activeRaise.OpenDispatchSummary = dispatchSummary;
            _chat?.AddSystemMessage(
                $"Queued raise PutItem release request #{requestId} for {ResolveQuestRewardRaiseItemName(placedPiece.ItemId)}. {dispatchSummary}",
                currTickCount);
            RefreshQuestRewardRaiseWindow();
        }

        private void HandleQuestRewardRaiseCancelRequested()
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            MarkQuestRewardRaiseOwnerDestroyPending(activeRaise);
            activeRaise = _questRewardRaiseManager.DestroyActiveRaise();
            RestoreQuestRewardRaisePlacedPieces(activeRaise);
            RetainQuestRewardRaiseObservedLifecycle(activeRaise);

            if (activeRaise?.Source == QuestRewardRaiseSourceKind.NpcOverlay && _activeNpcInteractionNpc != null)
            {
                OpenNpcInteraction(_activeNpcInteractionNpc, activeRaise.Prompt?.QuestId ?? 0);
            }
        }

        private void ResolveQuestRewardRaise()
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.DestroyActiveRaise();
            ClearQuestRewardRaiseWindow();

            if (activeRaise?.Prompt == null)
            {
                return;
            }

            if (!CommitQuestRewardRaisePlacedPieces(activeRaise))
            {
                RestoreQuestRewardRaisePlacedPieces(activeRaise);
                RetainQuestRewardRaiseObservedLifecycle(activeRaise);
                return;
            }

            _questRewardRaiseManager.RememberState(activeRaise);
            RetainQuestRewardRaiseObservedLifecycle(activeRaise);

            switch (activeRaise.Source)
            {
                case QuestRewardRaiseSourceKind.QuestWindow:
                    QuestWindowActionResult questWindowResult = activeRaise.Prompt.CompletionPhase
                        ? _questRuntime.TryCompleteFromQuestWindow(activeRaise.Prompt.QuestId, _playerManager?.Player?.Build, activeRaise.SelectedItemsByGroup)
                        : _questRuntime.TryAcceptFromQuestWindow(activeRaise.Prompt.QuestId, _playerManager?.Player?.Build, activeRaise.SelectedItemsByGroup);
                    HandleQuestWindowActionResult(questWindowResult);
                    break;

                case QuestRewardRaiseSourceKind.NpcOverlay:
                    if (activeRaise.Prompt.NpcId is not int npcId || npcId <= 0)
                    {
                        return;
                    }

                    QuestActionResult npcResult = _questRuntime.TryPerformPrimaryAction(
                        activeRaise.Prompt.QuestId,
                        npcId,
                        _playerManager?.Player?.Build,
                        activeRaise.SelectedItemsByGroup);
                    HandleNpcOverlayQuestActionResult(npcResult, activeRaise.Prompt.QuestId);
                    break;
            }
        }

        private bool CommitQuestRewardRaisePlacedPieces(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise?.PlacedPieces == null || activeRaise.PlacedPieces.Count == 0)
            {
                return true;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return false;
            }

            foreach (var groupedPiece in activeRaise.PlacedPieces
                         .GroupBy(piece => new { piece.InventoryType, piece.ItemId })
                         .Select(group => new
                         {
                             group.Key.InventoryType,
                             group.Key.ItemId,
                             Quantity = group.Sum(piece => Math.Max(1, piece.Quantity))
                         }))
            {
                if (inventoryWindow.GetItemCount(groupedPiece.InventoryType, groupedPiece.ItemId) < groupedPiece.Quantity)
                {
                    _chat?.AddSystemMessage(
                        $"The queued raise piece {ResolveQuestRewardRaiseItemName(groupedPiece.ItemId)} is no longer available in {groupedPiece.InventoryType}.",
                        currTickCount);
                    return false;
                }
            }

            foreach (QuestRewardRaisePlacedPiece piece in activeRaise.PlacedPieces)
            {
                inventoryWindow.TryClearPendingRequestState(piece.RequestId);
                if (!inventoryWindow.TryConsumeItem(piece.InventoryType, piece.ItemId, Math.Max(1, piece.Quantity)))
                {
                    _chat?.AddSystemMessage(
                        $"The queued raise piece {ResolveQuestRewardRaiseItemName(piece.ItemId)} could not be committed from {piece.InventoryType} slot {piece.SlotIndex + 1}.",
                        currTickCount);
                    return false;
                }
            }

            string confirmDispatchSummary = DispatchQuestRewardRaiseConfirmRequest(activeRaise);
            activeRaise.OpenDispatchSummary = confirmDispatchSummary;
            activeRaise.AwaitingConfirmAck = true;
            foreach (QuestRewardRaisePlacedPiece piece in activeRaise.PlacedPieces)
            {
                piece.LifecycleState = QuestRewardRaisePieceLifecycleState.PendingConfirmAck;
            }

            if (activeRaise.WindowMode == QuestRewardRaiseWindowMode.PiecePlacement)
            {
                _chat?.AddSystemMessage(
                    $"Committed {activeRaise.PlacedPieces.Count} local raise piece request{(activeRaise.PlacedPieces.Count == 1 ? string.Empty : "s")} for owner #{Math.Max(0, activeRaise.OwnerItemId)}. {confirmDispatchSummary}",
                    currTickCount);
            }

            return true;
        }

        private void RestoreQuestRewardRaisePlacedPieces(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise?.PlacedPieces == null || activeRaise.PlacedPieces.Count == 0)
            {
                return;
            }

            ClearQuestRewardRaisePlacedPieceReservations(activeRaise);

            IReadOnlyList<string> releaseSummaries = ReleaseQuestRewardRaisePlacedPieces(activeRaise);
            if (releaseSummaries.Count > 0)
            {
                activeRaise.OpenDispatchSummary = releaseSummaries[^1];
                _chat?.AddSystemMessage(
                    $"Destroyed raise owner #{Math.Max(0, activeRaise.OwnerItemId)} and mirrored {releaseSummaries.Count} PutItem release request{(releaseSummaries.Count == 1 ? string.Empty : "s")}. {releaseSummaries[^1]}",
                    currTickCount);
            }
        }

        private void RefreshQuestRewardRaiseWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is QuestRewardRaiseWindow raiseWindow)
            {
                raiseWindow.Configure(_questRewardRaiseManager.ActiveRaise);
            }
        }

        private void DismissQuestRewardRaise(bool clearState, bool restorePlacedPieces)
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (restorePlacedPieces)
            {
                RestoreQuestRewardRaisePlacedPieces(activeRaise);
            }

            if (clearState)
            {
                _questRewardRaiseManager.DestroyActiveRaise();
                RetainQuestRewardRaiseObservedLifecycle(activeRaise);
            }

            ClearQuestRewardRaiseWindow();
        }

        private bool CanDisplayQuestRewardRaise(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise?.Prompt == null)
            {
                return false;
            }

            return activeRaise.WindowMode == QuestRewardRaiseWindowMode.PiecePlacement
                || (activeRaise.Prompt.Groups != null
                    && activeRaise.GroupIndex >= 0
                    && activeRaise.GroupIndex < activeRaise.Prompt.Groups.Count);
        }

        private int GetNextQuestRewardRaiseRequestId()
        {
            int requestId = _nextQuestRewardRaiseRequestId++;
            if (requestId <= 0)
            {
                _nextQuestRewardRaiseRequestId = 2;
                requestId = 1;
            }

            return requestId;
        }

        private static string ResolveQuestRewardRaiseItemName(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName) && !string.IsNullOrWhiteSpace(resolvedName)
                ? resolvedName
                : $"Item #{Math.Max(0, itemId)}";
        }

        private static string BuildQuestRewardRaiseOpenSummary(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise == null)
            {
                return "Raise owner idle.";
            }

            return $"Opened raise owner session #{Math.Max(0, activeRaise.ManagerSessionId)} request #{Math.Max(0, activeRaise.RequestId)} for quest #{Math.Max(0, activeRaise.Prompt?.QuestId ?? 0)} owner #{Math.Max(0, activeRaise.OwnerItemId)} in {activeRaise.WindowMode} mode.";
        }

        private void DispatchQuestRewardRaisePieceRequest(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaisePlacedPiece placedPiece,
            QuestRewardRaiseOutboundRequest request,
            out string dispatchSummary)
        {
            dispatchSummary = DescribeQuestRewardRaiseOutboundDispatch(request);
            if (placedPiece == null)
            {
                return;
            }

            placedPiece.PacketOpcode = request?.Opcode ?? -1;
            placedPiece.PacketPayload = request?.Payload?.ToArray() ?? Array.Empty<byte>();
            placedPiece.DispatchSummary = dispatchSummary;
        }

        private IReadOnlyList<string> ReleaseQuestRewardRaisePlacedPieces(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise?.PlacedPieces == null || activeRaise.PlacedPieces.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> summaries = new(activeRaise.PlacedPieces.Count);
            foreach (QuestRewardRaisePlacedPiece piece in activeRaise.PlacedPieces)
            {
                if (piece.LifecycleState == QuestRewardRaisePieceLifecycleState.PendingReleaseAck)
                {
                    summaries.Add(string.IsNullOrWhiteSpace(piece.DispatchSummary)
                        ? $"Raise PutItem release request #{piece.RequestId} is already awaiting packet-owned acknowledgement."
                        : piece.DispatchSummary);
                    continue;
                }

                DispatchQuestRewardRaisePieceRequest(
                    activeRaise,
                    piece,
                    QuestRewardRaiseOutboundRequest.CreatePutItemRelease(activeRaise, piece),
                    out string dispatchSummary);
                piece.LifecycleState = QuestRewardRaisePieceLifecycleState.PendingReleaseAck;
                summaries.Add(dispatchSummary);
            }

            return summaries;
        }

        private string DispatchQuestRewardRaiseConfirmRequest(QuestRewardRaiseState activeRaise)
        {
            return activeRaise == null
                ? "Raise owner confirm dispatch was skipped because no active raise exists."
                : DescribeQuestRewardRaiseOutboundDispatch(QuestRewardRaiseOutboundRequest.CreatePutItemConfirm(activeRaise));
        }

        private static void MarkQuestRewardRaiseOwnerDestroyPending(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise == null)
            {
                return;
            }

            activeRaise.AwaitingOwnerDestroyAck = true;
            activeRaise.OpenDispatchSummary =
                $"Client-shaped raise owner destroy requested for owner #{Math.Max(0, activeRaise.OwnerItemId)} quest #{Math.Max(0, activeRaise.Prompt?.QuestId ?? 0)} session #{Math.Max(0, activeRaise.ManagerSessionId)}.";
        }

        private string DescribeQuestRewardRaiseOutboundDispatch(QuestRewardRaiseOutboundRequest request)
        {
            if (request == null)
            {
                return "Raise owner did not produce an outbound packet request.";
            }

            string payloadHex = Convert.ToHexString(request.Payload?.ToArray() ?? Array.Empty<byte>());
            string bridgeStatus;
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out bridgeStatus))
            {
                return $"{request.Summary} [{payloadHex}] dispatched through the live local-utility bridge. {bridgeStatus}";
            }

            string outboxStatus;
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out outboxStatus))
            {
                return $"{request.Summary} [{payloadHex}] dispatched through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out bridgeDeferredStatus))
            {
                return $"{request.Summary} [{payloadHex}] queued for deferred live official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus}";
            }

            string queuedStatus;
            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out queuedStatus))
            {
                return $"{request.Summary} [{payloadHex}] queued for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
            }

            return $"{request.Summary} [{payloadHex}] remained simulator-owned because neither the live local-utility bridge nor the deferred official-session bridge queue nor the generic outbox transport or deferred outbox queue accepted it. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
        }

        private bool TryApplyPacketOwnedQuestRewardRaisePayload(
            QuestRewardRaiseInboundPacketKind kind,
            byte[] payload,
            out string message)
        {
            message = null;
            if (!QuestRewardRaiseInboundPacketCodec.TryDecode(kind, payload, out QuestRewardRaiseInboundPacket packet, out string error))
            {
                message = error ?? "Raise inbound payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            message = ApplyPacketOwnedQuestRewardRaisePacket(packet);
            RefreshQuestRewardRaiseWindow();
            return true;
        }

        private string ApplyPacketOwnedQuestRewardRaisePacket(QuestRewardRaiseInboundPacket packet)
        {
            QuestRewardRaisePacketPayload decodedPayload = packet?.Payload;
            if (decodedPayload == null)
            {
                return "Raise inbound packet did not include decoded owner state.";
            }

            int questId = Math.Max(0, decodedPayload.QuestId);
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.ActiveRaise;
            if (packet.Kind == QuestRewardRaiseInboundPacketKind.OwnerSync && decodedPayload.QuestId > 0)
            {
                _questRewardRaiseManager.ObserveOwnerState(
                    decodedPayload.QuestId,
                    decodedPayload.OwnerItemId,
                    decodedPayload.QrData,
                    ResolveQuestRewardRaiseInboundMaxDropCount(activeRaise, decodedPayload),
                    decodedPayload.WindowMode,
                    decodedPayload.DisplayMode);
            }

            QuestRewardRaiseState observedRaise = activeRaise != null
                && Math.Max(0, activeRaise.Prompt?.QuestId ?? 0) == questId
                ? activeRaise
                : _questRewardRaiseManager.GetObservedRaiseByQuestId(questId);
            bool raiseIsActive = ReferenceEquals(observedRaise, activeRaise);
            if (observedRaise == null)
            {
                string inactiveSummary = DescribePacketOwnedQuestRewardRaisePacket(packet, null);
                _questRewardRaiseManager.ObserveInboundPacket(packet, inactiveSummary);
                return inactiveSummary;
            }

            SyncActiveQuestRewardRaiseFromInboundPayload(observedRaise, decodedPayload);
            string inboundSummary = DescribePacketOwnedQuestRewardRaisePacket(packet, observedRaise);
            observedRaise.LastInboundSummary = inboundSummary;

            switch (packet.Kind)
            {
                case QuestRewardRaiseInboundPacketKind.OwnerSync:
                    break;

                case QuestRewardRaiseInboundPacketKind.PutItemAddResult:
                    ApplyPacketOwnedQuestRewardRaiseAddResult(observedRaise, packet);
                    break;

                case QuestRewardRaiseInboundPacketKind.PutItemReleaseResult:
                    ApplyPacketOwnedQuestRewardRaiseReleaseResult(observedRaise, packet);
                    break;

                case QuestRewardRaiseInboundPacketKind.PutItemConfirmResult:
                    observedRaise.AwaitingConfirmAck = false;
                    foreach (QuestRewardRaisePlacedPiece piece in observedRaise.PlacedPieces)
                    {
                        piece.LifecycleState = packet.Success
                            ? QuestRewardRaisePieceLifecycleState.Confirmed
                            : QuestRewardRaisePieceLifecycleState.Active;
                    }
                    break;

                case QuestRewardRaiseInboundPacketKind.OwnerDestroyResult:
                    observedRaise.AwaitingOwnerDestroyAck = false;
                    ClearQuestRewardRaisePlacedPieceReservations(observedRaise);
                    if (raiseIsActive)
                    {
                        _questRewardRaiseManager.ClearActiveRaise();
                        ClearQuestRewardRaiseWindow();
                    }
                    else
                    {
                        _questRewardRaiseManager.ClearRetainedRaiseByQuestId(questId);
                    }
                    break;
            }

            _questRewardRaiseManager.ObserveInboundPacket(packet, inboundSummary);
            return observedRaise.LastInboundSummary;
        }

        private void ApplyPacketOwnedQuestRewardRaiseAddResult(QuestRewardRaiseState activeRaise, QuestRewardRaiseInboundPacket packet)
        {
            QuestRewardRaisePlacedPiece placedPiece = ResolveQuestRewardRaisePlacedPiece(activeRaise, packet, createIfMissing: packet.Success);
            if (placedPiece == null)
            {
                return;
            }

            ApplyPacketOwnedQuestRewardRaisePieceInboundState(placedPiece, packet);
            if (packet.Success)
            {
                placedPiece.LifecycleState = QuestRewardRaisePieceLifecycleState.Active;
                return;
            }

            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow)
            {
                inventoryWindow.TryClearPendingRequestState(placedPiece.RequestId);
            }

            activeRaise.PlacedPieces.Remove(placedPiece);
        }

        private void ApplyPacketOwnedQuestRewardRaiseReleaseResult(QuestRewardRaiseState activeRaise, QuestRewardRaiseInboundPacket packet)
        {
            QuestRewardRaisePlacedPiece placedPiece = ResolveQuestRewardRaisePlacedPiece(activeRaise, packet, createIfMissing: !packet.Success);
            if (placedPiece == null)
            {
                return;
            }

            ApplyPacketOwnedQuestRewardRaisePieceInboundState(placedPiece, packet);
            if (packet.Success)
            {
                if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow)
                {
                    inventoryWindow.TryClearPendingRequestState(placedPiece.RequestId);
                }

                activeRaise.PlacedPieces.Remove(placedPiece);
                return;
            }

            placedPiece.LifecycleState = QuestRewardRaisePieceLifecycleState.Active;
        }

        private QuestRewardRaisePlacedPiece ResolveQuestRewardRaisePlacedPiece(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaiseInboundPacket packet,
            bool createIfMissing)
        {
            if (activeRaise?.PlacedPieces == null || packet?.Payload == null)
            {
                return null;
            }

            QuestRewardRaisePacketPayload payload = packet.Payload;
            QuestRewardRaisePlacedPiece placedPiece = TryMatchQuestRewardRaisePlacedPieceByRequestId(activeRaise, payload);
            if (placedPiece != null)
            {
                return placedPiece;
            }

            placedPiece = TryMatchQuestRewardRaisePlacedPieceByInventorySlot(activeRaise, payload);
            if (placedPiece != null)
            {
                return placedPiece;
            }

            placedPiece = TryMatchQuestRewardRaisePlacedPieceByItem(activeRaise, payload);
            if (placedPiece != null || !createIfMissing)
            {
                return placedPiece;
            }

            if (payload.ItemId <= 0)
            {
                return null;
            }

            placedPiece = new QuestRewardRaisePlacedPiece
            {
                RequestId = ResolveSyntheticQuestRewardRaiseRequestId(activeRaise, payload),
                InventoryType = payload.InventoryType,
                SlotIndex = Math.Max(-1, payload.SlotIndex),
                ItemId = Math.Max(0, payload.ItemId),
                Quantity = Math.Max(1, payload.Quantity),
                Label = ResolveQuestRewardRaiseItemName(payload.ItemId),
                PacketOpcode = packet.Kind switch
                {
                    QuestRewardRaiseInboundPacketKind.PutItemAddResult => QuestRewardRaiseOutboundRequest.PutItemAddOpcode,
                    QuestRewardRaiseInboundPacketKind.PutItemReleaseResult => QuestRewardRaiseOutboundRequest.PutItemReleaseOpcode,
                    _ => -1
                },
                LifecycleState = packet.Kind == QuestRewardRaiseInboundPacketKind.PutItemReleaseResult
                    ? QuestRewardRaisePieceLifecycleState.PendingReleaseAck
                    : QuestRewardRaisePieceLifecycleState.PendingAddAck
            };
            activeRaise.PlacedPieces.Add(placedPiece);
            return placedPiece;
        }

        private int ResolveSyntheticQuestRewardRaiseRequestId(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaisePacketPayload payload)
        {
            int inboundRequestId = Math.Max(0, payload?.PieceRequestId ?? 0);
            if (inboundRequestId > 0
                && activeRaise?.PlacedPieces?.All(piece => piece.RequestId != inboundRequestId) != false)
            {
                return inboundRequestId;
            }

            int syntheticRequestId;
            do
            {
                syntheticRequestId = GetNextQuestRewardRaiseRequestId();
            }
            while (syntheticRequestId <= 0
                   || activeRaise?.PlacedPieces?.Any(piece => piece.RequestId == syntheticRequestId) == true);

            return syntheticRequestId;
        }

        private static QuestRewardRaisePlacedPiece TryMatchQuestRewardRaisePlacedPieceByRequestId(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaisePacketPayload payload)
        {
            if (activeRaise?.PlacedPieces == null || payload == null || payload.PieceRequestId <= 0)
            {
                return null;
            }

            return activeRaise.PlacedPieces.FirstOrDefault(piece => piece.RequestId == payload.PieceRequestId);
        }

        private static QuestRewardRaisePlacedPiece TryMatchQuestRewardRaisePlacedPieceByInventorySlot(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaisePacketPayload payload)
        {
            if (activeRaise?.PlacedPieces == null
                || payload == null
                || payload.InventoryType == InventoryType.NONE
                || payload.SlotIndex < 0)
            {
                return null;
            }

            return activeRaise.PlacedPieces.FirstOrDefault(piece =>
                piece.InventoryType == payload.InventoryType
                && piece.SlotIndex == payload.SlotIndex
                && (payload.ItemId <= 0 || piece.ItemId == payload.ItemId));
        }

        private static QuestRewardRaisePlacedPiece TryMatchQuestRewardRaisePlacedPieceByItem(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaisePacketPayload payload)
        {
            if (activeRaise?.PlacedPieces == null || payload == null || payload.ItemId <= 0)
            {
                return null;
            }

            QuestRewardRaisePlacedPiece[] matches = activeRaise.PlacedPieces
                .Where(piece => piece.ItemId == payload.ItemId)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static void ApplyPacketOwnedQuestRewardRaisePieceInboundState(
            QuestRewardRaisePlacedPiece placedPiece,
            QuestRewardRaiseInboundPacket packet)
        {
            if (placedPiece == null || packet == null)
            {
                return;
            }

            placedPiece.LastInboundPacketType = packet.Kind switch
            {
                QuestRewardRaiseInboundPacketKind.OwnerSync => LocalUtilityPacketInboxManager.QuestRewardRaiseOwnerSyncPacketType,
                QuestRewardRaiseInboundPacketKind.PutItemAddResult => LocalUtilityPacketInboxManager.QuestRewardRaisePutItemAddResultPacketType,
                QuestRewardRaiseInboundPacketKind.PutItemReleaseResult => LocalUtilityPacketInboxManager.QuestRewardRaisePutItemReleaseResultPacketType,
                QuestRewardRaiseInboundPacketKind.PutItemConfirmResult => LocalUtilityPacketInboxManager.QuestRewardRaisePutItemConfirmResultPacketType,
                QuestRewardRaiseInboundPacketKind.OwnerDestroyResult => LocalUtilityPacketInboxManager.QuestRewardRaiseOwnerDestroyResultPacketType,
                _ => -1
            };
            placedPiece.LastInboundPayload = packet.RawPayload?.ToArray() ?? Array.Empty<byte>();
            placedPiece.LastInboundSummary = DescribePacketOwnedQuestRewardRaisePacket(packet, null);
        }

        private void ClearQuestRewardRaisePlacedPieceReservations(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise?.PlacedPieces == null || uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return;
            }

            foreach (QuestRewardRaisePlacedPiece piece in activeRaise.PlacedPieces)
            {
                inventoryWindow.TryClearPendingRequestState(piece.RequestId);
            }
        }

        private static void SyncActiveQuestRewardRaiseFromInboundPayload(QuestRewardRaiseState activeRaise, QuestRewardRaisePacketPayload decodedPayload)
        {
            if (activeRaise == null || decodedPayload == null)
            {
                return;
            }

            activeRaise.ManagerSessionId = Math.Max(0, decodedPayload.ManagerSessionId);
            activeRaise.RequestId = Math.Max(0, decodedPayload.OwnerRequestId);
            activeRaise.OwnerItemId = Math.Max(0, decodedPayload.OwnerItemId);
            activeRaise.QrData = decodedPayload.QrData;
            activeRaise.MaxDropCount = ResolveQuestRewardRaiseInboundMaxDropCount(activeRaise, decodedPayload);
            activeRaise.WindowMode = decodedPayload.WindowMode;
            activeRaise.DisplayMode = decodedPayload.DisplayMode;
            activeRaise.SyncSelectionProgressFromPayload(decodedPayload);
        }

        private static int ResolveQuestRewardRaiseInboundMaxDropCount(
            QuestRewardRaiseState activeRaise,
            QuestRewardRaisePacketPayload decodedPayload)
        {
            return Math.Max(
                1,
                Math.Max(
                    Math.Max(activeRaise?.MaxDropCount ?? 1, decodedPayload?.PlacedPieceCount ?? 0),
                    decodedPayload?.MaxDropCount ?? 0));
        }

        private void RetainQuestRewardRaiseObservedLifecycle(QuestRewardRaiseState activeRaise)
        {
            int questId = Math.Max(0, activeRaise?.Prompt?.QuestId ?? 0);
            if (activeRaise == null || questId <= 0)
            {
                return;
            }

            bool shouldRetain = activeRaise.WindowMode == QuestRewardRaiseWindowMode.PiecePlacement
                || activeRaise.PlacedPieces.Count > 0
                || activeRaise.SelectedItemsByGroup.Count > 0
                || activeRaise.GroupIndex > 0
                || activeRaise.AwaitingConfirmAck
                || activeRaise.AwaitingOwnerDestroyAck
                || !string.IsNullOrWhiteSpace(activeRaise.LastInboundSummary)
                || !string.IsNullOrWhiteSpace(activeRaise.OpenDispatchSummary);
            if (!shouldRetain)
            {
                return;
            }

            _questRewardRaiseManager.RetainClosedRaise(activeRaise);
        }

        private static string DescribePacketOwnedQuestRewardRaisePacket(QuestRewardRaiseInboundPacket packet, QuestRewardRaiseState activeRaise)
        {
            QuestRewardRaisePacketPayload payload = packet?.Payload;
            if (packet == null || payload == null)
            {
                return "Raise inbound packet is unavailable.";
            }

            string ownerSummary = $"owner #{Math.Max(0, payload.OwnerItemId)} quest #{Math.Max(0, payload.QuestId)} session #{Math.Max(0, payload.ManagerSessionId)} request #{Math.Max(0, payload.OwnerRequestId)}";
            return packet.Kind switch
            {
                QuestRewardRaiseInboundPacketKind.OwnerSync => $"Packet-owned raise owner sync refreshed {ownerSummary} with QR {payload.QrData} in {payload.DisplayMode} mode.",
                QuestRewardRaiseInboundPacketKind.PutItemAddResult => $"Packet-owned raise PutItem add {(packet.Success ? "accepted" : "rejected")} row #{Math.Max(0, payload.PieceRequestId)} for {ownerSummary}.",
                QuestRewardRaiseInboundPacketKind.PutItemReleaseResult => $"Packet-owned raise PutItem release {(packet.Success ? "accepted" : "rejected")} row #{Math.Max(0, payload.PieceRequestId)} for {ownerSummary}.",
                QuestRewardRaiseInboundPacketKind.PutItemConfirmResult => $"Packet-owned raise PutItem confirm {(packet.Success ? "accepted" : "rejected")} for {ownerSummary}.",
                QuestRewardRaiseInboundPacketKind.OwnerDestroyResult => $"Packet-owned raise owner destroy acknowledged {ownerSummary}.",
                _ => "Raise inbound packet was observed."
            };
        }

        private void ApplyQuestRewardRaiseQuestRecordContext(QuestRewardRaiseState activeRaise)
        {
            int questId = Math.Max(0, activeRaise?.Prompt?.QuestId ?? 0);
            if (activeRaise == null
                || activeRaise.Prompt?.OwnerContext != null
                || questId <= 0
                || _questRuntime == null
                || !_questRuntime.TryGetQuestRecordValue(questId, out string questRecordValue)
                || !TryParseQuestRewardRaiseQrData(questRecordValue, out int qrData))
            {
                return;
            }

            activeRaise.QrData = qrData;
            _questRewardRaiseManager.ObserveOwnerState(
                questId,
                activeRaise.OwnerItemId,
                qrData,
                activeRaise.MaxDropCount,
                activeRaise.WindowMode,
                activeRaise.DisplayMode);
        }

        private static bool TryParseQuestRewardRaiseQrData(string text, out int qrData)
        {
            qrData = 0;
            return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out qrData);
        }

        private void ClearQuestRewardRaiseWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is QuestRewardRaiseWindow raiseWindow &&
                raiseWindow.IsVisible)
            {
                raiseWindow.DismissWithoutCancelling();
            }
        }
    }
}
