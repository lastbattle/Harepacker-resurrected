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
        private const int DefaultDamageMeterPanelWidth = 266;
        private const int DefaultDamageMeterMinimumHeight = 96;
        private const int DefaultHazardPanelWidth = 258;
        private const int DefaultHazardPanelHeight = 107;
        private const int PanelSpacing = 8;
        private const int HorizontalPadding = 18;
        private const int ProgressBarHeight = 9;
        private const int ProgressBarBottomPadding = 16;
        private const float TitleScale = 0.72f;
        private const float MessageScale = 0.66f;
        private const float FollowUpScale = 0.62f;
        private const float TransportScale = 0.56f;
        private const float TimerScale = 0.86f;
        private const int FallbackIconWidth = 18;
        private const int FallbackIconHeight = 18;
        private const int FollowUpBadgeHorizontalPadding = 7;
        private const int FollowUpBadgeVerticalPadding = 3;

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
                int damageMeterWidth = GetFrameWidth(_damageMeterTop, _damageMeterCenter, _damageMeterBottom, DefaultDamageMeterPanelWidth);
                int damageMeterHeight = GetMinimumFrameHeight(_damageMeterTop, _damageMeterCenter, _damageMeterBottom, DefaultDamageMeterMinimumHeight);
                Rectangle damageMeterBounds = new(
                    Math.Max(0, (_screenWidth - damageMeterWidth) / 2),
                    panelY,
                    damageMeterWidth,
                    damageMeterHeight);
                DrawDamageMeter(spriteBatch, runtime, damageMeterBounds, currentTickCount);
                panelY += damageMeterBounds.Height + PanelSpacing;
            }

            if (runtime.HasActiveFieldHazardNotice(currentTickCount))
            {
                int hazardWidth = GetFrameWidth(_fieldHazardTop, _fieldHazardCenter, _fieldHazardBottom, DefaultHazardPanelWidth);
                int hazardHeight = GetMinimumFrameHeight(_fieldHazardTop, _fieldHazardCenter, _fieldHazardBottom, DefaultHazardPanelHeight);
                Rectangle hazardBounds = new(
                    Math.Max(0, (_screenWidth - hazardWidth) / 2),
                    panelY,
                    hazardWidth,
                    hazardHeight);
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
                int iconWidth = Math.Max(1, _noticeIcon.Width);
                int iconHeight = Math.Max(1, _noticeIcon.Height);
                Rectangle iconBounds = new(
                    bounds.X + HorizontalPadding,
                    bounds.Y + 15,
                    iconWidth > 0 ? iconWidth : FallbackIconWidth,
                    iconHeight > 0 ? iconHeight : FallbackIconHeight);
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

            string stateText = GetFollowUpStateText(runtime.LastFieldHazardFollowUpKind);
            if (!string.IsNullOrWhiteSpace(stateText))
            {
                DrawFollowUpBadge(
                    spriteBatch,
                    stateText,
                    runtime.LastFieldHazardFollowUpKind,
                    new Vector2(bounds.Right - HorizontalPadding, bounds.Y + 15),
                    alpha);
            }

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

            if (!string.IsNullOrWhiteSpace(runtime.LastFieldHazardFollowUpDetail))
            {
                string followUp = TrimText(runtime.LastFieldHazardFollowUpDetail, FollowUpScale, bounds.Width - (textX - bounds.X) - HorizontalPadding);
                DrawTextWithShadow(
                    spriteBatch,
                    followUp,
                    new Vector2(textX, bounds.Y + 66),
                    GetFollowUpColor(runtime.LastFieldHazardFollowUpKind) * alpha,
                    Color.Black * alpha,
                    FollowUpScale);
            }

            if (!string.IsNullOrWhiteSpace(runtime.LastFieldHazardTransportDetail))
            {
                string transport = TrimText(runtime.LastFieldHazardTransportDetail, TransportScale, bounds.Width - (textX - bounds.X) - HorizontalPadding);
                DrawTextWithShadow(
                    spriteBatch,
                    transport,
                    new Vector2(textX, bounds.Y + 80),
                    new Color(188, 221, 255) * alpha,
                    Color.Black * alpha,
                    TransportScale);
            }
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
            return string.IsNullOrWhiteSpace(text)
                ? 0f
                : ClientTextDrawing.Measure((GraphicsDevice)null, text, scale, _font).X;
        }

        private static int GetFrameWidth(Texture2D top, Texture2D center, Texture2D bottom, int fallbackWidth)
        {
            return Math.Max(
                fallbackWidth,
                Math.Max(top?.Width ?? 0, Math.Max(center?.Width ?? 0, bottom?.Width ?? 0)));
        }

        private static int GetMinimumFrameHeight(Texture2D top, Texture2D center, Texture2D bottom, int fallbackHeight)
        {
            int frameHeight = (top?.Height ?? 0) + (center?.Height ?? 0) + (bottom?.Height ?? 0);
            return Math.Max(fallbackHeight, frameHeight);
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

        internal static string GetFollowUpStateText(FieldHazardFollowUpKind kind)
        {
            return kind switch
            {
                FieldHazardFollowUpKind.Pending => "REQUEST",
                FieldHazardFollowUpKind.Acknowledged => "ACK",
                FieldHazardFollowUpKind.Consumed => "USED",
                FieldHazardFollowUpKind.Failure => "FAILED",
                FieldHazardFollowUpKind.Throttled => "WAIT",
                FieldHazardFollowUpKind.Deferred => "QUEUE",
                FieldHazardFollowUpKind.Dispatched => "SENT",
                _ => string.Empty
            };
        }

        internal static Color GetFollowUpColor(FieldHazardFollowUpKind kind)
        {
            return kind switch
            {
                FieldHazardFollowUpKind.Pending => new Color(176, 224, 255),
                FieldHazardFollowUpKind.Acknowledged => new Color(189, 231, 255),
                FieldHazardFollowUpKind.Consumed => new Color(170, 255, 170),
                FieldHazardFollowUpKind.Failure => new Color(255, 181, 145),
                FieldHazardFollowUpKind.Throttled => new Color(255, 219, 145),
                FieldHazardFollowUpKind.Deferred => new Color(180, 203, 255),
                FieldHazardFollowUpKind.Dispatched => new Color(154, 244, 226),
                _ => Color.White
            };
        }

        private void DrawFollowUpBadge(
            SpriteBatch spriteBatch,
            string text,
            FieldHazardFollowUpKind kind,
            Vector2 topRight,
            float alpha)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null || _pixelTexture == null)
            {
                return;
            }

            Vector2 textSize = ClientTextDrawing.Measure((GraphicsDevice)null, text, FollowUpScale, _font);
            int badgeWidth = (int)Math.Ceiling(textSize.X) + (FollowUpBadgeHorizontalPadding * 2);
            int badgeHeight = (int)Math.Ceiling(textSize.Y) + (FollowUpBadgeVerticalPadding * 2);
            Rectangle badgeBounds = new(
                (int)Math.Round(topRight.X) - badgeWidth,
                (int)Math.Round(topRight.Y),
                badgeWidth,
                badgeHeight);

            Color accent = GetFollowUpColor(kind) * alpha;
            Color fill = new Color(
                Math.Clamp((int)(accent.R * 0.32f), 0, 255),
                Math.Clamp((int)(accent.G * 0.32f), 0, 255),
                Math.Clamp((int)(accent.B * 0.32f), 0, 255),
                Math.Clamp((int)(220 * alpha), 0, 255));
            Color outline = new Color(accent.R, accent.G, accent.B, Math.Clamp((int)(255 * alpha), 0, 255));

            spriteBatch.Draw(_pixelTexture, badgeBounds, fill);
            spriteBatch.Draw(_pixelTexture, new Rectangle(badgeBounds.X, badgeBounds.Y, badgeBounds.Width, 1), outline);
            spriteBatch.Draw(_pixelTexture, new Rectangle(badgeBounds.X, badgeBounds.Bottom - 1, badgeBounds.Width, 1), outline);
            spriteBatch.Draw(_pixelTexture, new Rectangle(badgeBounds.X, badgeBounds.Y, 1, badgeBounds.Height), outline);
            spriteBatch.Draw(_pixelTexture, new Rectangle(badgeBounds.Right - 1, badgeBounds.Y, 1, badgeBounds.Height), outline);

            DrawTextWithShadow(
                spriteBatch,
                text,
                new Vector2(
                    badgeBounds.X + FollowUpBadgeHorizontalPadding,
                    badgeBounds.Y + FollowUpBadgeVerticalPadding - 1),
                Color.White * alpha,
                Color.Black * alpha,
                FollowUpScale);
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
            ClientTextDrawing.Draw(spriteBatch, text, position + shadowOffset, shadowColor, scale, _font);
            ClientTextDrawing.Draw(spriteBatch, text, position, textColor, scale, _font);
        }
    }
}
