using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class WeddingInvitationController
    {
        private readonly WeddingInvitationRuntime _runtime = new();
        internal Action<IReadOnlyList<string>, int> SocialMessagesObserved { get; set; }

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
            if (windowManager?.GetWindow(MapSimulatorWindowNames.WeddingInvitation) is not WeddingInvitationWindow window)
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

        internal string OpenInvitation(
            string groomName,
            string brideName,
            WeddingInvitationStyle style,
            int? clientDialogType,
            string sourceDescription,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.OpenInvitation(groomName, brideName, style, clientDialogType, sourceDescription);
            PrepareForOpen(windowManager);
            WireWindow(windowManager, build, font, feedbackHandler);
            showWindow?.Invoke();
            PublishObservedSocialMessages();
            return message;
        }

        internal bool TryOpenFromMarriageResultPacket(
            byte[] payload,
            WeddingInvitationStyle style,
            string sourceDescription,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow,
            out string message)
        {
            if (!_runtime.TryOpenFromMarriageResultPacket(payload, style, sourceDescription, out message))
            {
                return false;
            }

            PrepareForOpen(windowManager);
            WireWindow(windowManager, build, font, feedbackHandler);
            showWindow?.Invoke();
            PublishObservedSocialMessages();
            return true;
        }

        internal string Accept(UIWindowManager windowManager)
        {
            string message = _runtime.Accept();
            if (!string.Equals(message, "No wedding invitation is active.", StringComparison.Ordinal))
            {
                windowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
                PublishObservedSocialMessages();
            }

            return message;
        }

        internal bool TryOpenWeddingWishListFromAcceptedInvitation(
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
            if (!_runtime.TryBuildWeddingWishListHandoff(build, out WeddingInvitationAcceptedHandoff handoff, out string handoffMessage))
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

        internal string Dismiss(UIWindowManager windowManager)
        {
            string message = _runtime.Dismiss();
            if (!string.Equals(message, "No wedding invitation is active.", StringComparison.Ordinal))
            {
                PublishObservedSocialMessages();
            }

            windowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }

        internal string Clear(UIWindowManager windowManager)
        {
            string message = _runtime.Clear();
            windowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }

        private static void PrepareForOpen(UIWindowManager windowManager)
        {
            windowManager?.HideWindow(MapSimulatorWindowNames.EngagementProposal);
        }

        private void PublishObservedSocialMessages()
        {
            IReadOnlyList<string> messages = _runtime.GetObservedSocialMessages();
            if (messages == null || messages.Count == 0)
            {
                return;
            }

            SocialMessagesObserved?.Invoke(messages, Environment.TickCount);
        }
    }
}
