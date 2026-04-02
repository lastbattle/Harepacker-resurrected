using HaCreator.MapSimulator.UI;
using HaSharedLibrary;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using static HaCreator.MapSimulator.UI.UIObjectButtonEvent;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Click-able XNA button 
    /// </summary>
    public class UIObject : UIObjectViewModelBase
    {
        #region Fields
        private UIObjectState _currentState = UIObjectState.Normal;

        private WzSoundResourceStreamer _seMouseClick, _seMouseOver;

        private readonly BaseDXDrawableItem _normalState;
        private readonly BaseDXDrawableItem _disabledState;
        private readonly BaseDXDrawableItem _pressedState;
        private readonly BaseDXDrawableItem _mouseOverState;


        /// <summary>
        /// On button click, and released.
        /// </summary>
        public event UIObjectButtonEventHandler ButtonClickReleased;


        /// <summary>
        /// Gets the current BaseDXDrawableItem based upon the current button state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public BaseDXDrawableItem GetBaseDXDrawableItemByState(UIObjectState state = UIObjectState.Null)
        {
            if (state == UIObjectState.Null)
                state = _currentState;

            switch (state)
            {
                case UIObjectState.Pressed:
                    return _pressedState;
                case UIObjectState.Disabled:
                    return _disabledState;
                case UIObjectState.MouseOver:
                    return _mouseOverState;
                default:
                case UIObjectState.Null:
                case UIObjectState.Normal:
                    return _normalState;
            }
        }
        #endregion

        #region Custom Members
        private int _canvasSnapshotHeight = -1;
        private int _canvasSnapshotWidth = -1;

        public int CanvasSnapshotHeight => _canvasSnapshotHeight;

        public int CanvasSnapshotWidth => _canvasSnapshotWidth;

        public UIObjectState CurrentState => _currentState;
        public bool Enabled => _currentState != UIObjectState.Disabled;

        private int _X;
        /// <summary>
        /// The additional relative position of the image (used primarily for UI overlay)
        /// </summary>
        public int X
        {
            get { return _X; }
            set { this._X = value; }
        }

        private int _Y;
        /// <summary>
        /// The additional relative position of the image (used primarily for UI overlay)
        /// </summary>
        public int Y
        {
            get { return _Y; }
            set { this._Y = value; }
        }

        private bool _buttonVisible = true;
        /// <summary>
        /// Whether the button is visible and can be interacted with
        /// </summary>
        public bool ButtonVisible
        {
            get { return _buttonVisible; }
            set { _buttonVisible = value; }
        }

        /// <summary>
        /// Set the visibility of the button
        /// </summary>
        /// <param name="visible">True to show, false to hide</param>
        public void SetVisible(bool visible)
        {
            _buttonVisible = visible;
        }
        #endregion


        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="_normalState"></param>
        /// <param name="_disabledState"></param>
        /// <param name="_pressedState"></param>
        /// <param name="_mouseOverState"></param>
        public UIObject(BaseDXDrawableItem _normalState, BaseDXDrawableItem _disabledState, BaseDXDrawableItem _pressedState, BaseDXDrawableItem _mouseOverState)
        {
            this._normalState = _normalState;
            this._disabledState = _disabledState;
            this._pressedState = _pressedState;
            this._mouseOverState = _mouseOverState;

            IDXObject snapshotFrame = _normalState?.Frame0 ?? _normalState?.LastFrameDrawn;
            if (snapshotFrame != null)
            {
                _canvasSnapshotWidth = snapshotFrame.Width;
                _canvasSnapshotHeight = snapshotFrame.Height;
            }

            if (_normalState != null)
            {
                X = _normalState.Position.X;
                Y = _normalState.Position.Y;
            }
        }

        /// <summary>
        /// Constructor. Create by the WzSubProperty object
        /// </summary>
        /// <param name="uiButtonProperty"></param>
        /// <param name="btMouseClickSoundProperty">The sound property for mouse click</param>
        /// <param name="btMouseOverSoundProperty">The sound property for mouse over.</param>
        /// <param name="flip"></param>
        /// <param name="relativePositionXY">The relative position of the button to be overlaid on top of the main BaseDXDrawableItem</param>
        /// <param name="graphicsDevice"></param>
        public UIObject(WzSubProperty uiButtonProperty, WzBinaryProperty btMouseClickSoundProperty, WzBinaryProperty btMouseOverSoundProperty,
            bool flip,
            Point relativePositionXY,
            GraphicsDevice graphicsDevice)
        {
            WzSubProperty normalStateProperty = uiButtonProperty?["normal"] as WzSubProperty;
            WzSubProperty disabledStateProperty = uiButtonProperty?["disabled"] as WzSubProperty;
            WzSubProperty pressedStateProperty = uiButtonProperty?["pressed"] as WzSubProperty;
            WzSubProperty mouseOverStateProperty = uiButtonProperty?["mouseOver"] as WzSubProperty;

            BaseDXDrawableItem normalState = CreateBaseDXDrawableItemWithWzProperty(normalStateProperty, flip, relativePositionXY, graphicsDevice);
            BaseDXDrawableItem disabledState = CreateBaseDXDrawableItemWithWzProperty(disabledStateProperty, flip, relativePositionXY, graphicsDevice);
            BaseDXDrawableItem pressedState = CreateBaseDXDrawableItemWithWzProperty(pressedStateProperty, flip, relativePositionXY, graphicsDevice);
            BaseDXDrawableItem mouseOverState = CreateBaseDXDrawableItemWithWzProperty(mouseOverStateProperty, flip, relativePositionXY, graphicsDevice);

            normalState ??= pressedState ?? mouseOverState ?? disabledState;
            if (normalState == null)
            {
                throw new InvalidOperationException("UI button could not be created because it has no drawable states.");
            }

            this._normalState = normalState;
            this._disabledState = disabledState ?? normalState;
            this._pressedState = pressedState ?? normalState;
            this._mouseOverState = mouseOverState ?? normalState;

            this._seMouseClick = CreateSoundEffectWithWzProperty(btMouseClickSoundProperty);
            this._seMouseOver = CreateSoundEffectWithWzProperty(btMouseOverSoundProperty);

            X = this._normalState.Position.X; // origin xy
            Y = this._normalState.Position.Y;
        }

        #region Init
        /// <summary>
        /// Create SoundEffect from WzBinaryProperty
        /// TODO: combined cache
        /// </summary>
        /// <param name="wzSoundProperty"></param>
        /// <returns></returns>
        private WzSoundResourceStreamer CreateSoundEffectWithWzProperty(WzBinaryProperty wzSoundProperty)
        {
            if (wzSoundProperty == null)
            {
                return null;
            }

            WzSoundResourceStreamer currAudio = new WzSoundResourceStreamer(wzSoundProperty, false) {
                Volume = 1.0f
            };
            return currAudio;
        }

        /// <summary>
        /// Creates a BaseDXDrawableItem UI item from WzSubProperty
        /// </summary>
        /// <param name="subProperty"></param>
        /// <param name="flip"></param>
        /// <param name="relativePositionXY">The relative position of the button to be overlaid on top of the main BaseDXDrawableItem</param>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        private BaseDXDrawableItem CreateBaseDXDrawableItemWithWzProperty(WzSubProperty subProperty, bool flip, Point relativePositionXY_, GraphicsDevice graphicsDevice)
        {
            if (subProperty == null)
            {
                return null;
            }

            bool bAddedOriginXY = false;
            Point relativePositionXY = new Point(relativePositionXY_.X, relativePositionXY_.Y);

            List<IDXObject> drawableImages = new List<IDXObject>();
            int i = 0;
            WzImageProperty imgProperty;
            while ((imgProperty = subProperty[i.ToString()]) != null)
            {
                if (imgProperty is WzCanvasProperty property)
                {
                    System.Drawing.Bitmap btImage = property.GetLinkedWzCanvasBitmap(); // maximise
                    if (btImage == null)
                    {
                        i++;
                        continue;
                    }

                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();
                    int? delay = property[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt();

                    if (_canvasSnapshotHeight == -1)
                    {
                        _canvasSnapshotHeight = btImage.Height; // set the snapshot width and height
                        _canvasSnapshotWidth = btImage.Width;
                    }

                    IDXObject dxObj = new DXObject(origin, btImage.ToTexture2DAndDispose(graphicsDevice), delay != null ? (int)delay : 0);
                    drawableImages.Add(dxObj);

                    // the origin X Y needed to update to this UIObject object
                    if (!bAddedOriginXY) {
                        bAddedOriginXY = true;

                        // This object's X and Y coordinates must be in sync with render origin x and y!
                        relativePositionXY.X -= (int) origin.X;
                        relativePositionXY.Y -= (int) origin.Y;
                    }
                }
                i++;
            }
            if (drawableImages.Count == 0)
            {
                return null;
            }

            return new BaseDXDrawableItem(drawableImages, flip)
            {
                Position = relativePositionXY
            };
        }
        #endregion

        #region Events
        public static bool CheckBitfieldState(UIObjectState currentOrPriorState, UIObjectState stateToCheck) {
            return (currentOrPriorState & stateToCheck) == stateToCheck;
        }

        /// <summary>
        /// Check if the button is in this boundary when the user clicks.
        /// </summary>
        /// <param name="shiftCenteredX"></param>
        /// <param name="shiftCenteredY"></param>
        /// <param name="containerParentX"></param>
        /// <param name="containerParentY"></param>
        /// <param name="mouseState"></param>
        /// <param name="mouseCursor"></param>
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, int containerParentX, int containerParentY, MouseState mouseState)
        {
            if (!_buttonVisible)
                return false; // hidden buttons don't react

            if (this._currentState == UIObjectState.Disabled)
                return false; // disabled buttons dont react

           /* BaseDXDrawableItem buttonToDraw = GetBaseDXDrawableItemByState();
            IDXObject lastFrameDrawn = buttonToDraw.LastFrameDrawn;
            if (lastFrameDrawn == null)
                return false;
         */
            // The position of the button relative to the minimap
            int buttonRelativeX = -(containerParentX) - X/* - lastFrameDrawn.X*/; // Left to right
            int buttonRelativeY = -(containerParentY) - Y/* - lastFrameDrawn.Y*/; // Top to bottom

            int buttonPositionXToMap = shiftCenteredX - buttonRelativeX;
            int buttonPositionYToMap = shiftCenteredY - buttonRelativeY;

            // The position of the mouse relative to the game
            Rectangle rect = new Rectangle(
                buttonPositionXToMap - (shiftCenteredX),
                buttonPositionYToMap - (shiftCenteredY),
                CanvasSnapshotWidth, CanvasSnapshotHeight);

            //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
            //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);

            if (rect.Contains(mouseState.X, mouseState.Y))
            {
                UIObjectState priorState = this._currentState;

                if (mouseState.LeftButton == ButtonState.Pressed && CheckBitfieldState(priorState, UIObjectState.Pressed))
                {
                    SetButtonState(UIObjectState.Pressed);

                    if (_seMouseClick != null) // play mouse click sound
                        _seMouseClick.Play();
                }
                else if (mouseState.LeftButton == ButtonState.Released && priorState == UIObjectState.Pressed)
                {
                    SetButtonState(UIObjectState.MouseOver);

                    // Invoke after setting MouseOver so the handler sees the released state.
                    ButtonClickReleased?.Invoke(this);
                }
                else
                {
                    // hover over sound effect
                    if (priorState != UIObjectState.Pressed && priorState != UIObjectState.Disabled && priorState != UIObjectState.MouseOver) {
                        if (_seMouseOver != null) // play mouse over sound
                            _seMouseOver.Play();
                    }

                    SetButtonState(UIObjectState.MouseOver);

                }
                return true;
            }
            else
            {
                SetButtonState(UIObjectState.Normal);
            }
            return false;
        }

        /// <summary>
        /// Sets the current state of the button
        /// </summary>
        /// <param name="state"></param>
        public void SetButtonState(UIObjectState state)
        {
            this._currentState = state;
        }

        /// <summary>
        /// Enable or disable the button
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (_currentState == UIObjectState.Disabled)
                    _currentState = UIObjectState.Normal;
            }
            else
            {
                _currentState = UIObjectState.Disabled;
            }
        }

        /// <summary>
        /// Check if the button is currently enabled
        /// </summary>
        public bool IsEnabled => _currentState != UIObjectState.Disabled;
        #endregion
    }
}
