using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private int? _pendingPacketOwnedQuestResultFollowUpQuestId;
        private int _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId;
        private string _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;

        private bool TryApplyPacketOwnedQuestResultPayload(byte[] payload, out string message)
        {
            message = null;
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);

            try
            {
                if (payload == null || payload.Length < 1)
                {
                    message = "Quest-result payload is missing.";
                    return false;
                }

                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                byte resultType = reader.ReadByte();
                bool applied = resultType switch
                {
                    6 => TryApplyPacketOwnedQuestTimerAddRange(reader, timeKeepQuestTimer: false, out message),
                    7 => TryApplyPacketOwnedQuestTimerRemoveRange(reader, timeKeepQuestTimer: false, out message),
                    8 => TryApplyPacketOwnedQuestTimerAddSingle(reader, timeKeepQuestTimer: true, out message),
                    9 => TryApplyPacketOwnedQuestTimerRemoveRange(reader, timeKeepQuestTimer: true, out message),
                    10 => TryApplyPacketOwnedQuestResultPresentation(reader, out message),
                    11 => TryApplyPacketOwnedQuestResultFixedNotice(resultType, out message),
                    12 => TryApplyPacketOwnedQuestResultActionNotice(reader, out message),
                    13 => TryApplyPacketOwnedQuestResultFixedNotice(resultType, out message),
                    14 => TryApplyPacketOwnedQuestResultNoOp(out message),
                    15 => TryApplyPacketOwnedQuestResultFixedNotice(resultType, out message),
                    16 => TryApplyPacketOwnedQuestResultFixedNotice(resultType, out message),
                    17 => TryApplyPacketOwnedQuestTimerExpiry(reader, out message),
                    18 => TryApplyPacketOwnedQuestTimerReset(reader, out message),
                    _ => ApplyUnsupportedPacketOwnedQuestResult(resultType, out message)
                };

                if (stream.Position != stream.Length)
                {
                    message = $"Quest-result payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                return applied;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException || ex is InvalidDataException)
            {
                message = $"Quest-result payload could not be decoded: {ex.Message}";
                return false;
            }
            finally
            {
                RefreshQuestUiState();
            }
        }

        private bool TryApplyPacketOwnedQuestTimerAddRange(BinaryReader reader, bool timeKeepQuestTimer, out string message)
        {
            ushort count = reader.ReadUInt16();
            var applied = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                int questId = reader.ReadUInt16();
                int remainingMs = reader.ReadInt32();
                applied.Add(_packetFieldStateRuntime.ApplyQuestTimer(questId, remainingMs, timeKeepQuestTimer, currTickCount));
            }

            message = applied.Count == 0
                ? "Quest-result timer packet did not contain any quest timers."
                : string.Join(" ", applied);
            return true;
        }

        private bool TryApplyPacketOwnedQuestTimerAddSingle(BinaryReader reader, bool timeKeepQuestTimer, out string message)
        {
            int questId = reader.ReadUInt16();
            int remainingMs = reader.ReadInt32();
            message = _packetFieldStateRuntime.ApplyQuestTimer(questId, remainingMs, timeKeepQuestTimer, currTickCount);
            return true;
        }

        private bool TryApplyPacketOwnedQuestTimerRemoveRange(BinaryReader reader, bool timeKeepQuestTimer, out string message)
        {
            ushort count = reader.ReadUInt16();
            var removed = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                int questId = reader.ReadUInt16();
                removed.Add(_packetFieldStateRuntime.RemoveQuestTimer(questId, timeKeepQuestTimer));
            }

            message = removed.Count == 0
                ? "Quest-result timer removal packet did not contain any quest timers."
                : string.Join(" ", removed);
            return true;
        }

        private bool TryApplyPacketOwnedQuestTimerExpiry(BinaryReader reader, out string message)
        {
            int questId = reader.ReadUInt16();
            string timerMessage = _packetFieldStateRuntime.RemoveQuestTimer(questId, timeKeepQuestTimer: false);
            string questName = _questRuntime.TryGetQuestName(questId, out string resolvedQuestName)
                ? resolvedQuestName
                : $"Quest #{questId}";
            string expiryMessage = $"Quest timer expired for {questName}.";
            _chat?.AddMessage(expiryMessage, new Color(255, 228, 151), currTickCount);
            message = $"{timerMessage} {expiryMessage}";
            return true;
        }

        private bool TryApplyPacketOwnedQuestTimerReset(BinaryReader reader, out string message)
        {
            int questId = reader.ReadUInt16();
            message = _packetFieldStateRuntime.ResetQuestTimer(questId, timeKeepQuestTimer: false, currTickCount);
            return true;
        }

        private bool TryApplyPacketOwnedQuestResultPresentation(BinaryReader reader, out string message)
        {
            int questId = reader.ReadUInt16();
            int speakerNpcId = reader.ReadInt32();
            if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(ushort))
            {
                throw new InvalidDataException("Quest-result subtype 10 requires a trailing follow-up quest id.");
            }

            int followUpQuestId = reader.ReadUInt16();
            bool hasQuestRecord = _questRuntime.HasQuestRecord(questId);
            if (!_questRuntime.TryBuildClientPacketQuestResultPresentation(
                    questId,
                    _playerManager?.Player?.Build,
                    hasQuestRecord,
                    out PacketQuestResultPresentation presentation))
            {
                message = $"Quest-result packet references unknown quest #{questId}.";
                return false;
            }

            bool showedNotice = false;
            bool openedModal = false;
            if (!string.IsNullOrWhiteSpace(presentation.NoticeText))
            {
                _chat?.AddMessage(presentation.NoticeText, new Color(255, 228, 151), currTickCount);
                showedNotice = true;
            }

            if (presentation.ModalPages.Count > 0 &&
                _npcInteractionOverlay != null)
            {
                OpenPacketOwnedQuestResultModal(speakerNpcId, presentation);
                openedModal = true;
            }

            string followUpStatus = string.Empty;
            string resultSummary = $"Applied packet-owned quest result for {presentation.QuestName}.";
            if (showedNotice && openedModal)
            {
                resultSummary = $"{resultSummary} Displayed the notice and opened {presentation.ModalPages.Count} modal quest page(s).";
            }
            else if (showedNotice)
            {
                resultSummary = $"{resultSummary} Displayed the quest notice.";
            }
            else if (openedModal)
            {
                resultSummary = $"{resultSummary} Opened {presentation.ModalPages.Count} modal quest page(s).";
            }

            if (followUpQuestId > 0)
            {
                string followUpQuestName = _questRuntime.TryGetQuestName(followUpQuestId, out string resolvedFollowUpName)
                    ? resolvedFollowUpName
                    : $"Quest #{followUpQuestId}";
                if (openedModal)
                {
                    QueuePendingPacketOwnedQuestResultFollowUp(followUpQuestId, speakerNpcId, followUpQuestName);
                    followUpStatus = $"Queued packet-owned StartQuest for {followUpQuestName} after the quest-result dialog closes.";
                }
                else
                {
                    followUpStatus = ApplyPacketOwnedQuestResultFollowUpQuest(followUpQuestId, speakerNpcId, followUpQuestName);
                }
            }

            if (!string.IsNullOrWhiteSpace(followUpStatus))
            {
                resultSummary = $"{resultSummary} {followUpStatus}";
            }

            message = resultSummary;
            return showedNotice
                || openedModal
                || !string.IsNullOrWhiteSpace(presentation.NoticeText)
                || !string.IsNullOrWhiteSpace(followUpStatus);
        }

        private bool TryApplyPacketOwnedQuestResultActionNotice(BinaryReader reader, out string message)
        {
            int questId = reader.ReadUInt16();
            bool hasQuestRecord = _questRuntime.HasQuestRecord(questId);
            if (!_questRuntime.TryBuildClientPacketQuestResultActionNotice(
                    questId,
                    _playerManager?.Player?.Build,
                    hasQuestRecord,
                    out string questName,
                    out string noticeText))
            {
                message = $"Quest-result packet references unknown quest #{questId}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(noticeText))
            {
                _chat?.AddMessage(noticeText, new Color(255, 228, 151), currTickCount);
            }

            message = string.IsNullOrWhiteSpace(noticeText)
                ? $"Quest-result action summary for {questName} did not resolve any visible notice text."
                : $"Displayed the packet-owned quest action summary for {questName}.";
            return !string.IsNullOrWhiteSpace(noticeText);
        }

        private bool TryApplyPacketOwnedQuestResultNoOp(out string message)
        {
            message = "Ignored packet-owned quest-result subtype 14; the client only refreshes quest availability after this branch.";
            return true;
        }

        private bool TryApplyPacketOwnedQuestResultFixedNotice(int resultType, out string message)
        {
            if (!QuestClientDirectNoticeText.TryResolve(resultType, out string noticeText, out int stringPoolId))
            {
                message = $"Quest-result subtype {resultType} is not modeled by the simulator yet.";
                return false;
            }

            _chat?.AddMessage(noticeText, new Color(255, 228, 151), currTickCount);
            message = $"Displayed the packet-owned fixed quest-result notice for subtype {resultType} (StringPool 0x{stringPoolId:X}).";
            return true;
        }

        private void OpenPacketOwnedQuestResultModal(int speakerNpcId, PacketQuestResultPresentation presentation)
        {
            string npcName = ResolvePacketOwnedQuestResultSpeakerName(speakerNpcId, presentation.QuestName);
            _npcInteractionOverlay.Open(new NpcInteractionState
            {
                NpcName = npcName,
                SelectedEntryId = presentation.QuestId,
                PresentationStyle = NpcInteractionPresentationStyle.PacketScriptUtilDialog,
                Entries = new[]
                {
                    new NpcInteractionEntry
                    {
                        EntryId = presentation.QuestId,
                        QuestId = presentation.QuestId,
                        Kind = NpcInteractionEntryKind.Talk,
                        Title = presentation.QuestName,
                        Subtitle = "Quest Result",
                        Pages = presentation.ModalPages
                    }
                }
            });
        }

        private string ResolvePacketOwnedQuestResultSpeakerName(int speakerNpcId, string fallbackQuestName)
        {
            NpcItem npc = FindNpcById(speakerNpcId);
            if (speakerNpcId > 0 &&
                Program.InfoManager?.NpcNameCache != null &&
                Program.InfoManager.NpcNameCache.TryGetValue(speakerNpcId.ToString(), out Tuple<string, string> npcInfo) &&
                !string.IsNullOrWhiteSpace(npcInfo?.Item1))
            {
                return npcInfo.Item1;
            }

            if (npc != null && speakerNpcId > 0)
            {
                return $"NPC {speakerNpcId}";
            }

            return speakerNpcId > 0
                ? $"NPC {speakerNpcId}"
                : fallbackQuestName;
        }

        private void QueuePendingPacketOwnedQuestResultFollowUp(int followUpQuestId, int speakerNpcId, string followUpQuestName)
        {
            _pendingPacketOwnedQuestResultFollowUpQuestId = followUpQuestId;
            _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = speakerNpcId;
            _pendingPacketOwnedQuestResultFollowUpQuestName = followUpQuestName ?? string.Empty;
        }

        private void UpdatePendingPacketOwnedQuestResultFollowUp()
        {
            if (!_pendingPacketOwnedQuestResultFollowUpQuestId.HasValue ||
                _pendingPacketOwnedQuestResultFollowUpQuestId.Value <= 0 ||
                _npcInteractionOverlay?.IsVisible == true)
            {
                return;
            }

            int followUpQuestId = _pendingPacketOwnedQuestResultFollowUpQuestId.Value;
            int speakerNpcId = _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId;
            string followUpQuestName = _pendingPacketOwnedQuestResultFollowUpQuestName;

            _pendingPacketOwnedQuestResultFollowUpQuestId = null;
            _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
            _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;

            string followUpStatus = ApplyPacketOwnedQuestResultFollowUpQuest(followUpQuestId, speakerNpcId, followUpQuestName);
            if (!string.IsNullOrWhiteSpace(followUpStatus))
            {
                _chat?.AddMessage(followUpStatus, new Color(255, 228, 151), currTickCount);
            }
        }

        private string ApplyPacketOwnedQuestResultFollowUpQuest(int followUpQuestId, int speakerNpcId, string followUpQuestName)
        {
            if (followUpQuestId <= 0)
            {
                return string.Empty;
            }

            QuestWindowActionResult result = _questRuntime.TryAcceptFromQuestWindow(followUpQuestId, _playerManager?.Player?.Build);
            HandleQuestWindowActionResult(result);

            string resolvedQuestName = string.IsNullOrWhiteSpace(followUpQuestName)
                ? (_questRuntime.TryGetQuestName(followUpQuestId, out string runtimeQuestName) ? runtimeQuestName : $"Quest #{followUpQuestId}")
                : followUpQuestName;
            bool started = result?.StateChanged == true;
            string speakerLabel = ResolvePacketOwnedQuestResultSpeakerName(speakerNpcId, resolvedQuestName);
            return started
                ? $"Packet-owned StartQuest accepted {resolvedQuestName} from {speakerLabel}."
                : $"Packet-owned StartQuest for {resolvedQuestName} did not change quest state.";
        }

        private static bool ApplyUnsupportedPacketOwnedQuestResult(int resultType, out string message)
        {
            message = $"Quest-result subtype {resultType} is not modeled by the simulator yet.";
            return false;
        }
    }
}
