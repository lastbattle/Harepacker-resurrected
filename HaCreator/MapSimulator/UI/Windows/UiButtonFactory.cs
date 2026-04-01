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
                CreateQuestCategoryDrawable(device, width, height, new Color(235, 212, 176), new Color(252, 246, 224), new Color(158, 116, 71), new Color(181, 140, 92), new Color(198, 164, 122), false),
                CreateQuestCategoryDrawable(device, width, height, new Color(201, 192, 178), new Color(224, 218, 206), new Color(142, 132, 117), new Color(156, 146, 132), new Color(166, 156, 144), true),
                CreateQuestCategoryDrawable(device, width, height, new Color(214, 190, 153), new Color(231, 216, 189), new Color(141, 99, 57), new Color(161, 120, 77), new Color(186, 149, 109), false),
                CreateQuestCategoryDrawable(device, width, height, new Color(244, 226, 193), new Color(255, 251, 235), new Color(169, 124, 76), new Color(191, 150, 101), new Color(208, 174, 133), false));
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
            Color accent,
            bool disabled)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] pixels = new Color[width * height];
            int lastX = Math.Max(0, width - 1);
            int lastY = Math.Max(0, height - 1);
            int dividerX = Math.Max(10, lastX - 18);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color;
                    bool outerEdge = x == 0 || y == 0 || x == lastX || y == lastY;
                    bool innerHighlight = (x == 1 || y == 1) && x < lastX && y < lastY;
                    bool innerShadow = (x == lastX - 1 || y == lastY - 1) && x > 0 && y > 0;
                    bool cornerCut =
                        (x == 1 && (y == 1 || y == lastY - 1))
                        || (x == lastX - 1 && (y == 1 || y == lastY - 1));
                    bool midBand = y >= 3 && y <= Math.Max(3, (height / 2));
                    bool divider = x == dividerX && y >= 3 && y <= lastY - 3;
                    bool dividerHighlight = x == dividerX - 1 && y >= 3 && y <= lastY - 3;
                    bool dividerShadow = x == dividerX + 1 && y >= 3 && y <= lastY - 3;
                    bool bevelDot = (x == 2 || x == lastX - 2) && (y == 2 || y == lastY - 2);

                    if (outerEdge)
                    {
                        color = border;
                    }
                    else if (cornerCut)
                    {
                        color = Color.Transparent;
                    }
                    else if (bevelDot)
                    {
                        color = highlight;
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
                        if (midBand)
                        {
                            color = Color.Lerp(color, accent, disabled ? 0.06f : 0.13f);
                        }
                        if (!disabled && (x + y) % 7 == 0)
                        {
                            color = Color.Lerp(color, highlight, 0.08f);
                        }
                    }

                    if (!cornerCut)
                    {
                        if (divider)
                        {
                            color = shadow;
                        }
                        else if (dividerHighlight)
                        {
                            color = Color.Lerp(highlight, accent, 0.35f);
                        }
                        else if (dividerShadow)
                        {
                            color = Color.Lerp(shadow, border, 0.3f);
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
