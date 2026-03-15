using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    public sealed class PetDefinition
    {
        public int ItemId { get; init; }
        public string Name { get; init; }
        internal PetAnimationSet Animations { get; } = new PetAnimationSet();
    }

    internal sealed class PetLoader
    {
        private static readonly string[] SupportedActions =
        {
            "stand0",
            "stand1",
            "move",
            "jump",
            "hang",
            "fly",
            "rest0"
        };

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, PetDefinition> _cache = new();

        public PetLoader(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public PetDefinition Load(int petItemId)
        {
            if (_cache.TryGetValue(petItemId, out PetDefinition cached))
            {
                return cached;
            }

            WzImage petImage = global::HaCreator.Program.FindImage("Item", $"Pet/{petItemId}.img");
            if (petImage == null)
            {
                return null;
            }

            petImage.ParseImage();

            var definition = new PetDefinition
            {
                ItemId = petItemId,
                Name = LoadPetName(petItemId) ?? $"Pet_{petItemId}"
            };

            foreach (string action in SupportedActions)
            {
                if (petImage[action] is WzSubProperty actionNode)
                {
                    List<IDXObject> frames = LoadActionFrames(actionNode);
                    if (frames.Count > 0)
                    {
                        definition.Animations.AddAnimation(action, frames);
                    }
                }
            }

            if (definition.Animations.ActionCount == 0)
            {
                return null;
            }

            _cache[petItemId] = definition;
            return definition;
        }

        private string LoadPetName(int petItemId)
        {
            WzImage stringImage = global::HaCreator.Program.FindImage("String", "Pet.img");
            if (stringImage == null)
            {
                return null;
            }

            stringImage.ParseImage();
            if (stringImage[petItemId.ToString()] is not WzSubProperty petString)
            {
                return null;
            }

            return (petString["name"] as WzStringProperty)?.Value;
        }

        private List<IDXObject> LoadActionFrames(WzSubProperty actionNode)
        {
            var frames = new List<IDXObject>();

            foreach (WzImageProperty child in actionNode.WzProperties.OrderBy(GetFrameOrder))
            {
                WzCanvasProperty canvas = child as WzCanvasProperty;
                if (canvas == null && child is WzUOLProperty uol)
                {
                    canvas = uol.LinkValue as WzCanvasProperty;
                }

                if (canvas == null)
                {
                    continue;
                }

                IDXObject frame = LoadTexture(canvas);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        private IDXObject LoadTexture(WzCanvasProperty canvas)
        {
            if (canvas?.PngProperty == null)
            {
                return null;
            }

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return null;
                }

                var texture = bitmap.ToTexture2DAndDispose(_device);
                if (texture == null)
                {
                    return null;
                }

                int delay = GetIntValue(canvas["delay"]) ?? 100;
                return new DXObject(0, 0, texture, delay)
                {
                    Tag = canvas
                };
            }
            catch
            {
                return null;
            }
        }

        private static int GetFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int index) ? index : int.MaxValue;
        }

        private static int? GetIntValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzIntProperty intProp => intProp.Value,
                WzShortProperty shortProp => shortProp.Value,
                WzLongProperty longProp => (int)longProp.Value,
                WzStringProperty strProp => int.TryParse(strProp.Value, out int value) ? value : null,
                _ => null
            };
        }
    }
}
