using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
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
                ClearAnimationDisplayerLocalQuestDeliveryOwner();
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
    }
}
