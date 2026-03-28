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
        private const int ReferenceClientHeight = 578;
        private const int MaxNotices = 3;
        private const int DisplayDurationMs = 2200;
        private const int FadeDurationMs = 260;
        private const int SlideSpeed = 480;
        private const float SpawnSlideOffset = 18f;
        private const int TopMargin = 42;
        private const int NoticeSpacing = 6;
        private const int IconSize = 32;
        private const int DefaultIconX = 18;
        private const int DefaultIconY = 26;
        private const int DefaultTitleX = 60;
        private const int DefaultTitleY = 22;
        private const int DefaultMessageY = 43;
        private const int DefaultTextRightPadding = 14;
        private const int DefaultTextBottomPadding = 14;
        private const float TitleScale = 0.78f;
        private const float MessageScale = 0.62f;
        private const int LayoutSampleAlphaThreshold = 16;
        private const int LayoutRowDominanceThreshold = 6;
        private const int LayoutIconInsetX = 10;
        private const int LayoutIconGapX = 10;
        private const int LayoutBodyPaddingTop = 5;
        private const int LayoutBodyPaddingBottom = 8;
        private const int LayoutBodyToTitleGap = 10;

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
            public int Height;
            public string[] WrappedMessageLines = Array.Empty<string>();
        }

        private readonly List<NoticeEntry> _notices = new List<NoticeEntry>();
        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private Texture2D _frameTop;
        private Texture2D _frameCenter;
        private Texture2D _frameBottom;
        private int _screenWidth;
        private int _screenHeight;
        private bool _initialized;
        private int _panelWidth = 266;
        private int _topHeight = 21;
        private int _centerHeight = 20;
        private int _bottomHeight = 55;
        private int _iconX = DefaultIconX;
        private int _iconY = DefaultIconY;
        private int _titleX = DefaultTitleX;
        private int _titleY = DefaultTitleY;
        private int _messageY = DefaultMessageY;
        private int _textRightPadding = DefaultTextRightPadding;
        private int _textBottomPadding = DefaultTextBottomPadding;

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

        public void SetFrameTextures(Texture2D frameTop, Texture2D frameCenter, Texture2D frameBottom)
        {
            _frameTop = frameTop;
            _frameCenter = frameCenter;
            _frameBottom = frameBottom;
            _panelWidth = Math.Max(frameTop?.Width ?? 0, Math.Max(frameCenter?.Width ?? 0, frameBottom?.Width ?? 0));
            if (_panelWidth <= 0)
            {
                _panelWidth = 266;
            }

            _topHeight = Math.Max(0, frameTop?.Height ?? 21);
            _centerHeight = Math.Max(1, frameCenter?.Height ?? 20);
            _bottomHeight = Math.Max(0, frameBottom?.Height ?? 55);
            UpdateFrameLayoutMetrics();
            RecalculateNoticeLayouts();
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
                if (entry.SkillId == skillId)
                {
                    existingEntry = entry;
                    break;
                }
            }

            if (existingEntry == null)
            {
                while (_notices.Count >= MaxNotices)
                {
                    _notices.RemoveAt(_notices.Count - 1);
                }

                existingEntry = new NoticeEntry();
                _notices.Insert(0, existingEntry);
                existingEntry.YOffset = -SpawnSlideOffset;
            }
            else
            {
                int existingIndex = _notices.IndexOf(existingEntry);
                if (existingIndex > 0)
                {
                    _notices.RemoveAt(existingIndex);
                    _notices.Insert(0, existingEntry);
                }

                existingEntry.YOffset = Math.Min(existingEntry.YOffset, -SpawnSlideOffset);
            }

            existingEntry.SkillId = skillId;
            existingEntry.Type = type;
            existingEntry.Title = string.IsNullOrWhiteSpace(title) ? "Skill Cooldown" : title;
            existingEntry.Message = message;
            existingEntry.IconTexture = iconTexture;
            existingEntry.SpawnTime = currentTime;
            existingEntry.Alpha = 1f;
            existingEntry.TargetYOffset = 0f;
            existingEntry.IsExpired = false;
            ApplyLayout(existingEntry);

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

            int baseX = Math.Max(0, (_screenWidth - _panelWidth) / 2);

            for (int i = 0; i < _notices.Count; i++)
            {
                NoticeEntry notice = _notices[i];
                if (notice.Alpha <= 0f)
                {
                    continue;
                }

                int noticeY = (int)Math.Round(GetTopMargin() + notice.YOffset);
                int noticeHeight = Math.Max(GetMinimumPanelHeight(), notice.Height);
                Rectangle panelRect = new Rectangle(baseX, noticeY, _panelWidth, noticeHeight);
                DrawNoticeFrame(spriteBatch, panelRect, notice.Alpha);

                Rectangle iconRect = new Rectangle(panelRect.X + _iconX, panelRect.Y + _iconY, IconSize, IconSize);
                if (notice.IconTexture != null)
                {
                    spriteBatch.Draw(notice.IconTexture, iconRect, Color.White * notice.Alpha);
                }
                else if (_pixelTexture != null)
                {
                    spriteBatch.Draw(_pixelTexture, iconRect, new Color(48, 76, 112) * notice.Alpha);
                }

                Color accentColor = GetAccentColor(notice.Type) * notice.Alpha;
                int textWidth = _panelWidth - _titleX - _textRightPadding;
                string title = TrimText(notice.Title, TitleScale, textWidth);

                DrawTextWithShadow(spriteBatch, title, new Vector2(panelRect.X + _titleX, panelRect.Y + _titleY), Color.White * notice.Alpha, Color.Black * notice.Alpha, TitleScale);

                for (int lineIndex = 0; lineIndex < notice.WrappedMessageLines.Length; lineIndex++)
                {
                    Vector2 linePosition = new Vector2(
                        panelRect.X + _titleX,
                        panelRect.Y + _messageY + (lineIndex * _font.LineSpacing * MessageScale));
                    DrawTextWithShadow(
                        spriteBatch,
                        notice.WrappedMessageLines[lineIndex],
                        linePosition,
                        accentColor,
                        Color.Black * notice.Alpha,
                        MessageScale);
                }
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
                spriteBatch.Draw(_frameTop, new Rectangle(panelRect.X, panelRect.Y, _panelWidth, _topHeight), color);
                int centerHeight = Math.Max(0, panelRect.Height - _topHeight - _bottomHeight);
                if (centerHeight > 0)
                {
                    spriteBatch.Draw(_frameCenter, new Rectangle(panelRect.X, panelRect.Y + _topHeight, _panelWidth, centerHeight), color);
                }

                spriteBatch.Draw(_frameBottom, new Rectangle(panelRect.X, panelRect.Bottom - _bottomHeight, _panelWidth, _bottomHeight), color);
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
            float accumulatedOffset = 0f;
            for (int i = 0; i < _notices.Count; i++)
            {
                NoticeEntry notice = _notices[i];
                notice.TargetYOffset = accumulatedOffset;
                accumulatedOffset += Math.Max(GetMinimumPanelHeight(), notice.Height) + NoticeSpacing;
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

        private void RecalculateNoticeLayouts()
        {
            for (int i = 0; i < _notices.Count; i++)
            {
                ApplyLayout(_notices[i]);
            }

            ReflowTargets();
        }

        private void ApplyLayout(NoticeEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            int availableWidth = Math.Max(32, _panelWidth - _titleX - _textRightPadding);
            entry.WrappedMessageLines = WrapText(entry.Message, MessageScale, availableWidth);
            int minimumHeight = GetMinimumPanelHeight();
            if (_font == null || entry.WrappedMessageLines.Length == 0)
            {
                entry.Height = minimumHeight;
                return;
            }

            float messageHeight = entry.WrappedMessageLines.Length * _font.LineSpacing * MessageScale;
            int contentBottom = (int)Math.Ceiling(_messageY + messageHeight + _textBottomPadding);
            entry.Height = Math.Max(minimumHeight, contentBottom);
        }

        private int GetMinimumPanelHeight()
        {
            return _topHeight + _centerHeight + _bottomHeight;
        }

        private int GetTopMargin()
        {
            if (_screenHeight <= 0)
            {
                return TopMargin;
            }

            return Math.Max(24, (int)Math.Round(_screenHeight * (TopMargin / (float)ReferenceClientHeight)));
        }

        private void UpdateFrameLayoutMetrics()
        {
            _iconX = DefaultIconX;
            _iconY = DefaultIconY;
            _titleX = DefaultTitleX;
            _titleY = DefaultTitleY;
            _messageY = DefaultMessageY;
            _textRightPadding = DefaultTextRightPadding;
            _textBottomPadding = DefaultTextBottomPadding;

            if (_frameCenter == null || _frameBottom == null)
            {
                return;
            }

            int innerLeft = FindInnerEdge(_frameCenter, fromLeft: true);
            int innerRight = FindInnerEdge(_frameCenter, fromLeft: false);
            if (innerLeft >= 0 && innerRight > innerLeft)
            {
                _iconX = Math.Max(innerLeft + LayoutIconInsetX, 12);
                _titleX = _iconX + IconSize + LayoutIconGapX;
                _textRightPadding = Math.Max(8, _panelWidth - innerRight + LayoutIconInsetX);
            }

            FindDominantNeutralBand(_frameBottom, out int bandStart, out int bandEnd);
            if (bandStart < 0 || bandEnd < bandStart)
            {
                return;
            }

            int bodyTop = _topHeight + _centerHeight + bandStart;
            int bodyBottom = _topHeight + _centerHeight + bandEnd;
            int bodyHeight = Math.Max(0, bodyBottom - bodyTop + 1);

            _messageY = bodyTop + LayoutBodyPaddingTop;
            _textBottomPadding = Math.Max(LayoutBodyPaddingBottom, GetMinimumPanelHeight() - bodyBottom + LayoutBodyPaddingBottom);

            int iconTop = bodyTop + Math.Max(0, (bodyHeight - IconSize) / 2);
            _iconY = Math.Max(_iconY, iconTop);

            int titleBottom = Math.Max(0, bodyTop - LayoutBodyToTitleGap);
            float titleHeight = _font != null ? _font.LineSpacing * TitleScale : 12f;
            _titleY = Math.Max(8, (int)Math.Round(titleBottom - titleHeight));
        }

        private static int FindInnerEdge(Texture2D texture, bool fromLeft)
        {
            if (texture == null || texture.Width <= 0 || texture.Height <= 0)
            {
                return -1;
            }

            int y = texture.Height / 2;
            Color[] row = new Color[texture.Width];
            texture.GetData(0, new Rectangle(0, y, texture.Width, 1), row, 0, row.Length);
            int start = fromLeft ? 0 : texture.Width - 1;
            int end = fromLeft ? texture.Width : -1;
            int step = fromLeft ? 1 : -1;
            for (int x = start; x != end; x += step)
            {
                if (IsFrameFillPixel(row[x]))
                {
                    return x;
                }
            }

            return -1;
        }

        private static void FindDominantNeutralBand(Texture2D texture, out int bandStart, out int bandEnd)
        {
            bandStart = -1;
            bandEnd = -1;
            if (texture == null || texture.Width <= 0 || texture.Height <= 0)
            {
                return;
            }

            Color[] row = new Color[texture.Width];
            int currentStart = -1;
            int currentLength = 0;
            int bestStart = -1;
            int bestLength = 0;

            for (int y = 0; y < texture.Height; y++)
            {
                texture.GetData(0, new Rectangle(0, y, texture.Width, 1), row, 0, row.Length);
                int neutralCount = 0;
                for (int x = 0; x < row.Length; x++)
                {
                    if (IsNeutralPanelPixel(row[x]))
                    {
                        neutralCount++;
                    }
                }

                bool isDominantNeutral = neutralCount >= texture.Width - LayoutRowDominanceThreshold;
                if (isDominantNeutral)
                {
                    if (currentStart < 0)
                    {
                        currentStart = y;
                        currentLength = 1;
                    }
                    else
                    {
                        currentLength++;
                    }
                }
                else if (currentStart >= 0)
                {
                    if (currentLength > bestLength)
                    {
                        bestStart = currentStart;
                        bestLength = currentLength;
                    }

                    currentStart = -1;
                    currentLength = 0;
                }
            }

            if (currentStart >= 0 && currentLength > bestLength)
            {
                bestStart = currentStart;
                bestLength = currentLength;
            }

            if (bestStart >= 0)
            {
                bandStart = bestStart;
                bandEnd = bestStart + bestLength - 1;
            }
        }

        private static bool IsFrameFillPixel(Color color)
        {
            if (color.A < LayoutSampleAlphaThreshold)
            {
                return false;
            }

            return color.B - color.R >= 40 || color.G - color.R >= 16;
        }

        private static bool IsNeutralPanelPixel(Color color)
        {
            if (color.A < LayoutSampleAlphaThreshold)
            {
                return false;
            }

            return Math.Abs(color.R - color.G) <= 8 &&
                   Math.Abs(color.G - color.B) <= 8 &&
                   color.R >= 180;
        }

        private string[] WrapText(string value, float scale, int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(value) || _font == null)
            {
                return Array.Empty<string>();
            }

            string[] words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new List<string>();
            string currentLine = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine)
                    ? words[i]
                    : currentLine + " " + words[i];
                if (MeasureTextWidth(candidate, scale) <= maxWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = string.Empty;
                    i--;
                    continue;
                }

                lines.Add(TrimText(words[i], scale, maxWidth));
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private float MeasureTextWidth(string value, float scale)
        {
            return _font == null || string.IsNullOrWhiteSpace(value)
                ? 0f
                : _font.MeasureString(value).X * scale;
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
