using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private int _nextQuestRewardRaiseRequestId = 1;
        private QuestRewardRaiseState _activeQuestRewardRaise;

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

            if (_activeQuestRewardRaise != null && CanDisplayQuestRewardRaise(_activeQuestRewardRaise))
            {
                raiseWindow.Configure(_activeQuestRewardRaise);
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

            _activeQuestRewardRaise = new QuestRewardRaiseState
            {
                Source = source,
                Prompt = prompt,
                GroupIndex = 0,
                OwnerItemId = Math.Max(0, prompt.OwnerContext?.OwnerItemId ?? 0),
                QrData = prompt.OwnerContext?.InitialQrData ?? 0,
                MaxDropCount = Math.Max(1, prompt.OwnerContext?.MaxDropCount ?? 1),
                WindowMode = windowMode
            };

            if (source == QuestRewardRaiseSourceKind.NpcOverlay)
            {
                _npcInteractionOverlay?.Close();
            }

            ShowActiveQuestRewardRaiseGroup();
        }

        private void ShowActiveQuestRewardRaiseGroup()
        {
            if (_activeQuestRewardRaise?.Prompt == null)
            {
                return;
            }

            if (_activeQuestRewardRaise.WindowMode != QuestRewardRaiseWindowMode.PiecePlacement
                && _activeQuestRewardRaise.GroupIndex >= _activeQuestRewardRaise.Prompt.Groups.Count)
            {
                ResolveQuestRewardRaise();
                return;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is not QuestRewardRaiseWindow raiseWindow)
            {
                return;
            }

            raiseWindow.Configure(_activeQuestRewardRaise);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.QuestRewardRaise);
        }

        private void HandleQuestRewardRaiseSelectionConfirmed(int selectedItemId)
        {
            if (_activeQuestRewardRaise?.Prompt?.Groups == null ||
                _activeQuestRewardRaise.GroupIndex < 0 ||
                _activeQuestRewardRaise.GroupIndex >= _activeQuestRewardRaise.Prompt.Groups.Count)
            {
                DismissQuestRewardRaise(clearState: true, restorePlacedPieces: true);
                return;
            }

            QuestRewardChoiceGroup group = _activeQuestRewardRaise.Prompt.Groups[_activeQuestRewardRaise.GroupIndex];
            if (selectedItemId <= 0 || group.Options == null || !group.Options.Any(option => option.ItemId == selectedItemId))
            {
                _chat?.AddSystemMessage("That quest reward choice is no longer available.", currTickCount);
                DismissQuestRewardRaise(clearState: true, restorePlacedPieces: true);
                return;
            }

            _activeQuestRewardRaise.SelectedItemsByGroup[group.GroupKey] = selectedItemId;
            _activeQuestRewardRaise.GroupIndex++;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is QuestRewardRaiseWindow raiseWindow)
            {
                raiseWindow.DismissWithoutCancelling();
            }

            ShowActiveQuestRewardRaiseGroup();
        }

        private void HandleQuestRewardRaisePlacementConfirmed()
        {
            if (_activeQuestRewardRaise == null || _activeQuestRewardRaise.WindowMode != QuestRewardRaiseWindowMode.PiecePlacement)
            {
                return;
            }

            ResolveQuestRewardRaise();
        }

        private void HandleQuestRewardRaisePieceDropRequested(QuestRewardRaisePieceDropRequest request)
        {
            if (_activeQuestRewardRaise == null
                || _activeQuestRewardRaise.WindowMode != QuestRewardRaiseWindowMode.PiecePlacement
                || request.SlotData == null
                || request.SourceInventoryType == InventoryType.NONE
                || request.SourceSlotIndex < 0)
            {
                return;
            }

            if ((_activeQuestRewardRaise.PlacedPieces?.Count ?? 0) >= _activeQuestRewardRaise.MaxDropCount)
            {
                _chat?.AddSystemMessage("The raise window has no free piece slots.", currTickCount);
                return;
            }

            if (_activeQuestRewardRaise.PlacedPieces.Any(piece =>
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

            _activeQuestRewardRaise.PlacedPieces.Add(new QuestRewardRaisePlacedPiece
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
            if (_activeQuestRewardRaise?.PlacedPieces == null || requestId <= 0)
            {
                return;
            }

            QuestRewardRaisePlacedPiece placedPiece = _activeQuestRewardRaise.PlacedPieces.FirstOrDefault(piece => piece.RequestId == requestId);
            if (placedPiece == null)
            {
                return;
            }

            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow)
            {
                inventoryWindow.TryClearPendingRequestState(requestId);
            }

            _activeQuestRewardRaise.PlacedPieces.RemoveAll(piece => piece.RequestId == requestId);
            _chat?.AddSystemMessage(
                $"Released raise PutItem request #{requestId} for {ResolveQuestRewardRaiseItemName(placedPiece.ItemId)}.",
                currTickCount);
            RefreshQuestRewardRaiseWindow();
        }

        private void HandleQuestRewardRaiseCancelRequested()
        {
            QuestRewardRaiseState activeRaise = _activeQuestRewardRaise;
            RestoreQuestRewardRaisePlacedPieces(activeRaise);
            _activeQuestRewardRaise = null;

            if (activeRaise?.Source == QuestRewardRaiseSourceKind.NpcOverlay && _activeNpcInteractionNpc != null)
            {
                OpenNpcInteraction(_activeNpcInteractionNpc, activeRaise.Prompt?.QuestId ?? 0);
            }
        }

        private void ResolveQuestRewardRaise()
        {
            QuestRewardRaiseState activeRaise = _activeQuestRewardRaise;
            _activeQuestRewardRaise = null;
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
                raiseWindow.Configure(_activeQuestRewardRaise);
            }
        }

        private void DismissQuestRewardRaise(bool clearState, bool restorePlacedPieces)
        {
            QuestRewardRaiseState activeRaise = _activeQuestRewardRaise;
            if (restorePlacedPieces)
            {
                RestoreQuestRewardRaisePlacedPieces(activeRaise);
            }

            if (clearState)
            {
                _activeQuestRewardRaise = null;
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
