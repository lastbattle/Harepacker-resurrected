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
    public enum OptionMenuMode
    {
        Game = 0,
        System = 1,
        Extra = 2,
        Joypad = 3,
    }

    public sealed class OptionMenuWindow : UIWindowBase
    {
        private readonly struct PageLayer
        {
            public PageLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private sealed class OptionRow
        {
            public OptionRow(string label, string description, Func<bool> getValue, Action<bool> setValue)
            {
                Label = label;
                Description = description;
                GetValue = getValue;
                SetValue = setValue;
            }

            public string Label { get; }
            public string Description { get; }
            public Func<bool> GetValue { get; }
            public Action<bool> SetValue { get; }
        }

        private readonly List<PageLayer> _layers = new();
        private readonly Dictionary<OptionMenuMode, List<OptionRow>> _rows = new();
        private readonly Texture2D _checkTexture;
        private readonly Texture2D _highlightTexture;
        private readonly string _windowName;
        private SpriteFont _font;
        private OptionMenuMode _mode;
        private string _statusMessage = string.Empty;

        public OptionMenuWindow(IDXObject frame, string windowName, Texture2D checkTexture, Texture2D highlightTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _checkTexture = checkTexture;
            _highlightTexture = highlightTexture;
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void ConfigureRows(
            Func<bool> getSmoothCamera,
            Action<bool> setSmoothCamera,
            Func<bool> getBgmMuted,
            Action<bool> setBgmMuted,
            Func<bool> getSfxMuted,
            Action<bool> setSfxMuted,
            Func<bool> getPauseOnFocusLoss,
            Action<bool> setPauseOnFocusLoss)
        {
            _rows[OptionMenuMode.Game] = new List<OptionRow>
            {
                new("Smooth Camera", "Toggles the simulator camera easing path.", getSmoothCamera, setSmoothCamera),
            };

            _rows[OptionMenuMode.System] = new List<OptionRow>
            {
                new("Mute BGM", "Silences field background music while keeping the owner open.", getBgmMuted, setBgmMuted),
                new("Mute Effects", "Silences UI and combat sound effects.", getSfxMuted, setSfxMuted),
                new("Pause On Focus Loss", "Matches the simulator focus-loss audio pause rule.", getPauseOnFocusLoss, setPauseOnFocusLoss),
            };

            _rows[OptionMenuMode.Extra] = new List<OptionRow>
            {
                new("Smooth Camera", "Same camera toggle exposed through the deeper option flow.", getSmoothCamera, setSmoothCamera),
                new("Mute BGM", "Shared audio setting surfaced from the additional options branch.", getBgmMuted, setBgmMuted),
                new("Mute Effects", "Shared audio setting surfaced from the additional options branch.", getSfxMuted, setSfxMuted),
            };

            _rows[OptionMenuMode.Joypad] = new List<OptionRow>();
        }

        public void ShowMode(OptionMenuMode mode)
        {
            _mode = mode;
            Show();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            if (!_rows.TryGetValue(_mode, out List<OptionRow> rows) || rows == null)
            {
                return false;
            }

            Rectangle rowBounds = GetRowBounds(0);
            for (int i = 0; i < rows.Count; i++)
            {
                rowBounds = GetRowBounds(i);
                if (!rowBounds.Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                OptionRow row = rows[i];
                if (row.GetValue != null && row.SetValue != null)
                {
                    bool nextValue = !row.GetValue();
                    row.SetValue(nextValue);
                    _statusMessage = $"{row.Label}: {(nextValue ? "On" : "Off")}";
                }

                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
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
            foreach (PageLayer layer in _layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, GetTitle(), new Vector2(Position.X + 16, Position.Y + 16), Color.White);
            sprite.DrawString(_font, GetSubtitle(), new Vector2(Position.X + 16, Position.Y + 38), new Color(214, 214, 214));

            if (!_rows.TryGetValue(_mode, out List<OptionRow> rows) || rows == null || rows.Count == 0)
            {
                DrawWrappedText(sprite, "The client joypad owner is loaded, but per-button calibration and pad remapping still remain outside this simulator pass.", Position.X + 16, Position.Y + 72, 248f, new Color(224, 224, 224));
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    Rectangle bounds = GetRowBounds(i);
                    sprite.Draw(_highlightTexture, bounds, new Color(36, 46, 62, 210));

                    OptionRow row = rows[i];
                    bool enabled = row.GetValue?.Invoke() ?? false;
                    if (enabled && _checkTexture != null)
                    {
                        sprite.Draw(_checkTexture, new Vector2(bounds.X + 8, bounds.Y + 7), Color.White);
                    }

                    sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 24, bounds.Y + 4), Color.White);
                    DrawWrappedText(sprite, row.Description, bounds.X + 24, bounds.Y + 22, bounds.Width - 30, new Color(204, 204, 204));
                }
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                sprite.DrawString(
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 16, Position.Y + (CurrentFrame?.Height ?? 320) - _font.LineSpacing - 12),
                    new Color(255, 228, 151));
            }
        }

        private string GetTitle()
        {
            return _mode switch
            {
                OptionMenuMode.Game => "Game Option",
                OptionMenuMode.System => "System Option",
                OptionMenuMode.Joypad => "Joypad",
                _ => "Option",
            };
        }

        private string GetSubtitle()
        {
            return _mode switch
            {
                OptionMenuMode.Game => "Client-backed option art with simulator-side game toggles.",
                OptionMenuMode.System => "Audio and focus behavior already connected to the runtime.",
                OptionMenuMode.Joypad => "Joypad entry now opens the dedicated owner instead of hard-blocking.",
                _ => "Shared miscellaneous toggles surfaced through the deeper option branch.",
            };
        }

        private Rectangle GetRowBounds(int index)
        {
            int width = Math.Max(220, (CurrentFrame?.Width ?? 283) - 30);
            return new Rectangle(Position.X + 12, Position.Y + 72 + (index * 84), width, 72);
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            foreach (string line in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, line, new Vector2(x, drawY), color);
                drawY += _font.LineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }
    }
}
