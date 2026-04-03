using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class WeddingInvitationController
    {
        private readonly WeddingInvitationRuntime _runtime = new();

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
            WireWindow(windowManager, build, font, feedbackHandler);
            showWindow?.Invoke();
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

            WireWindow(windowManager, build, font, feedbackHandler);
            showWindow?.Invoke();
            return true;
        }

        internal string Accept(UIWindowManager windowManager)
        {
            string message = _runtime.Accept();
            if (!string.Equals(message, "No wedding invitation is active.", StringComparison.Ordinal))
            {
                windowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            }

            return message;
        }

        internal string Dismiss(UIWindowManager windowManager)
        {
            string message = _runtime.Dismiss();
            windowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }

        internal string Clear(UIWindowManager windowManager)
        {
            string message = _runtime.Clear();
            windowManager?.HideWindow(MapSimulatorWindowNames.WeddingInvitation);
            return message;
        }
    }
}
