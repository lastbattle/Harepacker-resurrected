using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Loaders
{
    public static partial class UIWindowLoader
    {
        private static void RegisterQuestTimerWindows(
            UIWindowManager manager,
            GraphicsDevice device)
        {
            // Active packet-authored quest timers allocate their modeless owner pair on demand.
        }

        internal static UIWindowBase CreateQuestTimerRuntimeWindow(GraphicsDevice device, int questId, bool drawActionLayer)
        {
            Texture2D transparentTexture = new(device, 1, 1);
            transparentTexture.SetData(new[] { Color.Transparent });
            return drawActionLayer
                ? new QuestTimerActionWindow(
                    new DXObject(0, 0, transparentTexture, 0),
                    MapSimulatorWindowNames.GetQuestTimerActionWindowName(questId),
                    questId)
                : new QuestTimerWindow(
                    new DXObject(0, 0, transparentTexture, 0),
                    MapSimulatorWindowNames.GetQuestTimerWindowName(questId),
                    questId);
        }
    }
}
