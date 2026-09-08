using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Entities;
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
        public static IReadOnlyList<string> ResolvePublishedScriptNames(RuntimeLife runtimeLife, IRuntimeAssetSource assets)
        {
            WzImage npcImage = ResolveImage(runtimeLife, assets);
            return ResolvePublishedScriptNames(npcImage);
        }

        public static IReadOnlyList<FieldObjectScriptPublication> ResolvePublishedScriptPublications(RuntimeLife runtimeLife, IRuntimeAssetSource assets)
        {
            WzImage npcImage = ResolveImage(runtimeLife, assets);
            return ResolvePublishedScriptPublications(npcImage);
        }

        private static WzImage ResolveImage(RuntimeLife runtimeLife, IRuntimeAssetSource assets)
        {
            if (runtimeLife == null || assets == null)
                return null;
            string category = string.IsNullOrWhiteSpace(runtimeLife.Asset.Category) ? "Npc" : runtimeLife.Asset.Category;
            string path = string.IsNullOrWhiteSpace(runtimeLife.Asset.Path)
                ? runtimeLife.Id.PadLeft(7, '0') + ".img"
                : runtimeLife.Asset.Path;
            return assets.FindImage(category, path);
        }

        public static IReadOnlyList<string> ResolvePublishedScriptNames(
            int npcTemplateId,
            IRuntimeAssetSource assets)
        {
            if (npcTemplateId <= 0)
            {
                return Array.Empty<string>();
            }

            return ResolvePublishedScriptNames(NpcImgEntryResolver.Resolve(npcTemplateId, assets));
        }

        public static IReadOnlyList<FieldObjectScriptPublication> ResolvePublishedScriptPublications(
            int npcTemplateId,
            IRuntimeAssetSource assets)
        {
            if (npcTemplateId <= 0)
            {
                return Array.Empty<FieldObjectScriptPublication>();
            }

            return ResolvePublishedScriptPublications(NpcImgEntryResolver.Resolve(npcTemplateId, assets));
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

        private static IReadOnlyList<FieldObjectScriptPublication> ResolvePublishedScriptPublications(WzImage npcImage)
        {
            if (npcImage == null)
            {
                return Array.Empty<FieldObjectScriptPublication>();
            }

            if (!npcImage.Parsed && (npcImage.WzProperties == null || npcImage.WzProperties.Count == 0))
            {
                npcImage.ParseImage();
            }

            return ResolvePublishedScriptPublications(npcImage["info"]?["script"]);
        }

        internal static IReadOnlyList<string> ResolvePublishedScriptNames(WzImageProperty scriptProperty)
        {
            return QuestRuntimeManager.ParseScriptNames(scriptProperty);
        }

        internal static IReadOnlyList<FieldObjectScriptPublication> ResolvePublishedScriptPublications(WzImageProperty scriptProperty)
        {
            return FieldObjectScriptPublicationParser.Parse(scriptProperty);
        }
    }
}
