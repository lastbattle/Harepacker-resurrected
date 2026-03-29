using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class UiButtonFactory
    {
        public static UIObject CreateSolidButton(
            GraphicsDevice device,
            int width,
            int height,
            Color normal,
            Color pressed,
            Color hover,
            Color disabled)
        {
            return new UIObject(
                CreateDrawable(device, width, height, normal),
                CreateDrawable(device, width, height, disabled),
                CreateDrawable(device, width, height, pressed),
                CreateDrawable(device, width, height, hover));
        }

        public static UIObject CreateQuestCategoryButton(GraphicsDevice device, int width, int height)
        {
            return new UIObject(
                CreateQuestCategoryDrawable(device, width, height, new Color(237, 216, 180), new Color(253, 247, 226), new Color(160, 118, 73), new Color(181, 140, 92), false),
                CreateQuestCategoryDrawable(device, width, height, new Color(202, 194, 180), new Color(224, 218, 206), new Color(142, 132, 117), new Color(156, 146, 132), true),
                CreateQuestCategoryDrawable(device, width, height, new Color(215, 191, 154), new Color(232, 217, 190), new Color(142, 100, 58), new Color(162, 121, 78), false),
                CreateQuestCategoryDrawable(device, width, height, new Color(246, 229, 196), new Color(255, 251, 235), new Color(171, 125, 77), new Color(192, 151, 102), false));
        }

        private static BaseDXDrawableItem CreateDrawable(GraphicsDevice device, int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetData(pixels);
            return new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
        }

        private static BaseDXDrawableItem CreateQuestCategoryDrawable(
            GraphicsDevice device,
            int width,
            int height,
            Color fill,
            Color highlight,
            Color border,
            Color shadow,
            bool disabled)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] pixels = new Color[width * height];
            int lastX = Math.Max(0, width - 1);
            int lastY = Math.Max(0, height - 1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color;
                    bool outerEdge = x == 0 || y == 0 || x == lastX || y == lastY;
                    bool innerHighlight = x == 1 || y == 1;
                    bool innerShadow = x == lastX - 1 || y == lastY - 1;

                    if (outerEdge)
                    {
                        color = border;
                    }
                    else if (innerHighlight)
                    {
                        color = highlight;
                    }
                    else if (innerShadow)
                    {
                        color = shadow;
                    }
                    else
                    {
                        float verticalRatio = height > 1 ? (float)y / lastY : 0f;
                        color = Color.Lerp(highlight, fill, MathHelper.Clamp(verticalRatio * 1.1f, 0f, 1f));
                        if (!disabled && (x + y) % 7 == 0)
                        {
                            color = Color.Lerp(color, highlight, 0.08f);
                        }
                    }

                    pixels[(y * width) + x] = color;
                }
            }

            texture.SetData(pixels);
            return new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
        }
    }
}
