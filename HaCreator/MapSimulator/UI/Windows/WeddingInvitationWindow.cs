using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using SD = System.Drawing;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class WeddingInvitationWindow : UIWindowBase
    {
        private const string BasicBlackFontPathEnvironmentVariable = "MAPSIM_FONT_BASIC_BLACK_PATH";
        private const string BasicBlackFontFaceEnvironmentVariable = "MAPSIM_FONT_BASIC_BLACK_FACE";
        private static readonly string[] BasicBlackFontFamilyCandidates =
        {
            "DotumChe",
            "Dotum",
            "DOTOOMCHE",
            "DOTOOM",
            "DODUMCHE",
            "DODUM",
            "GulimChe",
            "Gulim"
        };

        private readonly GraphicsDevice _device;
        private readonly Texture2D _pixel;
        private readonly IReadOnlyDictionary<WeddingInvitationStyle, Texture2D> _backgrounds;
        private readonly IReadOnlyDictionary<int, Texture2D> _clientDialogBackgrounds;

        private SpriteFont _font;
        private UIObject _acceptButton;
        private KeyboardState _previousKeyboardState;
        private Func<WeddingInvitationSnapshot> _snapshotProvider;
        private Func<string> _acceptHandler;
        private Func<string> _dismissHandler;
        private Action<string> _feedbackHandler;
        private WeddingInvitationSnapshot _lastSnapshot = new();
        private ClientTextRasterizer _basicBlackTextRasterizer;

        internal WeddingInvitationWindow(
            IReadOnlyDictionary<WeddingInvitationStyle, Texture2D> backgrounds,
            IReadOnlyDictionary<int, Texture2D> clientDialogBackgrounds,
            GraphicsDevice device)
            : base(new DXObject(0, 0, CreateFrameTexture(device, backgrounds), 0))
        {
            _backgrounds = backgrounds ?? throw new ArgumentNullException(nameof(backgrounds));
            _clientDialogBackgrounds = clientDialogBackgrounds ?? new Dictionary<int, Texture2D>();
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _pixel = CreateFilledTexture(device, 1, 1, new Color(245, 233, 220));
        }

        public override string WindowName => MapSimulatorWindowNames.WeddingInvitation;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        internal void SetSnapshotProvider(Func<WeddingInvitationSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            RefreshLayout(RefreshSnapshot());
        }

        internal void SetActionHandlers(Func<string> acceptHandler, Func<string> dismissHandler, Action<string> feedbackHandler)
        {
            _acceptHandler = acceptHandler;
            _dismissHandler = dismissHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeControls(UIObject acceptButton)
        {
            _acceptButton = acceptButton;
            if (_acceptButton == null)
            {
                return;
            }

            AddButton(_acceptButton);
            _acceptButton.ButtonClickReleased += _ => ShowFeedback(_acceptHandler?.Invoke());
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            WeddingInvitationSnapshot snapshot = RefreshSnapshot();
            RefreshLayout(snapshot);

            KeyboardState keyboardState = Keyboard.GetState();
            if (IsVisible && snapshot.IsOpen && snapshot.CanAccept && snapshot.HasAcceptFocus && Pressed(keyboardState, Keys.Enter))
            {
                ShowFeedback(_acceptHandler?.Invoke());
            }

            if (IsVisible && Pressed(keyboardState, Keys.Escape))
            {
                ShowFeedback(_dismissHandler?.Invoke());
            }

            _previousKeyboardState = keyboardState;
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawBackground(sprite, _lastSnapshot);
            if (!CanDrawNames())
            {
                return;
            }

            DrawName(sprite, _lastSnapshot.GroomName, _lastSnapshot.GroomNamePosition.X, _lastSnapshot.GroomNamePosition.Y);
            DrawName(sprite, _lastSnapshot.BrideName, _lastSnapshot.BrideNamePosition.X, _lastSnapshot.BrideNamePosition.Y);
        }

        private void RefreshLayout(WeddingInvitationSnapshot snapshot)
        {
            _lastSnapshot = snapshot ?? new WeddingInvitationSnapshot();
            if (_acceptButton == null)
            {
                return;
            }

            _acceptButton.X = snapshot.AcceptButtonPosition.X;
            _acceptButton.Y = snapshot.AcceptButtonPosition.Y;
            _acceptButton.SetVisible(snapshot.IsOpen);
            _acceptButton.SetEnabled(snapshot.CanAccept);
            _acceptButton.ButtonVisible = snapshot.IsOpen;
        }

        private void DrawBackground(SpriteBatch sprite, WeddingInvitationSnapshot snapshot)
        {
            if (snapshot.UseClientDialogInvitationSurface
                && _clientDialogBackgrounds.TryGetValue(snapshot.ClientDialogType, out Texture2D dialogBackground)
                && dialogBackground != null)
            {
                sprite.Draw(dialogBackground, new Rectangle(Position.X, Position.Y, dialogBackground.Width, dialogBackground.Height), Color.White);
                return;
            }

            if (_backgrounds.TryGetValue(snapshot.Style, out Texture2D background) && background != null)
            {
                sprite.Draw(background, new Rectangle(Position.X, Position.Y, background.Width, background.Height), Color.White);
                return;
            }

            sprite.Draw(_pixel, new Rectangle(Position.X, Position.Y, 234, 250), Color.White);
        }

        private void DrawName(SpriteBatch sprite, string name, int offsetX, int offsetY)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Vector2 drawPosition = new(Position.X + offsetX, Position.Y + offsetY);
            if (_lastSnapshot.UseClientBasicBlackFont)
            {
                ClientTextRasterizer rasterizer = EnsureBasicBlackTextRasterizer();
                if (rasterizer != null)
                {
                    rasterizer.DrawString(sprite, name, drawPosition, Color.Black);
                    return;
                }
            }

            if (_font != null)
            {
                ClientTextDrawing.Draw(sprite, name, drawPosition, Color.Black, 1.0f, _font);
            }
        }

        private bool CanDrawNames()
        {
            if (_lastSnapshot.UseClientBasicBlackFont)
            {
                return true;
            }

            return _font != null;
        }

        private ClientTextRasterizer EnsureBasicBlackTextRasterizer()
        {
            if (_basicBlackTextRasterizer != null)
            {
                return _basicBlackTextRasterizer;
            }

            string requestedFamily = string.IsNullOrWhiteSpace(_lastSnapshot.NameFontFaceName)
                ? WeddingInvitationDialogText.GetBasicBlackFontFaceName()
                : _lastSnapshot.NameFontFaceName;
            string resolvedFamily = ClientTextRasterizer.ResolvePreferredFontFamily(
                requestedFamily,
                fontPathEnvironmentVariable: BasicBlackFontPathEnvironmentVariable,
                fontFaceEnvironmentVariable: BasicBlackFontFaceEnvironmentVariable,
                preferredPrivateFontFamilyCandidates: BasicBlackFontFamilyCandidates,
                preferEmbeddedPrivateFontSources: true);
            _basicBlackTextRasterizer = new ClientTextRasterizer(_device, resolvedFamily, 12f, SD.FontStyle.Regular);
            return _basicBlackTextRasterizer;
        }

        private WeddingInvitationSnapshot RefreshSnapshot()
        {
            _lastSnapshot = _snapshotProvider?.Invoke() ?? new WeddingInvitationSnapshot();
            return _lastSnapshot;
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private static Texture2D CreateFrameTexture(GraphicsDevice device, IReadOnlyDictionary<WeddingInvitationStyle, Texture2D> backgrounds)
        {
            foreach (Texture2D background in backgrounds?.Values ?? Array.Empty<Texture2D>())
            {
                if (background != null)
                {
                    return CreateFilledTexture(device, background.Width, background.Height, Color.Transparent);
                }
            }

            return CreateFilledTexture(device, 234, 250, Color.Transparent);
        }

        private static Texture2D CreateFilledTexture(GraphicsDevice device, int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = color;
            }

            texture.SetData(data);
            return texture;
        }
    }
}
