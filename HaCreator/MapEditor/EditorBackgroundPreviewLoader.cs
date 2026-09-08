using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapEditor.Instance;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
namespace HaCreator.MapEditor
{
    internal static class EditorBackgroundPreviewLoader
    {
        public static BackgroundItem CreateBackgroundFromProperty(TexturePool texturePool, WzImageProperty source, BackgroundInstance bgInstance, GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool flip) {
            List<IDXObject> frames = MapSimulatorLoader.LoadFrames(texturePool, source, bgInstance.BaseX, bgInstance.BaseY, device, usedProps, bgInstance.SpineAni);
            if (frames.Count == 0) {
                string error = string.Format("[MapSimulatorLoader] 0 frames loaded for bg texture from src: '{0}'", source.FullPath); // Back_003.wz\\BM3_3.img\\spine\\0

                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }

            if (frames.Count == 1) {
                return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, bgInstance.Page, frames[0], flip, bgInstance.screenMode);
            }
            return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, bgInstance.Page, frames, flip, bgInstance.screenMode);
        }

        public static BackgroundItem CreateBackgroundFromProperty(TexturePool texturePool, WzImageProperty source,
            BackgroundInstance bgInstance, GraphicsDevice device, ref List<WzObject> usedProps, bool flip) {
            var concurrentUsedProps = new ConcurrentBag<WzObject>(usedProps ?? Enumerable.Empty<WzObject>());
            BackgroundItem background = CreateBackgroundFromProperty(
                texturePool, source, bgInstance, device, concurrentUsedProps, flip);
            usedProps = concurrentUsedProps.ToList();
            return background;
        }

    }
}
