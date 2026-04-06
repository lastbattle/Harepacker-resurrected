using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class EngagementProposalController
    {
        private readonly EngagementProposalRuntime _runtime = new();

        internal void UpdateLocalContext(CharacterBuild build)
        {
            _runtime.UpdateLocalContext(build);
        }

        internal string DescribeStatus()
        {
            return _runtime.DescribeStatus();
        }

        internal void WireWindow(
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler)
        {
            if (windowManager?.GetWindow(MapSimulatorWindowNames.EngagementProposal) is not EngagementProposalWindow window)
            {
                return;
            }

            _runtime.UpdateLocalContext(build);
            window.SetSnapshotProvider(() => _runtime.BuildSnapshot());
            window.SetActionHandlers(
                () => Accept(windowManager),
                () => Dismiss(windowManager),
                feedbackHandler);
            window.SetFont(font);
        }

        internal string OpenIncomingProposal(
            string proposerName,
            string partnerName,
            int ringItemId,
            int sealItemId,
            string requestMessage,
            string customMessage,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.OpenProposal(proposerName, partnerName, ringItemId, sealItemId, requestMessage, customMessage);
            ShowWindow(windowManager, build, font, feedbackHandler, showWindow);
            return message;
        }

        internal bool TryOpenIncomingProposalFromLastRequestPayload(
            string proposerName,
            string partnerName,
            int sealItemId,
            string customMessage,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow,
            out string message)
        {
            if (!_runtime.TryOpenIncomingProposalFromLastRequestPayload(
                    proposerName,
                    partnerName,
                    sealItemId,
                    customMessage,
                    out message))
            {
                return false;
            }

            ShowWindow(windowManager, build, font, feedbackHandler, showWindow);
            return true;
        }

        internal bool TryOpenIncomingProposalFromRequestPayload(
            string proposerName,
            string partnerName,
            int sealItemId,
            byte[] requestPayload,
            string customMessage,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow,
            out string message)
        {
            if (!_runtime.TryOpenIncomingProposalFromRequestPayload(
                    proposerName,
                    partnerName,
                    sealItemId,
                    requestPayload,
                    customMessage,
                    out message))
            {
                return false;
            }

            ShowWindow(windowManager, build, font, feedbackHandler, showWindow);
            return true;
        }

        internal string OpenOutgoingProposal(
            string proposerName,
            string partnerName,
            int ringItemId,
            string requestMessage,
            bool enforceLocalRequesterChecks,
            IInventoryRuntime inventory,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow,
            out bool opened)
        {
            _runtime.UpdateLocalContext(build);
            if (!_runtime.TryValidateOutgoingOpen(proposerName, enforceLocalRequesterChecks, inventory, out string validationMessage))
            {
                opened = false;
                return validationMessage;
            }

            string message = _runtime.OpenOutgoingRequest(proposerName, partnerName, ringItemId, requestMessage);
            ShowWindow(windowManager, build, font, feedbackHandler, showWindow);
            opened = true;
            return message;
        }

        internal string Accept(UIWindowManager windowManager)
        {
            if (!_runtime.TryAccept(out _, out string message))
            {
                return message;
            }

            windowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
            return message;
        }

        internal string Dismiss(UIWindowManager windowManager)
        {
            string message = _runtime.Dismiss();
            windowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
            return message;
        }

        internal string Clear(UIWindowManager windowManager)
        {
            string message = _runtime.Clear();
            windowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
            return message;
        }

        internal bool TryBuildWeddingInvitationHandoff(
            CharacterBuild build,
            WeddingInvitationStyle style,
            int? clientDialogType,
            out WeddingInvitationHandoff handoff,
            out string message)
        {
            _runtime.UpdateLocalContext(build);
            return _runtime.TryBuildWeddingInvitationHandoff(build, style, clientDialogType, out handoff, out message);
        }

        internal bool TryOpenWeddingInvitationFromAcceptedProposal(
            WeddingInvitationController weddingInvitationController,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            WeddingInvitationStyle style,
            int? clientDialogType,
            Action showWindow,
            out string message)
        {
            _runtime.UpdateLocalContext(build);
            if (!_runtime.TryBuildWeddingInvitationHandoff(build, style, clientDialogType, out WeddingInvitationHandoff handoff, out string handoffMessage))
            {
                message = handoffMessage;
                return false;
            }

            byte[] payload = WeddingInvitationRuntime.BuildMarriageResultOpenPayload(
                handoff.GroomName,
                handoff.BrideName,
                handoff.ClientDialogType);

            return weddingInvitationController.TryOpenFromMarriageResultPacket(
                payload,
                handoff.Style,
                handoff.SourceDescription,
                windowManager,
                build,
                font,
                feedbackHandler,
                showWindow,
                out message);
        }

        internal bool TryOpenWeddingWishListFromAcceptedProposal(
            WeddingWishListController weddingWishListController,
            UIWindowManager windowManager,
            CharacterBuild build,
            IInventoryRuntime inventory,
            SpriteFont font,
            Action<string> feedbackHandler,
            WeddingWishListDialogMode mode,
            WeddingWishListRole? roleOverride,
            Action showWindow,
            out string message)
        {
            _runtime.UpdateLocalContext(build);
            if (!_runtime.TryBuildWeddingWishListHandoff(build, out WeddingWishListHandoff handoff, out string handoffMessage))
            {
                message = handoffMessage;
                return false;
            }

            WeddingWishListRole resolvedRole = roleOverride ?? handoff.LocalRole;
            string openMessage = weddingWishListController.Open(
                mode,
                resolvedRole,
                windowManager,
                build,
                inventory,
                font,
                feedbackHandler,
                showWindow);
            message = $"{handoffMessage} Opened wedding wish-list handoff for {handoff.GroomName} and {handoff.BrideName}: {openMessage}";
            return true;
        }

        private void ShowWindow(
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            WireWindow(windowManager, build, font, feedbackHandler);
            showWindow?.Invoke();
        }
    }
}
