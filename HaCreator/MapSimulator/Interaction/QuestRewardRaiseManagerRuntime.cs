using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class QuestRewardRaiseManagerRuntime
    {
        private sealed record QuestRewardRaiseOwnerSnapshot(
            int ManagerSessionId,
            int OwnerRequestId,
            int OwnerItemId,
            int QrData,
            int MaxDropCount,
            QuestRewardRaiseWindowMode WindowMode,
            QuestRewardRaiseWindowMode DisplayMode,
            bool AwaitingConfirmAck,
            bool AwaitingOwnerDestroyAck,
            string LastInboundSummary);

        private int _nextManagerSessionId = 1;
        private int _nextOwnerRequestId = 1;
        private readonly System.Collections.Generic.Dictionary<int, QuestRewardRaiseOwnerSnapshot> _ownerSnapshotsByQuestId = new();
        private readonly System.Collections.Generic.Dictionary<int, QuestRewardRaiseState> _retainedClosedRaisesByQuestId = new();
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
            QuestRewardRaiseWindowMode displayMode = prompt.OwnerContext?.WindowMode
                ?? snapshot?.DisplayMode
                ?? windowMode;
            int ownerItemId = Math.Max(0, prompt.OwnerContext?.OwnerItemId ?? snapshot?.OwnerItemId ?? 0);
            int qrData = prompt.OwnerContext?.InitialQrData ?? snapshot?.QrData ?? 0;
            int maxDropCount = Math.Max(1, prompt.OwnerContext?.MaxDropCount ?? snapshot?.MaxDropCount ?? 1);
            bool reuseObservedOwnerState = ShouldReuseObservedOwnerState(snapshot);
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
                ManagerSessionId = reuseObservedOwnerState && snapshot.ManagerSessionId > 0
                    ? snapshot.ManagerSessionId
                    : GetNextManagerSessionId(),
                RequestId = reuseObservedOwnerState && snapshot.OwnerRequestId > 0
                    ? snapshot.OwnerRequestId
                    : GetNextOwnerRequestId(),
                OwnerItemId = ownerItemId,
                QrData = qrData,
                MaxDropCount = maxDropCount,
                WindowMode = windowMode,
                DisplayMode = displayMode,
                WindowPosition = windowPosition,
                LastInboundSummary = snapshot?.LastInboundSummary ?? string.Empty,
                AwaitingConfirmAck = snapshot?.AwaitingConfirmAck ?? false,
                AwaitingOwnerDestroyAck = snapshot?.AwaitingOwnerDestroyAck ?? false
            };

            if (questId > 0)
            {
                RememberState(ActiveRaise);
            }

            return ActiveRaise;
        }

        public QuestRewardRaiseState Restore(QuestRewardRaiseState state)
        {
            ActiveRaise = state;
            return ActiveRaise;
        }

        public void RetainClosedRaise(QuestRewardRaiseState state)
        {
            int questId = Math.Max(0, state?.Prompt?.QuestId ?? 0);
            if (questId <= 0 || state == null)
            {
                return;
            }

            BackupWindowPosition(state);
            QuestRewardRaiseState retainedState = state.CloneShallow();
            retainedState.IsWindowDismissedLocally = true;
            _retainedClosedRaisesByQuestId[questId] = retainedState;
            RememberState(retainedState);
            if (ReferenceEquals(ActiveRaise, state))
            {
                ActiveRaise = null;
            }
        }

        public QuestRewardRaiseState DestroyActiveRaise()
        {
            QuestRewardRaiseState activeRaise = ActiveRaise;
            RememberState(activeRaise);
            ActiveRaise = null;
            return activeRaise;
        }

        public QuestRewardRaiseState ClearActiveRaise()
        {
            QuestRewardRaiseState activeRaise = ActiveRaise;
            ActiveRaise = null;
            return activeRaise;
        }

        public QuestRewardRaiseState GetObservedRaiseByQuestId(int questId)
        {
            questId = Math.Max(0, questId);
            if (questId <= 0)
            {
                return null;
            }

            if (ActiveRaise?.Prompt?.QuestId == questId)
            {
                return ActiveRaise;
            }

            _retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState);
            return retainedState;
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

            QuestRewardRaiseState observedRaise = GetObservedRaiseByQuestId(questId);
            if (observedRaise == null)
            {
                return snapshot != null;
            }

            observedRaise.QrData = qrData;
            updatedState = observedRaise;
            return true;
        }

        public void ObserveOwnerState(
            int questId,
            int ownerItemId,
            int qrData,
            int maxDropCount,
            QuestRewardRaiseWindowMode windowMode,
            QuestRewardRaiseWindowMode? displayMode = null)
        {
            questId = Math.Max(0, questId);
            if (questId <= 0)
            {
                return;
            }

            ownerItemId = Math.Max(0, ownerItemId);
            maxDropCount = Math.Max(1, maxDropCount);

            _ownerSnapshotsByQuestId.TryGetValue(questId, out QuestRewardRaiseOwnerSnapshot snapshot);
            bool isActiveQuest = ActiveRaise?.Prompt?.QuestId == questId;
            QuestRewardRaiseWindowMode resolvedDisplayMode = displayMode
                ?? (isActiveQuest
                    ? ActiveRaise.DisplayMode
                    : snapshot?.DisplayMode ?? windowMode);
            _ownerSnapshotsByQuestId[questId] = new QuestRewardRaiseOwnerSnapshot(
                isActiveQuest
                    ? ActiveRaise.ManagerSessionId
                    : snapshot?.ManagerSessionId ?? 0,
                isActiveQuest
                    ? ActiveRaise.RequestId
                    : snapshot?.OwnerRequestId ?? 0,
                ownerItemId,
                qrData,
                maxDropCount,
                windowMode,
                resolvedDisplayMode,
                isActiveQuest
                    ? ActiveRaise.AwaitingConfirmAck
                    : snapshot?.AwaitingConfirmAck ?? false,
                isActiveQuest
                    ? ActiveRaise.AwaitingOwnerDestroyAck
                    : snapshot?.AwaitingOwnerDestroyAck ?? false,
                isActiveQuest
                    ? ActiveRaise.LastInboundSummary ?? string.Empty
                    : snapshot?.LastInboundSummary ?? string.Empty);

            if (!isActiveQuest)
            {
                if (_retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState))
                {
                    retainedState.OwnerItemId = ownerItemId;
                    retainedState.QrData = qrData;
                    retainedState.MaxDropCount = maxDropCount;
                    retainedState.WindowMode = windowMode;
                    retainedState.DisplayMode = resolvedDisplayMode;
                }
                return;
            }

            ActiveRaise.OwnerItemId = ownerItemId;
            ActiveRaise.QrData = qrData;
            ActiveRaise.MaxDropCount = maxDropCount;
            ActiveRaise.WindowMode = windowMode;
            ActiveRaise.DisplayMode = resolvedDisplayMode;
        }

        public void RememberState(QuestRewardRaiseState state)
        {
            int questId = Math.Max(0, state?.Prompt?.QuestId ?? 0);
            BackupWindowPosition(state);
            if (questId <= 0 || state == null)
            {
                return;
            }

            if (!state.IsWindowDismissedLocally)
            {
                _retainedClosedRaisesByQuestId.Remove(questId);
            }

            _ownerSnapshotsByQuestId[questId] = new QuestRewardRaiseOwnerSnapshot(
                Math.Max(0, state.ManagerSessionId),
                Math.Max(0, state.RequestId),
                Math.Max(0, state.OwnerItemId),
                state.QrData,
                Math.Max(1, state.MaxDropCount),
                state.WindowMode,
                state.DisplayMode,
                state.AwaitingConfirmAck,
                state.AwaitingOwnerDestroyAck,
                state.LastInboundSummary ?? string.Empty);
        }

        public void ObserveInboundPacket(QuestRewardRaiseInboundPacket packet, string summary)
        {
            QuestRewardRaisePacketPayload payload = packet?.Payload;
            int questId = Math.Max(0, payload?.QuestId ?? 0);
            if (questId <= 0)
            {
                return;
            }

            if (packet.Kind == QuestRewardRaiseInboundPacketKind.OwnerDestroyResult)
            {
                _retainedClosedRaisesByQuestId.Remove(questId);
                _ownerSnapshotsByQuestId.Remove(questId);
                return;
            }

            _ownerSnapshotsByQuestId.TryGetValue(questId, out QuestRewardRaiseOwnerSnapshot snapshot);
            bool isActiveQuest = ActiveRaise?.Prompt?.QuestId == questId;
            if (_retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState))
            {
                retainedState.ManagerSessionId = Math.Max(0, payload.ManagerSessionId);
                retainedState.RequestId = Math.Max(0, payload.OwnerRequestId);
                retainedState.OwnerItemId = Math.Max(0, payload.OwnerItemId);
                retainedState.QrData = payload.QrData;
                retainedState.MaxDropCount = Math.Max(1, Math.Max(retainedState.MaxDropCount, payload.PlacedPieceCount));
                retainedState.WindowMode = payload.WindowMode;
                retainedState.DisplayMode = payload.DisplayMode;
                retainedState.AwaitingConfirmAck = packet.Kind == QuestRewardRaiseInboundPacketKind.PutItemConfirmResult
                    ? false
                    : retainedState.AwaitingConfirmAck;
                retainedState.LastInboundSummary = summary ?? string.Empty;
            }

            _ownerSnapshotsByQuestId[questId] = new QuestRewardRaiseOwnerSnapshot(
                Math.Max(0, payload.ManagerSessionId),
                Math.Max(0, payload.OwnerRequestId),
                Math.Max(0, payload.OwnerItemId),
                payload.QrData,
                Math.Max(1, Math.Max(snapshot?.MaxDropCount ?? 1, payload.PlacedPieceCount)),
                payload.WindowMode,
                payload.DisplayMode,
                packet.Kind == QuestRewardRaiseInboundPacketKind.PutItemConfirmResult
                    ? false
                    : isActiveQuest ? ActiveRaise.AwaitingConfirmAck : snapshot?.AwaitingConfirmAck ?? false,
                isActiveQuest ? ActiveRaise.AwaitingOwnerDestroyAck : snapshot?.AwaitingOwnerDestroyAck ?? false,
                summary ?? string.Empty);
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

        public QuestRewardRaiseState ClearRetainedRaiseByQuestId(int questId)
        {
            questId = Math.Max(0, questId);
            if (questId <= 0 || !_retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState))
            {
                return null;
            }

            _retainedClosedRaisesByQuestId.Remove(questId);
            return retainedState;
        }

        private void BackupWindowPosition(QuestRewardRaiseState state)
        {
            if (state?.OwnerItemId > 0 && state.WindowPosition != Point.Zero)
            {
                _windowPositionsByOwnerItemId[state.OwnerItemId] = state.WindowPosition;
            }
        }

        private static bool ShouldReuseObservedOwnerState(QuestRewardRaiseOwnerSnapshot snapshot)
        {
            return snapshot != null
                && snapshot.ManagerSessionId > 0
                && snapshot.OwnerRequestId > 0
                && (snapshot.AwaitingConfirmAck
                    || snapshot.AwaitingOwnerDestroyAck
                    || !string.IsNullOrWhiteSpace(snapshot.LastInboundSummary));
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
