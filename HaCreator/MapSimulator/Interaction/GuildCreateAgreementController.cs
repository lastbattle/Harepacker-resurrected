using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildCreateAgreementController
    {
        private readonly GuildCreateAgreementRuntime _runtime = new();

        internal string DescribeStatus()
        {
            return _runtime.DescribeStatus();
        }

        internal string Open(
            string masterName,
            string guildName,
            GuildDialogContext dialogContext,
            UIWindowManager windowManager,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.Open(masterName, guildName, dialogContext);
            WireWindow(windowManager, font, feedbackHandler);
            showWindow?.Invoke();
            return message;
        }

        internal void WireWindow(
            UIWindowManager windowManager,
            SpriteFont font,
            Action<string> feedbackHandler)
        {
            if (windowManager?.GetWindow(MapSimulatorWindowNames.GuildCreateAgreement) is not GuildCreateAgreementWindow window)
            {
                return;
            }

            window.SetSnapshotProvider(_runtime.BuildSnapshot);
            window.SetActionHandlers(
                elapsedMs =>
                {
                    string message = _runtime.Advance(elapsedMs);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        feedbackHandler?.Invoke(message);
                        windowManager.HideWindow(MapSimulatorWindowNames.GuildCreateAgreement);
                    }
                },
                () =>
                {
                    string message = _runtime.Accept(out _);
                    feedbackHandler?.Invoke(message);
                    windowManager?.HideWindow(MapSimulatorWindowNames.GuildCreateAgreement);
                },
                () => feedbackHandler?.Invoke(Close(windowManager, _runtime.Decline)));
            window.SetFont(font);
        }

        private static string Close(UIWindowManager windowManager, Func<string> action)
        {
            string message = action();
            windowManager?.HideWindow(MapSimulatorWindowNames.GuildCreateAgreement);
            return message;
        }
    }
}
