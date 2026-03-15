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
    public sealed class StatusBarPopupMenuWindow : UIWindowBase
    {
        private readonly string _windowName;
        private readonly Dictionary<string, Action> _actions = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

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

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            foreach (UIObject uiBtn in uiButtons)
            {
                bool handled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, Position.X, Position.Y, mouseState);
                if (handled)
                {
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
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
    }
}
