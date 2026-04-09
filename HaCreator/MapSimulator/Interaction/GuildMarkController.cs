using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildMarkController
    {
        private readonly GuildMarkRuntime _runtime = new();

        internal string DescribeStatus()
        {
            return _runtime.DescribeStatus();
        }

        internal GuildMarkSelection? GetCommittedSelection()
        {
            return _runtime.GetCommittedSelection();
        }

        internal string Open(
            UIWindowManager windowManager,
            SpriteFont font,
            GuildMarkSelection? currentSelection,
            Func<GuildMarkSelection, string> commitHandler,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.Open(currentSelection);
            WireWindow(windowManager, font, currentSelection, commitHandler, feedbackHandler);
            showWindow?.Invoke();
            return message;
        }

        internal void WireWindow(
            UIWindowManager windowManager,
            SpriteFont font,
            GuildMarkSelection? currentSelection,
            Func<GuildMarkSelection, string> commitHandler,
            Action<string> feedbackHandler)
        {
            if (windowManager?.GetWindow(MapSimulatorWindowNames.GuildMark) is not GuildMarkWindow window)
            {
                return;
            }

            _runtime.SyncCurrentSelection(currentSelection);
            window.SetSnapshotProvider(_runtime.BuildSnapshot);
            window.SetActionHandlers(
                elapsedMs => _runtime.Advance(elapsedMs),
                () =>
                {
                    feedbackHandler?.Invoke(Close(windowManager, _runtime.Confirm));
                    GuildMarkSelection? selection = _runtime.GetCommittedSelection();
                    if (selection.HasValue)
                    {
                        string message = commitHandler?.Invoke(selection.Value);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            feedbackHandler?.Invoke(message);
                        }
                    }
                },
                () => feedbackHandler?.Invoke(Close(windowManager, _runtime.Cancel)),
                delta => feedbackHandler?.Invoke(_runtime.MoveBackground(delta)),
                delta => feedbackHandler?.Invoke(_runtime.MoveMark(delta)),
                delta => feedbackHandler?.Invoke(_runtime.MoveBackgroundColor(delta)),
                delta => feedbackHandler?.Invoke(_runtime.MoveMarkColor(delta)),
                () => feedbackHandler?.Invoke(_runtime.CycleCombo()));
            window.SetFont(font);
        }

        private static string Close(UIWindowManager windowManager, Func<string> action)
        {
            string message = action();
            windowManager?.HideWindow(MapSimulatorWindowNames.GuildMark);
            return message;
        }
    }
}
