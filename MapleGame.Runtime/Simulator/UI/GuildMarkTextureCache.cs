using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class GuildMarkTextureCache
    {
        internal static Texture2D GetBackgroundTexture(
            GraphicsDevice device,
            IRuntimeAssetSource runtimeAssets,
            UILoaderResourceCache resourceCache,
            int backgroundId,
            int colorIndex)
        {
            ArgumentNullException.ThrowIfNull(runtimeAssets);
            ArgumentNullException.ThrowIfNull(resourceCache);
            GuildMarkCatalogData catalog = resourceCache.GetGuildMarkCatalog(runtimeAssets);
            return catalog.TryGetBackgroundCanvasPath(backgroundId, colorIndex, out string path)
                ? GetTexture(device, runtimeAssets, resourceCache, path)
                : null;
        }

        internal static Texture2D GetMarkTexture(
            GraphicsDevice device,
            IRuntimeAssetSource runtimeAssets,
            UILoaderResourceCache resourceCache,
            int markId,
            int colorIndex)
        {
            ArgumentNullException.ThrowIfNull(runtimeAssets);
            ArgumentNullException.ThrowIfNull(resourceCache);
            GuildMarkCatalogData catalog = resourceCache.GetGuildMarkCatalog(runtimeAssets);
            return catalog.TryGetMarkCanvasPath(markId, colorIndex, out string path)
                ? GetTexture(device, runtimeAssets, resourceCache, path)
                : null;
        }

        private static Texture2D GetTexture(
            GraphicsDevice device,
            IRuntimeAssetSource runtimeAssets,
            UILoaderResourceCache resourceCache,
            string path)
        {
            if (device == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (resourceCache.GuildMarkTextures.TryGetValue(path, out Texture2D existing) && existing != null && !existing.IsDisposed)
            {
                return existing;
            }

            try
            {
                WzImage image = runtimeAssets.FindImage("UI", "GuildMark.img");
                if (image == null)
                {
                    return null;
                }

                image.ParseImage();
                WzCanvasProperty canvas = ResolveCanvas(image, path);
                Texture2D texture = canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture != null)
                {
                    resourceCache.GuildMarkTextures[path] = texture;
                }

                return texture;
            }
            catch
            {
                return null;
            }
        }

        private static WzCanvasProperty ResolveCanvas(WzImage image, string path)
        {
            WzObject current = image;
            foreach (string segment in path.Split(['/'], StringSplitOptions.RemoveEmptyEntries))
            {
                current = current?[segment];
                if (current == null)
                {
                    return null;
                }
            }

            return current as WzCanvasProperty;
        }
    }
}
