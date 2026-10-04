using System.Collections.Generic;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapEditor.Preview
{
    internal sealed class EditorPreviewDrawable : BaseDXDrawableItem
    {
        public EditorPreviewDrawable(List<IDXObject> frames, bool flip)
            : base(frames, flip)
        {
        }

        public void DrawPreview(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonRenderer,
            GameTime gameTime,
            int tickCount,
            int x,
            int y,
            Color color)
        {
            IDXObject frame = GetCurrentFrame(tickCount);
            bool isSpine = frame.Texture == null;
            frame.DrawBackground(sprite, skeletonRenderer, gameTime,
                x + (isSpine ? 0 : frame.X),
                y + (isSpine ? 0 : frame.Y),
                color,
                flip,
                null);
        }
    }
}
