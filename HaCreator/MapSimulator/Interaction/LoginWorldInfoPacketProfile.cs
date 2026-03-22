using System;

namespace HaCreator.MapSimulator
{
    internal sealed class LoginWorldInfoPacketProfile
    {
        public LoginWorldInfoPacketProfile(int worldId, int visibleChannelCount, int occupancyPercent, bool requiresAdultAccess)
        {
            WorldId = Math.Max(0, worldId);
            VisibleChannelCount = Math.Clamp(visibleChannelCount, 0, 20);
            OccupancyPercent = Math.Clamp(occupancyPercent, 0, 100);
            RequiresAdultAccess = requiresAdultAccess;
        }

        public int WorldId { get; }
        public int VisibleChannelCount { get; }
        public int OccupancyPercent { get; }
        public bool RequiresAdultAccess { get; }
    }
}
