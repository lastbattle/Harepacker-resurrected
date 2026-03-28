using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool TryApplyPacketOwnedFieldStatePacket(int packetType, byte[] payload, out string message)
        {
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
            return _packetFieldStateRuntime.TryApplyPacket(
                packetType,
                payload,
                currTickCount,
                (tag, state, transitionTimeMs, currentTimeMs) => SetDynamicObjectTagState(tag, state, transitionTimeMs, currentTimeMs),
                HandleFieldSpecificDataPacketHandoff,
                out message);
        }

        private string HandleFieldSpecificDataPacketHandoff(byte[] payload, int currentTick)
        {
            string areaName = _specialFieldRuntime.ActiveArea?.ToString() ?? "no active special-field owner";
            return $"handoff target={areaName}";
        }

        private QuestLogSnapshot BuildQuestLogSnapshotWithPacketState(QuestLogTabType tab, bool showAllLevels)
        {
            QuestLogSnapshot snapshot = _questRuntime.BuildQuestLogSnapshot(tab, _playerManager?.Player?.Build, showAllLevels);
            if (snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return snapshot;
            }

            List<QuestLogEntrySnapshot> updatedEntries = null;
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                QuestLogEntrySnapshot entry = snapshot.Entries[i];
                if (!_packetFieldStateRuntime.TryGetQuestTimerText(entry.QuestId, currTickCount, out string timerText))
                {
                    continue;
                }

                updatedEntries ??= new List<QuestLogEntrySnapshot>(snapshot.Entries);
                updatedEntries[i] = new QuestLogEntrySnapshot
                {
                    QuestId = entry.QuestId,
                    Name = entry.Name,
                    State = entry.State,
                    StatusText = string.IsNullOrWhiteSpace(entry.StatusText)
                        ? timerText
                        : $"{entry.StatusText} | {timerText}",
                    SummaryText = entry.SummaryText,
                    StageText = string.IsNullOrWhiteSpace(entry.StageText)
                        ? timerText
                        : $"{entry.StageText}\n{timerText}",
                    NpcText = entry.NpcText,
                    ProgressRatio = entry.ProgressRatio,
                    CanStart = entry.CanStart,
                    CanComplete = entry.CanComplete,
                    RequirementLines = entry.RequirementLines,
                    RewardLines = entry.RewardLines,
                    IssueLines = entry.IssueLines
                };
            }

            return updatedEntries == null
                ? snapshot
                : new QuestLogSnapshot { Entries = updatedEntries };
        }

        private QuestWindowDetailState GetQuestWindowDetailStateWithPacketState(int questId)
        {
            QuestWindowDetailState state = _questRuntime.GetQuestWindowDetailState(questId, _playerManager?.Player?.Build);
            if (state == null || !_packetFieldStateRuntime.TryGetQuestTimerText(questId, currTickCount, out string timerText))
            {
                return state;
            }

            string hintText = string.IsNullOrWhiteSpace(state.HintText)
                ? timerText
                : $"{timerText}\n{state.HintText}";
            return new QuestWindowDetailState
            {
                QuestId = state.QuestId,
                Title = state.Title,
                State = state.State,
                SummaryText = state.SummaryText,
                RequirementText = state.RequirementText,
                RewardText = state.RewardText,
                HintText = hintText,
                NpcText = state.NpcText,
                RequirementLines = state.RequirementLines,
                RewardLines = state.RewardLines,
                CurrentProgress = state.CurrentProgress,
                TotalProgress = state.TotalProgress,
                PrimaryAction = state.PrimaryAction,
                PrimaryActionEnabled = state.PrimaryActionEnabled,
                PrimaryActionLabel = state.PrimaryActionLabel,
                SecondaryAction = state.SecondaryAction,
                SecondaryActionEnabled = state.SecondaryActionEnabled,
                SecondaryActionLabel = state.SecondaryActionLabel,
                TertiaryAction = state.TertiaryAction,
                TertiaryActionEnabled = state.TertiaryActionEnabled,
                TertiaryActionLabel = state.TertiaryActionLabel,
                QuaternaryAction = state.QuaternaryAction,
                QuaternaryActionEnabled = state.QuaternaryActionEnabled,
                QuaternaryActionLabel = state.QuaternaryActionLabel,
                TargetNpcId = state.TargetNpcId,
                TargetNpcName = state.TargetNpcName,
                TargetMobId = state.TargetMobId,
                TargetMobName = state.TargetMobName,
                TargetItemId = state.TargetItemId,
                TargetItemName = state.TargetItemName,
                DeliveryCashItemId = state.DeliveryCashItemId,
                DeliveryCashItemName = state.DeliveryCashItemName,
                NpcButtonStyle = state.NpcButtonStyle
            };
        }
    }
}
