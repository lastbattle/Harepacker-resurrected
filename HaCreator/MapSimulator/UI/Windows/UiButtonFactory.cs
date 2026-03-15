using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    }
}
