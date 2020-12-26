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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.MapObjects.UIObject
{
    /// <summary>
    /// Click-able XNA button 
    /// </summary>
    public class UIObject : UIObjectViewModelBase
    {
        #region Fields
        private UIObjectState currentState = UIObjectState.Normal;

        private SoundEffect seBtMouseClick, seBtMouseOver;

        private readonly BaseDXDrawableItem normalState;
        private readonly BaseDXDrawableItem disabledState;
        private readonly BaseDXDrawableItem pressedState;
        private readonly BaseDXDrawableItem mouseOverState;

        /// <summary>
        /// Gets the current BaseDXDrawableItem based upon the current button state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public BaseDXDrawableItem GetBaseDXDrawableItemByState(UIObjectState state = UIObjectState.Null)
        {
            if (state == UIObjectState.Null)
                state = currentState;

            switch (state)
            {
                case UIObjectState.Pressed:
                    return pressedState;
                case UIObjectState.Disabled:
                    return disabledState;
                case UIObjectState.MouseOver:
                    return mouseOverState;
                default:
                case UIObjectState.Null:
                case UIObjectState.Normal:
                    return normalState;
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
        #endregion


        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="normalState"></param>
        /// <param name="disabledState"></param>
        /// <param name="pressedState"></param>
        /// <param name="mouseOverState"></param>
        public UIObject(BaseDXDrawableItem normalState, BaseDXDrawableItem disabledState, BaseDXDrawableItem pressedState, BaseDXDrawableItem mouseOverState)
        {
            this.normalState = normalState;
            this.disabledState = disabledState;
            this.pressedState = pressedState;
            this.mouseOverState = mouseOverState;
        }

        /// <summary>
        /// Constructor. Create by the WzSubProperty object
        /// </summary>
        /// <param name="uiButtonProperty"></param>
        /// <param name="BtMouseClickProperty"></param>
        /// <param name="BtMouseOverProperty"></param>
        /// <param name="flip"></param>
        /// <param name="relativePositionXY">The relative position of the button to be overlaid on top of the main BaseDXDrawableItem</param>
        /// <param name="graphicsDevice"></param>
        public UIObject(WzSubProperty uiButtonProperty, WzBinaryProperty BtMouseClickProperty, WzBinaryProperty BtMouseOverProperty,
            bool flip,
            Point relativePositionXY,
            GraphicsDevice graphicsDevice)
        {
            WzSubProperty normalStateProperty = (WzSubProperty)uiButtonProperty["normal"];
            WzSubProperty disabledStateProperty = (WzSubProperty)uiButtonProperty["disabled"];
            WzSubProperty pressedStateProperty = (WzSubProperty)uiButtonProperty["pressed"];
            WzSubProperty mouseOverStateProperty = (WzSubProperty)uiButtonProperty["mouseOver"];

            this.normalState = CreateBaseDXDrawableItemWithWzProperty(normalStateProperty, flip, relativePositionXY, graphicsDevice);
            this.disabledState = CreateBaseDXDrawableItemWithWzProperty(disabledStateProperty, flip, relativePositionXY, graphicsDevice);
            this.pressedState = CreateBaseDXDrawableItemWithWzProperty(pressedStateProperty, flip, relativePositionXY, graphicsDevice);
            this.mouseOverState = CreateBaseDXDrawableItemWithWzProperty(mouseOverStateProperty, flip, relativePositionXY, graphicsDevice);

            this.seBtMouseClick = CreateSoundEffectWithWzProperty(BtMouseClickProperty);
            this.seBtMouseOver = CreateSoundEffectWithWzProperty(BtMouseOverProperty);
        }

        #region Init
        /// <summary>
        /// Create SoundEffect from WzBinaryProperty
        /// TODO: combined cache
        /// </summary>
        /// <param name="BtMouseProperty"></param>
        /// <returns></returns>
        private SoundEffect CreateSoundEffectWithWzProperty(WzBinaryProperty BtMouseProperty)
        {
            /*using (MemoryStream ms = new MemoryStream(BtMouseProperty.GetBytes(true)))  // dont dispose until its no longer needed
            {
                WaveFormat wavFmt = BtMouseProperty.WavFormat;
                if (wavFmt.Encoding == WaveFormatEncoding.MpegLayer3)
                {
                    Mp3FileReader mpegStream = new Mp3FileReader(ms);
                    SoundEffect effect = SoundEffect.FromStream(mpegStream);
                }
                else if (wavFmt.Encoding == WaveFormatEncoding.Pcm)
                {
                    WaveFileReader waveFileStream = new WaveFileReader(ms);
                    SoundEffect effect = SoundEffect.FromStream(waveFileStream);
                }
            }*/
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
                }*/
            return null;
        }

        /// <summary>
        /// Creates a BaseDXDrawableItem UI item from WzSubProperty
        /// </summary>
        /// <param name="subProperty"></param>
        /// <param name="flip"></param>
        /// <param name="relativePositionXY">The relative position of the button to be overlaid on top of the main BaseDXDrawableItem</param>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        private BaseDXDrawableItem CreateBaseDXDrawableItemWithWzProperty(WzSubProperty subProperty, bool flip, Point relativePositionXY, GraphicsDevice graphicsDevice)
        {
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

                    IDXObject dxObj_miniMapPixel = new DXObject(origin, btImage.ToTexture2D(graphicsDevice), delay != null ? (int)delay : 0);
                    drawableImages.Add(dxObj_miniMapPixel);
                }
                i++;
            }
            if (drawableImages.Count == 0) // oh noz u sux
                throw new Exception("Error creating BaseDXDrawableItem from WzSubProperty.");

            if (drawableImages.Count > 0)
            {
                BaseDXDrawableItem item_pixelDot = new BaseDXDrawableItem(drawableImages, flip)
                {
                    Position = relativePositionXY
                };
                return item_pixelDot;
            }
            return new BaseDXDrawableItem(drawableImages[0], flip)
            {
                Position = relativePositionXY
            };
        }
        #endregion

        #region Events
        /// <summary>
        /// Check if the button is in this boundary when the user clicks.
        /// </summary>
        /// <param name="shiftCenteredX"></param>
        /// <param name="shiftCenteredY"></param>
        /// <param name="containerParentX"></param>
        /// <param name="containerParentY"></param>
        /// <param name="mouseState"></param>
        public void CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, int containerParentX, int containerParentY, MouseState mouseState)
        {
            if (this.currentState == UIObjectState.Disabled)
                return; // disabled buttons dont react

            // The position of the button relative to the minimap
            int minimapButtonRelativeX = -(containerParentX) - X; // Left to right
            int minimapButtonRelativeY = -(containerParentY) - Y; // Top to bottom

            int buttonPositionXToMap = shiftCenteredX - minimapButtonRelativeX;
            int buttonPositionYToMap = shiftCenteredY - minimapButtonRelativeY;

            // The position of the mouse relative to the game
            Rectangle rect = new Rectangle(
                buttonPositionXToMap - (shiftCenteredX),
                buttonPositionYToMap - (shiftCenteredY),
                CanvasSnapshotWidth, CanvasSnapshotHeight);

            //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
            //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);

            if (rect.Contains(mouseState.X, mouseState.Y))
            {
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    SetButtonState(UIObjectState.Pressed);

                    if (seBtMouseClick != null) // play mouse click sound
                        seBtMouseClick.Play();
                }
                else if (mouseState.LeftButton == ButtonState.Released)
                {
                    SetButtonState(UIObjectState.MouseOver);
                }
                else
                {
                    SetButtonState(UIObjectState.MouseOver);

                    if (seBtMouseOver != null) // play mouse over sound
                        seBtMouseOver.Play();
                }
            }
            else
            {
                SetButtonState(UIObjectState.Normal);
            }
        }

        /// <summary>
        /// Sets the current state of the button
        /// </summary>
        /// <param name="state"></param>
        private void SetButtonState(UIObjectState state)
        {
            this.currentState = state;
        }
        #endregion
    }
}
