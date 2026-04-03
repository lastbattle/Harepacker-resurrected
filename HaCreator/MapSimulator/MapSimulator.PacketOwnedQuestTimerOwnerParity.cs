using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private void SyncPacketOwnedQuestTimerOwnerWindows(int currentTick)
        {
            if (uiWindowManager == null || GraphicsDevice == null)
            {
                return;
            }

            uiWindowManager.RemoveWindow(MapSimulatorWindowNames.QuestTimer);
            uiWindowManager.RemoveWindow(MapSimulatorWindowNames.QuestTimerAction);

            IReadOnlyList<int> activeQuestIds = _packetFieldStateRuntime.GetActiveQuestTimerIds();
            HashSet<string> activeWindowNames = new(StringComparer.Ordinal);

            for (int i = 0; i < activeQuestIds.Count; i++)
            {
                int questId = activeQuestIds[i];
                EnsureQuestTimerOwnerWindow(questId, drawActionLayer: false, currentTick, activeWindowNames);
                EnsureQuestTimerOwnerWindow(questId, drawActionLayer: true, currentTick, activeWindowNames);
            }

            UIWindowBase[] staleWindows = uiWindowManager.Windows
                .Where(window => MapSimulatorWindowNames.IsQuestTimerRuntimeWindowName(window.WindowName)
                    && !activeWindowNames.Contains(window.WindowName))
                .ToArray();

            for (int i = 0; i < staleWindows.Length; i++)
            {
                uiWindowManager.RemoveWindow(staleWindows[i]);
            }
        }

        private void EnsureQuestTimerOwnerWindow(
            int questId,
            bool drawActionLayer,
            int currentTick,
            ISet<string> activeWindowNames)
        {
            string windowName = drawActionLayer
                ? MapSimulatorWindowNames.GetQuestTimerActionWindowName(questId)
                : MapSimulatorWindowNames.GetQuestTimerWindowName(questId);
            activeWindowNames.Add(windowName);

            if (uiWindowManager.GetWindow(windowName) is not QuestTimerRuntimeWindowBase window)
            {
                window = UIWindowLoader.CreateQuestTimerRuntimeWindow(GraphicsDevice, questId, drawActionLayer) as QuestTimerRuntimeWindowBase;
                if (window == null)
                {
                    return;
                }

                uiWindowManager.RegisterCustomWindow(window);
            }

            window.SetFont(_fontChat);
            window.BindRuntime(
                _packetFieldStateRuntime,
                () => _renderParams.RenderWidth,
                () => _renderParams.RenderHeight,
                () => currentTick);
            window.IsVisible = true;
        }
    }
}
