using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private string OpenGuildRankWindow()
        {
            GuildDialogContext dialogContext = _socialListRuntime.BuildGuildDialogContext(_playerManager?.Player?.Build);
            _guildRankController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
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
            _guildMarkController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            return _guildMarkController.Open(
                uiWindowManager,
                _fontChat,
                ResolveEffectiveGuildMarkSelection(),
                _socialListRuntime.SubmitLocalGuildMarkSelection,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildMark));
        }

        private string OpenGuildCreateAgreementWindow(string masterName, string guildName)
        {
            GuildDialogContext dialogContext = _socialListRuntime.BuildGuildDialogContext(_playerManager?.Player?.Build);
            _guildCreateAgreementController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            return _guildCreateAgreementController.Open(
                masterName,
                guildName,
                dialogContext,
                uiWindowManager,
                _fontChat,
                _socialListRuntime.SubmitGuildCreateAgreementAcceptance,
                ShowUtilityFeedbackMessage,
                () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildCreateAgreement));
        }

        private void WireGuildRankWindowData()
        {
            GuildDialogContext dialogContext = _socialListRuntime.BuildGuildDialogContext(_playerManager?.Player?.Build);
            _guildRankController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
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
            _guildMarkController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            _guildMarkController.WireWindow(
                uiWindowManager,
                _fontChat,
                ResolveEffectiveGuildMarkSelection(),
                _socialListRuntime.SubmitLocalGuildMarkSelection,
                ShowUtilityFeedbackMessage);
        }

        private void WireGuildCreateAgreementWindowData()
        {
            _guildCreateAgreementController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            _guildCreateAgreementController.WireWindow(
                uiWindowManager,
                _fontChat,
                _socialListRuntime.SubmitGuildCreateAgreementAcceptance,
                ShowUtilityFeedbackMessage);
        }

        private GuildMarkSelection? ResolveEffectiveGuildMarkSelection()
        {
            return _guildMarkController.GetCommittedSelection() ?? _socialListRuntime.GetEffectiveGuildMarkSelection();
        }
    }
}
