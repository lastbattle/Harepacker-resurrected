using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class QuestRewardRaiseManagerRuntime
    {
        private sealed record QuestRewardRaiseOwnerSnapshot(
            int OwnerItemId,
            int QrData,
            int MaxDropCount,
            QuestRewardRaiseWindowMode WindowMode);

        private int _nextManagerSessionId = 1;
        private int _nextOwnerRequestId = 1;
        private readonly System.Collections.Generic.Dictionary<int, QuestRewardRaiseOwnerSnapshot> _ownerSnapshotsByQuestId = new();
        private readonly System.Collections.Generic.Dictionary<int, Point> _windowPositionsByOwnerItemId = new();

        public QuestRewardRaiseState ActiveRaise { get; private set; }

        public QuestRewardRaiseState Open(QuestRewardChoicePrompt prompt, QuestRewardRaiseSourceKind source, Point defaultPosition)
        {
            if (prompt == null)
            {
                ActiveRaise = null;
                return null;
            }

            int questId = Math.Max(0, prompt.QuestId);
            _ownerSnapshotsByQuestId.TryGetValue(questId, out QuestRewardRaiseOwnerSnapshot snapshot);

            QuestRewardRaiseWindowMode windowMode = prompt.OwnerContext?.WindowMode
                ?? snapshot?.WindowMode
                ?? QuestRewardRaiseWindowMode.Selection;
            int ownerItemId = Math.Max(0, prompt.OwnerContext?.OwnerItemId ?? snapshot?.OwnerItemId ?? 0);
            int qrData = prompt.OwnerContext?.InitialQrData ?? snapshot?.QrData ?? 0;
            int maxDropCount = Math.Max(1, prompt.OwnerContext?.MaxDropCount ?? snapshot?.MaxDropCount ?? 1);
            Point windowPosition = defaultPosition;
            if (ActiveRaise != null
                && ActiveRaise.Prompt?.QuestId == prompt.QuestId
                && ActiveRaise.OwnerItemId == ownerItemId
                && ActiveRaise.WindowMode == windowMode
                && ActiveRaise.WindowPosition != Point.Zero)
            {
                windowPosition = ActiveRaise.WindowPosition;
            }
            else if (ownerItemId > 0
                && _windowPositionsByOwnerItemId.TryGetValue(ownerItemId, out Point backupPosition)
                && backupPosition != Point.Zero)
            {
                windowPosition = backupPosition;
            }

            ActiveRaise = new QuestRewardRaiseState
            {
                Source = source,
                Prompt = prompt,
                GroupIndex = 0,
                ManagerSessionId = GetNextManagerSessionId(),
                RequestId = GetNextOwnerRequestId(),
                OwnerItemId = ownerItemId,
                QrData = qrData,
                MaxDropCount = maxDropCount,
                WindowMode = windowMode,
                DisplayMode = windowMode,
                WindowPosition = windowPosition
            };

            if (questId > 0)
            {
                _ownerSnapshotsByQuestId[questId] = new QuestRewardRaiseOwnerSnapshot(ownerItemId, qrData, maxDropCount, windowMode);
            }

            return ActiveRaise;
        }

        public QuestRewardRaiseState Restore(QuestRewardRaiseState state)
        {
            ActiveRaise = state;
            return ActiveRaise;
        }

        public QuestRewardRaiseState DestroyActiveRaise()
        {
            QuestRewardRaiseState activeRaise = ActiveRaise;
            BackupWindowPosition(activeRaise);
            ActiveRaise = null;
            return activeRaise;
        }

        public bool TrySetQrDataForQuest(int questId, int qrData, out QuestRewardRaiseState updatedState)
        {
            updatedState = null;
            questId = Math.Max(0, questId);
            if (questId <= 0)
            {
                return false;
            }

            if (_ownerSnapshotsByQuestId.TryGetValue(questId, out QuestRewardRaiseOwnerSnapshot snapshot))
            {
                _ownerSnapshotsByQuestId[questId] = snapshot with { QrData = qrData };
            }

            if (ActiveRaise?.Prompt?.QuestId != questId)
            {
                return snapshot != null;
            }

            ActiveRaise.QrData = qrData;
            updatedState = ActiveRaise;
            return true;
        }

        public QuestRewardRaiseState DestroyByQuestId(int questId)
        {
            questId = Math.Max(0, questId);
            if (questId <= 0 || ActiveRaise?.Prompt?.QuestId != questId)
            {
                return null;
            }

            return DestroyActiveRaise();
        }

        private void BackupWindowPosition(QuestRewardRaiseState state)
        {
            if (state?.OwnerItemId > 0 && state.WindowPosition != Point.Zero)
            {
                _windowPositionsByOwnerItemId[state.OwnerItemId] = state.WindowPosition;
            }

            int questId = Math.Max(0, state?.Prompt?.QuestId ?? 0);
            if (questId > 0)
            {
                _ownerSnapshotsByQuestId[questId] = new QuestRewardRaiseOwnerSnapshot(
                    Math.Max(0, state.OwnerItemId),
                    state.QrData,
                    Math.Max(1, state.MaxDropCount),
                    state.WindowMode);
            }
        }

        private int GetNextManagerSessionId()
        {
            int sessionId = _nextManagerSessionId++;
            if (sessionId <= 0)
            {
                _nextManagerSessionId = 2;
                sessionId = 1;
            }

            return sessionId;
        }

        private int GetNextOwnerRequestId()
        {
            int requestId = _nextOwnerRequestId++;
            if (requestId <= 0)
            {
                _nextOwnerRequestId = 2;
                requestId = 1;
            }

            return requestId;
        }
    }
}
