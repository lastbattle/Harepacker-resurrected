using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool TryApplyPacketOwnedInitialQuizPayload(byte[] payload, out string message)
        {
            bool applied = _initialQuizTimerRuntime.TryApplyPayload(payload, currTickCount, out message);
            if (applied)
            {
                SyncUtilityChannelSelectorAvailability();
            }

            return applied;
        }
    }
}
