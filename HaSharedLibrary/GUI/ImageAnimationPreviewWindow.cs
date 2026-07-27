using HaRepacker.Utils;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DrawingBitmap = System.Drawing.Bitmap;

namespace HaSharedLibrary.GUI
{
    public sealed class ImageAnimationPreviewLayer
    {
        public ImageAnimationPreviewLayer(WzCanvasProperty canvas, string tag = null)
        {
            Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            Tag = tag ?? canvas.FullPath;
        }

        public WzCanvasProperty Canvas { get; }
        public string Tag { get; }
    }

    public sealed class ImageAnimationPreviewFrame
    {
        public ImageAnimationPreviewFrame(IEnumerable<ImageAnimationPreviewLayer> layers)
        {
            Layers = (layers ?? throw new ArgumentNullException(nameof(layers))).Where(layer => layer != null).ToList();
        }

        public ImageAnimationPreviewFrame(WzCanvasProperty canvas, string tag = null)
            : this(new[] { new ImageAnimationPreviewLayer(canvas, tag) })
        {
        }

        public IReadOnlyList<ImageAnimationPreviewLayer> Layers { get; }
    }

    public class ImageAnimationPreviewWindow : Microsoft.Xna.Framework.Game
    {
        private const int RenderWidth = 1366;
        private const int RenderHeight = 768;

        private GraphicsDeviceManager graphicsDeviceMgr;
        private readonly IReadOnlyList<ImageAnimationPreviewFrame> selectedAnimationFrames;
        private readonly List<PreviewFrame> animationFrames = new();

        private float renderAnimationScaling = 1.0f;
        private float renderTextScaling = 1.0f;
        private float userScreenScaleFactor = 1.0f;
        private int currentFrameIndex;
        private int frameStartedAt;

        private SpriteFont fontDebugValues;
        private Texture2D textureDebugBoundaryRect;
        private SpriteBatch spriteBatch;
        private SpriteFont font;

        public int mapShiftX = -600;
        public int mapShiftY = -400;

        public ImageAnimationPreviewWindow(IEnumerable<WzObject> selectedAnimationObjects, string titlePath)
            : this(CreateSingleLayerFrames(selectedAnimationObjects), titlePath)
        {
        }

        public ImageAnimationPreviewWindow(IEnumerable<ImageAnimationPreviewFrame> selectedAnimationFrames, string titlePath)
        {
            this.selectedAnimationFrames = (selectedAnimationFrames ?? throw new ArgumentNullException(nameof(selectedAnimationFrames)))
                .Where(frame => frame != null && frame.Layers.Count > 0)
                .ToList();

            IsMouseVisible = true;
            Window.Title = titlePath ?? "Animate";
            IsFixedTimeStep = false;
            Content.RootDirectory = "Content";

            userScreenScaleFactor = (float)ScreenDPIUtil.GetScreenScaleFactor();
            renderAnimationScaling *= userScreenScaleFactor;
            renderTextScaling *= userScreenScaleFactor;

            graphicsDeviceMgr = new GraphicsDeviceManager(this)
            {
                SynchronizeWithVerticalRetrace = true,
                HardwareModeSwitch = true,
                GraphicsProfile = GraphicsProfile.HiDef,
                IsFullScreen = false,
                PreferMultiSampling = true,
                SupportedOrientations = DisplayOrientation.Default,
                PreferredBackBufferWidth = (int)(RenderWidth * userScreenScaleFactor),
                PreferredBackBufferHeight = (int)(RenderHeight * userScreenScaleFactor),
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            };
            graphicsDeviceMgr.ApplyChanges();
        }

