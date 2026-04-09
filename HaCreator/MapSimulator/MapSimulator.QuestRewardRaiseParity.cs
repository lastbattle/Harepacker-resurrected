using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using System;
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
                Label = string.IsNullOrWhiteSpace(request.SlotData.ItemName)
                    ? ResolveQuestRewardRaiseItemName(request.SlotData.ItemId)
                    : request.SlotData.ItemName.Trim()
            });

            _chat?.AddSystemMessage(
                $"Queued raise PutItem request #{requestId} for {ResolveQuestRewardRaiseItemName(request.SlotData.ItemId)} on {request.SourceInventoryType} slot {request.SourceSlotIndex + 1}.",
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
            if (placedPiece == null)
            {
                return;
            }

            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow)
            {
                inventoryWindow.TryClearPendingRequestState(requestId);
            }

            activeRaise.PlacedPieces.RemoveAll(piece => piece.RequestId == requestId);
            _chat?.AddSystemMessage(
                $"Released raise PutItem request #{requestId} for {ResolveQuestRewardRaiseItemName(placedPiece.ItemId)}.",
                currTickCount);
            RefreshQuestRewardRaiseWindow();
        }

        private void HandleQuestRewardRaiseCancelRequested()
        {
            QuestRewardRaiseState activeRaise = _questRewardRaiseManager.DestroyActiveRaise();
            RestoreQuestRewardRaisePlacedPieces(activeRaise);

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
                return;
            }

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

            if (activeRaise.WindowMode == QuestRewardRaiseWindowMode.PiecePlacement)
            {
                _chat?.AddSystemMessage(
                    $"Committed {activeRaise.PlacedPieces.Count} local raise piece request{(activeRaise.PlacedPieces.Count == 1 ? string.Empty : "s")} for owner #{Math.Max(0, activeRaise.OwnerItemId)}.",
                    currTickCount);
            }

            return true;
        }

        private void RestoreQuestRewardRaisePlacedPieces(QuestRewardRaiseState activeRaise)
        {
            if (activeRaise?.PlacedPieces == null || activeRaise.PlacedPieces.Count == 0 || uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return;
            }

            foreach (QuestRewardRaisePlacedPiece piece in activeRaise.PlacedPieces)
            {
                inventoryWindow.TryClearPendingRequestState(piece.RequestId);
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
