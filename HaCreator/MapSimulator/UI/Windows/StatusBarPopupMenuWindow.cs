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
        private readonly List<PopupEntry> _entries = new List<PopupEntry>();

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

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

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
    }
}
