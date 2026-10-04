using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Assets;

namespace HaCreator.MapSimulator.Loaders
{
    public static class NpcImgEntryResolver
    {
        private static readonly ConditionalWeakTable<IRuntimeAssetSource, ConcurrentDictionary<int, Lazy<WzImage>>>
            NormalizedImageEntryCaches = new();

        internal static WzImage Resolve(int templateId, IRuntimeAssetSource assets)
        {
            if (templateId <= 0 || assets == null)
            {
                return null;
            }

            ConcurrentDictionary<int, Lazy<WzImage>> cache = NormalizedImageEntryCaches.GetOrCreateValue(assets);
            Lazy<WzImage> lazyEntry = cache.GetOrAdd(
                templateId,
                key => new Lazy<WzImage>(
                    () => BuildNormalizedEntry(
                        key,
                        id => assets.FindImage("Npc", id.ToString("D7", CultureInfo.InvariantCulture) + ".img")),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyEntry.Value;
        }

        internal static void ClearCaches()
        {
            NormalizedImageEntryCaches.Clear();
        }

        public static WzImage BuildNormalizedEntry(int templateId, Func<int, WzImage> imageProvider)
        {
            return BuildNormalizedEntry(templateId, imageProvider, new HashSet<int>());
        }

        private static WzImage BuildNormalizedEntry(int templateId, Func<int, WzImage> imageProvider, ISet<int> activeTemplateIds)
        {
            if (templateId <= 0 || imageProvider == null || !activeTemplateIds.Add(templateId))
            {
                return null;
            }

            WzImage sourceImage = imageProvider(templateId);
            if (sourceImage == null)
            {
                activeTemplateIds.Remove(templateId);
                return null;
            }

            EnsureParsed(sourceImage);

            int linkedTemplateId = ResolveLinkedTemplateId(sourceImage);
            if (linkedTemplateId <= 0 || linkedTemplateId == templateId)
            {
                activeTemplateIds.Remove(templateId);
                return sourceImage;
            }

            WzImage linkedEntry = BuildNormalizedEntry(linkedTemplateId, imageProvider, activeTemplateIds);
            activeTemplateIds.Remove(templateId);
            if (linkedEntry == null)
            {
                return sourceImage;
            }

            return CopyMissingLinkedTopLevelBranches(sourceImage, linkedEntry);
        }

        private static WzImage CopyMissingLinkedTopLevelBranches(WzImage sourceImage, WzImage linkedEntry)
        {
            WzImage normalized = sourceImage.DeepClone();
            EnsureParsed(linkedEntry);

            foreach (WzImageProperty linkedProperty in linkedEntry.WzProperties)
            {
                if (linkedProperty == null || IsInfoProperty(linkedProperty) || normalized[linkedProperty.Name] != null)
                {
                    continue;
                }

                normalized.AddProperty(linkedProperty.DeepClone());
            }

            normalized.MarkWzImageAsParsed();
            return normalized;
        }

        private static int ResolveLinkedTemplateId(WzImage sourceImage)
        {
            return TryGetTemplateId(sourceImage?["info"]?["link"], out int linkedTemplateId)
                ? linkedTemplateId
                : 0;
        }

        private static bool TryGetTemplateId(WzImageProperty property, out int templateId)
        {
            templateId = 0;
            switch (property)
            {
                case WzStringProperty stringProperty:
                    return TryParseTemplateId(stringProperty.Value, out templateId);

                case WzIntProperty or WzShortProperty or WzLongProperty:
                    templateId = property.GetInt();
                    return templateId > 0;

                default:
                    return false;
            }
        }

        private static bool TryParseTemplateId(string value, out int templateId)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out templateId)
                && templateId > 0;
        }

        private static bool IsInfoProperty(WzImageProperty property)
        {
            return string.Equals(property?.Name, "info", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureParsed(WzImage image)
        {
            if (image != null && !image.Parsed)
            {
                image.ParseImage();
            }
        }
    }
}
