using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private string OpenGuildRankWindow()
        {
            GuildDialogContext dialogContext = _socialListRuntime.BuildGuildDialogContext(_playerManager?.Player?.Build);
            return _guildRankController.Open(
                uiWindowManager,
                _playerManager?.Player?.Build,
                dialogContext,
                ResolveEffectiveGuildMarkSelection(),
                _fontChat,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildRank));
        }

        private string OpenGuildMarkWindow()
        {
            return _guildMarkController.Open(
                uiWindowManager,
                _fontChat,
                _socialListRuntime.ApplyLocalGuildMarkSelection,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildMark));
        }

        private string OpenGuildCreateAgreementWindow(string masterName, string guildName)
        {
            GuildDialogContext dialogContext = _socialListRuntime.BuildGuildDialogContext(_playerManager?.Player?.Build);
            return _guildCreateAgreementController.Open(
                masterName,
                guildName,
                dialogContext,
                uiWindowManager,
                _fontChat,
                _socialListRuntime.ApplyGuildCreateAgreementAcceptance,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildCreateAgreement));
        }

        private void WireGuildRankWindowData()
        {
            GuildDialogContext dialogContext = _socialListRuntime.BuildGuildDialogContext(_playerManager?.Player?.Build);
            _guildRankController.WireWindow(
                uiWindowManager,
                _playerManager?.Player?.Build,
                dialogContext,
                ResolveEffectiveGuildMarkSelection(),
                _fontChat,
                ShowUtilityFeedbackMessage);
        }

        private void WireGuildMarkWindowData()
        {
            _guildMarkController.WireWindow(
                uiWindowManager,
                _fontChat,
                _socialListRuntime.ApplyLocalGuildMarkSelection,
                ShowUtilityFeedbackMessage);
        }

        private void WireGuildCreateAgreementWindowData()
        {
            _guildCreateAgreementController.WireWindow(
                uiWindowManager,
                _fontChat,
                _socialListRuntime.ApplyGuildCreateAgreementAcceptance,
                ShowUtilityFeedbackMessage);
        }

        private GuildMarkSelection? ResolveEffectiveGuildMarkSelection()
        {
            return _guildMarkController.GetCommittedSelection() ?? _socialListRuntime.GetEffectiveGuildMarkSelection();
        }
    }
}
