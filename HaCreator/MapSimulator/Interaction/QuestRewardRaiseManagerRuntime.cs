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
            _retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState);

            QuestRewardRaiseWindowMode windowMode = ResolveOpenWindowMode(prompt, retainedState, snapshot);
            QuestRewardRaiseWindowMode displayMode = prompt.OwnerContext?.WindowMode
                ?? retainedState?.DisplayMode
                ?? snapshot?.DisplayMode
                ?? windowMode;
            int ownerItemId = ResolveOpenOwnerItemId(prompt, retainedState, snapshot);
            int qrData = ResolveOpenQrData(prompt, retainedState, snapshot);
            int maxDropCount = ResolveOpenMaxDropCount(prompt, retainedState, snapshot, windowMode);
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

            bool reuseRetainedClosedRaise = ShouldReuseRetainedClosedRaise(retainedState, prompt);
            if (reuseRetainedClosedRaise)
            {
                ActiveRaise = retainedState.CloneShallow();
                _retainedClosedRaisesByQuestId.Remove(questId);
            }
            else
            {
                ActiveRaise = new QuestRewardRaiseState();
            }

            ActiveRaise.Source = source;
            ActiveRaise.Prompt = prompt;
            ActiveRaise.GroupIndex = ResolveOpenGroupIndex(prompt, ActiveRaise, displayMode);
            ActiveRaise.ManagerSessionId = ResolveOpenManagerSessionId(reuseObservedOwnerState, snapshot, reuseRetainedClosedRaise, retainedState);
            ActiveRaise.RequestId = ResolveOpenOwnerRequestId(reuseObservedOwnerState, snapshot, reuseRetainedClosedRaise, retainedState);
            ActiveRaise.OwnerItemId = ownerItemId;
            ActiveRaise.QrData = qrData;
            ActiveRaise.MaxDropCount = maxDropCount;
            ActiveRaise.WindowMode = windowMode;
            ActiveRaise.DisplayMode = displayMode;
            ActiveRaise.WindowPosition = windowPosition;
            if (string.IsNullOrWhiteSpace(ActiveRaise.LastInboundSummary))
            {
                ActiveRaise.LastInboundSummary = snapshot?.LastInboundSummary ?? string.Empty;
            }

            ActiveRaise.AwaitingConfirmAck = snapshot?.AwaitingConfirmAck ?? ActiveRaise.AwaitingConfirmAck;
            ActiveRaise.AwaitingOwnerDestroyAck = snapshot?.AwaitingOwnerDestroyAck ?? ActiveRaise.AwaitingOwnerDestroyAck;
            ActiveRaise.IsWindowDismissedLocally = false;

            if (questId > 0)
            {
                RememberState(ActiveRaise);
            }

            return ActiveRaise;
        }

        public QuestRewardRaiseWindowMode ResolveOpenWindowMode(QuestRewardChoicePrompt prompt)
        {
            if (prompt == null)
            {
                return QuestRewardRaiseWindowMode.Selection;
            }

            int questId = Math.Max(0, prompt.QuestId);
            _ownerSnapshotsByQuestId.TryGetValue(questId, out QuestRewardRaiseOwnerSnapshot snapshot);
            _retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState);
            return ResolveOpenWindowMode(prompt, retainedState, snapshot);
        }

        private static int ResolveOpenGroupIndex(
            QuestRewardChoicePrompt prompt,
            QuestRewardRaiseState restoredState,
            QuestRewardRaiseWindowMode displayMode)
        {
            int groupCount = prompt?.Groups?.Count ?? 0;
            if (displayMode == QuestRewardRaiseWindowMode.PiecePlacement || groupCount <= 0)
            {
                return 0;
            }

            if (restoredState?.SelectedItemsByGroup?.Count > 0)
            {
                return Math.Clamp(
                    QuestRewardRaiseState.ResolveSelectionProgressGroupIndex(prompt, restoredState.SelectedItemsByGroup),
                    0,
                    groupCount);
            }

            return Math.Clamp(restoredState?.GroupIndex ?? 0, 0, Math.Max(0, groupCount - 1));
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

            _ownerSnapshotsByQuestId.TryGetValue(questId, out QuestRewardRaiseOwnerSnapshot snapshot);
            _retainedClosedRaisesByQuestId.TryGetValue(questId, out QuestRewardRaiseState retainedState);
            bool isActiveQuest = ActiveRaise?.Prompt?.QuestId == questId;
            ownerItemId = ResolvePositiveObservedValue(
                ownerItemId,
                isActiveQuest ? ActiveRaise.OwnerItemId : 0,
                retainedState?.OwnerItemId ?? 0,
                snapshot?.OwnerItemId ?? 0);
            maxDropCount = Math.Max(
                1,
                ResolvePositiveObservedValue(
                    maxDropCount,
                    isActiveQuest ? ActiveRaise.MaxDropCount : 0,
                    retainedState?.MaxDropCount ?? 0,
                    snapshot?.MaxDropCount ?? 0));
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
                if (retainedState != null)
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
                retainedState.ManagerSessionId = ResolvePositiveObservedValue(
                    payload.ManagerSessionId,
                    retainedState.ManagerSessionId,
                    snapshot?.ManagerSessionId ?? 0);
                retainedState.RequestId = ResolvePositiveObservedValue(
                    payload.OwnerRequestId,
                    retainedState.RequestId,
                    snapshot?.OwnerRequestId ?? 0);
                retainedState.OwnerItemId = ResolvePositiveObservedValue(
                    payload.OwnerItemId,
                    retainedState.OwnerItemId,
                    snapshot?.OwnerItemId ?? 0);
                retainedState.QrData = payload.QrData;
                retainedState.MaxDropCount = ResolveObservedMaxDropCount(retainedState, snapshot, payload);
                retainedState.WindowMode = payload.WindowMode;
                retainedState.DisplayMode = payload.DisplayMode;
                retainedState.SyncSelectionProgressFromPayload(payload);
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
                ResolveObservedMaxDropCount(isActiveQuest ? ActiveRaise : retainedState, snapshot, payload),
                payload.WindowMode,
                payload.DisplayMode,
                packet.Kind == QuestRewardRaiseInboundPacketKind.PutItemConfirmResult
                    ? false
                    : isActiveQuest ? ActiveRaise.AwaitingConfirmAck : snapshot?.AwaitingConfirmAck ?? false,
                isActiveQuest ? ActiveRaise.AwaitingOwnerDestroyAck : snapshot?.AwaitingOwnerDestroyAck ?? false,
                summary ?? string.Empty);
        }

        private static int ResolvePositiveObservedValue(int primaryValue, params int[] fallbackValues)
        {
            if (primaryValue > 0)
            {
                return primaryValue;
            }

            for (int i = 0; i < fallbackValues.Length; i++)
            {
                if (fallbackValues[i] > 0)
                {
                    return fallbackValues[i];
                }
            }

            return 0;
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

        private static int ResolveObservedMaxDropCount(
            QuestRewardRaiseState observedState,
            QuestRewardRaiseOwnerSnapshot snapshot,
            QuestRewardRaisePacketPayload payload)
        {
            return Math.Max(
                1,
                Math.Max(
                    Math.Max(observedState?.MaxDropCount ?? 1, snapshot?.MaxDropCount ?? 1),
                    Math.Max(payload?.PlacedPieceCount ?? 0, payload?.MaxDropCount ?? 0)));
        }

        private static bool ShouldReuseRetainedClosedRaise(QuestRewardRaiseState retainedState, QuestRewardChoicePrompt prompt)
        {
            if (retainedState?.Prompt == null || prompt == null)
            {
                return false;
            }

            if (Math.Max(0, retainedState.Prompt.QuestId) != Math.Max(0, prompt.QuestId))
            {
                return false;
            }

            int promptOwnerItemId = Math.Max(0, prompt.OwnerContext?.OwnerItemId ?? 0);
            int retainedOwnerItemId = Math.Max(0, retainedState.OwnerItemId);
            if (promptOwnerItemId > 0 && retainedOwnerItemId > 0 && promptOwnerItemId != retainedOwnerItemId)
            {
                return false;
            }

            return retainedState.PlacedPieces.Count > 0
                || retainedState.SelectedItemsByGroup.Count > 0
                || retainedState.GroupIndex > 0
                || retainedState.AwaitingConfirmAck
                || retainedState.AwaitingOwnerDestroyAck
                || !string.IsNullOrWhiteSpace(retainedState.LastInboundSummary)
                || !string.IsNullOrWhiteSpace(retainedState.OpenDispatchSummary);
        }

        private static int ResolveOpenOwnerItemId(
            QuestRewardChoicePrompt prompt,
            QuestRewardRaiseState retainedState,
            QuestRewardRaiseOwnerSnapshot snapshot)
        {
            int ownerContextOwnerItemId = Math.Max(0, prompt?.OwnerContext?.OwnerItemId ?? 0);
            int observedOwnerItemId = Math.Max(0, retainedState?.OwnerItemId ?? snapshot?.OwnerItemId ?? 0);
            return ownerContextOwnerItemId > 0
                ? ownerContextOwnerItemId
                : observedOwnerItemId;
        }

        private static int ResolveOpenQrData(
            QuestRewardChoicePrompt prompt,
            QuestRewardRaiseState retainedState,
            QuestRewardRaiseOwnerSnapshot snapshot)
        {
            int ownerContextQrData = prompt?.OwnerContext?.InitialQrData ?? 0;
            if (ownerContextQrData != 0)
            {
                return ownerContextQrData;
            }

            return retainedState?.QrData ?? snapshot?.QrData ?? ownerContextQrData;
        }

        private static int ResolveOpenMaxDropCount(
            QuestRewardChoicePrompt prompt,
            QuestRewardRaiseState retainedState,
            QuestRewardRaiseOwnerSnapshot snapshot,
            QuestRewardRaiseWindowMode windowMode)
        {
            int ownerContextMaxDropCount = Math.Max(1, prompt?.OwnerContext?.MaxDropCount ?? 1);
            int observedMaxDropCount = Math.Max(1, retainedState?.MaxDropCount ?? snapshot?.MaxDropCount ?? 1);
            int promptDerivedMaxDropCount = windowMode == QuestRewardRaiseWindowMode.PiecePlacement
                ? Math.Max(1, QuestRewardRaiseState.CountEnabledDropItems(prompt))
                : 1;
            return Math.Max(ownerContextMaxDropCount, Math.Max(observedMaxDropCount, promptDerivedMaxDropCount));
        }

        private static QuestRewardRaiseWindowMode ResolveOpenWindowMode(
            QuestRewardChoicePrompt prompt,
            QuestRewardRaiseState retainedState,
            QuestRewardRaiseOwnerSnapshot snapshot)
        {
            return prompt?.OwnerContext?.WindowMode
                ?? retainedState?.WindowMode
                ?? snapshot?.WindowMode
                ?? QuestRewardRaiseWindowMode.Selection;
        }

        private int ResolveOpenManagerSessionId(
            bool reuseObservedOwnerState,
            QuestRewardRaiseOwnerSnapshot snapshot,
            bool reuseRetainedClosedRaise,
            QuestRewardRaiseState retainedState)
        {
            if (reuseObservedOwnerState && snapshot?.ManagerSessionId > 0)
            {
                return snapshot.ManagerSessionId;
            }

            if (reuseRetainedClosedRaise && retainedState?.ManagerSessionId > 0)
            {
                return retainedState.ManagerSessionId;
            }

            return GetNextManagerSessionId();
        }

        private int ResolveOpenOwnerRequestId(
            bool reuseObservedOwnerState,
            QuestRewardRaiseOwnerSnapshot snapshot,
            bool reuseRetainedClosedRaise,
            QuestRewardRaiseState retainedState)
        {
            if (reuseObservedOwnerState && snapshot?.OwnerRequestId > 0)
            {
                return snapshot.OwnerRequestId;
            }

            if (reuseRetainedClosedRaise && retainedState?.RequestId > 0)
            {
                return retainedState.RequestId;
            }

            return GetNextOwnerRequestId();
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
