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
            if (manager == null || device == null)
            {
                return;
            }

            if (manager.GetWindow(MapSimulatorWindowNames.QuestTimer) == null)
            {
                manager.RegisterCustomWindow(CreateQuestTimerRuntimeWindow(device, MapSimulatorWindowNames.QuestTimer, drawActionLayer: false));
            }

            if (manager.GetWindow(MapSimulatorWindowNames.QuestTimerAction) == null)
            {
                manager.RegisterCustomWindow(CreateQuestTimerRuntimeWindow(device, MapSimulatorWindowNames.QuestTimerAction, drawActionLayer: true));
            }
        }

        private static UIWindowBase CreateQuestTimerRuntimeWindow(GraphicsDevice device, string windowName, bool drawActionLayer)
        {
            Texture2D transparentTexture = new(device, 1, 1);
            transparentTexture.SetData(new[] { Color.Transparent });
            return new QuestTimerRuntimeWindow(new DXObject(0, 0, transparentTexture, 0), windowName, drawActionLayer);
        }
    }
}
