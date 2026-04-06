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
            bool hasAuthoritativePopulationData = false,
            string recommendMessage = null,
            int? recommendOrder = null,
            byte worldState = 0,
            bool blocksCharacterCreation = false,
            string worldName = null)
        {
            WorldId = Math.Max(0, worldId);
            Channels = channels ?? Array.Empty<ChannelSelectionState>();
            RequiresAdultAccount = requiresAdultAccount;
            HasAuthoritativePopulationData = hasAuthoritativePopulationData;
            RecommendMessage = string.IsNullOrWhiteSpace(recommendMessage) ? null : recommendMessage.Trim();
            RecommendOrder = recommendOrder;
            WorldState = worldState;
            BlocksCharacterCreation = blocksCharacterCreation;
            WorldName = string.IsNullOrWhiteSpace(worldName) ? null : worldName.Trim();
        }
        public int WorldId { get; }
        public IReadOnlyList<ChannelSelectionState> Channels { get; }
        public bool RequiresAdultAccount { get; }
        public bool HasAuthoritativePopulationData { get; }
        public string RecommendMessage { get; }
        public int? RecommendOrder { get; }
        public byte WorldState { get; }
        public bool BlocksCharacterCreation { get; }
        public string WorldName { get; }
    }
}
