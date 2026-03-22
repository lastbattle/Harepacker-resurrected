using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    internal sealed class LoginWorldInfoChannelPacketProfile
    {
        public LoginWorldInfoChannelPacketProfile(int channelId, int userCount, bool requiresAdultAccess, string name = null)
        {
            ChannelId = Math.Max(0, channelId);
            UserCount = Math.Max(0, userCount);
            RequiresAdultAccess = requiresAdultAccess;
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        public int ChannelId { get; }
        public int UserCount { get; }
        public bool RequiresAdultAccess { get; }
        public string Name { get; }
    }

    internal sealed class LoginWorldInfoPacketProfile
    {
        public LoginWorldInfoPacketProfile(int worldId, int visibleChannelCount, int occupancyPercent, bool requiresAdultAccess)
            : this(
                worldId,
                visibleChannelCount,
                occupancyPercent,
                requiresAdultAccess,
                worldState: (byte)(occupancyPercent >= 95 ? 2 : occupancyPercent >= 70 ? 1 : 0),
                blocksCharacterCreation: false,
                worldName: null,
                channels: null)
        {
        }

        public LoginWorldInfoPacketProfile(
            int worldId,
            int visibleChannelCount,
            int occupancyPercent,
            bool requiresAdultAccess,
            byte worldState,
            bool blocksCharacterCreation,
            string worldName,
            IReadOnlyList<LoginWorldInfoChannelPacketProfile> channels)
        {
            WorldId = Math.Max(0, worldId);
            VisibleChannelCount = Math.Clamp(visibleChannelCount, 0, 20);
            OccupancyPercent = Math.Clamp(occupancyPercent, 0, 100);
            RequiresAdultAccess = requiresAdultAccess || (channels?.Any(channel => channel?.RequiresAdultAccess == true) ?? false);
            WorldState = worldState;
            BlocksCharacterCreation = blocksCharacterCreation;
            WorldName = string.IsNullOrWhiteSpace(worldName) ? null : worldName.Trim();
            Channels = channels?
                .Where(channel => channel != null)
                .OrderBy(channel => channel.ChannelId)
                .ToArray()
                ?? Array.Empty<LoginWorldInfoChannelPacketProfile>();
        }

        public int WorldId { get; }
        public int VisibleChannelCount { get; }
        public int OccupancyPercent { get; }
        public bool RequiresAdultAccess { get; }
        public byte WorldState { get; }
        public bool BlocksCharacterCreation { get; }
        public string WorldName { get; }
        public IReadOnlyList<LoginWorldInfoChannelPacketProfile> Channels { get; }
        public bool HasAuthoritativeChannelPopulation => Channels.Count > 0;
    }
}
