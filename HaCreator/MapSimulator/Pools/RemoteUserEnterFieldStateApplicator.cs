using System;

namespace HaCreator.MapSimulator.Pools
{
    internal static class RemoteUserEnterFieldStateApplicator
    {
        internal static bool TryApply(
            RemoteUserActorPool remoteUserPool,
            RemoteUserEnterFieldPacket packet,
            int currentTime,
            Action<int> syncAnimationDisplayerRemoteUserState,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Remote user pool is unavailable.";
                return false;
            }

            if (packet.PortableChairItemId.HasValue
                && !remoteUserPool.TrySetPortableChair(packet.CharacterId, packet.PortableChairItemId, out message))
            {
                return false;
            }

            if (!remoteUserPool.TryApplyTemporaryStatSnapshot(
                    packet.CharacterId,
                    packet.TemporaryStats,
                    delay: 0,
                    out message))
            {
                return false;
            }

            syncAnimationDisplayerRemoteUserState?.Invoke(packet.CharacterId);

            return remoteUserPool.TryApplyEnterFieldAvatarPresentation(
                packet,
                currentTime,
                out message);
        }
    }
}
