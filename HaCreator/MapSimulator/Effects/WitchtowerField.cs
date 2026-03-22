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
    #region Witchtower Field (CField_Witchtower)
    /// <summary>
    /// Witchtower Field - Score tracking for witchtower event.
    ///
    /// Client evidence:
    /// - CField_Witchtower::OnScoreUpdate (0x564ad0): lazy-creates a scoreboard window, stores a Decode1 score, invalidates it
    /// - CScoreboard_Witchtower::OnCreate (0x564e50): loads dedicated background, key, and score-font assets
    /// - CScoreboard_Witchtower::Draw (0x564bd0): draws a 115x36 center-top widget, overlays the key art at (7, 0),
    ///   then renders a zero-padded score at (67, 4)
    /// </summary>
    public class WitchtowerField
    {
        #region State
        private bool _isActive = false;
        private int _score = 0;
        private GraphicsDevice _graphicsDevice;
        private Texture2D _backgroundTexture;
        private Texture2D _keyTexture;
        private readonly Texture2D[] _digitTextures = new Texture2D[10];
        private bool _assetsLoaded;
        #endregion

        #region Scoreboard Position (from client: CWnd::CreateWnd position)
        private const int SCOREBOARD_OFFSET_X = -57;
        private const int SCOREBOARD_Y = 92;
        private const int SCOREBOARD_WIDTH = 115;
        private const int SCOREBOARD_HEIGHT = 36;
        #endregion

        #region Focus Pulse
        private const int SCORE_PULSE_DURATION_MS = 650;
        private int _lastScoreUpdateTime = int.MinValue;
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int Score => _score;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
            _graphicsDevice = device;
            EnsureAssetsLoaded();
        }

        public void Enable()
        {
            _isActive = true;
            _score = 0;
            _lastScoreUpdateTime = int.MinValue;
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnScoreUpdate - Packet 358
        /// From client: this->m_pScoreboard.p->m_nScore = Decode1(iPacket);
        /// </summary>
        public void OnScoreUpdate(int newScore, int currentTimeMs)
        {
            int clampedScore = Math.Clamp(newScore, 0, byte.MaxValue);
            System.Diagnostics.Debug.WriteLine($"[WitchtowerField] OnScoreUpdate: {_score} -> {clampedScore}");
            _score = clampedScore;
            _lastScoreUpdateTime = currentTimeMs;
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            EnsureAssetsLoaded();

            Rectangle widgetBounds = GetScoreboardBounds(spriteBatch.GraphicsDevice.Viewport);
            float pulseStrength = GetPulseStrength();
            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Vector2(widgetBounds.X, widgetBounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, widgetBounds, new Color(88, 71, 44));
            }

            if (_keyTexture != null)
            {
                spriteBatch.Draw(_keyTexture, new Vector2(widgetBounds.X + 7, widgetBounds.Y), Color.White);
            }
            else
            {
                DrawKeyGlyph(spriteBatch, pixelTexture, new Rectangle(widgetBounds.X + 7, widgetBounds.Y, 22, 22), new Color(197, 168, 93));
            }

            if (pulseStrength > 0f)
            {
                spriteBatch.Draw(pixelTexture, widgetBounds, new Color(255, 234, 154) * (pulseStrength * 0.22f));
            }

            if (TryDrawBitmapScore(spriteBatch, widgetBounds))
            {
                return;
            }

            if (font != null)
            {
                string scoreText = _score.ToString("00");
                Vector2 textPos = new Vector2(widgetBounds.X + 67, widgetBounds.Y + 4);
                spriteBatch.DrawString(font, scoreText, textPos + new Vector2(1, 1), Color.Black);
                spriteBatch.DrawString(font, scoreText, textPos, Color.White);
            }
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _score = 0;
            _lastScoreUpdateTime = int.MinValue;
        }
        #endregion

        #region Debug Helpers
        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Witchtower scoreboard inactive";
            }

            return $"Witchtower scoreboard active, score={_score:00}";
        }
        #endregion

        #region Private Helpers
        private static Rectangle GetScoreboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + SCOREBOARD_OFFSET_X;
            return new Rectangle(x, SCOREBOARD_Y, SCOREBOARD_WIDTH, SCOREBOARD_HEIGHT);
        }

        private float GetPulseStrength()
        {
            if (_lastScoreUpdateTime == int.MinValue)
            {
                return 0f;
            }

            int elapsed = Environment.TickCount - _lastScoreUpdateTime;
            if (elapsed < 0 || elapsed >= SCORE_PULSE_DURATION_MS)
            {
                return 0f;
            }

            float normalized = 1f - (elapsed / (float)SCORE_PULSE_DURATION_MS);
            return normalized * normalized;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage objImage = global::HaCreator.Program.FindImage("Map", "Obj/etc.img");
            WzImageProperty goldKey = objImage?["goldkey"];
            _backgroundTexture = LoadCanvasTexture(goldKey?["backgrnd"] as WzCanvasProperty);
            _keyTexture = LoadCanvasTexture(goldKey?["key"] as WzCanvasProperty);

            WzImageProperty digits = goldKey?["number"];
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(digits?[i.ToString()] as WzCanvasProperty);
            }

            _assetsLoaded = true;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null || canvas == null)
            {
                return null;
            }

            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_graphicsDevice);
        }

        private bool TryDrawBitmapScore(SpriteBatch spriteBatch, Rectangle widgetBounds)
        {
            string scoreText = _score.ToString("00");
            int drawX = widgetBounds.X + 67;
            int drawY = widgetBounds.Y + 4;

            foreach (char digitChar in scoreText)
            {
                int digit = digitChar - '0';
                if (digit < 0 || digit >= _digitTextures.Length)
                {
                    return false;
                }

                Texture2D digitTexture = _digitTextures[digit];
                if (digitTexture == null)
                {
                    return false;
                }

                spriteBatch.Draw(digitTexture, new Vector2(drawX, drawY), Color.White);
                drawX += digitTexture.Width - 2;
            }

            return true;
        }

        private static void DrawKeyGlyph(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle keyBounds, Color keyColor)
        {
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 4, keyBounds.Y + 3, 10, 10), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 6, keyBounds.Y + 5, 6, 6), new Color(72, 52, 24, 255));
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 12, keyBounds.Y + 7, 8, 4), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 17, keyBounds.Y + 7, 2, 7), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 14, keyBounds.Y + 11, 2, 5), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 18, keyBounds.Y + 11, 2, 3), new Color(116, 88, 42, 255));
        }
        #endregion
    }
    #endregion
}
