using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class EngagementProposalWindow : UIWindowBase
    {
        private const int FrameWidth = 260;
        private const int TextTop = 18;
        private const int TextWidth = 234;
        private const int TextLeft = 13;
        private const int LineHeight = 16;
        private const int MinimumLineCount = 2;
        private const int HeightPadding = 67;
        private const int ButtonX = 197;
        private const int ButtonBottomOffset = 31;

        private readonly GraphicsDevice _device;
        private readonly Texture2D _pixel;
        private readonly EngagementProposalWindowAssets _assets;

        private SpriteFont _font;
        private UIObject _acceptButton;
        private KeyboardState _previousKeyboardState;
        private Func<EngagementProposalSnapshot> _snapshotProvider;
        private Func<string> _acceptHandler;
        private Func<string> _dismissHandler;
        private Action<string> _feedbackHandler;
        private IReadOnlyList<string> _wrappedLines = Array.Empty<string>();
        private int _frameHeight = HeightPadding + (MinimumLineCount * LineHeight);

        internal EngagementProposalWindow(EngagementProposalWindowAssets assets, GraphicsDevice device)
            : base(new DXObject(0, 0, CreateFilledTexture(device, FrameWidth, HeightPadding + (MinimumLineCount * LineHeight), Color.Transparent), 0))
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _pixel = CreateFilledTexture(device, 1, 1, Color.White);
            RefreshLayout(new EngagementProposalSnapshot());
        }

        public override string WindowName => MapSimulatorWindowNames.EngagementProposal;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        internal void SetSnapshotProvider(Func<EngagementProposalSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            RefreshLayout(GetSnapshot());
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
            RefreshLayout(GetSnapshot());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            EngagementProposalSnapshot snapshot = GetSnapshot();
            RefreshLayout(snapshot);

            KeyboardState keyboardState = Keyboard.GetState();
            if (IsVisible && snapshot.IsOpen && Pressed(keyboardState, Keys.Enter))
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
            DrawFrame(sprite);
            if (_font == null)
            {
                return;
            }

            int drawY = Position.Y + TextTop;
            for (int i = 0; i < _wrappedLines.Count; i++)
            {
                string line = _wrappedLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    drawY += LineHeight;
                    continue;
                }

                Vector2 size = _font.MeasureString(line);
                float drawX = Position.X + TextLeft + Math.Max(0f, (TextWidth - size.X) / 2f);
                DrawOutlinedText(sprite, line, new Vector2(drawX, drawY), new Color(83, 53, 24), new Color(255, 255, 255, 220));
                drawY += LineHeight;
            }
        }

        private void RefreshLayout(EngagementProposalSnapshot snapshot)
        {
            int lineCount = Math.Max(MinimumLineCount, CountWrappedLines(snapshot.BodyText));
            int newHeight = HeightPadding + (lineCount * LineHeight);
            if (newHeight != _frameHeight)
            {
                _frameHeight = newHeight;
                Frame = new DXObject(0, 0, CreateFilledTexture(_device, FrameWidth, _frameHeight, Color.Transparent), 0);
            }

            _wrappedLines = WrapText(snapshot.BodyText, TextWidth);
            if (_acceptButton != null)
            {
                _acceptButton.X = ButtonX;
                _acceptButton.Y = Math.Max(TextTop + (MinimumLineCount * LineHeight), _frameHeight - ButtonBottomOffset);
                _acceptButton.SetVisible(snapshot.IsOpen);
                _acceptButton.SetEnabled(snapshot.CanAccept);
                _acceptButton.ButtonVisible = snapshot.IsOpen;
            }
        }

        private void DrawFrame(SpriteBatch sprite)
        {
            DrawHorizontalBand(sprite, _assets.Top, Position.X, Position.Y, _assets.TopHeight);

            int centerTop = Position.Y + _assets.TopHeight;
            int centerHeight = Math.Max(1, _frameHeight - _assets.TopHeight - _assets.BottomHeight);
            for (int y = 0; y < centerHeight; y += _assets.CenterHeight)
            {
                DrawHorizontalBand(sprite, _assets.Center, Position.X, centerTop + y, Math.Min(_assets.CenterHeight, centerHeight - y));
            }

            DrawHorizontalBand(sprite, _assets.Bottom, Position.X, Position.Y + _frameHeight - _assets.BottomHeight, _assets.BottomHeight);
        }

        private void DrawHorizontalBand(SpriteBatch sprite, EngagementProposalBand band, int x, int y, int drawHeight)
        {
            if (band == null || drawHeight <= 0)
            {
                return;
            }

            DrawTexture(sprite, band.Left, new Rectangle(x, y, band.Left?.Width ?? 0, drawHeight));
            DrawTexture(sprite, band.Right, new Rectangle(x + FrameWidth - (band.Right?.Width ?? 0), y, band.Right?.Width ?? 0, drawHeight));

            int centerStart = x + (band.Left?.Width ?? 0);
            int centerWidth = Math.Max(0, FrameWidth - (band.Left?.Width ?? 0) - (band.Right?.Width ?? 0));
            if (band.Center != null)
            {
                for (int offset = 0; offset < centerWidth; offset += band.Center.Width)
                {
                    DrawTexture(sprite, band.Center, new Rectangle(centerStart + offset, y, Math.Min(band.Center.Width, centerWidth - offset), drawHeight));
                }
            }
            else
            {
                sprite.Draw(_pixel, new Rectangle(centerStart, y, centerWidth, drawHeight), new Color(245, 230, 212));
            }
        }

        private static void DrawTexture(SpriteBatch sprite, Texture2D texture, Rectangle destination)
        {
            if (texture == null || destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            sprite.Draw(texture, destination, Color.White);
        }

        private void DrawOutlinedText(SpriteBatch sprite, string text, Vector2 position, Color fillColor, Color outlineColor)
        {
            sprite.DrawString(_font, text, position + new Vector2(-1f, 0f), outlineColor);
            sprite.DrawString(_font, text, position + new Vector2(1f, 0f), outlineColor);
            sprite.DrawString(_font, text, position + new Vector2(0f, -1f), outlineColor);
            sprite.DrawString(_font, text, position + new Vector2(0f, 1f), outlineColor);
            sprite.DrawString(_font, text, position, fillColor);
        }

        private IReadOnlyList<string> WrapText(string text, float maxWidth)
        {
            List<string> lines = new();
            if (_font == null)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text.Trim());
                }

                return lines;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return lines;
            }

            foreach (string paragraph in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string currentLine = string.Empty;
                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < words.Length; i++)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = words[i];
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
            }

            return lines;
        }

        private int CountWrappedLines(string text)
        {
            return WrapText(text, TextWidth).Count;
        }

        private EngagementProposalSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new EngagementProposalSnapshot();
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

    internal sealed class EngagementProposalWindowAssets
    {
        internal EngagementProposalWindowAssets(EngagementProposalBand top, EngagementProposalBand center, EngagementProposalBand bottom)
        {
            Top = top ?? throw new ArgumentNullException(nameof(top));
            Center = center ?? throw new ArgumentNullException(nameof(center));
            Bottom = bottom ?? throw new ArgumentNullException(nameof(bottom));
        }

        internal EngagementProposalBand Top { get; }
        internal EngagementProposalBand Center { get; }
        internal EngagementProposalBand Bottom { get; }
        internal int TopHeight => Top.Height;
        internal int CenterHeight => Math.Max(1, Center.Height);
        internal int BottomHeight => Bottom.Height;
    }

    internal sealed class EngagementProposalBand
    {
        internal EngagementProposalBand(Texture2D left, Texture2D center, Texture2D right)
        {
            Left = left;
            Center = center;
            Right = right;
            Height = Math.Max(left?.Height ?? 0, Math.Max(center?.Height ?? 0, right?.Height ?? 0));
        }

        internal Texture2D Left { get; }
        internal Texture2D Center { get; }
        internal Texture2D Right { get; }
        internal int Height { get; }
    }
}
