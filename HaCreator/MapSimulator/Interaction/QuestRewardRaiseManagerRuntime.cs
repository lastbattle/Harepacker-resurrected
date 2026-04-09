using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class QuestRewardRaiseManagerRuntime
    {
        private int _nextManagerSessionId = 1;

        public QuestRewardRaiseState ActiveRaise { get; private set; }

        public QuestRewardRaiseState Open(QuestRewardChoicePrompt prompt, QuestRewardRaiseSourceKind source, Point defaultPosition)
        {
            if (prompt == null)
            {
                ActiveRaise = null;
                return null;
            }

            QuestRewardRaiseWindowMode windowMode = prompt.OwnerContext?.WindowMode ?? QuestRewardRaiseWindowMode.Selection;
            Point windowPosition = defaultPosition;
            if (ActiveRaise != null
                && ActiveRaise.Prompt?.QuestId == prompt.QuestId
                && ActiveRaise.OwnerItemId == Math.Max(0, prompt.OwnerContext?.OwnerItemId ?? 0)
                && ActiveRaise.WindowMode == windowMode
                && ActiveRaise.WindowPosition != Point.Zero)
            {
                windowPosition = ActiveRaise.WindowPosition;
            }

            ActiveRaise = new QuestRewardRaiseState
            {
                Source = source,
                Prompt = prompt,
                GroupIndex = 0,
                ManagerSessionId = GetNextManagerSessionId(),
                OwnerItemId = Math.Max(0, prompt.OwnerContext?.OwnerItemId ?? 0),
                QrData = prompt.OwnerContext?.InitialQrData ?? 0,
                MaxDropCount = Math.Max(1, prompt.OwnerContext?.MaxDropCount ?? 1),
                WindowMode = windowMode,
                DisplayMode = windowMode,
                WindowPosition = windowPosition
            };
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
            ActiveRaise = null;
            return activeRaise;
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
    }
}
