using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    internal sealed class LoginWorldSelectorMetadata
    {
        public LoginWorldSelectorMetadata(
            int worldId,
            IReadOnlyList<ChannelSelectionState> channels,
            bool requiresAdultAccount,
            string recommendMessage = null,
            int? recommendOrder = null)
        {
            WorldId = Math.Max(0, worldId);
            Channels = channels ?? Array.Empty<ChannelSelectionState>();
            RequiresAdultAccount = requiresAdultAccount;
            RecommendMessage = string.IsNullOrWhiteSpace(recommendMessage) ? null : recommendMessage.Trim();
            RecommendOrder = recommendOrder;
        }

        public int WorldId { get; }

        public IReadOnlyList<ChannelSelectionState> Channels { get; }

        public bool RequiresAdultAccount { get; }

        public string RecommendMessage { get; }

        public int? RecommendOrder { get; }
    }
}
