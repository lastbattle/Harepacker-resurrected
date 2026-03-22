using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public enum SkillCooldownNoticeType
    {
        Started,
        Blocked,
        Ready
    }

    internal sealed class SkillCooldownNoticeUI
    {
        private const int MaxNotices = 3;
        private const int DisplayDurationMs = 2200;
        private const int FadeDurationMs = 260;
        private const int SlideSpeed = 480;
        private const int TopMargin = 42;
        private const int NoticeSpacing = 6;
        private const int PanelWidth = 266;
        private const int PanelHeight = 92;
        private const int IconSize = 32;
        private const int IconX = 18;
        private const int IconY = 26;
        private const int TitleX = 60;
        private const int TitleY = 22;
        private const int MessageY = 43;
        private const int TextRightPadding = 14;
        private const float TitleScale = 0.78f;
        private const float MessageScale = 0.62f;

        private sealed class NoticeEntry
        {
            public int SkillId;
            public SkillCooldownNoticeType Type;
            public string Title;
            public string Message;
            public Texture2D IconTexture;
            public int SpawnTime;
            public float Alpha = 1f;
            public float YOffset;
            public float TargetYOffset;
            public bool IsExpired;
        }

        private readonly List<NoticeEntry> _notices = new List<NoticeEntry>();
        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private Texture2D _frameTop;
        private Texture2D _frameCenter;
        private Texture2D _frameBottom;
        private int _screenWidth;
        private bool _initialized;

        public void Initialize(SpriteFont font, Texture2D pixelTexture, int screenWidth)
        {
            _font = font;
            _pixelTexture = pixelTexture;
            _screenWidth = screenWidth;
            _initialized = true;
        }

        public void SetScreenWidth(int screenWidth)
        {
            _screenWidth = screenWidth;
        }

        public void SetFrameTextures(Texture2D frameTop, Texture2D frameCenter, Texture2D frameBottom)
        {
            _frameTop = frameTop;
            _frameCenter = frameCenter;
            _frameBottom = frameBottom;
        }

        public void AddNotice(int skillId, string title, string message, Texture2D iconTexture, SkillCooldownNoticeType type, int currentTime)
        {
            if (!_initialized || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            NoticeEntry existingEntry = null;
            for (int i = 0; i < _notices.Count; i++)
            {
                NoticeEntry entry = _notices[i];
                if (entry.SkillId == skillId && entry.Type == type)
                {
                    existingEntry = entry;
                    break;
                }
            }

            if (existingEntry == null)
            {
                while (_notices.Count >= MaxNotices)
                {
                    _notices.RemoveAt(0);
                }

                existingEntry = new NoticeEntry();
                _notices.Add(existingEntry);
            }

            existingEntry.SkillId = skillId;
            existingEntry.Type = type;
            existingEntry.Title = string.IsNullOrWhiteSpace(title) ? "Skill Cooldown" : title;
            existingEntry.Message = message;
            existingEntry.IconTexture = iconTexture;
            existingEntry.SpawnTime = currentTime;
            existingEntry.Alpha = 1f;
            existingEntry.YOffset = 0f;
            existingEntry.TargetYOffset = 0f;
            existingEntry.IsExpired = false;

            ReflowTargets();
        }

        public void Update(int currentTime, float deltaSeconds)
        {
            if (!_initialized || _notices.Count == 0)
            {
                return;
            }

            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                NoticeEntry notice = _notices[i];
                int elapsed = currentTime - notice.SpawnTime;

                float offsetDelta = notice.TargetYOffset - notice.YOffset;
                if (Math.Abs(offsetDelta) > 0.1f)
                {
                    float step = SlideSpeed * deltaSeconds;
                    if (Math.Abs(offsetDelta) <= step)
                    {
                        notice.YOffset = notice.TargetYOffset;
                    }
                    else
                    {
                        notice.YOffset += Math.Sign(offsetDelta) * step;
                    }
                }

                if (elapsed <= DisplayDurationMs)
                {
                    notice.Alpha = 1f;
                    continue;
                }

                int fadeElapsed = elapsed - DisplayDurationMs;
                if (fadeElapsed >= FadeDurationMs)
                {
                    notice.IsExpired = true;
                    _notices.RemoveAt(i);
                    ReflowTargets();
                    continue;
                }

                notice.Alpha = 1f - (fadeElapsed / (float)FadeDurationMs);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_initialized || _font == null || _notices.Count == 0)
            {
                return;
            }

            int baseX = Math.Max(0, (_screenWidth - PanelWidth) / 2);

            for (int i = 0; i < _notices.Count; i++)
            {
                NoticeEntry notice = _notices[i];
                if (notice.Alpha <= 0f)
                {
                    continue;
                }

                int noticeY = (int)Math.Round(TopMargin + notice.YOffset);
                Rectangle panelRect = new Rectangle(baseX, noticeY, PanelWidth, PanelHeight);
                DrawNoticeFrame(spriteBatch, panelRect, notice.Alpha);

                Rectangle iconRect = new Rectangle(panelRect.X + IconX, panelRect.Y + IconY, IconSize, IconSize);
                if (notice.IconTexture != null)
                {
                    spriteBatch.Draw(notice.IconTexture, iconRect, Color.White * notice.Alpha);
                }
                else if (_pixelTexture != null)
                {
                    spriteBatch.Draw(_pixelTexture, iconRect, new Color(48, 76, 112) * notice.Alpha);
                }

                Color accentColor = GetAccentColor(notice.Type) * notice.Alpha;
                if (_pixelTexture != null)
                {
                    Rectangle accentRect = new Rectangle(iconRect.Right + 8, panelRect.Y + 48, PanelWidth - iconRect.Right - 22, 2);
                    spriteBatch.Draw(_pixelTexture, accentRect, accentColor);
                }

                int textWidth = PanelWidth - TitleX - TextRightPadding;
                string title = TrimText(notice.Title, TitleScale, textWidth);
                string message = TrimText(notice.Message, MessageScale, textWidth);

                DrawTextWithShadow(spriteBatch, title, new Vector2(panelRect.X + TitleX, panelRect.Y + TitleY), Color.White * notice.Alpha, Color.Black * notice.Alpha, TitleScale);
                DrawTextWithShadow(spriteBatch, message, new Vector2(panelRect.X + TitleX, panelRect.Y + MessageY), accentColor, Color.Black * notice.Alpha, MessageScale);
            }
        }

        public void Clear()
        {
            _notices.Clear();
        }

        private void DrawNoticeFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha)
        {
            Color color = Color.White * alpha;
            if (_frameTop != null && _frameCenter != null && _frameBottom != null)
            {
                spriteBatch.Draw(_frameTop, new Rectangle(panelRect.X, panelRect.Y, PanelWidth, _frameTop.Height), color);
                int centerHeight = Math.Max(0, PanelHeight - _frameTop.Height - _frameBottom.Height);
                if (centerHeight > 0)
                {
                    spriteBatch.Draw(_frameCenter, new Rectangle(panelRect.X, panelRect.Y + _frameTop.Height, PanelWidth, centerHeight), color);
                }

                spriteBatch.Draw(_frameBottom, new Rectangle(panelRect.X, panelRect.Bottom - _frameBottom.Height, PanelWidth, _frameBottom.Height), color);
                return;
            }

            if (_pixelTexture == null)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, panelRect, new Color(50, 96, 146, 220) * alpha);
            spriteBatch.Draw(_pixelTexture, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), Color.White * alpha);
            spriteBatch.Draw(_pixelTexture, new Rectangle(panelRect.X, panelRect.Bottom - 2, panelRect.Width, 2), new Color(170, 198, 227) * alpha);
        }

        private void ReflowTargets()
        {
            for (int i = 0; i < _notices.Count; i++)
            {
                _notices[i].TargetYOffset = i * (PanelHeight + NoticeSpacing);
            }
        }

        private Color GetAccentColor(SkillCooldownNoticeType type)
        {
            return type switch
            {
                SkillCooldownNoticeType.Ready => new Color(184, 255, 178),
                SkillCooldownNoticeType.Blocked => new Color(255, 223, 153),
                _ => new Color(196, 228, 255)
            };
        }

        private string TrimText(string value, float scale, int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(value) || _font == null)
            {
                return string.Empty;
            }

            if ((_font.MeasureString(value).X * scale) <= maxWidth)
            {
                return value;
            }

            const string ellipsis = "...";
            string trimmed = value;
            while (trimmed.Length > 0)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
                string candidate = trimmed + ellipsis;
                if ((_font.MeasureString(candidate).X * scale) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private void DrawTextWithShadow(SpriteBatch spriteBatch, string text, Vector2 position, Color textColor, Color shadowColor, float scale)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
            {
                return;
            }

            Vector2 shadowOffset = new Vector2(1f, 1f);
            spriteBatch.DrawString(_font, text, position + shadowOffset, shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
