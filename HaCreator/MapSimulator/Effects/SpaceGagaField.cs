using HaSharedLibrary.Render.DX;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data;
using Spine;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapSimulator.Character;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Util;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Special Effect Field System - Manages specialized field types from MapleStory client.
    ///
    /// Handles:
    /// - CField_Wedding: Wedding ceremony effects (packets 379, 380)
    /// - CField_Witchtower: Witch tower score tracking (packet 358)
    /// - CField_GuildBoss: Guild boss healer/pulley mechanics (packets 344, 345)
    /// - CField_Dojang: Mu Lung Dojo timer and HUD gauges
    /// - CField_SpaceGAGA: Rescue Gaga timerboard clock
    /// - CField_Massacre: Kill counting and gauge system (packet 173)
    /// </summary>
    #region SpaceGAGA Field (CField_SpaceGAGA)
    /// <summary>
    /// SpaceGAGA timerboard HUD.
    ///
    /// WZ evidence:
    /// - Space Gaga maps 922240000, 922240100, and 922240200 all declare fieldType 20
    ///   (FIELDTYPE_SPACEGAGA) in map/Map\Map9.
    /// - Map/Obj/etc.img/space exposes the dedicated SpaceGAGA timerboard art via backgrnd
    ///   (228x69) plus fontTime digits and comma, which is the fixed WZ source this runtime now
    ///   prefers before falling back to older heuristic discovery.
    ///
    /// Client evidence:
    /// - CField_SpaceGAGA::OnClock (0x5625d0) only reacts to clock type 2, destroys the
    ///   previous clock, creates a 258x69 timerboard at (-114, 30), then sets and starts it.
    /// - CTimerboard_SpaceGAGA::Draw (0x5626c0) renders a dedicated source canvas and draws
    ///   zero-padded minutes and seconds at fixed positions: (44, 23) and (131, 23).
    ///
    /// This simulator pass adds the dedicated timerboard flow and clock ownership seam.
    /// The client still references this through StringPool id 0x140D in OnCreate, but the
    /// runtime now pins the concrete WZ source node instead of scanning unrelated UIWindow trees.
    /// </summary>
    public class SpaceGagaField
    {
        private const int TimerboardWidth = 258;
        private const int TimerboardHeight = 69;
        private const int TimerboardOffsetX = -114;
        private const int TimerboardY = 30;
        private const int MinuteTextX = 44;
        private const int SecondTextX = 131;
        private const int TextY = 23;
        private const int DividerX = 110;
        private const int DividerY = 16;
        private const int DividerWidth = 38;
        private const int DividerHeight = 36;
        private const int ResetPulseDurationMs = 600;
        private const string SpaceMapObjectImageName = "Obj/etc.img";
        private const string SpaceTimerboardRootPath = "space";
        private const string SpaceTimerboardBackgroundPath = "space/backgrnd";
        private const string SpaceTimerboardDigitsPath = "space/fontTime";
        private bool _isActive;
        private int _mapId;
        private int _durationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastResetTick = int.MinValue;
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private Texture2D _backgroundTexture;
        private Texture2D _colonTexture;
        private readonly Texture2D[] _digitTextures = new Texture2D[10];

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int DurationSeconds => _durationSec;
        public int RemainingSeconds
        {
            get
            {
                if (_timeOverTick == int.MinValue)
                {
                    return 0;
                }

                int remainingMs = _timeOverTick - Environment.TickCount;
                if (remainingMs <= 0)
                {
                    return 0;
                }

                return (remainingMs + 999) / 1000;
            }
        }

        public void Initialize(GraphicsDevice device)
        {
            _graphicsDevice = device;
            _assetsLoaded = false;
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _durationSec = 0;
            _timeOverTick = int.MinValue;
            _lastResetTick = int.MinValue;
        }

        public void OnClock(int clockType, int durationSec, int currentTimeMs)
        {
            if (clockType != 2)
            {
                return;
            }

            _durationSec = Math.Max(0, durationSec);
            _timeOverTick = currentTimeMs + (_durationSec * 1000);
            _lastResetTick = currentTimeMs;
        }

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            EnsureAssetsLoaded();

            Rectangle bounds = GetTimerboardBounds(spriteBatch.GraphicsDevice.Viewport);

            float pulse = GetResetPulseStrength();
            float urgency = RemainingSeconds <= 10 ? 1f - (RemainingSeconds / 10f) : 0f;

            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Vector2(bounds.X, bounds.Y), Color.White);
                if (pulse > 0f || urgency > 0f)
                {
                    float overlayStrength = Math.Clamp((pulse * 0.25f) + (urgency * 0.15f), 0f, 0.35f);
                    spriteBatch.Draw(pixelTexture, bounds, new Color(255, 233, 158) * overlayStrength);
                }
            }
            else
            {
                Rectangle innerBounds = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
                Rectangle faceBounds = new(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, bounds.Height - 20);
                Rectangle dividerBounds = new(bounds.X + DividerX, bounds.Y + DividerY, DividerWidth, DividerHeight);

                Color outerBorder = Color.Lerp(new Color(68, 120, 166, 255), new Color(255, 233, 158, 255), pulse * 0.65f);
                Color innerFill = Color.Lerp(new Color(10, 22, 41, 232), new Color(25, 51, 84, 240), pulse * 0.35f);
                Color faceFill = Color.Lerp(new Color(25, 53, 87, 230), new Color(123, 49, 33, 236), urgency * 0.75f);
                Color faceHighlight = Color.Lerp(new Color(122, 193, 227, 255), new Color(255, 209, 122, 255), Math.Max(pulse, urgency));
                Color dividerColor = Color.Lerp(new Color(188, 222, 244, 255), new Color(255, 212, 118, 255), Math.Max(pulse, urgency));

                spriteBatch.Draw(pixelTexture, bounds, outerBorder);
                spriteBatch.Draw(pixelTexture, innerBounds, innerFill);
                spriteBatch.Draw(pixelTexture, faceBounds, faceFill);
                spriteBatch.Draw(pixelTexture, new Rectangle(faceBounds.X, faceBounds.Y, faceBounds.Width, 2), faceHighlight);
                spriteBatch.Draw(pixelTexture, new Rectangle(faceBounds.X, faceBounds.Bottom - 2, faceBounds.Width, 2), new Color(8, 14, 24, 255));
                spriteBatch.Draw(pixelTexture, dividerBounds, dividerColor * 0.18f);
                DrawDividerDots(spriteBatch, pixelTexture, bounds, dividerColor);
            }

            if (!TryDrawBitmapTimer(spriteBatch, bounds) && font != null)
            {
                string minutesText = (Math.Max(0, RemainingSeconds) / 60).ToString("00");
                string secondsText = (Math.Max(0, RemainingSeconds) % 60).ToString("00");
                Color timeColor = RemainingSeconds <= 10 ? new Color(255, 229, 177) : Color.White;

                DrawDigitString(spriteBatch, font, minutesText, new Vector2(bounds.X + MinuteTextX, bounds.Y + TextY), timeColor);
                DrawDigitString(spriteBatch, font, secondsText, new Vector2(bounds.X + SecondTextX, bounds.Y + TextY), timeColor);
            }
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "SpaceGAGA timerboard inactive";
            }

            string timerText = _timeOverTick == int.MinValue ? "stopped" : FormatTimer(RemainingSeconds);
            return $"SpaceGAGA timerboard active on map {_mapId}, timer={timerText}, duration={_durationSec}s";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _durationSec = 0;
            _timeOverTick = int.MinValue;
            _lastResetTick = int.MinValue;
        }

        private float GetResetPulseStrength()
        {
            if (_lastResetTick == int.MinValue)
            {
                return 0f;
            }

            int elapsed = Environment.TickCount - _lastResetTick;
            if (elapsed < 0 || elapsed >= ResetPulseDurationMs)
            {
                return 0f;
            }

            float normalized = 1f - (elapsed / (float)ResetPulseDurationMs);
            return normalized * normalized;
        }

        private static Rectangle GetTimerboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + TimerboardOffsetX;
            return new Rectangle(x, TimerboardY, TimerboardWidth, TimerboardHeight);
        }

        private static void DrawDividerDots(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle bounds, Color dividerColor)
        {
            Rectangle topDot = new(bounds.X + DividerX + 15, bounds.Y + DividerY + 7, 8, 8);
            Rectangle bottomDot = new(bounds.X + DividerX + 15, bounds.Y + DividerY + 21, 8, 8);
            spriteBatch.Draw(pixelTexture, topDot, dividerColor);
            spriteBatch.Draw(pixelTexture, bottomDot, dividerColor);
        }

        private static void DrawDigitString(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }

        private bool TryDrawBitmapTimer(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_digitTextures.Any(texture => texture == null))
            {
                return false;
            }

            int minutes = Math.Clamp(RemainingSeconds / 60, 0, 99);
            int seconds = Math.Clamp(RemainingSeconds % 60, 0, 59);

            DrawTwoDigits(spriteBatch, bounds, MinuteTextX, TextY, minutes);
            DrawTwoDigits(spriteBatch, bounds, SecondTextX, TextY, seconds);

            if (_colonTexture != null)
            {
                spriteBatch.Draw(_colonTexture, new Vector2(bounds.X + DividerX, bounds.Y + DividerY), Color.White);
            }

            return true;
        }

        private void DrawTwoDigits(SpriteBatch spriteBatch, Rectangle bounds, int x, int y, int value)
        {
            int tens = (value / 10) % 10;
            int ones = value % 10;
            Texture2D tensTexture = _digitTextures[tens];
            Texture2D onesTexture = _digitTextures[ones];
            spriteBatch.Draw(tensTexture, new Vector2(bounds.X + x, bounds.Y + y), Color.White);
            spriteBatch.Draw(onesTexture, new Vector2(bounds.X + x + tensTexture.Width, bounds.Y + y), Color.White);
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage objImage = global::HaCreator.Program.FindImage("Map", SpaceMapObjectImageName);
            WzImageProperty spaceRoot = objImage?[SpaceTimerboardRootPath];
            if (spaceRoot != null)
            {
                _backgroundTexture ??= LoadCanvasTexture(objImage?[SpaceTimerboardBackgroundPath] as WzCanvasProperty);
                if (_digitTextures.Any(texture => texture == null))
                {
                    LoadDigitTextures(objImage?[SpaceTimerboardDigitsPath]);
                }
            }

            _assetsLoaded = true;
        }

        private void LoadDigitTextures(WzImageProperty digitContainer)
        {
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] ??= LoadCanvasTexture(ResolveCanvas(digitContainer?[i.ToString()]));
            }

            _colonTexture ??= LoadCanvasTexture(
                ResolveCanvas(digitContainer?["bar"])
                ?? ResolveCanvas(digitContainer?["colon"])
                ?? ResolveCanvas(digitContainer?["comma"]));
        }

        private static WzCanvasProperty ResolveCanvas(WzImageProperty property)
        {
            if (property is WzCanvasProperty canvas)
            {
                return canvas;
            }

            if (property?.WzProperties == null)
            {
                return null;
            }

            if (property["0"] is WzCanvasProperty indexedCanvas)
            {
                return indexedCanvas;
            }

            return property.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault();
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null || canvas == null)
            {
                return null;
            }

            try
            {
                using var bitmap = canvas.GetLinkedWzCanvasBitmap();
                return bitmap?.ToTexture2DAndDispose(_graphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatTimer(int remainingSeconds)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
    #endregion
}
