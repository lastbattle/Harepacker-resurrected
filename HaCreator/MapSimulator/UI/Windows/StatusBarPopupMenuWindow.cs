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
    /// <summary>
    /// Small popup owner used by the status-bar Menu/System buttons.
    /// The client treats these as dedicated interaction surfaces instead
    /// of forwarding directly to the target windows.
    /// </summary>
    public enum StatusBarPopupCursorHint
    {
        Normal = 0,
        Forbidden = 1,
        Busy = 2,
    }

    public sealed class StatusBarPopupMenuWindow : UIWindowBase
    {
        private readonly struct PopupEntry
        {
            public PopupEntry(string entryName, UIObject button)
            {
                EntryName = entryName;
                Button = button;
            }

            public string EntryName { get; }
            public UIObject Button { get; }
        }

        private readonly string _windowName;
        private readonly Dictionary<string, Action> _actions = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Func<StatusBarPopupCursorHint>> _cursorHints = new Dictionary<string, Func<StatusBarPopupCursorHint>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _tooltips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PopupEntry> _entries = new List<PopupEntry>();
        private string _hoveredEntryName = string.Empty;
        private Texture2D _tooltipPixel;

        public StatusBarPopupMenuWindow(IDXObject frame, string windowName, Point position)
            : base(frame, position)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
        }

        public override string WindowName => _windowName;

        public override bool SupportsDragging => false;

        public void AddEntry(string entryName, UIObject button)
        {
            if (string.IsNullOrWhiteSpace(entryName) || button == null)
            {
                return;
            }

            AddButton(button);
            _entries.Add(new PopupEntry(entryName, button));
            button.ButtonClickReleased += _ => OnEntryClicked(entryName);
        }

        public void BindEntryAction(string entryName, Action action)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return;
            }

            if (action == null)
            {
                _actions.Remove(entryName);
                return;
            }

            _actions[entryName] = action;
        }

        public void BindEntryCursorHint(string entryName, Func<StatusBarPopupCursorHint> cursorHintResolver)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return;
            }

            if (cursorHintResolver == null)
            {
                _cursorHints.Remove(entryName);
                return;
            }

            _cursorHints[entryName] = cursorHintResolver;
        }

        public void SetEntryEnabled(string entryName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                PopupEntry entry = _entries[i];
                if (string.Equals(entry.EntryName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Button?.SetEnabled(enabled);
                    return;
                }
            }
        }

        public void SetEntryTooltip(string entryName, string tooltipText)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(tooltipText))
            {
                _tooltips.Remove(entryName);
                if (string.Equals(_hoveredEntryName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    _hoveredEntryName = string.Empty;
                }

                return;
            }

            _tooltips[entryName] = tooltipText.Trim();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _hoveredEntryName = string.Empty;
                return false;
            }

            _hoveredEntryName = TryResolveHoveredEntryName(mouseState.X, mouseState.Y);
            foreach (PopupEntry entry in _entries)
            {
                bool handled = entry.Button.CheckMouseEvent(shiftCenteredX, shiftCenteredY, Position.X, Position.Y, mouseState);
                if (handled)
                {
                    ApplyCursorHint(mouseCursor, ResolveCursorHint(entry.EntryName));
                    return true;
                }
            }

            if ((mouseState.LeftButton == ButtonState.Pressed || mouseState.RightButton == ButtonState.Pressed)
                && !ContainsPoint(mouseState.X, mouseState.Y))
            {
                Hide();
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
        }

        protected override void DrawOverlay(
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
            base.DrawOverlay(sprite, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);

            if (!CanDrawWindowText
                || string.IsNullOrWhiteSpace(_hoveredEntryName)
                || !_tooltips.TryGetValue(_hoveredEntryName, out string tooltipText)
                || string.IsNullOrWhiteSpace(tooltipText))
            {
                return;
            }

            Rectangle? entryBounds = TryGetEntryBounds(_hoveredEntryName);
            if (!entryBounds.HasValue)
            {
                return;
            }

            DrawShortcutTooltip(sprite, entryBounds.Value, tooltipText, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        private void OnEntryClicked(string entryName)
        {
            Hide();

            if (_actions.TryGetValue(entryName, out Action action))
            {
                action?.Invoke();
            }
        }

        private StatusBarPopupCursorHint ResolveCursorHint(string entryName)
        {
            if (_cursorHints.TryGetValue(entryName, out Func<StatusBarPopupCursorHint> resolver) && resolver != null)
            {
                return resolver();
            }

            return StatusBarPopupCursorHint.Normal;
        }

        private static void ApplyCursorHint(MouseCursorItem mouseCursor, StatusBarPopupCursorHint cursorHint)
        {
            if (mouseCursor == null)
            {
                return;
            }

            switch (cursorHint)
            {
                case StatusBarPopupCursorHint.Forbidden:
                    mouseCursor.SetMouseCursorForbidden();
                    break;
                case StatusBarPopupCursorHint.Busy:
                    mouseCursor.SetMouseCursorBusy();
                    break;
                default:
                    mouseCursor.SetMouseCursorMovedToClickableItem();
                    break;
            }
        }

        private string TryResolveHoveredEntryName(int mouseX, int mouseY)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                PopupEntry entry = _entries[i];
                Rectangle? bounds = TryGetEntryBounds(entry.EntryName);
                if (bounds.HasValue && bounds.Value.Contains(mouseX, mouseY))
                {
                    return entry.EntryName;
                }
            }

            return string.Empty;
        }

        private Rectangle? TryGetEntryBounds(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return null;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                PopupEntry entry = _entries[i];
                if (!string.Equals(entry.EntryName, entryName, StringComparison.OrdinalIgnoreCase)
                    || entry.Button == null
                    || !entry.Button.ButtonVisible)
                {
                    continue;
                }

                int width = entry.Button.CanvasSnapshotWidth;
                int height = entry.Button.CanvasSnapshotHeight;
                if (width <= 0 || height <= 0)
                {
                    BaseDXDrawableItem drawable = entry.Button.GetBaseDXDrawableItemByState();
                    width = drawable?.LastFrameDrawn?.Width ?? 0;
                    height = drawable?.LastFrameDrawn?.Height ?? 0;
                }

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                return new Rectangle(Position.X + entry.Button.X, Position.Y + entry.Button.Y, width, height);
            }

            return null;
        }

        private void DrawShortcutTooltip(SpriteBatch sprite, Rectangle anchorBounds, string tooltipText, int renderWidth, int renderHeight)
        {
            Vector2 textSize = MeasureWindowText(sprite, tooltipText, 0.75f);
            int paddingX = 6;
            int paddingY = 4;
            int width = Math.Max(22, (int)Math.Ceiling(textSize.X) + (paddingX * 2));
            int height = Math.Max(16, (int)Math.Ceiling(textSize.Y) + (paddingY * 2));
            int x = anchorBounds.Right + 8;
            int y = anchorBounds.Center.Y - (height / 2);

            if (x + width > renderWidth)
            {
                x = anchorBounds.Left - width - 8;
            }

            if (x < 8)
            {
                x = Math.Max(8, Math.Min(anchorBounds.Left, renderWidth - width - 8));
                y = anchorBounds.Top - height - 6;
            }

            if (y + height > renderHeight - 8)
            {
                y = renderHeight - height - 8;
            }

            if (y < 8)
            {
                y = Math.Min(renderHeight - height - 8, anchorBounds.Bottom + 6);
            }

            Rectangle background = new Rectangle(x, y, width, height);
            Texture2D pixel = EnsureTooltipPixel(sprite.GraphicsDevice);
            sprite.Draw(pixel, background, new Color(24, 28, 37, 220));
            sprite.Draw(pixel, new Rectangle(background.X, background.Y, background.Width, 1), new Color(255, 238, 155, 210));
            sprite.Draw(pixel, new Rectangle(background.X, background.Bottom - 1, background.Width, 1), new Color(255, 238, 155, 210));
            sprite.Draw(pixel, new Rectangle(background.X, background.Y, 1, background.Height), new Color(255, 238, 155, 210));
            sprite.Draw(pixel, new Rectangle(background.Right - 1, background.Y, 1, background.Height), new Color(255, 238, 155, 210));
            DrawWindowText(sprite, tooltipText, new Vector2(background.X + paddingX, background.Y + paddingY), new Color(255, 238, 155), 0.75f);
        }

        private Texture2D EnsureTooltipPixel(GraphicsDevice device)
        {
            if (_tooltipPixel == null)
            {
                _tooltipPixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
                _tooltipPixel.SetData(new[] { Color.White });
            }

            return _tooltipPixel;
        }
    }
}
