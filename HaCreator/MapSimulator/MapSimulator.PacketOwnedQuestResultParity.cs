using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private int _pendingPacketOwnedQuestResultContinuationQuestId;
        private byte[] _pendingPacketOwnedQuestResultTrailingFollowUpPayload = Array.Empty<byte>();
        private int? _pendingPacketOwnedQuestResultFollowUpQuestId;
        private int _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId;
        private string _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
        private bool _pendingPacketOwnedQuestResultFollowUpReady;
        private string _pendingPacketOwnedQuestResultDeferredNoticeText = string.Empty;
        private PacketQuestResultNoticeSurface _pendingPacketOwnedQuestResultDeferredNoticeSurface;
        private bool _pendingPacketOwnedQuestResultDeferredNoticeAutoSeparated = true;
        private IReadOnlyList<int> _pendingPacketOwnedQuestResultAvailableQuestIdsBeforePacket = Array.Empty<int>();
        private bool _hasPendingPacketOwnedQuestResultAvailabilityRefresh;
        private bool _deferPacketOwnedQuestAvailabilityRefreshForCurrentPayload;
        private readonly List<PendingQuestDeliveryResultOwnership> _pendingQuestDeliveryResults = new();
        private int _pendingPacketOwnedStartQuestResponseQuestId;
        private int _pendingPacketOwnedStartQuestResponseSpeakerNpcId;
        private string _pendingPacketOwnedStartQuestResponseQuestName = string.Empty;
        private int _pendingPacketOwnedStartQuestResponseRequestedAtTick = int.MinValue;
        private bool _packetOwnedQuestResultStartQuestRequestSent;
        private int _packetOwnedQuestResultStartQuestRequestTick = int.MinValue;
        private readonly PacketQuestResultFadeWindowRuntime _packetQuestResultFadeWindowRuntime = new();
        private const int PacketOwnedQuestResultStartQuestExclusiveRequestCooldownMs = 500;
        private const int MaxPendingQuestDeliveryResultOwnershipCount = 8;

        private bool TryApplyPacketOwnedQuestResultPayload(byte[] payload, out string message)
        {
            message = null;
            _deferPacketOwnedQuestAvailabilityRefreshForCurrentPayload = false;
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
            IReadOnlyList<int> availableQuestIdsBeforePacket = _questRuntime.CaptureAvailableQuestIds(_playerManager?.Player?.Build);

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
                    10 => TryApplyPacketOwnedQuestResultPresentation(reader, availableQuestIdsBeforePacket, out message),
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

                if (applied && !_deferPacketOwnedQuestAvailabilityRefreshForCurrentPayload)
                {
                    AppendPacketOwnedQuestAvailabilityRefreshSummary(ref message, availableQuestIdsBeforePacket);
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
                _deferPacketOwnedQuestAvailabilityRefreshForCurrentPayload = false;
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
            string expiryMessage = QuestClientPacketResultNoticeText.FormatQuestExpiredNotice(questName);
            _chat?.AddSystemMessage(expiryMessage, currTickCount);
            message = $"{timerMessage} {expiryMessage}";
            return true;
        }

        private bool TryApplyPacketOwnedQuestTimerReset(BinaryReader reader, out string message)
        {
            int questId = reader.ReadUInt16();
            message = _packetFieldStateRuntime.ResetQuestTimer(questId, timeKeepQuestTimer: false, currTickCount);
            return true;
        }

        private bool TryApplyPacketOwnedQuestResultPresentation(
            BinaryReader reader,
            IReadOnlyList<int> availableQuestIdsBeforePacket,
            out string message)
        {
            int questId = reader.ReadUInt16();
            int speakerNpcId = reader.ReadInt32();
            long trailingPayloadLength = reader.BaseStream.Length - reader.BaseStream.Position;
            if (trailingPayloadLength < sizeof(ushort))
            {
                throw new InvalidDataException("Quest-result subtype 10 requires a trailing follow-up quest id.");
            }

            if (trailingPayloadLength != sizeof(ushort))
            {
                throw new InvalidDataException("Quest-result subtype 10 trailing follow-up quest id must be exactly 2 bytes.");
            }

            byte[] trailingFollowUpPayload = reader.ReadBytes((int)trailingPayloadLength);
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

            // CUserLocal::OnQuestResult always issues DeleteFadeWnd(7, 0, questId, "", 0)
            // before it consumes the trailing follow-up quest id, even when no modal pages open.
            _packetQuestResultFadeWindowRuntime.RegisterQuestFadeWindow(questId);

            bool showedNotice = false;
            bool openedModal = presentation.ModalPages.Count > 0 &&
                               _npcInteractionOverlay != null;
            bool deferredNoticeUntilDialogClose = false;
            PacketQuestResultNoticeRouting noticeRouting = default;
            if (!string.IsNullOrWhiteSpace(presentation.NoticeText))
            {
                noticeRouting = PacketQuestResultClientSemantics.ResolveNoticeRouting(resultType: 10, openedModal);
                deferredNoticeUntilDialogClose = noticeRouting.Stage == PacketQuestResultNoticeDispatchStage.AfterDialog;
                if (deferredNoticeUntilDialogClose)
                {
                    QueuePendingPacketOwnedQuestResultNotice(
                        presentation.NoticeText,
                        noticeRouting.Surface,
                        noticeRouting.AutoSeparated);
                }
                else
                {
                    DispatchPacketOwnedQuestResultNotice(
                        presentation.NoticeText,
                        noticeRouting.Surface,
                        noticeRouting.AutoSeparated);
                }

                showedNotice = true;
            }

            if (openedModal)
            {
                OpenPacketOwnedQuestResultModal(speakerNpcId, presentation);
            }

            string followUpStatus = string.Empty;
            string resultSummary = $"Applied packet-owned quest result for {presentation.QuestName}.";
            if (showedNotice && openedModal)
            {
                resultSummary = deferredNoticeUntilDialogClose
                    ? $"{resultSummary} Opened {presentation.ModalPages.Count} modal quest page(s) and queued the client-shaped notice for display after the dialog closes."
                    : $"{resultSummary} Displayed the notice and opened {presentation.ModalPages.Count} modal quest page(s).";
            }
            else if (showedNotice)
            {
                resultSummary = $"{resultSummary} Displayed the quest notice.";
            }
            else if (openedModal)
            {
                resultSummary = $"{resultSummary} Opened {presentation.ModalPages.Count} modal quest page(s).";
            }

            if (openedModal)
            {
                QueuePendingPacketOwnedQuestResultContinuation(questId, trailingFollowUpPayload, speakerNpcId);
                QueuePendingPacketOwnedQuestResultAvailabilityRefresh(availableQuestIdsBeforePacket);
                _deferPacketOwnedQuestAvailabilityRefreshForCurrentPayload =
                    PacketQuestResultClientSemantics.ResolveAvailabilityRefreshDisposition(
                        resultType: 10,
                        openedModal: true) == PacketQuestResultAvailabilityRefreshDisposition.AfterModalContinuation;
                followUpStatus =
                    "Queued packet-owned subtype 10 trailing follow-up quest processing and availability refresh after the quest-result dialog returns through Next or OK.";
            }
            else
            {
                bool clearedQuestFadeWindow = ApplyPendingPacketOwnedQuestResultFadeCleanup(questId);
                if (clearedQuestFadeWindow)
                {
                    resultSummary =
                        $"{resultSummary} Cleared the packet-owned quest fade window owner before consuming the trailing follow-up quest id.";
                }

                if (!PacketQuestResultClientSemantics.TryDecodeSubtype10TrailingFollowUpQuestId(
                        trailingFollowUpPayload,
                        out int followUpQuestId,
                        out string decodeError))
                {
                    message = decodeError;
                    return false;
                }

                if (followUpQuestId > 0)
                {
                    string followUpQuestName = _questRuntime.TryGetQuestName(followUpQuestId, out string resolvedFollowUpName)
                        ? resolvedFollowUpName
                        : $"Quest #{followUpQuestId}";
                    bool blocksFollowUpUntilNoticeClose = showedNotice
                                                          && noticeRouting.Surface == PacketQuestResultNoticeSurface.UtilDialogNotice;
                    if (blocksFollowUpUntilNoticeClose)
                    {
                        QueuePendingPacketOwnedQuestResultFollowUp(followUpQuestId, speakerNpcId, followUpQuestName);
                        _pendingPacketOwnedQuestResultFollowUpReady = true;
                        followUpStatus =
                            $"Queued packet-owned StartQuest continuation for {followUpQuestName} until the client-shaped quest notice closes.";
                    }
                    else
                    {
                        followUpStatus = ApplyPacketOwnedQuestResultFollowUpQuest(followUpQuestId, speakerNpcId, followUpQuestName);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(followUpStatus))
            {
                resultSummary = $"{resultSummary} {followUpStatus}";
            }

            if (TryResolvePendingPacketOwnedStartQuestResponse(questId, presentation.QuestName, out string startQuestResponseOutcome))
            {
                resultSummary = $"{resultSummary} {startQuestResponseOutcome}";
            }

            if (TryResolvePendingQuestDeliveryQuestResult(questId, out string deliveryOutcome))
            {
                resultSummary = $"{resultSummary} {deliveryOutcome}";
            }

            message = resultSummary;
            // CUserLocal::OnQuestResult still owns subtype 10 after decode even when
            // the filtered Quest/Act.img branch resolves to no visible notice.
            return true;
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

            PacketQuestResultNoticeRouting noticeRouting = default;
            if (!string.IsNullOrWhiteSpace(noticeText))
            {
                noticeRouting = PacketQuestResultClientSemantics.ResolveNoticeRouting(resultType: 12, openedModal: false);
                DispatchPacketOwnedQuestResultNotice(
                    noticeText,
                    noticeRouting.Surface,
                    noticeRouting.AutoSeparated);
            }

            message = string.IsNullOrWhiteSpace(noticeText)
                ? $"Quest-result action summary for {questName} did not resolve any visible notice text."
                : $"Displayed the packet-owned quest action summary for {questName} through the client-shaped UtilDlgEx notice surface (bAutoSeparated = {(noticeRouting.AutoSeparated ? 1 : 0)}).";
            if (TryResolvePendingPacketOwnedStartQuestResponse(questId, questName, out string startQuestResponseOutcome))
            {
                message = $"{message} {startQuestResponseOutcome}";
            }

            if (TryResolvePendingQuestDeliveryQuestResult(questId, out string deliveryOutcome))
            {
                message = $"{message} {deliveryOutcome}";
            }

            return true;
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

            ShowPacketOwnedRewardResultNotice(
                noticeText,
                autoSeparated: PacketQuestResultClientSemantics.ResolveUtilDialogNoticeAutoSeparated(resultType));
            message = $"Displayed the packet-owned fixed quest-result CUtilDlg::Notice for subtype {resultType} (StringPool 0x{stringPoolId:X}, bAutoSeparated = 0).";
            return true;
        }

        private void AppendPacketOwnedQuestAvailabilityRefreshSummary(ref string message, IReadOnlyList<int> availableQuestIdsBeforePacket)
        {
            IReadOnlyList<int> newlyAvailableQuestIds = _questRuntime.RefreshPacketOwnedQuestAvailability(
                _playerManager?.Player?.Build,
                availableQuestIdsBeforePacket);
            if (newlyAvailableQuestIds == null || newlyAvailableQuestIds.Count == 0)
            {
                return;
            }

            string summary;
            if (newlyAvailableQuestIds.Count == 1)
            {
                int questId = newlyAvailableQuestIds[0];
                string questName = _questRuntime.TryGetQuestName(questId, out string resolvedQuestName)
                    ? resolvedQuestName
                    : $"Quest #{questId}";
                summary = $"Refreshed packet-owned quest availability and flagged {questName} as newly available.";
            }
            else
            {
                summary = $"Refreshed packet-owned quest availability and flagged {newlyAvailableQuestIds.Count} newly available quest(s).";
            }

            message = string.IsNullOrWhiteSpace(message)
                ? summary
                : $"{message} {summary}";
        }

        private void OpenPacketOwnedQuestResultModal(int speakerNpcId, PacketQuestResultPresentation presentation)
        {
            string npcName = ResolvePacketOwnedQuestResultSpeakerName(speakerNpcId, presentation.QuestName);
            _npcInteractionOverlay.Open(new NpcInteractionState
            {
                NpcName = npcName,
                SpeakerTemplateId = speakerNpcId,
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
                },
                PresentationStyle = NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
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
            _pendingPacketOwnedQuestResultFollowUpReady = false;
        }

        private void QueuePendingPacketOwnedQuestResultContinuation(
            int questId,
            byte[] trailingFollowUpPayload,
            int speakerNpcId)
        {
            _pendingPacketOwnedQuestResultContinuationQuestId = Math.Max(0, questId);
            _pendingPacketOwnedQuestResultTrailingFollowUpPayload = trailingFollowUpPayload ?? Array.Empty<byte>();
            _pendingPacketOwnedQuestResultFollowUpQuestId = null;
            _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = Math.Max(0, speakerNpcId);
            _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
            _pendingPacketOwnedQuestResultFollowUpReady = false;
        }

        private void QueuePendingPacketOwnedQuestResultNotice(
            string noticeText,
            PacketQuestResultNoticeSurface surface,
            bool autoSeparated)
        {
            _pendingPacketOwnedQuestResultDeferredNoticeText = noticeText ?? string.Empty;
            _pendingPacketOwnedQuestResultDeferredNoticeSurface = surface;
            _pendingPacketOwnedQuestResultDeferredNoticeAutoSeparated = autoSeparated;
        }

        private void QueuePendingPacketOwnedQuestResultAvailabilityRefresh(IReadOnlyList<int> availableQuestIdsBeforePacket)
        {
            _pendingPacketOwnedQuestResultAvailableQuestIdsBeforePacket =
                availableQuestIdsBeforePacket ?? Array.Empty<int>();
            _hasPendingPacketOwnedQuestResultAvailabilityRefresh = true;
        }

        private void DispatchPacketOwnedQuestResultNotice(
            string noticeText,
            PacketQuestResultNoticeSurface surface,
            bool autoSeparated)
        {
            if (string.IsNullOrWhiteSpace(noticeText))
            {
                return;
            }

            if (surface == PacketQuestResultNoticeSurface.UtilDialogNotice)
            {
                ShowPacketOwnedRewardResultNotice(noticeText, autoSeparated: autoSeparated);
                return;
            }

            _chat?.AddSystemMessage(noticeText, currTickCount);
        }

        private void DispatchPendingPacketOwnedQuestResultNotice()
        {
            if (string.IsNullOrWhiteSpace(_pendingPacketOwnedQuestResultDeferredNoticeText))
            {
                return;
            }

            DispatchPacketOwnedQuestResultNotice(
                _pendingPacketOwnedQuestResultDeferredNoticeText,
                _pendingPacketOwnedQuestResultDeferredNoticeSurface,
                _pendingPacketOwnedQuestResultDeferredNoticeAutoSeparated);
            _pendingPacketOwnedQuestResultDeferredNoticeText = string.Empty;
            _pendingPacketOwnedQuestResultDeferredNoticeSurface = PacketQuestResultNoticeSurface.Chat;
            _pendingPacketOwnedQuestResultDeferredNoticeAutoSeparated = true;
        }

        private void HandlePacketOwnedQuestResultOverlayClose(NpcInteractionOverlayCloseKind closeKind)
        {
            if (closeKind == NpcInteractionOverlayCloseKind.None ||
                !HasPendingPacketOwnedQuestResultOverlayCloseWork())
            {
                return;
            }

            PacketQuestResultAvailabilityRefreshDisposition availabilityRefreshDisposition =
                PacketQuestResultClientSemantics.ResolveAvailabilityRefreshDisposition(
                    resultType: 10,
                    openedModal: true,
                    closeKind);
            PacketQuestResultUtilDialogModalHostSnapshot modalHostSnapshot =
                _npcInteractionOverlay?.PacketQuestResultModalHostSnapshot ?? default;
            int? doModalReturnCode = modalHostSnapshot.TerminalResultCount > 0
                ? modalHostSnapshot.LastTerminalDoModalReturnCode
                : null;
            PacketQuestResultSubtype10ContinuationDisposition continuationDisposition =
                PacketQuestResultClientSemantics.ResolveSubtype10ContinuationDisposition(
                    closeKind,
                    doModalReturnCode);
            // CUserLocal::OnQuestResult always runs the post-branch tail after subtype 10
            // modal return, regardless of DoModal result. Only StartQuest continuation is
            // gated by the Next/OK return value (0x2001).
            DispatchPendingPacketOwnedQuestResultNotice();
            ApplyPendingPacketOwnedQuestResultFadeCleanup(_pendingPacketOwnedQuestResultContinuationQuestId);
            if (!TryArmPendingPacketOwnedQuestResultFollowUpFromTrailingPayload(out string error))
            {
                _chat?.AddSystemMessage(error, currTickCount);
            }

            if (continuationDisposition == PacketQuestResultSubtype10ContinuationDisposition.Continue &&
                _pendingPacketOwnedQuestResultFollowUpReady)
            {
                _pendingPacketOwnedQuestResultContinuationQuestId = 0;
                TryApplyPendingPacketOwnedQuestResultFollowUpImmediate();
            }

            if (availabilityRefreshDisposition == PacketQuestResultAvailabilityRefreshDisposition.AfterModalContinuation)
            {
                DispatchPendingPacketOwnedQuestAvailabilityRefresh(availabilityRefreshDisposition);
            }
            else if (availabilityRefreshDisposition == PacketQuestResultAvailabilityRefreshDisposition.Abandon)
            {
                ClearPendingPacketOwnedQuestResultAvailabilityRefresh();
            }

            if (_pendingPacketOwnedQuestResultFollowUpQuestId.HasValue &&
                continuationDisposition != PacketQuestResultSubtype10ContinuationDisposition.Continue)
            {
                _pendingPacketOwnedQuestResultFollowUpQuestId = null;
                _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
                _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
                _pendingPacketOwnedQuestResultFollowUpReady = false;
            }

            ClearPendingPacketOwnedQuestResultContinuation();
        }

        private bool HasPendingPacketOwnedQuestResultOverlayCloseWork()
        {
            return _pendingPacketOwnedQuestResultContinuationQuestId > 0
                   || (_pendingPacketOwnedQuestResultTrailingFollowUpPayload?.Length ?? 0) > 0
                   || _pendingPacketOwnedQuestResultFollowUpQuestId.HasValue
                   || !string.IsNullOrWhiteSpace(_pendingPacketOwnedQuestResultDeferredNoticeText)
                   || _hasPendingPacketOwnedQuestResultAvailabilityRefresh;
        }

        private void UpdatePendingPacketOwnedQuestResultFollowUp()
        {
            TryApplyPendingPacketOwnedQuestResultFollowUpImmediate();
        }

        private void TryApplyPendingPacketOwnedQuestResultFollowUpImmediate()
        {
            if (!_pendingPacketOwnedQuestResultFollowUpQuestId.HasValue ||
                _pendingPacketOwnedQuestResultFollowUpQuestId.Value <= 0 ||
                !_pendingPacketOwnedQuestResultFollowUpReady ||
                _npcInteractionOverlay?.IsVisible == true ||
                IsPacketOwnedQuestResultNoticeVisible())
            {
                return;
            }

            int followUpQuestId = _pendingPacketOwnedQuestResultFollowUpQuestId.Value;
            int speakerNpcId = _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId;
            string followUpQuestName = _pendingPacketOwnedQuestResultFollowUpQuestName;

            _pendingPacketOwnedQuestResultFollowUpQuestId = null;
            _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
            _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
            _pendingPacketOwnedQuestResultFollowUpReady = false;

            string followUpStatus = ApplyPacketOwnedQuestResultFollowUpQuest(followUpQuestId, speakerNpcId, followUpQuestName);
            if (!string.IsNullOrWhiteSpace(followUpStatus))
            {
                _chat?.AddSystemMessage(followUpStatus, currTickCount);
            }
        }

        private void DispatchPendingPacketOwnedQuestAvailabilityRefresh(
            PacketQuestResultAvailabilityRefreshDisposition availabilityRefreshDisposition)
        {
            if (availabilityRefreshDisposition != PacketQuestResultAvailabilityRefreshDisposition.AfterModalContinuation)
            {
                ClearPendingPacketOwnedQuestResultAvailabilityRefresh();
                return;
            }

            string refreshStatus = string.Empty;
            AppendPacketOwnedQuestAvailabilityRefreshSummary(
                ref refreshStatus,
                ConsumePendingPacketOwnedQuestResultAvailabilitySnapshot());
            if (!string.IsNullOrWhiteSpace(refreshStatus))
            {
                _chat?.AddSystemMessage(refreshStatus, currTickCount);
            }
        }

        private bool ApplyPendingPacketOwnedQuestResultFadeCleanup(int questId)
        {
            _pendingPacketOwnedQuestResultContinuationQuestId = 0;
            return _packetQuestResultFadeWindowRuntime.ApplyQuestResultDeleteFadeWindow(questId);
        }

        private bool TryArmPendingPacketOwnedQuestResultFollowUpFromTrailingPayload(out string error)
        {
            error = string.Empty;
            if (_pendingPacketOwnedQuestResultTrailingFollowUpPayload == null ||
                _pendingPacketOwnedQuestResultTrailingFollowUpPayload.Length == 0)
            {
                _pendingPacketOwnedQuestResultFollowUpQuestId = null;
                _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
                _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
                _pendingPacketOwnedQuestResultFollowUpReady = false;
                return true;
            }

            if (!PacketQuestResultClientSemantics.TryDecodeSubtype10TrailingFollowUpQuestId(
                    _pendingPacketOwnedQuestResultTrailingFollowUpPayload,
                    out int followUpQuestId,
                    out error))
            {
                _pendingPacketOwnedQuestResultTrailingFollowUpPayload = Array.Empty<byte>();
                _pendingPacketOwnedQuestResultFollowUpQuestId = null;
                _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
                _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
                _pendingPacketOwnedQuestResultFollowUpReady = false;
                return false;
            }

            _pendingPacketOwnedQuestResultTrailingFollowUpPayload = Array.Empty<byte>();
            if (followUpQuestId <= 0)
            {
                _pendingPacketOwnedQuestResultFollowUpQuestId = null;
                _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
                _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
                _pendingPacketOwnedQuestResultFollowUpReady = false;
                return true;
            }

            _pendingPacketOwnedQuestResultFollowUpQuestId = followUpQuestId;
            _pendingPacketOwnedQuestResultFollowUpQuestName =
                _questRuntime.TryGetQuestName(followUpQuestId, out string followUpQuestName)
                    ? followUpQuestName
                    : $"Quest #{followUpQuestId}";
            _pendingPacketOwnedQuestResultFollowUpReady = true;
            return true;
        }

        private void ClearPendingPacketOwnedQuestResultContinuation()
        {
            _pendingPacketOwnedQuestResultContinuationQuestId = 0;
            _pendingPacketOwnedQuestResultTrailingFollowUpPayload = Array.Empty<byte>();
            _pendingPacketOwnedQuestResultFollowUpQuestId = null;
            _pendingPacketOwnedQuestResultFollowUpSpeakerNpcId = 0;
            _pendingPacketOwnedQuestResultFollowUpQuestName = string.Empty;
            _pendingPacketOwnedQuestResultFollowUpReady = false;
            _pendingPacketOwnedQuestResultDeferredNoticeText = string.Empty;
            _pendingPacketOwnedQuestResultDeferredNoticeSurface = PacketQuestResultNoticeSurface.Chat;
            _pendingPacketOwnedQuestResultDeferredNoticeAutoSeparated = true;
            ClearPendingPacketOwnedQuestResultAvailabilityRefresh();
        }

        private bool IsPacketOwnedQuestResultNoticeVisible()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.PacketOwnedRewardResultNotice) is PacketOwnedRewardNoticeWindow noticeWindow
                   && noticeWindow.IsVisible;
        }

        private string ApplyPacketOwnedQuestResultFollowUpQuest(int followUpQuestId, int speakerNpcId, string followUpQuestName)
        {
            if (followUpQuestId <= 0)
            {
                return string.Empty;
            }

            string resolvedQuestName = string.IsNullOrWhiteSpace(followUpQuestName)
                ? (_questRuntime.TryGetQuestName(followUpQuestId, out string runtimeQuestName) ? runtimeQuestName : $"Quest #{followUpQuestId}")
                : followUpQuestName;
            string requestDispatchStatus = DispatchPacketOwnedQuestResultFollowUpStartQuestRequest(
                followUpQuestId,
                speakerNpcId,
                resolvedQuestName,
                out bool requestAccepted);
            string speakerLabel = ResolvePacketOwnedQuestResultSpeakerName(speakerNpcId, resolvedQuestName);
            if (requestAccepted)
            {
                RegisterPendingPacketOwnedStartQuestResponse(followUpQuestId, speakerNpcId, resolvedQuestName);
                return
                    $"Queued packet-owned StartQuest continuation for {resolvedQuestName} from {speakerLabel} and deferred local quest-state mutation until a matching quest-result response arrives. {requestDispatchStatus}";
            }

            return
                $"Packet-owned StartQuest continuation for {resolvedQuestName} from {speakerLabel} stayed request-owned and did not mutate local quest state because no outbound request path was accepted. {requestDispatchStatus}";
        }

        private string DispatchPacketOwnedQuestResultFollowUpStartQuestRequest(
            int followUpQuestId,
            int speakerNpcId,
            string followUpQuestName,
            out bool requestAccepted)
        {
            requestAccepted = false;
            int currentTick = currTickCount;
            if (IsPacketOwnedQuestResultStartQuestRequestBlocked(currentTick))
            {
                return $"Mirrored CWvsContext::StartQuest request remained blocked by the client 500 ms exclusive-request cooldown for {followUpQuestName}.";
            }

            var player = _playerManager?.Player;
            short userX = (short)Math.Clamp(
                (int)Math.Round(player?.X ?? 0f),
                short.MinValue,
                short.MaxValue);
            short userY = (short)Math.Clamp(
                (int)Math.Round(player?.Y ?? 0f),
                short.MinValue,
                short.MaxValue);
            int deliveryItemPosition = ResolvePacketOwnedQuestStartDeliveryItemPosition();
            bool includeUserPosition = PacketOwnedQuestStartRequest.ResolveIncludeUserPosition(
                _questRuntime.IsPacketOwnedAutoAlertQuest(followUpQuestId, _playerManager?.Player?.Build));
            HaCreator.MapSimulator.Interaction.PacketOwnedQuestStartRequest request =
                HaCreator.MapSimulator.Interaction.PacketOwnedQuestStartRequest.Create(
                followUpQuestId,
                speakerNpcId,
                deliveryItemPosition,
                userX,
                userY,
                includeUserPosition);
            byte[] payload = new byte[request.Payload.Count];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = request.Payload[i];
            }

            string payloadHex = payload.Length > 0
                ? Convert.ToHexString(payload)
                : "<empty>";
            string userPositionEncodingText = request.IncludesUserPosition
                ? "Included live user position coordinates because the quest is not auto-alert-owned."
                : "Omitted live user position coordinates because the quest is auto-alert-owned.";
            string summary =
                $"Mirrored CWvsContext::StartQuest(..., bAutoStart = 0) request as opcode {request.Opcode} [{payloadHex}] for {followUpQuestName}. {userPositionEncodingText}";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                    request.Opcode,
                    payload,
                    out string dispatchStatus))
            {
                requestAccepted = true;
                MarkPacketOwnedQuestResultStartQuestRequestSent();
                return $"{summary} Dispatched it through the live local-utility bridge. {dispatchStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                    request.Opcode,
                    payload,
                    out string outboxStatus))
            {
                requestAccepted = true;
                MarkPacketOwnedQuestResultStartQuestRequestSent();
                return $"{summary} Dispatched it through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string deferredBridgeStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    request.Opcode,
                    payload,
                    out deferredBridgeStatus))
            {
                requestAccepted = true;
                MarkPacketOwnedQuestResultStartQuestRequestSent();
                return $"{summary} Queued it for deferred official-session injection after the immediate bridge and outbox paths were unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                    request.Opcode,
                    payload,
                    out string queuedStatus))
            {
                requestAccepted = true;
                MarkPacketOwnedQuestResultStartQuestRequestSent();
                return $"{summary} Queued it for deferred generic local-utility outbox delivery after the immediate bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {queuedStatus}";
            }

            return $"{summary} It remained simulator-owned because neither the live bridge nor the generic outbox nor either deferred queue accepted the request. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}";
        }

        private int ResolvePacketOwnedQuestStartDeliveryItemPosition()
        {
            if (_lastDeliveryItemId <= 0 ||
                !TryResolveQuestDeliveryCashItemSlot(
                    _lastDeliveryItemId,
                    out _,
                    out _,
                    out int deliveryClientSlotIndex))
            {
                return 0;
            }

            return Math.Max(0, deliveryClientSlotIndex);
        }

        private void RegisterPendingPacketOwnedStartQuestResponse(int questId, int speakerNpcId, string questName)
        {
            _pendingPacketOwnedStartQuestResponseQuestId = Math.Max(0, questId);
            _pendingPacketOwnedStartQuestResponseSpeakerNpcId = Math.Max(0, speakerNpcId);
            _pendingPacketOwnedStartQuestResponseQuestName = questName ?? string.Empty;
            _pendingPacketOwnedStartQuestResponseRequestedAtTick = currTickCount;
        }

        private bool TryResolvePendingPacketOwnedStartQuestResponse(
            int questId,
            string fallbackQuestName,
            out string outcome)
        {
            outcome = string.Empty;
            if (_pendingPacketOwnedStartQuestResponseQuestId <= 0 ||
                _pendingPacketOwnedStartQuestResponseQuestId != questId)
            {
                return false;
            }

            QuestWindowActionResult result = _questRuntime.TryStartFromPacketOwnedQuestResult(
                questId,
                _playerManager?.Player?.Build);
            ApplyPacketOwnedQuestResultFollowUpStateChanges(result, questId);
            ClearPacketOwnedQuestResultStartQuestRequestLatch();

            int ageMs = _pendingPacketOwnedStartQuestResponseRequestedAtTick == int.MinValue
                ? 0
                : Math.Max(0, unchecked(currTickCount - _pendingPacketOwnedStartQuestResponseRequestedAtTick));
            string resolvedQuestName = !string.IsNullOrWhiteSpace(_pendingPacketOwnedStartQuestResponseQuestName)
                ? _pendingPacketOwnedStartQuestResponseQuestName
                : (!string.IsNullOrWhiteSpace(fallbackQuestName)
                    ? fallbackQuestName
                    : $"Quest #{questId}");
            string speakerName = ResolvePacketOwnedQuestResultSpeakerName(
                _pendingPacketOwnedStartQuestResponseSpeakerNpcId,
                resolvedQuestName);
            outcome = result?.StateChanged == true
                ? $"Resolved pending packet-owned StartQuest response for {resolvedQuestName} from {speakerName} via quest-result ownership (age {ageMs}ms)."
                : $"Consumed pending packet-owned StartQuest response for {resolvedQuestName} from {speakerName} via quest-result ownership without local quest-state change (age {ageMs}ms).";

            ClearPendingPacketOwnedStartQuestResponse();
            return true;
        }

        private void ClearPendingPacketOwnedStartQuestResponse()
        {
            _pendingPacketOwnedStartQuestResponseQuestId = 0;
            _pendingPacketOwnedStartQuestResponseSpeakerNpcId = 0;
            _pendingPacketOwnedStartQuestResponseQuestName = string.Empty;
            _pendingPacketOwnedStartQuestResponseRequestedAtTick = int.MinValue;
        }

        private void ApplyPacketOwnedQuestResultFollowUpStateChanges(QuestWindowActionResult result, int followUpQuestId)
        {
            if (result?.StateChanged != true)
            {
                return;
            }

            // CUserLocal::OnQuestResult subtype 10 continuation uses CWvsContext::StartQuest
            // (bAutoStart = 0), which is request-owned rather than a quest-window local flow.
            // Keep packet-owned follow-up state refresh in this seam without replaying
            // quest-window chat lines or start-script publication surfaces.
            RefreshQuestUiState();
            SelectQuestInActiveWindow(followUpQuestId);
            UpdateQuestDetailWindow();
        }

        private bool IsPacketOwnedQuestResultStartQuestRequestBlocked(int currentTick)
        {
            return IsPacketOwnedQuestResultStartQuestRequestBlocked(
                _packetOwnedQuestResultStartQuestRequestSent,
                _packetOwnedQuestResultStartQuestRequestTick,
                _packetOwnedUtilityRequestTick,
                currentTick,
                PacketOwnedQuestResultStartQuestExclusiveRequestCooldownMs);
        }

        internal static bool IsPacketOwnedQuestResultStartQuestRequestBlocked(
            bool requestSent,
            int requestSentTick,
            int sharedUtilityRequestTick,
            int currentTick,
            int cooldownMs)
        {
            // CWvsContext::CanSendExclRequest denies send while bExclRequestSent is true,
            // independent of elapsed cooldown ticks.
            if (requestSent)
            {
                return true;
            }

            if (requestSentTick != int.MinValue &&
                unchecked(currentTick - requestSentTick) < Math.Max(0, cooldownMs))
            {
                return true;
            }

            return sharedUtilityRequestTick != int.MinValue &&
                   unchecked(currentTick - sharedUtilityRequestTick) < Math.Max(0, cooldownMs);
        }

        private void MarkPacketOwnedQuestResultStartQuestRequestSent()
        {
            _packetOwnedQuestResultStartQuestRequestSent = true;
            _packetOwnedQuestResultStartQuestRequestTick = currTickCount;
            StampPacketOwnedUtilityRequestState();
        }

        private void ClearPacketOwnedQuestResultStartQuestRequestLatch()
        {
            _packetOwnedQuestResultStartQuestRequestSent = false;
            _packetOwnedQuestResultStartQuestRequestTick = currTickCount;
            StampPacketOwnedUtilityRequestState();
        }

        private bool TryConsumePacketOwnedQuestResultStartQuestLatchFromSharedExclusiveReset()
        {
            if (!ShouldClearPacketOwnedQuestResultStartQuestRequestLatchFromSharedExclusiveReset(
                    _packetOwnedQuestResultStartQuestRequestSent))
            {
                return false;
            }

            ClearPacketOwnedQuestResultStartQuestRequestLatch();
            return true;
        }

        internal static bool ShouldClearPacketOwnedQuestResultStartQuestRequestLatchFromSharedExclusiveReset(
            bool requestSent)
        {
            // Follow-up quest-start ownership shares CWvsContext exclusive-request send state.
            return requestSent;
        }

        private bool TryConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(byte[] payload)
        {
            if (!ShouldConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(payload))
            {
                return false;
            }

            return TryConsumePacketOwnedQuestResultStartQuestLatchFromSharedExclusiveReset();
        }

        internal static bool ShouldConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(
            byte[] payload)
        {
            return TryDecodeInventoryOperationExclusiveResetMarker(
                       payload,
                       out bool resetMarker,
                       out _)
                   && resetMarker;
        }

        private IReadOnlyList<int> ConsumePendingPacketOwnedQuestResultAvailabilitySnapshot()
        {
            if (!_hasPendingPacketOwnedQuestResultAvailabilityRefresh)
            {
                return _questRuntime.CaptureAvailableQuestIds(_playerManager?.Player?.Build);
            }

            IReadOnlyList<int> snapshot = _pendingPacketOwnedQuestResultAvailableQuestIdsBeforePacket;
            ClearPendingPacketOwnedQuestResultAvailabilityRefresh();
            return snapshot ?? Array.Empty<int>();
        }

        private void ClearPendingPacketOwnedQuestResultAvailabilityRefresh()
        {
            _pendingPacketOwnedQuestResultAvailableQuestIdsBeforePacket = Array.Empty<int>();
            _hasPendingPacketOwnedQuestResultAvailabilityRefresh = false;
        }

        private void RegisterPendingQuestDeliveryQuestResult(
            int questId,
            bool completionPhase,
            int cashItemId,
            int commoditySn,
            string sourceContext,
            bool startQuestRequestSent = false)
        {
            int normalizedQuestId = Math.Max(0, questId);
            if (normalizedQuestId <= 0)
            {
                return;
            }

            _pendingQuestDeliveryResults.RemoveAll(result =>
                result.QuestId == normalizedQuestId &&
                result.CompletionPhase == completionPhase);
            _pendingQuestDeliveryResults.Add(new PendingQuestDeliveryResultOwnership(
                normalizedQuestId,
                completionPhase,
                Math.Max(0, cashItemId),
                Math.Max(0, commoditySn),
                currTickCount,
                sourceContext ?? string.Empty,
                startQuestRequestSent));
            TrimPendingQuestDeliveryResultOwnershipQueue(
                _pendingQuestDeliveryResults,
                MaxPendingQuestDeliveryResultOwnershipCount);
        }

        private bool TryResolvePendingQuestDeliveryQuestResult(int questId, out string outcome)
        {
            outcome = string.Empty;
            QuestDetailDeliveryType deliveryTypeHint = ResolvePacketOwnedQuestDeliveryTypeHint(questId);
            bool? preferredCompletionPhase = deliveryTypeHint switch
            {
                QuestDetailDeliveryType.Accept => false,
                QuestDetailDeliveryType.Complete => true,
                _ => null
            };

            int ownershipIndex = FindPendingQuestDeliveryResultOwnershipIndex(
                _pendingQuestDeliveryResults,
                questId,
                preferredCompletionPhase);
            if (ownershipIndex < 0)
            {
                return false;
            }

            PendingQuestDeliveryResultOwnership ownership = _pendingQuestDeliveryResults[ownershipIndex];
            int ageMs = ownership.RequestedAtTick == int.MinValue
                ? 0
                : Math.Max(0, unchecked(currTickCount - ownership.RequestedAtTick));
            string phaseText = ownership.CompletionPhase ? "completion" : "accept";
            string resultPrefix = !string.IsNullOrWhiteSpace(ownership.SourceContext)
                ? ownership.SourceContext
                : "quest-detail delivery action";
            string commodityText = ownership.CommoditySn > 0
                ? $" commodity SN {ownership.CommoditySn}"
                : string.Empty;
            string cashItemText = ownership.CashItemId > 0
                ? $" cash item {ownership.CashItemId}"
                : string.Empty;

            outcome =
                $"Resolved pending {phaseText} delivery from {resultPrefix} via quest-result packet for quest #{questId} "
                + $"(age {ageMs}ms;{cashItemText}{commodityText}).";

            if (ownership.StartQuestRequestSent)
            {
                ClearPacketOwnedQuestResultStartQuestRequestLatch();
            }

            _pendingQuestDeliveryResults.RemoveAt(ownershipIndex);
            return true;
        }

        private static int FindPendingQuestDeliveryResultOwnershipIndex(
            IReadOnlyList<PendingQuestDeliveryResultOwnership> pendingResults,
            int questId)
        {
            return FindPendingQuestDeliveryResultOwnershipIndex(
                pendingResults,
                questId,
                preferredCompletionPhase: null);
        }

        private static int FindPendingQuestDeliveryResultOwnershipIndex(
            IReadOnlyList<PendingQuestDeliveryResultOwnership> pendingResults,
            int questId,
            bool? preferredCompletionPhase)
        {
            int normalizedQuestId = Math.Max(0, questId);
            if (pendingResults == null || pendingResults.Count == 0 || normalizedQuestId <= 0)
            {
                return -1;
            }

            int latestQuestOwnershipIndex = -1;
            for (int i = pendingResults.Count - 1; i >= 0; i--)
            {
                if (pendingResults[i].QuestId != normalizedQuestId)
                {
                    continue;
                }

                if (latestQuestOwnershipIndex < 0)
                {
                    latestQuestOwnershipIndex = i;
                }

                if (!preferredCompletionPhase.HasValue ||
                    pendingResults[i].CompletionPhase == preferredCompletionPhase.Value)
                {
                    return i;
                }
            }

            return latestQuestOwnershipIndex;
        }

        private static void TrimPendingQuestDeliveryResultOwnershipQueue(
            List<PendingQuestDeliveryResultOwnership> pendingResults,
            int maxCount)
        {
            if (pendingResults == null || maxCount <= 0)
            {
                return;
            }

            while (pendingResults.Count > maxCount)
            {
                pendingResults.RemoveAt(0);
            }
        }

        internal static IReadOnlyList<int> RegisterPendingQuestDeliveryQuestResultIdsForTesting(
            IReadOnlyList<int> existingQuestIds,
            int questId,
            int maxCount = MaxPendingQuestDeliveryResultOwnershipCount)
        {
            var pending = new List<PendingQuestDeliveryResultOwnership>();
            if (existingQuestIds != null)
            {
                for (int i = 0; i < existingQuestIds.Count; i++)
                {
                    int existingQuestId = Math.Max(0, existingQuestIds[i]);
                    if (existingQuestId > 0)
                    {
                        pending.Add(new PendingQuestDeliveryResultOwnership(
                            existingQuestId,
                            false,
                            0,
                            0,
                            int.MinValue,
                            string.Empty,
                            false));
                    }
                }
            }

            int normalizedQuestId = Math.Max(0, questId);
            if (normalizedQuestId > 0)
            {
                pending.RemoveAll(result => result.QuestId == normalizedQuestId && !result.CompletionPhase);
                pending.Add(new PendingQuestDeliveryResultOwnership(
                    normalizedQuestId,
                    false,
                    0,
                    0,
                    int.MinValue,
                    string.Empty,
                    false));
                TrimPendingQuestDeliveryResultOwnershipQueue(pending, Math.Max(1, maxCount));
            }

            var result = new int[pending.Count];
            for (int i = 0; i < pending.Count; i++)
            {
                result[i] = pending[i].QuestId;
            }

            return result;
        }

        internal static IReadOnlyList<int> RegisterPendingQuestDeliveryQuestResultIdsWithPhaseForTesting(
            IReadOnlyList<int> existingQuestIds,
            IReadOnlyList<bool> existingCompletionPhases,
            int questId,
            bool completionPhase,
            out IReadOnlyList<bool> resultingCompletionPhases,
            int maxCount = MaxPendingQuestDeliveryResultOwnershipCount)
        {
            var pending = new List<PendingQuestDeliveryResultOwnership>();
            if (existingQuestIds != null)
            {
                for (int i = 0; i < existingQuestIds.Count; i++)
                {
                    int existingQuestId = Math.Max(0, existingQuestIds[i]);
                    if (existingQuestId <= 0)
                    {
                        continue;
                    }

                    bool existingCompletionPhase = existingCompletionPhases != null
                                                   && i < existingCompletionPhases.Count
                                                   && existingCompletionPhases[i];
                    pending.Add(new PendingQuestDeliveryResultOwnership(
                        existingQuestId,
                        existingCompletionPhase,
                        0,
                        0,
                        int.MinValue,
                        string.Empty,
                        false));
                }
            }

            int normalizedQuestId = Math.Max(0, questId);
            if (normalizedQuestId > 0)
            {
                pending.RemoveAll(result =>
                    result.QuestId == normalizedQuestId &&
                    result.CompletionPhase == completionPhase);
                pending.Add(new PendingQuestDeliveryResultOwnership(
                    normalizedQuestId,
                    completionPhase,
                    0,
                    0,
                    int.MinValue,
                    string.Empty,
                    false));
                TrimPendingQuestDeliveryResultOwnershipQueue(pending, Math.Max(1, maxCount));
            }

            var questIds = new int[pending.Count];
            var phases = new bool[pending.Count];
            for (int i = 0; i < pending.Count; i++)
            {
                questIds[i] = pending[i].QuestId;
                phases[i] = pending[i].CompletionPhase;
            }

            resultingCompletionPhases = phases;
            return questIds;
        }

        internal static int FindPendingQuestDeliveryQuestResultIndexForTesting(
            IReadOnlyList<int> queuedQuestIds,
            int questId)
        {
            if (queuedQuestIds == null || queuedQuestIds.Count == 0)
            {
                return -1;
            }

            var pending = new List<PendingQuestDeliveryResultOwnership>(queuedQuestIds.Count);
            for (int i = 0; i < queuedQuestIds.Count; i++)
            {
                int queuedQuestId = Math.Max(0, queuedQuestIds[i]);
                if (queuedQuestId <= 0)
                {
                    continue;
                }

                pending.Add(new PendingQuestDeliveryResultOwnership(
                    queuedQuestId,
                    false,
                    0,
                    0,
                    int.MinValue,
                    string.Empty,
                    false));
            }

            return FindPendingQuestDeliveryResultOwnershipIndex(pending, questId);
        }

        internal static int FindPendingQuestDeliveryQuestResultIndexWithPreferredPhaseForTesting(
            IReadOnlyList<int> queuedQuestIds,
            IReadOnlyList<bool> queuedCompletionPhases,
            int questId,
            bool? preferredCompletionPhase)
        {
            if (queuedQuestIds == null || queuedQuestIds.Count == 0)
            {
                return -1;
            }

            var pending = new List<PendingQuestDeliveryResultOwnership>(queuedQuestIds.Count);
            for (int i = 0; i < queuedQuestIds.Count; i++)
            {
                int queuedQuestId = Math.Max(0, queuedQuestIds[i]);
                if (queuedQuestId <= 0)
                {
                    continue;
                }

                bool completionPhase = queuedCompletionPhases != null &&
                                       i < queuedCompletionPhases.Count &&
                                       queuedCompletionPhases[i];
                pending.Add(new PendingQuestDeliveryResultOwnership(
                    queuedQuestId,
                    completionPhase,
                    0,
                    0,
                    int.MinValue,
                    string.Empty,
                    false));
            }

            return FindPendingQuestDeliveryResultOwnershipIndex(
                pending,
                questId,
                preferredCompletionPhase);
        }

        private readonly record struct PendingQuestDeliveryResultOwnership(
            int QuestId,
            bool CompletionPhase,
            int CashItemId,
            int CommoditySn,
            int RequestedAtTick,
            string SourceContext,
            bool StartQuestRequestSent);

        private static bool ApplyUnsupportedPacketOwnedQuestResult(int resultType, out string message)
        {
            message = PacketQuestResultClientSemantics.IsHandledSubtype(resultType)
                ? $"Quest-result subtype {resultType} is client-handled but not modeled by the simulator yet."
                : $"Quest-result subtype {resultType} falls outside the client-handled OnQuestResult range ({PacketQuestResultClientSemantics.FirstHandledSubtype} through {PacketQuestResultClientSemantics.LastHandledSubtype}).";
            return false;
        }
    }
}
