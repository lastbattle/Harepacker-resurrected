using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly EngagementProposalController _engagementProposalController = new();
        private readonly WeddingInvitationController _weddingInvitationController = new();
        private readonly WeddingWishListController _weddingWishListController = new();
    }
}
