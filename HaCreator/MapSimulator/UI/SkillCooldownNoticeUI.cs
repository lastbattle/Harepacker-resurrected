using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Effects;

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
        internal readonly record struct NoticeFrameCandidate(
            string Name,
            bool HasTop,
            bool HasCenter,
            bool HasBottom,
            int TopWidth,
            int TopHeight,
            int CenterWidth,
            int CenterHeight,
            int BottomWidth,
            int BottomHeight,
            int ExtraPartCount);

        private const int MaxNotices = 1;
        // Client evidence:
        // - CUIStatusBar::SetItemMsg refuses new notice layers while quiz/item/float notice layers exist.
        // - CUIStatusBar::FloatNotice passes 0x1388 to SetItemMsg before creating its own notice layer.
        // - CUIStatusBar::Update expires m_dwItemMsg/m_dwFloatNotice directly on timer overflow.
        // Keep cooldown notices on the same single-layer, fixed-duration, no-fade expiration seam.
        private const int ClientNoticeDurationMs = 5000;
        private const int SlideSpeed = 0;
        private const float SpawnSlideOffset = 0f;
        // The packet-owned top-center HUD notices use the client's 800x578 anchor at 44px.
        // Cooldown notices share that same top-center seam more closely than the older 42px guess.
        private const int TopMargin = 44;
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
        private const int LayoutTitlePaddingTop = 6;
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

        internal static string ResolveNoticeFrameFamilyForClientParity(IReadOnlyList<NoticeFrameCandidate> candidates)
        {
            string exactMatchName = ResolveExactNotice3FamilyMatch(candidates);
            if (!string.IsNullOrEmpty(exactMatchName))
            {
                return exactMatchName;
            }

            string bestName = null;
            int bestScore = int.MaxValue;

            if (candidates == null)
            {
                return null;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                NoticeFrameCandidate candidate = candidates[i];
                if (!candidate.HasTop || !candidate.HasCenter || !candidate.HasBottom)
                {
                    continue;
                }

                if (candidate.TopWidth <= 0 || candidate.CenterWidth <= 0 || candidate.BottomWidth <= 0)
                {
                    continue;
                }

                int widthSpread =
                    Math.Abs(candidate.TopWidth - candidate.CenterWidth) +
                    Math.Abs(candidate.CenterWidth - candidate.BottomWidth);
                int score =
                    (Math.Abs(candidate.TopWidth - 266) * 6) +
                    (Math.Abs(candidate.TopHeight - 21) * 4) +
                    (Math.Abs(candidate.CenterHeight - 20) * 4) +
                    (Math.Abs(candidate.BottomHeight - 55) * 3) +
                    (widthSpread * 8) +
                    (Math.Max(0, candidate.ExtraPartCount) * 40);

                if (score < bestScore
                    || (score == bestScore && string.Equals(candidate.Name, "Notice3", StringComparison.OrdinalIgnoreCase)))
                {
                    bestName = candidate.Name;
                    bestScore = score;
                }
            }

            return bestName;
        }

        private static string ResolveExactNotice3FamilyMatch(IReadOnlyList<NoticeFrameCandidate> candidates)
        {
            if (candidates == null)
            {
                return null;
            }

            string exactNamedMatch = null;
            string exactUnnamedMatch = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                NoticeFrameCandidate candidate = candidates[i];
                if (!IsExactNotice3Family(candidate))
                {
                    continue;
                }

                if (string.Equals(candidate.Name, "Notice3", StringComparison.OrdinalIgnoreCase))
                {
                    exactNamedMatch = candidate.Name;
                    break;
                }

                exactUnnamedMatch ??= candidate.Name;
            }

            return exactNamedMatch ?? exactUnnamedMatch;
        }

        private static bool IsExactNotice3Family(NoticeFrameCandidate candidate)
        {
            return candidate.HasTop
                   && candidate.HasCenter
                   && candidate.HasBottom
                   && candidate.TopWidth == 266
                   && candidate.CenterWidth == 266
                   && candidate.BottomWidth == 266
                   && candidate.TopHeight == 21
                   && candidate.CenterHeight == 20
                   && candidate.BottomHeight == 55
                   && candidate.ExtraPartCount <= 0;
        }

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

        public bool AddNotice(
            int skillId,
            string title,
            string message,
            Texture2D iconTexture,
            SkillCooldownNoticeType type,
            int currentTime,
            bool hasBlockingStatusNoticeOwner = false)
        {
            if (!_initialized || string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            if (!ShouldAcceptIncomingNoticeForClientParity(_notices, skillId, hasBlockingStatusNoticeOwner))
            {
                return false;
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
                int maxNotices = ResolveMaxConcurrentNoticesForClientParity();
                while (_notices.Count >= maxNotices)
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
            return true;
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
                    if (step <= 0f || Math.Abs(offsetDelta) <= step)
                    {
                        notice.YOffset = notice.TargetYOffset;
                    }
                    else
                    {
                        notice.YOffset += Math.Sign(offsetDelta) * step;
                    }
                }

                int noticeDurationMs = ResolveNoticeDurationForClientParity(notice.Type);
                if (elapsed >= noticeDurationMs)
                {
                    notice.IsExpired = true;
                    _notices.RemoveAt(i);
                    ReflowTargets();
                    continue;
                }

                notice.Alpha = 1f;
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
                DrawTiledNoticeCenter(spriteBatch, panelRect.X, panelRect.Y + _topHeight, centerHeight, color);

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

            if (ClientTextDrawing.Measure((GraphicsDevice)null, value, scale, _font).X <= maxWidth)
            {
                return value;
            }

            const string ellipsis = "...";
            string trimmed = value;
            while (trimmed.Length > 0)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
                string candidate = trimmed + ellipsis;
                if (ClientTextDrawing.Measure((GraphicsDevice)null, candidate, scale, _font).X <= maxWidth)
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
            return ResolveTopMarginForClientParity(_screenHeight);
        }

        internal static int ResolveTopMarginForClientParity(int screenHeight)
        {
            return TopMargin;
        }

        internal static int ResolveNoticeDurationForClientParity(SkillCooldownNoticeType type)
        {
            return ClientNoticeDurationMs;
        }

        internal static int ResolveMaxConcurrentNoticesForClientParity()
        {
            return MaxNotices;
        }

        internal static bool ShouldAcceptIncomingNoticeForClientParity(
            IReadOnlyList<(int SkillId, bool IsExpired)> activeNotices,
            int incomingSkillId,
            bool hasBlockingStatusNoticeOwner = false)
        {
            if (hasBlockingStatusNoticeOwner)
            {
                return false;
            }

            if (activeNotices == null || activeNotices.Count == 0)
            {
                return true;
            }

            int activeOwnerSkillId = 0;
            bool hasActiveOwner = false;
            for (int i = 0; i < activeNotices.Count; i++)
            {
                (int skillId, bool isExpired) = activeNotices[i];
                if (isExpired)
                {
                    continue;
                }

                activeOwnerSkillId = skillId;
                hasActiveOwner = true;
                break;
            }

            if (!hasActiveOwner)
            {
                return true;
            }

            return activeOwnerSkillId == incomingSkillId;
        }

        internal static bool HasActiveStatusBarItemMsgOwnerForClientParity(
            IReadOnlyList<WeatherMessageInfo> weatherMessages,
            int currentTime)
        {
            if (weatherMessages == null || weatherMessages.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < weatherMessages.Count; i++)
            {
                WeatherMessageInfo message = weatherMessages[i];
                if (message == null)
                {
                    continue;
                }

                if (message.OwnerKind != WeatherMessageOwnerKind.StatusBarItemMsg)
                {
                    continue;
                }

                int duration = Math.Max(0, message.Duration);
                if (duration <= 0)
                {
                    continue;
                }

                int elapsed = unchecked(currentTime - message.StartTime);
                if (elapsed >= 0 && elapsed < duration)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldAcceptIncomingNoticeForClientParity(
            IReadOnlyList<NoticeEntry> activeNotices,
            int incomingSkillId,
            bool hasBlockingStatusNoticeOwner)
        {
            if (hasBlockingStatusNoticeOwner)
            {
                return false;
            }

            if (activeNotices == null || activeNotices.Count == 0)
            {
                return true;
            }

            int activeOwnerSkillId = 0;
            bool hasActiveOwner = false;
            for (int i = 0; i < activeNotices.Count; i++)
            {
                NoticeEntry entry = activeNotices[i];
                if (entry == null || entry.IsExpired)
                {
                    continue;
                }

                activeOwnerSkillId = entry.SkillId;
                hasActiveOwner = true;
                break;
            }

            if (!hasActiveOwner)
            {
                return true;
            }

            return activeOwnerSkillId == incomingSkillId;
        }

        private void DrawTiledNoticeCenter(SpriteBatch spriteBatch, int x, int startY, int centerHeight, Color color)
        {
            if (_frameCenter == null || centerHeight <= 0)
            {
                return;
            }

            int sourceHeight = Math.Max(1, _frameCenter.Height);
            int drawnHeight = 0;
            while (drawnHeight < centerHeight)
            {
                int segmentHeight = Math.Min(sourceHeight, centerHeight - drawnHeight);
                Rectangle destinationRect = new Rectangle(x, startY + drawnHeight, _panelWidth, segmentHeight);
                Rectangle sourceRect = new Rectangle(0, 0, _frameCenter.Width, segmentHeight);
                spriteBatch.Draw(_frameCenter, destinationRect, sourceRect, color);
                drawnHeight += segmentHeight;
            }
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

            if (!TryResolveNoticeBands(out int titleBandStart, out int titleBandEnd, out int bodyBandStart, out int bodyBandEnd))
            {
                return;
            }

            int bodyTop = bodyBandStart;
            int bodyBottom = bodyBandEnd;
            int bodyHeight = Math.Max(0, bodyBottom - bodyTop + 1);
            int titleTop = titleBandStart;
            int titleBottom = titleBandEnd;
            int contentLeft = innerLeft;
            int contentRight = innerRight;

            if (TryResolveBandInnerBounds(bodyBandStart, bodyBandEnd, out int bodyLeft, out int bodyRight))
            {
                contentLeft = bodyLeft;
                contentRight = bodyRight;
                _iconX = Math.Max(bodyLeft + LayoutIconInsetX, 12);
                _textRightPadding = Math.Max(8, _panelWidth - bodyRight + LayoutIconInsetX);
            }

            if (TryResolveBandInnerBounds(titleBandStart, titleBandEnd, out int titleLeft, out int titleRight))
            {
                contentLeft = Math.Min(contentLeft, titleLeft);
                contentRight = Math.Max(contentRight, titleRight);
                _textRightPadding = Math.Max(_textRightPadding, Math.Max(8, _panelWidth - titleRight + LayoutIconInsetX));
            }

            _titleX = Math.Max(_iconX + IconSize + LayoutIconGapX, contentLeft + LayoutIconInsetX);

            float titleHeight = _font != null ? _font.LineSpacing * TitleScale : 12f;
            int titleLaneTop = Math.Max(8, titleTop + LayoutTitlePaddingTop);
            int titleLaneBottomExclusive = Math.Max(titleLaneTop + 1, Math.Min(bodyTop - LayoutBodyToTitleGap, titleBottom + 1));
            int maxTitleY = Math.Max(8, titleLaneBottomExclusive - (int)Math.Ceiling(titleHeight));
            int centeredTitleY = titleLaneTop;
            if (titleLaneBottomExclusive > titleLaneTop)
            {
                float remainingTitleLane = Math.Max(0f, titleLaneBottomExclusive - titleLaneTop - titleHeight);
                centeredTitleY = titleLaneTop + (int)Math.Round(remainingTitleLane * 0.5f);
            }

            _titleY = Math.Clamp(centeredTitleY, 8, maxTitleY);
            _messageY = Math.Max(bodyTop + LayoutBodyPaddingTop, _titleY + (int)Math.Ceiling(titleHeight) + LayoutBodyToTitleGap);
            _textBottomPadding = Math.Max(LayoutBodyPaddingBottom, GetMinimumPanelHeight() - bodyBottom + LayoutBodyPaddingBottom);

            int iconTop = bodyTop + Math.Max(0, (bodyHeight - IconSize) / 2);
            _iconY = Math.Max(12, iconTop);
        }

        private bool TryResolveNoticeBands(out int titleBandStart, out int titleBandEnd, out int bodyBandStart, out int bodyBandEnd)
        {
            titleBandStart = -1;
            titleBandEnd = -1;
            bodyBandStart = -1;
            bodyBandEnd = -1;

            if (!TryFindDominantBandAcrossNotice(IsTitlePanelPixel, out titleBandStart, out titleBandEnd))
            {
                return false;
            }

            if (!TryFindDominantBandAcrossNotice(IsNeutralPanelPixel, out bodyBandStart, out bodyBandEnd, titleBandEnd + 1))
            {
                return false;
            }

            return titleBandEnd >= titleBandStart && bodyBandEnd >= bodyBandStart;
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

        private bool TryResolveBandInnerBounds(int bandStart, int bandEnd, out int innerLeft, out int innerRight)
        {
            innerLeft = int.MaxValue;
            innerRight = int.MinValue;
            if (bandStart < 0 || bandEnd < bandStart || _panelWidth <= 0)
            {
                return false;
            }

            bool found = false;
            Color[] row = new Color[_panelWidth];
            for (int y = bandStart; y <= bandEnd; y++)
            {
                if (!TryGetCombinedRow(y, row))
                {
                    continue;
                }

                int rowLeft = FindRowEdge(row, fromLeft: true);
                int rowRight = FindRowEdge(row, fromLeft: false);
                if (rowLeft < 0 || rowRight <= rowLeft)
                {
                    continue;
                }

                innerLeft = Math.Min(innerLeft, rowLeft);
                innerRight = Math.Max(innerRight, rowRight);
                found = true;
            }

            if (!found)
            {
                innerLeft = -1;
                innerRight = -1;
            }

            return found;
        }

        private static int FindRowEdge(Color[] row, bool fromLeft)
        {
            if (row == null || row.Length == 0)
            {
                return -1;
            }

            int start = fromLeft ? 0 : row.Length - 1;
            int end = fromLeft ? row.Length : -1;
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

        private bool TryFindDominantBandAcrossNotice(Func<Color, bool> predicate, out int bandStart, out int bandEnd, int searchStartY = 0)
        {
            bandStart = -1;
            bandEnd = -1;
            int noticeHeight = GetMinimumPanelHeight();
            if (_panelWidth <= 0 || noticeHeight <= 0)
            {
                return false;
            }

            int currentStart = -1;
            int currentLength = 0;
            int bestStart = -1;
            int bestLength = 0;
            Color[] row = new Color[_panelWidth];

            for (int y = Math.Max(0, searchStartY); y < noticeHeight; y++)
            {
                if (!TryGetCombinedRow(y, row))
                {
                    continue;
                }

                int matchCount = 0;
                for (int x = 0; x < row.Length; x++)
                {
                    if (predicate(row[x]))
                    {
                        matchCount++;
                    }
                }

                bool isDominant = matchCount >= row.Length - LayoutRowDominanceThreshold;
                if (isDominant)
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

            if (bestStart < 0)
            {
                return false;
            }

            bandStart = bestStart;
            bandEnd = bestStart + bestLength - 1;
            return true;
        }

        private bool TryGetCombinedRow(int absoluteY, Color[] row)
        {
            if (row == null || row.Length < _panelWidth)
            {
                return false;
            }

            if (_frameTop != null && absoluteY < _topHeight)
            {
                _frameTop.GetData(0, new Rectangle(0, absoluteY, _panelWidth, 1), row, 0, row.Length);
                return true;
            }

            int localY = absoluteY - _topHeight;
            if (_frameCenter != null && localY < _centerHeight)
            {
                _frameCenter.GetData(0, new Rectangle(0, localY, _panelWidth, 1), row, 0, row.Length);
                return true;
            }

            localY -= _centerHeight;
            if (_frameBottom != null && localY >= 0 && localY < _bottomHeight)
            {
                _frameBottom.GetData(0, new Rectangle(0, localY, _panelWidth, 1), row, 0, row.Length);
                return true;
            }

            return false;
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

        private static bool IsTitlePanelPixel(Color color)
        {
            if (color.A < LayoutSampleAlphaThreshold)
            {
                return false;
            }

            return color.B - color.R >= 30 || color.G - color.R >= 12;
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
            return string.IsNullOrWhiteSpace(value)
                ? 0f
                : ClientTextDrawing.Measure((GraphicsDevice)null, value, scale, _font).X;
        }

        private void DrawTextWithShadow(SpriteBatch spriteBatch, string text, Vector2 position, Color textColor, Color shadowColor, float scale)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
            {
                return;
            }

            Vector2 shadowOffset = new Vector2(1f, 1f);
            ClientTextDrawing.Draw(spriteBatch, text, position + shadowOffset, shadowColor, scale, _font);
            ClientTextDrawing.Draw(spriteBatch, text, position, textColor, scale, _font);
        }
    }
}
