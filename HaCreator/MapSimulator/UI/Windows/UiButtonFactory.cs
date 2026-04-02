using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class UiButtonFactory
    {
        private const int QuestCategoryCountGutterWidth = 18;

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
                CreateQuestCategoryDrawable(
                    device,
                    width,
                    height,
                    new Color(244, 228, 194),
                    new Color(255, 250, 235),
                    new Color(160, 118, 72),
                    new Color(122, 83, 47),
                    new Color(225, 194, 151),
                    new Color(205, 171, 125),
                    false),
                CreateQuestCategoryDrawable(
                    device,
                    width,
                    height,
                    new Color(208, 200, 189),
                    new Color(231, 226, 217),
                    new Color(145, 136, 122),
                    new Color(119, 111, 101),
                    new Color(196, 188, 177),
                    new Color(182, 174, 163),
                    true),
                CreateQuestCategoryDrawable(
                    device,
                    width,
                    height,
                    new Color(228, 205, 166),
                    new Color(245, 231, 204),
                    new Color(149, 105, 59),
                    new Color(117, 78, 43),
                    new Color(210, 178, 135),
                    new Color(192, 157, 113),
                    false),
                CreateQuestCategoryDrawable(
                    device,
                    width,
                    height,
                    new Color(250, 238, 212),
                    new Color(255, 252, 241),
                    new Color(169, 126, 79),
                    new Color(136, 96, 56),
                    new Color(234, 205, 163),
                    new Color(216, 185, 141),
                    false));
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
            Color gutterFill,
            bool disabled)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] pixels = new Color[width * height];
            int lastX = Math.Max(0, width - 1);
            int lastY = Math.Max(0, height - 1);
            int gutterWidth = Math.Clamp(QuestCategoryCountGutterWidth, 12, Math.Max(12, width / 3));
            int dividerX = Math.Max(8, width - gutterWidth - 2);
            int labelInsetX = 4;
            int topStripeY = Math.Max(2, Math.Min(lastY - 2, 3));
            int bottomStripeY = Math.Max(2, lastY - 3);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color;
                    bool outerEdge = x == 0 || y == 0 || x == lastX || y == lastY;
                    bool innerHighlight = (x == 1 || y == 1) && x < lastX && y < lastY;
                    bool innerShadow = (x == lastX - 1 || y == lastY - 1) && x > 0 && y > 0;
                    bool cornerCut =
                        (x == 1 && (y == 1 || y == lastY - 1)) ||
                        (x == lastX - 1 && (y == 1 || y == lastY - 1));
                    bool bevelDot = (x == 2 || x == lastX - 2) && (y == 2 || y == lastY - 2);
                    bool divider = x == dividerX && y >= 2 && y <= lastY - 2;
                    bool dividerHighlight = x == dividerX - 1 && y >= 2 && y <= lastY - 2;
                    bool dividerShadow = x == dividerX + 1 && y >= 2 && y <= lastY - 2;
                    bool gutter = x > dividerX && x < lastX;
                    bool topStripe = y == topStripeY && x >= labelInsetX && x < dividerX - 1;
                    bool bottomStripe = y == bottomStripeY && x >= labelInsetX && x < dividerX - 1;
                    bool leftBevel = x == 2 && y >= 2 && y <= lastY - 2;
                    bool innerRivet = x == dividerX + Math.Max(2, gutterWidth / 2) && (y == 3 || y == lastY - 3);

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
                        Color bandBase = gutter ? gutterFill : fill;
                        color = Color.Lerp(highlight, bandBase, MathHelper.Clamp(verticalRatio * 1.12f, 0f, 1f));
                        if (y >= 3 && y <= Math.Max(3, height / 2))
                        {
                            color = Color.Lerp(color, accent, disabled ? 0.05f : 0.14f);
                        }
                        if (!disabled && !gutter && (x + (y * 2)) % 11 == 0)
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
                        else if (topStripe)
                        {
                            color = Color.Lerp(highlight, accent, disabled ? 0.15f : 0.28f);
                        }
                        else if (bottomStripe)
                        {
                            color = Color.Lerp(shadow, accent, disabled ? 0.12f : 0.2f);
                        }
                        else if (leftBevel)
                        {
                            color = Color.Lerp(highlight, accent, disabled ? 0.18f : 0.3f);
                        }
                        else if (innerRivet)
                        {
                            color = Color.Lerp(highlight, gutterFill, 0.45f);
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
