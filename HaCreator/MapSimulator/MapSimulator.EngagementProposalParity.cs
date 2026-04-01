using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly EngagementProposalRuntime _engagementProposalRuntime = new EngagementProposalRuntime();

        private void WireEngagementProposalWindowData()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.EngagementProposal) is not EngagementProposalWindow window)
            {
                return;
            }

            _engagementProposalRuntime.UpdateLocalContext(_playerManager?.Player?.Build);
            window.SetSnapshotProvider(() => _engagementProposalRuntime.BuildSnapshot());
            window.SetActionHandlers(AcceptEngagementProposal, DismissEngagementProposal, ShowUtilityFeedbackMessage);
            window.SetFont(_fontChat);
        }

        private void ShowEngagementProposalWindow()
        {
            WireEngagementProposalWindowData();
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.EngagementProposal);
        }

        private string AcceptEngagementProposal()
        {
            if (!_engagementProposalRuntime.TryAccept(out _, out string message))
            {
                return message;
            }

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
            return message;
        }

        private string DismissEngagementProposal()
        {
            string message = _engagementProposalRuntime.Dismiss();
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
            return message;
        }

        private string OpenEngagementProposal(
            string proposerName,
            string partnerName,
            int ringItemId,
            int sealItemId,
            string customMessage)
        {
            string message = _engagementProposalRuntime.OpenProposal(proposerName, partnerName, ringItemId, sealItemId, customMessage);
            ShowEngagementProposalWindow();
            return message;
        }

        private string OpenOutgoingEngagementProposal(
            string proposerName,
            string partnerName,
            int ringItemId,
            string requestMessage)
        {
            string message = _engagementProposalRuntime.OpenOutgoingRequest(proposerName, partnerName, ringItemId, requestMessage);
            ShowEngagementProposalWindow();
            return message;
        }

        private string ClearEngagementProposal()
        {
            string message = _engagementProposalRuntime.Clear();
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
            return message;
        }
    }
}
