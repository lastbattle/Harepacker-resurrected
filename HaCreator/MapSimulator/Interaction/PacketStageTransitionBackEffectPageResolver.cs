using HaCreator.MapSimulator.Entities;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketStageTransitionBackEffectPageResolver
    {
        internal static IReadOnlyList<T> SelectTargets<T>(IEnumerable<T> items, System.Func<T, int> pageSelector, int pageId)
        {
            if (items == null || pageSelector == null)
            {
                return System.Array.Empty<T>();
            }

            return items
                .Where(static item => item != null)
                .Where(item => pageSelector(item) == pageId)
                .ToArray();
        }

        internal static IReadOnlyList<BackgroundItem> SelectTargets(IEnumerable<BackgroundItem> backgrounds, int pageId)
        {
            return SelectTargets(backgrounds, static background => background.PageId, pageId);
        }

        internal static byte ResolveCurrentAlpha<T>(IReadOnlyList<T> targets, System.Func<T, byte> alphaSelector, byte fallbackAlpha = byte.MaxValue)
        {
            if (targets == null || targets.Count == 0 || alphaSelector == null)
            {
                return fallbackAlpha;
            }

            return alphaSelector(targets[0]);
        }

        internal static byte ResolveCurrentAlpha(IReadOnlyList<BackgroundItem> targets, byte fallbackAlpha = byte.MaxValue)
        {
            return ResolveCurrentAlpha(targets, static background => background.Color.A, fallbackAlpha);
        }
    }
}
