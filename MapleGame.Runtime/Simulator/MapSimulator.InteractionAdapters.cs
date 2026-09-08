using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Assets;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly EngagementProposalController _engagementProposalController = new();
        private readonly WeddingInvitationController _weddingInvitationController = new();
        private readonly WeddingWishListController _weddingWishListController = new();
        private readonly GuildRankController _guildRankController = new();
        private readonly GuildMarkController _guildMarkController;
        private readonly GuildCreateAgreementController _guildCreateAgreementController = new();
    }
}
