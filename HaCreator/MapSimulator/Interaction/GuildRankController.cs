using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildRankController
    {
        private readonly GuildRankRuntime _runtime = new();

        internal string DescribeStatus()
        {
            return _runtime.DescribeStatus();
        }

        internal string Open(
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            string message = _runtime.Open(build);
            WireWindow(windowManager, build, font, feedbackHandler);
            showWindow?.Invoke();
            return message;
        }

        internal string Close(UIWindowManager windowManager)
        {
            string message = _runtime.Close();
            windowManager?.HideWindow(MapSimulatorWindowNames.GuildRank);
            return message;
        }

        internal void WireWindow(
            UIWindowManager windowManager,
            CharacterBuild build,
            SpriteFont font,
            Action<string> feedbackHandler)
        {
            if (windowManager?.GetWindow(MapSimulatorWindowNames.GuildRank) is not GuildRankWindow window)
            {
                return;
            }

            _runtime.UpdateLocalContext(build);
            window.SetSnapshotProvider(_runtime.BuildSnapshot);
            window.SetActionHandlers(
                delta => feedbackHandler?.Invoke(_runtime.MovePage(delta)),
                () => feedbackHandler?.Invoke(Close(windowManager)));
            window.SetFont(font);
        }
    }
}
