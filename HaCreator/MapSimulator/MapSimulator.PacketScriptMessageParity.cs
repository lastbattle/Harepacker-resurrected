using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;

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

            OpenPacketOwnedScriptInteraction(request);
            return true;
        }

        private void OpenPacketOwnedScriptInteraction(PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request)
        {
            if (request?.State == null || _npcInteractionOverlay == null)
            {
                return;
            }

            _gameState.EnterDirectionMode();
            _scriptedDirectionModeOwnerActive = true;

            NpcItem npc = FindNpcById(request.SpeakerNpcId);
            _activeNpcInteractionNpc = npc;
            _activeNpcInteractionNpcId = request.SpeakerNpcId;

            if (npc != null)
            {
                PublishDynamicObjectTagStatesForScriptNames(
                    FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(npc.NpcInstance),
                    currTickCount);
            }

            _npcInteractionOverlay.Open(request.State);
        }
    }
}
