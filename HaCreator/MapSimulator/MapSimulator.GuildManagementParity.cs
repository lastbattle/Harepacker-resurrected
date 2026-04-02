using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private string OpenGuildRankWindow()
        {
            return _guildRankController.Open(
                uiWindowManager,
                _playerManager?.Player?.Build,
                _fontChat,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildRank));
        }

        private string OpenGuildMarkWindow()
        {
            return _guildMarkController.Open(
                uiWindowManager,
                _fontChat,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildMark));
        }

        private string OpenGuildCreateAgreementWindow(string masterName, string guildName)
        {
            return _guildCreateAgreementController.Open(
                masterName,
                guildName,
                uiWindowManager,
                _fontChat,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildCreateAgreement));
        }

        private void WireGuildRankWindowData()
        {
            _guildRankController.WireWindow(
                uiWindowManager,
                _playerManager?.Player?.Build,
                _fontChat,
                ShowUtilityFeedbackMessage);
        }

        private void WireGuildMarkWindowData()
        {
            _guildMarkController.WireWindow(
                uiWindowManager,
                _fontChat,
                ShowUtilityFeedbackMessage);
        }

        private void WireGuildCreateAgreementWindowData()
        {
            _guildCreateAgreementController.WireWindow(
                uiWindowManager,
                _fontChat,
                ShowUtilityFeedbackMessage);
        }
    }
}
