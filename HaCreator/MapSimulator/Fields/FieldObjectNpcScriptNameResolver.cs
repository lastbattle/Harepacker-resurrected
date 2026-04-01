using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Interaction;
using HaCreator.Wz;
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
            return ResolvePublishedScriptNames(npcImage);
        }

        public static IReadOnlyList<string> ResolvePublishedScriptNames(int npcTemplateId)
        {
            if (npcTemplateId <= 0)
            {
                return Array.Empty<string>();
            }

            return ResolvePublishedScriptNames(ResolveNpcImage(npcTemplateId));
        }

        private static IReadOnlyList<string> ResolvePublishedScriptNames(WzImage npcImage)
        {
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

        private static WzImage ResolveNpcImage(int npcTemplateId)
        {
            string key = npcTemplateId.ToString();
            if (Program.InfoManager?.NpcPropertyCache != null
                && Program.InfoManager.NpcPropertyCache.TryGetValue(key, out WzImage cachedImage)
                && cachedImage != null)
            {
                return cachedImage;
            }

            return Program.FindImage("Npc", $"{key}.img");
        }

        internal static IReadOnlyList<string> ResolvePublishedScriptNames(WzImageProperty scriptProperty)
        {
            return QuestRuntimeManager.ParseScriptNames(scriptProperty);
        }
    }
}
