using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            if (TryApplyStructuredFieldSpecificDataPayload(payload, currentTick, out string structuredMessage))
            {
                return structuredMessage;
            }

            string wrapperMessage = HandleClientOwnedFieldSpecificDataPacket(payload, currentTick);
            if (!string.IsNullOrWhiteSpace(wrapperMessage))
            {
                return wrapperMessage;
            }

            string areaName = _specialFieldRuntime.ActiveArea?.ToString() ?? "no active special-field owner";
            return $"handoff target={areaName}";
        }

        private bool TryApplyStructuredFieldSpecificDataPayload(byte[] payload, int currentTick, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0 || !TryDecodeFieldSpecificStringPairs(payload, out IReadOnlyList<KeyValuePair<string, string>> pairs))
            {
                return false;
            }

            List<string> applied = new();
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                if (TryApplyStructuredFieldSpecificPair(pair.Key, pair.Value, currentTick, out string target))
                {
                    applied.Add($"{pair.Key}={pair.Value} ({target})");
                }
            }

            if (applied.Count == 0)
            {
                message = $"decoded {pairs.Count} field-specific key/value pair(s), but no active owner accepted them";
                return true;
            }

            message = $"decoded {pairs.Count} field-specific key/value pair(s): {string.Join(", ", applied.Take(4))}";
            return true;
        }

        private bool TryApplyStructuredFieldSpecificPair(string key, string value, int currentTick, out string target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (_specialFieldRuntime.PartyRaid.IsActive && _specialFieldRuntime.PartyRaid.OnFieldSetVariable(key, value))
            {
                target = "PartyRaidField";
                return true;
            }

            if (IsEscortResultWrapperMap(_mapBoard?.MapInfo) &&
                TryApplyClientOwnedWrapperFieldValue("escortresult", key, value, currentTick, out _))
            {
                target = "escort-result wrapper";
                return true;
            }

            if (_mapBoard?.MapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON &&
                TryApplyClientOwnedWrapperFieldValue("huntingadballoon", key, value, currentTick, out _))
            {
                target = "hunting-ad-balloon wrapper";
                return true;
            }

            return false;
        }

        private static bool TryDecodeFieldSpecificStringPairs(byte[] payload, out IReadOnlyList<KeyValuePair<string, string>> pairs)
        {
            pairs = null;
            payload ??= Array.Empty<byte>();
            return TryDecodeFieldSpecificStringPairs(payload, 1, out pairs)
                || TryDecodeFieldSpecificStringPairs(payload, 2, out pairs)
                || TryDecodeFieldSpecificStringPairs(payload, 4, out pairs)
                || TryDecodeFieldSpecificStringPairs(payload, 0, out pairs);
        }

        private static bool TryDecodeFieldSpecificStringPairs(byte[] payload, int headerSize, out IReadOnlyList<KeyValuePair<string, string>> pairs)
        {
            pairs = null;
            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int declaredCount = 0;
                if (headerSize == 1)
                {
                    declaredCount = reader.ReadByte();
                }
                else if (headerSize == 2)
                {
                    declaredCount = reader.ReadUInt16();
                }
                else if (headerSize == 4)
                {
                    declaredCount = reader.ReadInt32();
                }

                List<KeyValuePair<string, string>> decoded = new();
                if (headerSize == 0)
                {
                    while (stream.Position < stream.Length)
                    {
                        string key = ReadMapleString(reader);
                        string value = ReadMapleString(reader);
                        decoded.Add(new KeyValuePair<string, string>(key, value));
                    }
                }
                else
                {
                    if (declaredCount <= 0 || declaredCount > 32)
                    {
                        return false;
                    }

                    for (int i = 0; i < declaredCount; i++)
                    {
                        string key = ReadMapleString(reader);
                        string value = ReadMapleString(reader);
                        decoded.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                if (decoded.Count == 0 || stream.Position != stream.Length || decoded.Any(static pair => string.IsNullOrWhiteSpace(pair.Key)))
                {
                    return false;
                }

                pairs = decoded;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
            {
                return false;
            }
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Field-specific data string ended before its declared Maple-string length.");
            }

            return Encoding.Default.GetString(bytes);
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
