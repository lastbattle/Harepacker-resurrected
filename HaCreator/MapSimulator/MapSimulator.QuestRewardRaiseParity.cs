using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
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
            raiseWindow.CancelRequested -= HandleQuestRewardRaiseCancelRequested;
            raiseWindow.CancelRequested += HandleQuestRewardRaiseCancelRequested;

            if (_activeQuestRewardRaise?.Prompt?.Groups != null &&
                _activeQuestRewardRaise.GroupIndex >= 0 &&
                _activeQuestRewardRaise.GroupIndex < _activeQuestRewardRaise.Prompt.Groups.Count)
            {
                raiseWindow.Configure(_activeQuestRewardRaise.Prompt, _activeQuestRewardRaise.GroupIndex);
            }
            else if (raiseWindow.IsVisible)
            {
                raiseWindow.DismissWithoutCancelling();
            }
        }

        private void OpenQuestRewardChoicePrompt(QuestRewardChoicePrompt prompt, QuestRewardRaiseSourceKind source)
        {
            if (prompt?.Groups == null || prompt.Groups.Count == 0)
            {
                return;
            }

            _activeQuestRewardRaise = new QuestRewardRaiseState
            {
                Source = source,
                Prompt = prompt,
                GroupIndex = 0
            };

            if (source == QuestRewardRaiseSourceKind.NpcOverlay)
            {
                _npcInteractionOverlay?.Close();
            }

            ShowActiveQuestRewardRaiseGroup();
        }

        private void ShowActiveQuestRewardRaiseGroup()
        {
            if (_activeQuestRewardRaise?.Prompt?.Groups == null)
            {
                return;
            }

            if (_activeQuestRewardRaise.GroupIndex >= _activeQuestRewardRaise.Prompt.Groups.Count)
            {
                ResolveQuestRewardRaise();
                return;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) is not QuestRewardRaiseWindow raiseWindow)
            {
                return;
            }

            raiseWindow.Configure(_activeQuestRewardRaise.Prompt, _activeQuestRewardRaise.GroupIndex);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.QuestRewardRaise);
        }

        private void HandleQuestRewardRaiseSelectionConfirmed(int selectedItemId)
        {
            if (_activeQuestRewardRaise?.Prompt?.Groups == null ||
                _activeQuestRewardRaise.GroupIndex < 0 ||
                _activeQuestRewardRaise.GroupIndex >= _activeQuestRewardRaise.Prompt.Groups.Count)
            {
                _activeQuestRewardRaise = null;
                ClearQuestRewardRaiseWindow();
                return;
            }

            QuestRewardChoiceGroup group = _activeQuestRewardRaise.Prompt.Groups[_activeQuestRewardRaise.GroupIndex];
            if (selectedItemId <= 0 || group.Options == null || !group.Options.Any(option => option.ItemId == selectedItemId))
            {
                _chat?.AddSystemMessage("That quest reward choice is no longer available.", currTickCount);
                _activeQuestRewardRaise = null;
                ClearQuestRewardRaiseWindow();
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

        private void HandleQuestRewardRaiseCancelRequested()
        {
            QuestRewardRaiseState activeRaise = _activeQuestRewardRaise;
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