        protected override void Initialize()
        {
            font = Content.Load<SpriteFont>("XnaDefaultFont");
            fontDebugValues = Content.Load<SpriteFont>("XnaFont_Debug");
            font.DefaultCharacter = '?';
            fontDebugValues.DefaultCharacter = '?';
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            foreach (ImageAnimationPreviewFrame frame in selectedAnimationFrames)
            {
                List<IDXObject> layers = new();
                foreach (ImageAnimationPreviewLayer layer in frame.Layers.OrderBy(GetLayerZ))
                {
                    try
                    {
                        using DrawingBitmap image = layer.Canvas.GetLinkedWzCanvasBitmap();
                        if (image == null)
                            continue;

                        System.Drawing.PointF origin = layer.Canvas.GetCanvasOriginPosition();
                        Texture2D texture = image.ToTexture2D(GraphicsDevice);
                        if (texture == null)
                            continue;

                        layers.Add(new DXObject((int)-origin.X, (int)-origin.Y, texture,
                            layer.Canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 0)
                        {
                            Tag = layer.Tag
                        });
                    }
                    catch
                    {
                        // Skip an individual unreadable layer while keeping the rest of the animation previewable.
                    }
                }

                if (layers.Count > 0)
                    animationFrames.Add(new PreviewFrame(layers));
            }

            if (animationFrames.Count == 0)
                throw new InvalidOperationException("The selected animation contains no renderable canvas images.");

            frameStartedAt = Environment.TickCount;

            using DrawingBitmap bitmapDebug = new(1, 1);
            bitmapDebug.SetPixel(0, 0, System.Drawing.Color.White);
            textureDebugBoundaryRect = bitmapDebug.ToTexture2D(GraphicsDevice);
        }

        protected override void UnloadContent()
        {
            foreach (PreviewFrame frame in animationFrames)
            foreach (IDXObject layer in frame.Layers)
                layer.Texture?.Dispose();

            textureDebugBoundaryRect?.Dispose();
            graphicsDeviceMgr?.EndDraw();
            graphicsDeviceMgr?.Dispose();
            graphicsDeviceMgr = null;
            animationFrames.Clear();
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState keyboardState = Keyboard.GetState();

#if !WINDOWS_STOREAPP
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
                Exit();
#endif

            bool altEnterPressed = keyboardState.IsKeyDown(Keys.LeftAlt) && keyboardState.IsKeyDown(Keys.Enter);
            if (altEnterPressed)
            {
                graphicsDeviceMgr.IsFullScreen = !graphicsDeviceMgr.IsFullScreen;
                graphicsDeviceMgr.ApplyChanges();
            }

            float frameRate = Math.Max(1, 1 / (float)Math.Max(gameTime.ElapsedGameTime.TotalSeconds, 0.001));
            float zoomOffset = 1.5f / frameRate;
            if (keyboardState.IsKeyDown(Keys.OemPlus))
                renderAnimationScaling += zoomOffset;
            if (keyboardState.IsKeyDown(Keys.OemMinus))
                renderAnimationScaling = Math.Max(0.1f, renderAnimationScaling - zoomOffset);

            int moveOffset = (int)(500f / frameRate);
            if (keyboardState.IsKeyDown(Keys.Left))
                mapShiftX += (int)(moveOffset / renderAnimationScaling);
            else if (keyboardState.IsKeyDown(Keys.Right))
                mapShiftX -= (int)(moveOffset / renderAnimationScaling);
            if (keyboardState.IsKeyDown(Keys.Up))
                mapShiftY += (int)(moveOffset / renderAnimationScaling);
            else if (keyboardState.IsKeyDown(Keys.Down))
                mapShiftY -= (int)(moveOffset / renderAnimationScaling);

            AdvanceFrame(Environment.TickCount);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            float frameRate = Math.Max(1, 1 / (float)Math.Max(gameTime.ElapsedGameTime.TotalSeconds, 0.001));
            MouseState mouseState = Mouse.GetState();
            int mouseXRelativeToMap = mouseState.X - mapShiftX;
            int mouseYRelativeToMap = mouseState.Y - mapShiftY;
            PreviewFrame currentFrame = animationFrames[currentFrameIndex];

            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null, null,
                Matrix.CreateScale(renderAnimationScaling));
            foreach (IDXObject layer in currentFrame.Layers)
                layer.DrawObject(spriteBatch, null, gameTime, mapShiftX, mapShiftY, false, null);

            IDXObject lastLayer = currentFrame.Layers.LastOrDefault();
            if (lastLayer != null)
            {
                Rectangle rectBox = new(
                    lastLayer.X - mapShiftX,
                    lastLayer.Y - mapShiftY,
                    lastLayer.Width,
                    lastLayer.Height);
                DrawBorder(spriteBatch, rectBox, 1, Color.White);
            }
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null, null,
                Matrix.CreateScale(renderTextScaling));

