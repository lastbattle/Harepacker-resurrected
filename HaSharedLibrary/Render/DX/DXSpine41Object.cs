using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using S41AnimationState = Spine41.AnimationState;
using S41AnimationStateData = Spine41.AnimationStateData;
using S41Atlas = Spine41.Atlas;
using S41Skeleton = Spine41.Skeleton;
using S41SkeletonBinary = Spine41.SkeletonBinary;
using S41SkeletonData = Spine41.SkeletonData;
using S41SkeletonRenderer = Spine41.SkeletonRenderer;
using S41Skin = Spine41.Skin;
using SkeletonMeshRenderer = Spine.SkeletonMeshRenderer;
using static MapleLib.WzDataReader;

namespace HaSharedLibrary.Render.DX
{
    public class DXSpine41Object : IDXObject
    {
        private readonly Spine41Object spineObject;
        private readonly int _x;
        private readonly int _y;
        private readonly int delay;
        private object _Tag;

        public DXSpine41Object(Spine41Object spineObject, int x, int y, int delay = 0)
        {
            this.spineObject = spineObject;
            _x = x;
            _y = y;
            this.delay = delay;
        }

        public static bool TryLoadRawSkeleton(WzRawDataProperty skeletonProperty, GraphicsDevice graphicsDevice, string animationName, out Spine41Object spineObject)
        {
            spineObject = null;
            if (skeletonProperty?.Parent is not WzImageProperty parentProperty)
                return false;

            WzStringProperty atlasProperty = parentProperty.WzProperties
                .OfType<WzStringProperty>()
                .FirstOrDefault(property => property.IsSpineAtlasResources);
            if (atlasProperty == null)
            {
                return false;
            }

            string atlasData = atlasProperty.GetString();
            byte[] skeletonBytes = skeletonProperty.GetBytes(false);
            if (string.IsNullOrWhiteSpace(atlasData) || skeletonBytes == null || skeletonBytes.Length == 0)
                return false;

            try
            {
                using StringReader atlasReader = new StringReader(atlasData);
                Spine41TextureLoader textureLoader = new Spine41TextureLoader(parentProperty, graphicsDevice);
                S41Atlas atlas = new S41Atlas(atlasReader, string.Empty, textureLoader);

                using MemoryStream skeletonStream = new MemoryStream(skeletonBytes);
                S41SkeletonBinary skeletonBinary = new S41SkeletonBinary(atlas);
                S41SkeletonData skeletonData = skeletonBinary.ReadSkeletonData(skeletonStream);
                if (skeletonData == null)
                    return false;

                S41Skeleton skeleton = new S41Skeleton(skeletonData);
                S41Skin skin = skeletonData.DefaultSkin ?? skeletonData.Skins.FirstOrDefault();
                if (skin != null)
                    skeleton.SetSkin(skin);
                skeleton.SetToSetupPose();

                S41AnimationStateData stateData = new S41AnimationStateData(skeleton.Data);
                S41AnimationState state = new S41AnimationState(stateData);

                if (!string.IsNullOrEmpty(animationName))
                {
                    state.SetAnimation(0, animationName, true);
                }
                else
                {
                    int trackIndex = 0;
                    foreach (Spine41.Animation animation in skeletonData.Animations)
                    {
                        state.SetAnimation(trackIndex++, animation.Name, true);
                    }
                }

                spineObject = new Spine41Object(skeletonData, skeleton, state, IsPremultipliedAlpha(atlasData, parentProperty));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsPremultipliedAlpha(string atlasData, WzImageProperty parentProperty)
        {
            if (atlasData.IndexOf("pma:true", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return parentProperty["PMA"].ReadValue(0) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
            DrawModern(sprite, skeletonMeshRenderer, gameTime, X - mapShiftX, Y - mapShiftY, Color.White, flip);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int x, int y, Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
            DrawModern(sprite, skeletonMeshRenderer, gameTime, x, y, color, flip);
        }

        private void DrawModern(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, int x, int y, Color color, bool flip)
        {
            S41SkeletonRenderer renderer = Spine41RendererCache.Get(sprite.GraphicsDevice);
            renderer.PremultipliedAlpha = spineObject.PremultipliedAlpha;
            if (renderer.Effect is BasicEffect modernEffect && skeletonMeshRenderer?.Effect is BasicEffect oldEffect)
                modernEffect.World = oldEffect.World;

            spineObject.State.Update(gameTime.ElapsedGameTime.Milliseconds / 1000f);
            spineObject.State.Apply(spineObject.Skeleton);

            spineObject.Skeleton.ScaleX = flip ? -1f : 1f;
            spineObject.Skeleton.X = x;
            spineObject.Skeleton.Y = y;
            spineObject.Skeleton.R = 1f;
            spineObject.Skeleton.G = 1f;
            spineObject.Skeleton.B = 1f;
            spineObject.Skeleton.A = color.A / 255f;
            spineObject.Skeleton.UpdateWorldTransform();
            renderer.Draw(spineObject.Skeleton);
        }

        public int Delay => delay;
        public int X => _x;
        public int Y => _y;
        public int Width => spineObject.Width;
        public int Height => spineObject.Height;
        public Texture2D Texture => null;
        public object Tag { get => _Tag; set => _Tag = value; }

        public sealed class Spine41Object
        {
            public Spine41Object(S41SkeletonData skeletonData, S41Skeleton skeleton, S41AnimationState state, bool premultipliedAlpha)
            {
                SkeletonData = skeletonData;
                Skeleton = skeleton;
                State = state;
                PremultipliedAlpha = premultipliedAlpha;
            }

            public S41SkeletonData SkeletonData { get; }
            public S41Skeleton Skeleton { get; }
            public S41AnimationState State { get; }
            public bool PremultipliedAlpha { get; }
            public int Width => Math.Max(1, (int)Math.Ceiling(SkeletonData.Width));
            public int Height => Math.Max(1, (int)Math.Ceiling(SkeletonData.Height));
        }
    }
}
