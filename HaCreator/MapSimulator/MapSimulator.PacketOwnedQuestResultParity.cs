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
            PacketQuestResultTextKind textKind = PacketQuestResultTextKind.Auto;
            PacketQuestResultViewMode viewMode = PacketQuestResultViewMode.NoticeAndModal;
            int followUpQuestId = 0;

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte encodedTextKind = reader.ReadByte();
                if (Enum.IsDefined(typeof(PacketQuestResultTextKind), encodedTextKind))
                {
                    textKind = (PacketQuestResultTextKind)encodedTextKind;
                }
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte encodedViewMode = reader.ReadByte();
                if (Enum.IsDefined(typeof(PacketQuestResultViewMode), encodedViewMode))
                {
                    viewMode = (PacketQuestResultViewMode)encodedViewMode;
                }
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(ushort))
                {
                    throw new InvalidDataException("Quest-result follow-up quest id must be a UInt16 value.");
                }

                followUpQuestId = reader.ReadUInt16();
            }

            if (!_questRuntime.TryBuildPacketQuestResultPresentation(
                    questId,
                    _playerManager?.Player?.Build,
                    textKind,
                    out PacketQuestResultPresentation presentation))
            {
                message = $"Quest-result packet references unknown quest #{questId}.";
                return false;
            }

            bool showedNotice = false;
            bool openedModal = false;
            if (viewMode != PacketQuestResultViewMode.ModalOnly &&
                !string.IsNullOrWhiteSpace(presentation.NoticeText))
            {
                _chat?.AddMessage(presentation.NoticeText, new Color(255, 228, 151), currTickCount);
                showedNotice = true;
            }

            if (viewMode != PacketQuestResultViewMode.NoticeOnly &&
                presentation.ModalPages.Count > 0 &&
                _npcInteractionOverlay != null)
            {
                OpenPacketOwnedQuestResultModal(speakerNpcId, presentation);
                openedModal = true;
            }

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
                resultSummary = $"{resultSummary} Follow-up quest requested: {followUpQuestName}.";
            }

            message = resultSummary;
            return showedNotice || openedModal || !string.IsNullOrWhiteSpace(presentation.NoticeText);
        }

        private void OpenPacketOwnedQuestResultModal(int speakerNpcId, PacketQuestResultPresentation presentation)
        {
            string npcName = ResolvePacketOwnedQuestResultSpeakerName(speakerNpcId, presentation.QuestName);
            _npcInteractionOverlay.Open(new NpcInteractionState
            {
                NpcName = npcName,
                SelectedEntryId = presentation.QuestId,
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
            if (npc != null && speakerNpcId > 0)
            {
                return $"NPC {speakerNpcId}";
            }

            return speakerNpcId > 0
                ? $"NPC {speakerNpcId}"
                : fallbackQuestName;
        }

        private static bool ApplyUnsupportedPacketOwnedQuestResult(int resultType, out string message)
        {
            message = $"Quest-result subtype {resultType} is not modeled by the simulator yet.";
            return false;
        }
    }
}
