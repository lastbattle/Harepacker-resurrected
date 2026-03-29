using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly WeddingInvitationRuntime _weddingInvitationRuntime = new WeddingInvitationRuntime();

        private void WireWeddingInvitationWindowData()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WeddingInvitation) is not WeddingInvitationWindow window)
            {
                return;
            }

            _weddingInvitationRuntime.UpdateLocalContext(_playerManager?.Player?.Build);
            window.SetSnapshotProvider(() => _weddingInvitationRuntime.BuildSnapshot());
            window.SetActionHandlers(AcceptWeddingInvitation, DismissWeddingInvitation, ShowUtilityFeedbackMessage);
            window.SetFont(_fontChat);
        }

        private void ShowWeddingInvitationWindow()
        {
            WireWeddingInvitationWindowData();
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.WeddingInvitation);
        }

        private string OpenWeddingInvitation(string groomName, string brideName, WeddingInvitationStyle style)
        {
            string message = _weddingInvitationRuntime.OpenInvitation(groomName, brideName, style);
            ShowWeddingInvitationWindow();
            return message;
        }

        private string AcceptWeddingInvitation()
        {
            string message = _weddingInvitationRuntime.Accept();
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }

        private string DismissWeddingInvitation()
        {
            string message = _weddingInvitationRuntime.Dismiss();
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }

        private string ClearWeddingInvitation()
        {
            string message = _weddingInvitationRuntime.Clear();
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }
    }
}
