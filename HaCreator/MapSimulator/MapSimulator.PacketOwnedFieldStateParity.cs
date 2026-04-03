using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool TryApplyPacketOwnedFieldStatePacket(int packetType, byte[] payload, out string message)
        {
            if (TryApplyClientOwnedWrapperPacket(packetType, payload, currTickCount, out message))
            {
                return true;
            }

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
            string wrapperMessage = HandleClientOwnedFieldSpecificDataPacket(payload, currentTick);
            if (!string.IsNullOrWhiteSpace(wrapperMessage))
            {
                return wrapperMessage;
            }

            if (TryApplyStructuredFieldSpecificDataPayload(payload, currentTick, out string structuredMessage))
            {
                return structuredMessage;
            }

            string areaName = _specialFieldRuntime.ActiveArea?.ToString() ?? "no active special-field owner";
            return $"handoff target={areaName}";
        }

        private bool TryApplyStructuredFieldSpecificDataPayload(byte[] payload, int currentTick, out string message)
        {
            message = null;
            FieldSpecificStringPairOwnerMask activeOwners = GetActiveFieldSpecificStringPairOwners();
            if (payload == null ||
                payload.Length == 0 ||
                activeOwners == FieldSpecificStringPairOwnerMask.None ||
                !PacketFieldSpecificDataCodec.TryDecodeStringPairs(payload, out IReadOnlyList<KeyValuePair<string, string>> pairs, out int headerSize))
            {
                return false;
            }

            List<string> applied = new();
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                string key = pair.Key;
                PacketFieldSpecificDataOwnerHint ownerHint = PacketFieldSpecificDataCodec.ResolveOwnerHint(ref key);
                if (TryApplyStructuredFieldSpecificPair(key, pair.Value, ownerHint, currentTick, out string target))
                {
                    string ownerPrefix = ownerHint == PacketFieldSpecificDataOwnerHint.None ? string.Empty : $"{ownerHint.ToString().ToLowerInvariant()}:";
                    applied.Add($"{ownerPrefix}{key}={pair.Value} ({target})");
                }
            }

            if (applied.Count == 0)
            {
                message =
                    $"decoded {pairs.Count} field-specific key/value pair(s) for {DescribeFieldSpecificStringPairOwners(activeOwners)} " +
                    $"using header size {headerSize}, but no active owner accepted them";
                return true;
            }

            message =
                $"decoded {pairs.Count} field-specific key/value pair(s) for {DescribeFieldSpecificStringPairOwners(activeOwners)} " +
                $"using header size {headerSize}: {string.Join(", ", applied.Take(4))}";
            return true;
        }

        private bool TryApplyStructuredFieldSpecificPair(
            string key,
            string value,
            PacketFieldSpecificDataOwnerHint ownerHint,
            int currentTick,
            out string target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (ShouldRouteFieldSpecificPairToPartyRaid(ownerHint) &&
                _specialFieldRuntime.PartyRaid.IsActive &&
                _specialFieldRuntime.PartyRaid.OnFieldSetVariable(key, value))
            {
                target = "PartyRaidField";
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                IsEscortResultWrapperMap(_mapBoard?.MapInfo) &&
                TryApplyClientOwnedWrapperFieldValue("escortresult", key, value, currentTick, out _))
            {
                target = "escort-result wrapper";
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                _mapBoard?.MapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON &&
                TryApplyClientOwnedWrapperFieldValue("huntingadballoon", key, value, currentTick, out _))
            {
                target = "hunting-ad-balloon wrapper";
                return true;
            }

            return false;
        }

        private static bool ShouldRouteFieldSpecificPairToPartyRaid(PacketFieldSpecificDataOwnerHint ownerHint)
        {
            return ownerHint is PacketFieldSpecificDataOwnerHint.None
                or PacketFieldSpecificDataOwnerHint.Party
                or PacketFieldSpecificDataOwnerHint.Session;
        }

        private static bool ShouldRouteFieldSpecificPairToFieldWrappers(PacketFieldSpecificDataOwnerHint ownerHint)
        {
            return ownerHint is PacketFieldSpecificDataOwnerHint.None
                or PacketFieldSpecificDataOwnerHint.Field
                or PacketFieldSpecificDataOwnerHint.Session;
        }

        private FieldSpecificStringPairOwnerMask GetActiveFieldSpecificStringPairOwners()
        {
            FieldSpecificStringPairOwnerMask owners = FieldSpecificStringPairOwnerMask.None;
            if (_specialFieldRuntime.PartyRaid.IsActive)
            {
                owners |= FieldSpecificStringPairOwnerMask.PartyRaid;
            }

            if (IsEscortResultWrapperMap(_mapBoard?.MapInfo))
            {
                owners |= FieldSpecificStringPairOwnerMask.EscortResult;
            }

            if (_mapBoard?.MapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON)
            {
                owners |= FieldSpecificStringPairOwnerMask.HuntingAdBalloon;
            }

            return owners;
        }

        private static string DescribeFieldSpecificStringPairOwners(FieldSpecificStringPairOwnerMask owners)
        {
            List<string> names = new();
            if ((owners & FieldSpecificStringPairOwnerMask.PartyRaid) != 0)
            {
                names.Add("PartyRaidField");
            }

            if ((owners & FieldSpecificStringPairOwnerMask.EscortResult) != 0)
            {
                names.Add("escort-result wrapper");
            }

            if ((owners & FieldSpecificStringPairOwnerMask.HuntingAdBalloon) != 0)
            {
                names.Add("hunting-ad-balloon wrapper");
            }

            return names.Count == 0 ? "no known owner" : string.Join(", ", names);
        }

        [Flags]
        private enum FieldSpecificStringPairOwnerMask
        {
            None = 0,
            PartyRaid = 1 << 0,
            EscortResult = 1 << 1,
            HuntingAdBalloon = 1 << 2
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
                HeaderNoteText = state.HeaderNoteText,
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
                PrimaryActionSelected = state.PrimaryActionSelected,
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
                HasDetailInset = true,
                TimeLimitSeconds = state.TimeLimitSeconds,
                TimerUiKey = state.TimerUiKey,
                DeliveryType = state.DeliveryType,
                DeliveryActionEnabled = state.DeliveryActionEnabled,
                DeliveryCashItemId = state.DeliveryCashItemId,
                DeliveryCashItemName = state.DeliveryCashItemName,
                NpcButtonStyle = state.NpcButtonStyle
            };
        }
    }
}