            StringBuilder debugText = new();
            debugText.Append("FPS: ").Append(frameRate).Append(Environment.NewLine);
            debugText.Append("Mouse : X ").Append(mouseXRelativeToMap).Append(", Y ").Append(mouseYRelativeToMap).Append(Environment.NewLine);
            debugText.Append("RMouse: X ").Append(mouseState.X).Append(", Y ").Append(mouseState.Y);
            spriteBatch.DrawString(fontDebugValues, debugText.ToString(), new Vector2(RenderWidth - 170, 10), Color.White);

            if (lastLayer != null)
            {
                string path = string.Join(", ", currentFrame.Layers.Select(layer => layer.Tag as string).Where(tag => !string.IsNullOrEmpty(tag)).Distinct());
                string imageRenderInfoText = string.Format(
                    "[Path: {0}]{7}[Origin: x = {1}, y = {2}]{8}[Dimension: W = {3}, H = {4}]{9}[Delay: {5}]{10}[Scale: {6}x]",
                    path,
                    lastLayer.X,
                    lastLayer.Y,
                    lastLayer.Width,
                    lastLayer.Height,
                    currentFrame.Delay,
                    Math.Round(renderAnimationScaling, 2),
                    Environment.NewLine,
                    Environment.NewLine,
                    Environment.NewLine,
                    Environment.NewLine);
                spriteBatch.DrawString(fontDebugValues, imageRenderInfoText,
                    new Vector2((RenderWidth / 2) - 100, RenderHeight - 100), Color.White);
            }

            if (gameTime.TotalGameTime.TotalSeconds < 3)
                spriteBatch.DrawString(font,
                    string.Format("Press [Left] [Right] [Up] [Down] for navigation.{0}   [+ -] for zoom", Environment.NewLine),
                    new Vector2(20, 10), Color.White);

            spriteBatch.End();
            base.Draw(gameTime);
        }

        private void AdvanceFrame(int tickCount)
        {
            if (animationFrames.Count <= 1)
                return;

            while (tickCount - frameStartedAt > animationFrames[currentFrameIndex].Delay)
            {
                currentFrameIndex = (currentFrameIndex + 1) % animationFrames.Count;
                frameStartedAt = tickCount;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawBorder(SpriteBatch sprite, Rectangle rectangleToDraw, int thicknessOfBorder, Color borderColor)
        {
            sprite.Draw(textureDebugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, rectangleToDraw.Width, thicknessOfBorder), borderColor);
            sprite.Draw(textureDebugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);
            sprite.Draw(textureDebugBoundaryRect, new Rectangle(rectangleToDraw.Right - thicknessOfBorder, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);
            sprite.Draw(textureDebugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Bottom - thicknessOfBorder, rectangleToDraw.Width, thicknessOfBorder), borderColor);
        }

        private static List<ImageAnimationPreviewFrame> CreateSingleLayerFrames(IEnumerable<WzObject> objects)
        {
            List<ImageAnimationPreviewFrame> frames = new();
            if (objects == null)
                return frames;

            foreach (WzObject obj in objects)
            {
                WzCanvasProperty canvas = obj as WzCanvasProperty;
                if (canvas == null && obj is WzUOLProperty uol)
                    canvas = uol.LinkValue as WzCanvasProperty;
                if (canvas != null)
                    frames.Add(new ImageAnimationPreviewFrame(canvas, obj.FullPath));
            }
            return frames;
        }

        private static int GetLayerZ(ImageAnimationPreviewLayer layer)
        {
            return layer.Canvas?["z"] switch
            {
                WzIntProperty integer => integer.Value,
                WzStringProperty text when int.TryParse(text.Value, out int value) => value,
                _ => 0
            };
        }

        private sealed class PreviewFrame
        {
            public PreviewFrame(IReadOnlyList<IDXObject> layers)
            {
                Layers = layers;
                Delay = Math.Max(1, layers.Max(layer => Math.Max(1, layer.Delay)));
            }

            public IReadOnlyList<IDXObject> Layers { get; }
            public int Delay { get; }
        }
    }
}
