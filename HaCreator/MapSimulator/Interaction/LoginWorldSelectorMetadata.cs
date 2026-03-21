using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    internal sealed class LoginWorldSelectorMetadata
    {
        public LoginWorldSelectorMetadata(int worldId, IReadOnlyList<ChannelSelectionState> channels, bool requiresAdultAccount)
        {
            WorldId = Math.Max(0, worldId);
            Channels = channels ?? Array.Empty<ChannelSelectionState>();
            RequiresAdultAccount = requiresAdultAccount;
        }

        public int WorldId { get; }

        public IReadOnlyList<ChannelSelectionState> Channels { get; }

        public bool RequiresAdultAccount { get; }
    }
}
