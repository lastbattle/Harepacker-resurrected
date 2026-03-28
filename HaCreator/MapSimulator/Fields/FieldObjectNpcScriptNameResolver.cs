using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    internal static class FieldObjectNpcScriptNameResolver
    {
        public static IReadOnlyList<string> ResolvePublishedScriptNames(NpcInstance npcInstance)
        {
            WzImage npcImage = npcInstance?.NpcInfo?.LinkedWzImage;
            if (npcImage == null)
            {
                return Array.Empty<string>();
            }

            if (!npcImage.Parsed && (npcImage.WzProperties == null || npcImage.WzProperties.Count == 0))
            {
                npcImage.ParseImage();
            }

            return ResolvePublishedScriptNames(npcImage["info"]?["script"]);
        }

        internal static IReadOnlyList<string> ResolvePublishedScriptNames(WzImageProperty scriptProperty)
        {
            return QuestRuntimeManager.ParseScriptNames(scriptProperty);
        }
    }
}
