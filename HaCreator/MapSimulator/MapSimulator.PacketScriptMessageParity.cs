using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private enum PendingQuestRewardChoiceSource
        {
            QuestWindow,
            NpcOverlay
        }

        private sealed class PendingQuestRewardChoiceState
        {
            public PendingQuestRewardChoiceSource Source { get; init; }
            public QuestRewardChoicePrompt Prompt { get; init; }
            public Dictionary<int, int> SelectedItemsByGroup { get; } = new();
            public int GroupIndex { get; set; }
        }

        private PendingQuestRewardChoiceState _pendingQuestRewardChoice;

        private bool TryApplyPacketOwnedScriptMessagePacket(byte[] payload, out string message)
        {
            if (!_packetScriptMessageRuntime.TryDecode(
                payload,
                FindNpcById,
                _activeNpcInteractionNpc,
                out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
                out message))
            {
                return false;
            }

            string dispatchStatus = OpenPacketOwnedScriptInteraction(request);
            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? dispatchStatus
                    : $"{message} {dispatchStatus}";
            }

            return true;
        }

        private string OpenPacketOwnedScriptInteraction(PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request)
        {
            if (request == null || _npcInteractionOverlay == null)
            {
                return null;
            }

            if (request.CloseExistingDialog || request.State == null)
            {
                _npcInteractionOverlay.Close();
                _activeNpcInteractionNpc = null;
                _activeNpcInteractionNpcId = 0;
                return DispatchPacketOwnedScriptAutoResponse(request.AutoResponse);
            }

            _gameState.EnterDirectionMode();
            _scriptedDirectionModeOwnerActive = true;

            NpcItem npc = FindNpcById(request.SpeakerNpcId);
            _activeNpcInteractionNpc = npc;
            _activeNpcInteractionNpcId = request.SpeakerNpcId;

            IReadOnlyList<string> publishedScriptNames = npc != null
                ? FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(npc.NpcInstance)
                : FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(request.SpeakerNpcId);
            PublishDynamicObjectTagStatesForScriptNames(publishedScriptNames, currTickCount);

            _npcInteractionOverlay.Open(request.State);
            return DispatchPacketOwnedScriptAutoResponse(request.AutoResponse);
        }

        private string DispatchPacketOwnedScriptAutoResponse(PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket)
        {
            if (responsePacket == null)
            {
                return null;
            }

            bool dispatched = _packetScriptReplyTransport.TrySendResponse(responsePacket, out string dispatchStatus);
            _packetScriptMessageRuntime.RecordResponseDispatch(responsePacket, dispatched, dispatchStatus);
            return dispatchStatus;
        }

        private void HandleNpcOverlayInputSubmission(NpcInteractionInputSubmission submission)
        {
            if (_pendingQuestRewardChoice != null)
            {
                HandleQuestRewardChoiceSubmission(submission);
                return;
            }

            if (submission?.PresentationStyle == NpcInteractionPresentationStyle.PacketScriptUtilDialog)
            {
                if (_packetScriptMessageRuntime.TryBuildResponsePacket(
                    submission,
                    out PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket,
                    out string message))
                {
                    bool dispatched = _packetScriptReplyTransport.TrySendResponse(responsePacket, out string dispatchStatus);
                    _packetScriptMessageRuntime.RecordResponseDispatch(responsePacket, dispatched, dispatchStatus);
                    ShowUtilityFeedbackMessage($"{message} {dispatchStatus}".Trim());
                }
                else if (!string.IsNullOrWhiteSpace(message))
                {
                    ShowUtilityFeedbackMessage(message);
                }

                return;
            }

            ShowUtilityFeedbackMessage($"Submitted {submission?.EntryTitle ?? "NPC"} input: {submission?.Value ?? string.Empty}");
        }

        private void OpenQuestRewardChoicePrompt(QuestRewardChoicePrompt prompt, PendingQuestRewardChoiceSource source)
        {
            if (prompt == null || prompt.Groups == null || prompt.Groups.Count == 0 || _npcInteractionOverlay == null)
            {
                return;
            }

            _pendingQuestRewardChoice = new PendingQuestRewardChoiceState
            {
                Source = source,
                Prompt = prompt,
                GroupIndex = 0
            };

            OpenNextQuestRewardChoiceGroup();
        }

        private void OpenNextQuestRewardChoiceGroup()
        {
            if (_pendingQuestRewardChoice?.Prompt?.Groups == null ||
                _pendingQuestRewardChoice.GroupIndex >= _pendingQuestRewardChoice.Prompt.Groups.Count)
            {
                ResolvePendingQuestRewardChoice();
                return;
            }

            QuestRewardChoiceGroup group = _pendingQuestRewardChoice.Prompt.Groups[_pendingQuestRewardChoice.GroupIndex];
            IReadOnlyList<NpcInteractionChoice> choices = group.Options?
                .Select(option => new NpcInteractionChoice
                {
                    Label = option.Label,
                    SubmitSelection = true,
                    SubmissionKind = NpcInteractionInputKind.Number,
                    SubmissionNumericValue = option.ItemId,
                    SubmissionValue = option.ItemId.ToString(CultureInfo.InvariantCulture)
                })
                .ToArray()
                ?? System.Array.Empty<NpcInteractionChoice>();

            string body = group.Options == null || group.Options.Count == 0
                ? group.PromptText
                : $"{group.PromptText}\n\n{string.Join("\n", group.Options.Select(static option => option.DetailText))}";

            _npcInteractionOverlay.Open(new NpcInteractionState
            {
                NpcName = string.IsNullOrWhiteSpace(_pendingQuestRewardChoice.Prompt.QuestName) ? "Quest Reward" : _pendingQuestRewardChoice.Prompt.QuestName,
                SelectedEntryId = group.GroupKey,
                Entries = new[]
                {
                    new NpcInteractionEntry
                    {
                        EntryId = group.GroupKey,
                        Kind = NpcInteractionEntryKind.Talk,
                        Title = _pendingQuestRewardChoice.Prompt.ActionLabel,
                        Subtitle = $"Reward choice {_pendingQuestRewardChoice.GroupIndex + 1}/{_pendingQuestRewardChoice.Prompt.Groups.Count}",
                        Pages = new[]
                        {
                            new NpcInteractionPage
                            {
                                Text = body,
                                Choices = choices
                            }
                        }
                    }
                }
            });
        }

        private void HandleQuestRewardChoiceSubmission(NpcInteractionInputSubmission submission)
        {
            if (_pendingQuestRewardChoice?.Prompt?.Groups == null ||
                _pendingQuestRewardChoice.GroupIndex >= _pendingQuestRewardChoice.Prompt.Groups.Count)
            {
                _pendingQuestRewardChoice = null;
                return;
            }

            QuestRewardChoiceGroup group = _pendingQuestRewardChoice.Prompt.Groups[_pendingQuestRewardChoice.GroupIndex];
            int selectedItemId = submission?.NumericValue ?? 0;
            if (selectedItemId <= 0 || group.Options.All(option => option.ItemId != selectedItemId))
            {
                _chat?.AddSystemMessage("That quest reward choice is no longer available.", currTickCount);
                _pendingQuestRewardChoice = null;
                return;
            }

            _pendingQuestRewardChoice.SelectedItemsByGroup[group.GroupKey] = selectedItemId;
            _pendingQuestRewardChoice.GroupIndex++;
            OpenNextQuestRewardChoiceGroup();
        }

        private void ResolvePendingQuestRewardChoice()
        {
            PendingQuestRewardChoiceState pendingChoice = _pendingQuestRewardChoice;
            _pendingQuestRewardChoice = null;
            if (pendingChoice?.Prompt == null)
            {
                return;
            }

            switch (pendingChoice.Source)
            {
                case PendingQuestRewardChoiceSource.QuestWindow:
                    QuestWindowActionResult questWindowResult = pendingChoice.Prompt.CompletionPhase
                        ? _questRuntime.TryCompleteFromQuestWindow(pendingChoice.Prompt.QuestId, _playerManager?.Player?.Build, pendingChoice.SelectedItemsByGroup)
                        : _questRuntime.TryAcceptFromQuestWindow(pendingChoice.Prompt.QuestId, _playerManager?.Player?.Build, pendingChoice.SelectedItemsByGroup);
                    HandleQuestWindowActionResult(questWindowResult);
                    break;

                case PendingQuestRewardChoiceSource.NpcOverlay:
                    if (pendingChoice.Prompt.NpcId is not int npcId || npcId <= 0)
                    {
                        return;
                    }

                    QuestActionResult npcResult = _questRuntime.TryPerformPrimaryAction(
                        pendingChoice.Prompt.QuestId,
                        npcId,
                        _playerManager?.Player?.Build,
                        pendingChoice.SelectedItemsByGroup);
                    HandleNpcOverlayQuestActionResult(npcResult, pendingChoice.Prompt.QuestId);
                    break;
            }
        }
    }
}
