using HaCreator;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class GuildMarkTextureCache
    {
        private static readonly Dictionary<string, Texture2D> Textures = [];

        internal static Texture2D GetBackgroundTexture(GraphicsDevice device, int backgroundId, int colorIndex)
        {
            GuildMarkCatalogData catalog = GuildMarkCatalog.GetCatalog();
            return catalog.TryGetBackgroundCanvasPath(backgroundId, colorIndex, out string path)
                ? GetTexture(device, path)
                : null;
        }

        internal static Texture2D GetMarkTexture(GraphicsDevice device, int markId, int colorIndex)
        {
            GuildMarkCatalogData catalog = GuildMarkCatalog.GetCatalog();
            return catalog.TryGetMarkCanvasPath(markId, colorIndex, out string path)
                ? GetTexture(device, path)
                : null;
        }

        private static Texture2D GetTexture(GraphicsDevice device, string path)
        {
            if (device == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Textures.TryGetValue(path, out Texture2D existing) && existing != null && !existing.IsDisposed)
            {
                return existing;
            }

            try
            {
                WzImage image = Program.FindImage("UI", "GuildMark.img");
                if (image == null)
                {
                    return null;
                }

                image.ParseImage();
                WzCanvasProperty canvas = ResolveCanvas(image, path);
                Texture2D texture = canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture != null)
                {
                    Textures[path] = texture;
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
