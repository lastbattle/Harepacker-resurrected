using HaCreator.MapSimulator.UI;
using HaSharedLibrary;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
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
        private int _CanvasSnapshotHeight = -1, _CanvasSnapshotWidth = -1; // a snapshot of the height and width of the canvas for initialization of the buttons
        public int CanvasSnapshotHeight
        {
            get { return _CanvasSnapshotHeight; }
            private set { }
        }
        public int CanvasSnapshotWidth
        {
            get { return _CanvasSnapshotWidth; }
            private set { }
        }

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
            WzSubProperty normalStateProperty = (WzSubProperty)uiButtonProperty["normal"];
            WzSubProperty disabledStateProperty = (WzSubProperty)uiButtonProperty["disabled"];
            WzSubProperty pressedStateProperty = (WzSubProperty)uiButtonProperty["pressed"];
            WzSubProperty mouseOverStateProperty = (WzSubProperty)uiButtonProperty["mouseOver"];

            this._normalState = CreateBaseDXDrawableItemWithWzProperty(normalStateProperty, flip, relativePositionXY, graphicsDevice);
            this._disabledState = CreateBaseDXDrawableItemWithWzProperty(disabledStateProperty, flip, relativePositionXY, graphicsDevice);
            this._pressedState = CreateBaseDXDrawableItemWithWzProperty(pressedStateProperty, flip, relativePositionXY, graphicsDevice);
            this._mouseOverState = CreateBaseDXDrawableItemWithWzProperty(mouseOverStateProperty, flip, relativePositionXY, graphicsDevice);

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
            WzSoundResourceStreamer currAudio = new WzSoundResourceStreamer(wzSoundProperty, false) {
                Volume = 1.0f
            };
            return currAudio;

            /*using (MemoryStream ms = new MemoryStream(wzSoundProperty.GetBytes(true)))  // dont dispose until its no longer needed
            {
                SoundEffect soundEffect = null;

                WaveFormat wavFmt = BtMouseProperty.WavFormat;
                if (wavFmt.Encoding == WaveFormatEncoding.MpegLayer3)
                 {
                     Mp3FileReader mpegStream = new Mp3FileReader(ms);
                     soundEffect = SoundEffect.FromStream(mpegStream);
                 }
                 else if (wavFmt.Encoding == WaveFormatEncoding.Pcm)
                 {
                     WaveFileReader waveFileStream = new WaveFileReader(ms);
                     soundEffect = SoundEffect.FromStream(waveFileStream);
                 }
                return soundEffect;
            }

          /*      using (MemoryStream ms = new MemoryStream(BtMouseProperty.GetBytes(true)))  // dont dispose until its no longer needed
                {
                    using (Mp3FileReader reader = new Mp3FileReader(ms))
                    {
                        using (WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(reader))
                        {
                            // WaveFileWriter.CreateWaveFile(outputFile, pcmStream);
                            SoundEffect effect = SoundEffect.FromStream(pcmStream);
                        }
                    }
                }

                using (MemoryStream ms = new MemoryStream(BtMouseProperty.GetBytes(true)))  // dont dispose until its no longer needed
                {
                    using (WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(new Mp3FileReader(ms)))
                    {
                        // convert 16 bit to 8 bit pcm stream
                        var newFormat = new WaveFormat(8000, 16, 1);
                        using (var conversionStream = new WaveFormatConversionStream(newFormat, pcm))  // https://stackoverflow.com/questions/49776648/wav-file-conversion-pcm-48khz-16-bit-rate-to-u-law-8khz-8-bit-rate-using-naudio ty
                        {
                            SoundEffect effect = SoundEffect.FromStream(conversionStream);
                            return effect; // TODO: dispose this later
                        }
                    }
                }
            return null;*/
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
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();
                    int? delay = property[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt();

                    if (_CanvasSnapshotHeight == -1)
                    {
                        _CanvasSnapshotHeight = btImage.Height; // set the snapshot width and height
                        _CanvasSnapshotWidth = btImage.Width;
                    }

                    IDXObject dxObj = new DXObject(origin, btImage.ToTexture2D(graphicsDevice), delay != null ? (int)delay : 0);
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
            if (drawableImages.Count == 0) // oh noz u sux
                throw new Exception("Error creating BaseDXDrawableItem from WzSubProperty.");

            BaseDXDrawableItem ret;
            if (drawableImages.Count > 0) {
                ret = new BaseDXDrawableItem(drawableImages, flip) {
                    Position = relativePositionXY
                };
            }
            else {
                ret = new BaseDXDrawableItem(drawableImages[0], flip) {
                    Position = relativePositionXY
                };
            }
            return ret;
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

                    if (priorState == UIObjectState.Pressed) // this after setting the MouseOver state, so user-code does not get override
                    {
                        // Invoke clicked event
                        ButtonClickReleased?.Invoke(this);
                    }
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
