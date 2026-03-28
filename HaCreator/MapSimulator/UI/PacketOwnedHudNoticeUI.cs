using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class PacketOwnedHudNoticeUI
    {
        private const int ReferenceClientHeight = 578;
        private const int TopMargin = 44;
        private const int DamageMeterPanelWidth = 266;
        private const int DamageMeterMinimumHeight = 78;
        private const int HazardPanelWidth = 266;
        private const int HazardPanelHeight = 72;
        private const int PanelSpacing = 8;
        private const int HorizontalPadding = 18;
        private const int ProgressBarHeight = 9;
        private const int ProgressBarBottomPadding = 16;
        private const float TitleScale = 0.72f;
        private const float MessageScale = 0.66f;
        private const float TimerScale = 0.86f;
        private const int IconSize = 18;

        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private Texture2D _damageMeterTop;
        private Texture2D _damageMeterCenter;
        private Texture2D _damageMeterBottom;
        private Texture2D _fieldHazardTop;
        private Texture2D _fieldHazardCenter;
        private Texture2D _fieldHazardBottom;
        private Texture2D _noticeIcon;
        private int _screenWidth;
        private int _screenHeight;
        private bool _initialized;

        public void Initialize(SpriteFont font, Texture2D pixelTexture, int screenWidth, int screenHeight)
        {
            _font = font;
            _pixelTexture = pixelTexture;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _initialized = true;
        }

        public void SetScreenSize(int screenWidth, int screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
        }

        public void SetDamageMeterFrameTextures(Texture2D top, Texture2D center, Texture2D bottom)
        {
            _damageMeterTop = top;
            _damageMeterCenter = center;
            _damageMeterBottom = bottom;
        }

        public void SetFieldHazardFrameTextures(Texture2D top, Texture2D center, Texture2D bottom)
        {
            _fieldHazardTop = top;
            _fieldHazardCenter = center;
            _fieldHazardBottom = bottom;
        }

        public void SetNoticeIcon(Texture2D noticeIcon)
        {
            _noticeIcon = noticeIcon;
        }

        public void Draw(SpriteBatch spriteBatch, LocalOverlayRuntime runtime, int currentTickCount)
        {
            if (!_initialized || spriteBatch == null || _font == null || runtime == null)
            {
                return;
            }

            int panelY = GetTopMargin();
            if (runtime.HasDamageMeterTimer(currentTickCount))
            {
                Rectangle damageMeterBounds = new(
                    Math.Max(0, (_screenWidth - DamageMeterPanelWidth) / 2),
                    panelY,
                    DamageMeterPanelWidth,
                    DamageMeterMinimumHeight);
                DrawDamageMeter(spriteBatch, runtime, damageMeterBounds, currentTickCount);
                panelY += damageMeterBounds.Height + PanelSpacing;
            }

            if (runtime.HasActiveFieldHazardNotice(currentTickCount))
            {
                Rectangle hazardBounds = new(
                    Math.Max(0, (_screenWidth - HazardPanelWidth) / 2),
                    panelY,
                    HazardPanelWidth,
                    HazardPanelHeight);
                DrawFieldHazardNotice(spriteBatch, runtime, hazardBounds, currentTickCount);
            }
        }

        private void DrawDamageMeter(SpriteBatch spriteBatch, LocalOverlayRuntime runtime, Rectangle bounds, int currentTickCount)
        {
            DrawNoticeFrame(spriteBatch, bounds, _damageMeterTop, _damageMeterCenter, _damageMeterBottom, Color.White);

            const string title = "DAMAGE METER";
            string timerText = $"{runtime.GetRemainingDamageMeterSeconds(currentTickCount)}s";
            string sharedTimingText = $"context {runtime.GetDamageMeterSharedTimingAgeMs(currentTickCount)}ms";

            DrawTextWithShadow(
                spriteBatch,
                title,
                new Vector2(bounds.X + HorizontalPadding, bounds.Y + 15),
                new Color(255, 241, 194),
                Color.Black,
                TitleScale);

            float timerTextWidth = MeasureTextWidth(timerText, TimerScale);
            DrawTextWithShadow(
                spriteBatch,
                timerText,
                new Vector2(bounds.Right - HorizontalPadding - timerTextWidth, bounds.Y + 12),
                new Color(188, 241, 255),
                Color.Black,
                TimerScale);

            DrawTextWithShadow(
                spriteBatch,
                sharedTimingText,
                new Vector2(bounds.X + HorizontalPadding, bounds.Y + 34),
                new Color(177, 217, 255),
                Color.Black,
                MessageScale);

            Rectangle progressBounds = new(
                bounds.X + HorizontalPadding,
                bounds.Bottom - ProgressBarBottomPadding - ProgressBarHeight,
                bounds.Width - (HorizontalPadding * 2),
                ProgressBarHeight);
            DrawProgressBar(spriteBatch, progressBounds, runtime.GetDamageMeterProgress(currentTickCount));
        }

        private void DrawFieldHazardNotice(SpriteBatch spriteBatch, LocalOverlayRuntime runtime, Rectangle bounds, int currentTickCount)
        {
            float alpha = runtime.GetFieldHazardNoticeAlpha(currentTickCount);
            if (alpha <= 0f)
            {
                return;
            }

            DrawNoticeFrame(
                spriteBatch,
                bounds,
                _fieldHazardTop,
                _fieldHazardCenter,
                _fieldHazardBottom,
                Color.White * alpha);

            int textX = bounds.X + HorizontalPadding;
            if (_noticeIcon != null)
            {
                Rectangle iconBounds = new(bounds.X + HorizontalPadding, bounds.Y + 16, IconSize, IconSize);
                spriteBatch.Draw(_noticeIcon, iconBounds, Color.White * alpha);
                textX = iconBounds.Right + 8;
            }

            DrawTextWithShadow(
                spriteBatch,
                "FIELD HAZARD",
                new Vector2(textX, bounds.Y + 14),
                new Color(255, 215, 145) * alpha,
                Color.Black * alpha,
                TitleScale);

            string damageText = runtime.LastFieldHazardDamage > 0
                ? $"HP -{runtime.LastFieldHazardDamage}"
                : "HP loss";
            DrawTextWithShadow(
                spriteBatch,
                damageText,
                new Vector2(textX, bounds.Y + 34),
                new Color(255, 169, 133) * alpha,
                Color.Black * alpha,
                MessageScale);

            string message = TrimText(runtime.LastFieldHazardMessage, MessageScale, bounds.Width - (textX - bounds.X) - HorizontalPadding);
            DrawTextWithShadow(
                spriteBatch,
                message,
                new Vector2(textX, bounds.Y + 49),
                Color.White * alpha,
                Color.Black * alpha,
                MessageScale);
        }

        private void DrawNoticeFrame(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            Texture2D top,
            Texture2D center,
            Texture2D bottom,
            Color color)
        {
            if (top != null && center != null && bottom != null)
            {
                int topHeight = top.Height;
                int bottomHeight = bottom.Height;
                int centerHeight = Math.Max(0, bounds.Height - topHeight - bottomHeight);

                spriteBatch.Draw(top, new Rectangle(bounds.X, bounds.Y, bounds.Width, topHeight), color);
                if (centerHeight > 0)
                {
                    spriteBatch.Draw(center, new Rectangle(bounds.X, bounds.Y + topHeight, bounds.Width, centerHeight), color);
                }

                spriteBatch.Draw(bottom, new Rectangle(bounds.X, bounds.Bottom - bottomHeight, bounds.Width, bottomHeight), color);
                return;
            }

            if (_pixelTexture == null)
            {
                return;
            }

            float alpha = color.A / 255f;
            spriteBatch.Draw(_pixelTexture, bounds, new Color(42, 64, 92, 225) * alpha);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(255, 238, 193) * alpha);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(142, 181, 220) * alpha);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), new Color(196, 218, 240) * alpha);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), new Color(196, 218, 240) * alpha);
        }

        private void DrawProgressBar(SpriteBatch spriteBatch, Rectangle bounds, float progress)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, new Color(26, 38, 54, 230));
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(179, 211, 241));
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(11, 18, 27));

            int fillWidth = Math.Clamp((int)Math.Round(bounds.Width * progress), 0, bounds.Width);
            Rectangle fillBounds = new(bounds.X + 1, bounds.Y + 1, Math.Max(0, fillWidth - 2), Math.Max(0, bounds.Height - 2));
            if (fillBounds.Width > 0 && fillBounds.Height > 0)
            {
                spriteBatch.Draw(_pixelTexture, fillBounds, new Color(255, 188, 80));
            }
        }

        private int GetTopMargin()
        {
            if (_screenHeight <= 0)
            {
                return TopMargin;
            }

            return Math.Max(24, (int)Math.Round(_screenHeight * (TopMargin / (float)ReferenceClientHeight)));
        }

        private float MeasureTextWidth(string text, float scale)
        {
            return _font == null || string.IsNullOrWhiteSpace(text)
                ? 0f
                : _font.MeasureString(text).X * scale;
        }

        private string TrimText(string value, float scale, int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(value) || _font == null || maxWidth <= 0)
            {
                return string.Empty;
            }

            if (MeasureTextWidth(value, scale) <= maxWidth)
            {
                return value;
            }

            const string ellipsis = "...";
            string trimmed = value.Trim();
            while (trimmed.Length > 0)
            {
                trimmed = trimmed[..^1];
                string candidate = trimmed + ellipsis;
                if (MeasureTextWidth(candidate, scale) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private void DrawTextWithShadow(
            SpriteBatch spriteBatch,
            string text,
            Vector2 position,
            Color textColor,
            Color shadowColor,
            float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                return;
            }

            Vector2 shadowOffset = new(1f, 1f);
            spriteBatch.DrawString(_font, text, position + shadowOffset, shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
