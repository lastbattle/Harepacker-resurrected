using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
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
            WzImage npcImage = NpcImgEntryResolver.Resolve(npcInstance?.NpcInfo);
            return ResolvePublishedScriptNames(npcImage);
        }

        public static IReadOnlyList<string> ResolvePublishedScriptNames(int npcTemplateId)
        {
            if (npcTemplateId <= 0)
            {
                return Array.Empty<string>();
            }

            return ResolvePublishedScriptNames(NpcImgEntryResolver.Resolve(npcTemplateId));
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

        internal static IReadOnlyList<string> ResolvePublishedScriptNames(WzImageProperty scriptProperty)
        {
            return QuestRuntimeManager.ParseScriptNames(scriptProperty);
        }
    }
}
