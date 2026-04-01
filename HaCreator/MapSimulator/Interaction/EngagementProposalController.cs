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
            string customMessage,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.OpenProposal(proposerName, partnerName, ringItemId, sealItemId, customMessage);
            ShowWindow(windowManager, build, font, feedbackHandler, showWindow);
            return message;
        }

        internal string OpenOutgoingProposal(
            string proposerName,
            string partnerName,
            int ringItemId,
            string requestMessage,
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.OpenOutgoingRequest(proposerName, partnerName, ringItemId, requestMessage);
            ShowWindow(windowManager, build, font, feedbackHandler, showWindow);
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
