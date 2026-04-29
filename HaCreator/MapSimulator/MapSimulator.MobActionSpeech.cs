using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Entities;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int MobActionSpeechHorizontalPadding = 18;
        private const int MobActionSpeechVerticalPadding = 12;
        private const int MobActionSpeechOwnerMaxTextWidth = 220;
        private const int MobActionSpeechScreenMaxTextWidth = 360;

        private readonly Dictionary<int, LocalOverlayBalloonSkin> _mobActionSpeechBalloonSkins = new();
        private bool _mobActionSpeechBalloonSkinsLoaded;

        internal sealed class MobActionSpeechTextLayout
        {
            public IReadOnlyList<string> Lines { get; init; }
            public Vector2 TextSize { get; init; }
        }

        private void DrawMobActionSpeechFeedback(in Managers.RenderContext renderContext)
        {
            if (_fontChat == null ||
                _debugBoundaryTexture == null ||
                _visibleMobs == null ||
                _visibleMobsCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _visibleMobsCount; i++)
            {
                MobItem mob = _visibleMobs[i];
                if (mob?.IsActionSpeechActive(renderContext.TickCount) == true)
                {
                    DrawMobActionSpeechBalloon(mob, renderContext);
                }
            }
        }

        private void DrawMobActionSpeechBalloon(MobItem mob, in Managers.RenderContext renderContext)
        {
            if (mob == null ||
                !mob.HasActiveActionSpeech ||
                _fontChat == null ||
                _debugBoundaryTexture == null)
            {
                return;
            }

            MobActionSpeechTextLayout textLayout = BuildMobActionSpeechTextLayout(
                mob.ActiveActionSpeechText,
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice,
                renderContext.RenderParams.RenderWidth,
                MeasureChatTextWithFallback);
            Rectangle bounds = ResolveMobActionSpeechBounds(
                textLayout.TextSize,
                mob.CurrentX,
                mob.CurrentY - mob.GetVisualHeight(60),
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice,
                renderContext.MapShiftX,
                renderContext.MapShiftY,
                renderContext.MapCenterX,
                renderContext.MapCenterY,
                renderContext.RenderParams.RenderWidth,
                renderContext.RenderParams.RenderHeight);

            if (bounds.Right < 0 ||
                bounds.Bottom < 0 ||
                bounds.Left > renderContext.RenderParams.RenderWidth ||
                bounds.Top > renderContext.RenderParams.RenderHeight)
            {
                return;
            }

            float remainingAlpha = MathHelper.Clamp((mob.ActiveActionSpeechExpiresAt - renderContext.TickCount) / 400f, 0f, 1f);
            ResolveMobActionSpeechColors(
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice,
                remainingAlpha,
                out Color backgroundColor,
                out Color borderColor,
                out Color textColor);

            LocalOverlayBalloonSkin skin = IsMobActionSpeechFloatNotice(mob.ActiveActionSpeechFloatNotice)
                ? null
                : ResolveMobActionSpeechBalloonSkin(mob.ActiveActionSpeechChatBalloon);
            bool drewAuthoredSkin = DrawMobActionSpeechBalloonSkin(skin, bounds, remainingAlpha);
            if (!drewAuthoredSkin)
            {
                DrawMobActionSpeechFallbackFrame(
                    bounds,
                    backgroundColor,
                    borderColor,
                    !IsMobActionSpeechScreenNotice(mob.ActiveActionSpeechChatBalloon, mob.ActiveActionSpeechFloatNotice));
            }

            DrawMobActionSpeechText(textLayout, bounds, textColor);
        }

        private LocalOverlayBalloonSkin ResolveMobActionSpeechBalloonSkin(int chatBalloon)
        {
            EnsureMobActionSpeechBalloonSkinsLoaded();
            int normalizedChatBalloon = Math.Max(0, chatBalloon);
            if (_mobActionSpeechBalloonSkins.TryGetValue(normalizedChatBalloon, out LocalOverlayBalloonSkin skin) &&
                skin?.IsLoaded == true)
            {
                return skin;
            }

            return normalizedChatBalloon != 0 &&
                   _mobActionSpeechBalloonSkins.TryGetValue(0, out LocalOverlayBalloonSkin fallback) &&
                   fallback?.IsLoaded == true
                ? fallback
                : null;
        }

        private void EnsureMobActionSpeechBalloonSkinsLoaded()
        {
            if (_mobActionSpeechBalloonSkinsLoaded)
            {
                return;
            }

            _mobActionSpeechBalloonSkinsLoaded = true;
            WzImage chatBalloonImage = Program.FindImage("UI", "ChatBalloon.img");
            if (chatBalloonImage == null)
            {
                return;
            }

            chatBalloonImage.ParseImage();
            if (chatBalloonImage["mob"] is not WzSubProperty mobBalloonRoot)
            {
                return;
            }

            foreach (WzImageProperty child in mobBalloonRoot.WzProperties)
            {
                if (!int.TryParse(child?.Name, out int chatBalloonId) ||
                    child is not WzSubProperty source)
                {
                    continue;
                }

                LocalOverlayBalloonSkin skin = LoadMobActionSpeechBalloonSkin(source);
                if (skin?.IsLoaded == true)
                {
                    _mobActionSpeechBalloonSkins[chatBalloonId] = skin;
                }
            }
        }

        private LocalOverlayBalloonSkin LoadMobActionSpeechBalloonSkin(WzSubProperty source)
        {
            if (source == null)
            {
                return null;
            }

            return new LocalOverlayBalloonSkin
            {
                NorthWest = LoadUiCanvasTexture(source["nw"] as WzCanvasProperty),
                NorthEast = LoadUiCanvasTexture(source["ne"] as WzCanvasProperty),
                SouthWest = LoadUiCanvasTexture(source["sw"] as WzCanvasProperty),
                SouthEast = LoadUiCanvasTexture(source["se"] as WzCanvasProperty),
                North = LoadUiCanvasTexture(source["n"] as WzCanvasProperty),
                South = LoadUiCanvasTexture(source["s"] as WzCanvasProperty),
                West = LoadUiCanvasTexture(source["w"] as WzCanvasProperty),
                East = LoadUiCanvasTexture(source["e"] as WzCanvasProperty),
                Center = LoadUiCanvasTexture(source["c"] as WzCanvasProperty),
                Arrow = LoadUiArrowSprite(source["arrow"] as WzCanvasProperty),
                TextColor = IsMobActionSpeechScreenChatSource(source) ? Color.White : Color.Black
            };
        }

        private bool DrawMobActionSpeechBalloonSkin(LocalOverlayBalloonSkin skin, Rectangle bounds, float alpha)
        {
            if (skin?.IsLoaded != true)
            {
                return false;
            }

            Color tint = Color.White * MathHelper.Clamp(alpha, 0f, 1f);
            DrawMobActionSpeechNineSlice(skin, bounds, tint);

            LocalOverlayBalloonArrowSprite arrow = skin.Arrow;
            if (arrow?.IsLoaded == true)
            {
                int arrowX = bounds.Left + (bounds.Width / 2) - arrow.Origin.X;
                int arrowY = bounds.Bottom - arrow.Origin.Y;
                _spriteBatch.Draw(arrow.Texture, new Vector2(arrowX, arrowY), tint);
            }

            return true;
        }

        private void DrawMobActionSpeechNineSlice(LocalOverlayBalloonSkin skin, Rectangle bounds, Color tint)
        {
            Texture2D northWest = skin.NorthWest;
            Texture2D northEast = skin.NorthEast;
            Texture2D southWest = skin.SouthWest;
            Texture2D southEast = skin.SouthEast;
            Texture2D north = skin.North;
            Texture2D south = skin.South;
            Texture2D west = skin.West;
            Texture2D east = skin.East;
            Texture2D center = skin.Center;

            int leftWidth = northWest.Width;
            int rightWidth = northEast.Width;
            int topHeight = northWest.Height;
            int bottomHeight = southWest.Height;
            int centerWidth = Math.Max(0, bounds.Width - leftWidth - rightWidth);
            int centerHeight = Math.Max(0, bounds.Height - topHeight - bottomHeight);

            _spriteBatch.Draw(center, new Rectangle(bounds.X + leftWidth, bounds.Y + topHeight, centerWidth, centerHeight), tint);
            _spriteBatch.Draw(northWest, new Vector2(bounds.X, bounds.Y), tint);
            _spriteBatch.Draw(northEast, new Vector2(bounds.Right - rightWidth, bounds.Y), tint);
            _spriteBatch.Draw(southWest, new Vector2(bounds.X, bounds.Bottom - bottomHeight), tint);
            _spriteBatch.Draw(southEast, new Vector2(bounds.Right - rightWidth, bounds.Bottom - bottomHeight), tint);

            if (centerWidth > 0)
            {
                _spriteBatch.Draw(north, new Rectangle(bounds.X + leftWidth, bounds.Y, centerWidth, north.Height), tint);
                _spriteBatch.Draw(south, new Rectangle(bounds.X + leftWidth, bounds.Bottom - south.Height, centerWidth, south.Height), tint);
            }

            if (centerHeight > 0)
            {
                _spriteBatch.Draw(west, new Rectangle(bounds.X, bounds.Y + topHeight, west.Width, centerHeight), tint);
                _spriteBatch.Draw(east, new Rectangle(bounds.Right - east.Width, bounds.Y + topHeight, east.Width, centerHeight), tint);
            }
        }

        private void DrawMobActionSpeechFallbackFrame(Rectangle bounds, Color backgroundColor, Color borderColor, bool includeArrow)
        {
            _spriteBatch.Draw(_debugBoundaryTexture, bounds, backgroundColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Top, bounds.Width, 2), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Bottom - 2, bounds.Width, 2), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Top, 2, bounds.Height), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Right - 2, bounds.Top, 2, bounds.Height), borderColor);

            if (!includeArrow)
            {
                return;
            }

            int arrowX = bounds.Left + (bounds.Width / 2) - 5;
            int arrowY = bounds.Bottom - 1;
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX, arrowY, 10, 4), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX + 2, arrowY + 4, 6, 3), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX + 4, arrowY + 7, 2, 3), borderColor);
        }

        internal static Rectangle ResolveMobActionSpeechBounds(
            Vector2 textSize,
            int mobWorldX,
            int mobWorldTop,
            int chatBalloon,
            int floatNotice,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight)
        {
            int boxWidth = Math.Max(MobActionSpeechHorizontalPadding, (int)Math.Ceiling(textSize.X) + MobActionSpeechHorizontalPadding);
            int boxHeight = Math.Max(20, (int)Math.Ceiling(textSize.Y) + MobActionSpeechVerticalPadding);

            if (IsMobActionSpeechScreenNotice(chatBalloon, floatNotice))
            {
                return new Rectangle(
                    Math.Max(0, (renderWidth - boxWidth) / 2),
                    Math.Max(0, Math.Min(renderHeight - boxHeight, 84)),
                    boxWidth,
                    boxHeight);
            }

            int boxX = mobWorldX - mapShiftX + mapCenterX - (boxWidth / 2);
            int boxY = mobWorldTop - mapShiftY + mapCenterY - boxHeight - 24;
            return new Rectangle(boxX, boxY, boxWidth, boxHeight);
        }

        internal static MobActionSpeechTextLayout BuildMobActionSpeechTextLayout(
            string text,
            int chatBalloon,
            int floatNotice,
            int renderWidth,
            Func<string, Vector2> measureText)
        {
            measureText ??= _ => Vector2.Zero;
            string normalizedText = NormalizeMobActionSpeechText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return new MobActionSpeechTextLayout
                {
                    Lines = Array.Empty<string>(),
                    TextSize = Vector2.Zero
                };
            }

            int maxTextWidth = ResolveMobActionSpeechMaxTextWidth(chatBalloon, floatNotice, renderWidth);
            List<string> lines = WrapMobActionSpeechText(normalizedText, maxTextWidth, measureText);
            if (lines.Count == 0)
            {
                lines.Add(normalizedText);
            }

            float maxLineWidth = 0f;
            float lineHeight = Math.Max(1f, measureText("Ay").Y);
            foreach (string line in lines)
            {
                Vector2 lineSize = measureText(line);
                maxLineWidth = Math.Max(maxLineWidth, lineSize.X);
                lineHeight = Math.Max(lineHeight, lineSize.Y);
            }

            return new MobActionSpeechTextLayout
            {
                Lines = lines,
                TextSize = new Vector2(maxLineWidth, lineHeight * lines.Count)
            };
        }

        private static int ResolveMobActionSpeechMaxTextWidth(int chatBalloon, int floatNotice, int renderWidth)
        {
            int authoredMaxWidth = IsMobActionSpeechScreenNotice(chatBalloon, floatNotice)
                ? MobActionSpeechScreenMaxTextWidth
                : MobActionSpeechOwnerMaxTextWidth;
            int viewportMaxWidth = renderWidth > 0
                ? Math.Max(48, renderWidth - (MobActionSpeechHorizontalPadding * 2))
                : authoredMaxWidth;
            return Math.Max(24, Math.Min(authoredMaxWidth, viewportMaxWidth));
        }

        private static List<string> WrapMobActionSpeechText(
            string text,
            int maxTextWidth,
            Func<string, Vector2> measureText)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string normalizedParagraph = NormalizeMobActionSpeechParagraph(paragraph);
                if (string.IsNullOrWhiteSpace(normalizedParagraph))
                {
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string word in normalizedParagraph.Split(' '))
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        continue;
                    }

                    string candidateLine = string.IsNullOrEmpty(currentLine)
                        ? word
                        : currentLine + " " + word;
                    if (measureText(candidateLine).X <= maxTextWidth)
                    {
                        currentLine = candidateLine;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = string.Empty;
                    }

                    if (measureText(word).X <= maxTextWidth)
                    {
                        currentLine = word;
                        continue;
                    }

                    foreach (string segment in SplitMobActionSpeechLongWord(word, maxTextWidth, measureText))
                    {
                        if (measureText(segment).X <= maxTextWidth)
                        {
                            lines.Add(segment);
                        }
                        else
                        {
                            currentLine = segment;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
            }

            return lines;
        }

        private static IEnumerable<string> SplitMobActionSpeechLongWord(
            string word,
            int maxTextWidth,
            Func<string, Vector2> measureText)
        {
            string segment = string.Empty;
            foreach (char character in word)
            {
                string candidate = segment + character;
                if (candidate.Length > 1 && measureText(candidate).X > maxTextWidth)
                {
                    yield return segment;
                    segment = character.ToString();
                    continue;
                }

                segment = candidate;
            }

            if (!string.IsNullOrEmpty(segment))
            {
                yield return segment;
            }
        }

        private static string NormalizeMobActionSpeechText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string[] lines = text.Replace("\r\n", "\n")
                .Split('\n')
                .Select(NormalizeMobActionSpeechParagraph)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            return lines.Length == 0 ? null : string.Join("\n", lines);
        }

        private static string NormalizeMobActionSpeechParagraph(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? null
                : string.Join(" ", text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private void DrawMobActionSpeechText(MobActionSpeechTextLayout layout, Rectangle bounds, Color textColor)
        {
            if (layout?.Lines == null || layout.Lines.Count == 0)
            {
                return;
            }

            float lineHeight = Math.Max(1f, MeasureChatTextWithFallback("Ay").Y);
            Vector2 position = new Vector2(bounds.Left + 9, bounds.Top + 6);
            foreach (string line in layout.Lines)
            {
                DrawChatTextWithFallback(line, position, textColor);
                position.Y += lineHeight;
            }
        }

        internal static bool IsMobActionSpeechScreenChat(int chatBalloon)
        {
            // UI/ChatBalloon.img/mob/1 carries screenChat=1; other mob balloons stay anchored to the owner.
            return chatBalloon == 1;
        }

        internal static string ResolveMobActionSpeechBalloonSkinPathForTests(int chatBalloon)
        {
            return $"UI/ChatBalloon.img/mob/{Math.Max(0, chatBalloon)}";
        }

        internal static bool IsMobActionSpeechScreenChatSource(WzImageProperty source)
        {
            return (source?["screenChat"] as WzIntProperty)?.Value != 0;
        }

        internal static bool IsMobActionSpeechFloatNotice(int floatNotice)
        {
            return floatNotice > 0;
        }

        internal static bool IsMobActionSpeechScreenNotice(int chatBalloon, int floatNotice)
        {
            return IsMobActionSpeechScreenChat(chatBalloon) || IsMobActionSpeechFloatNotice(floatNotice);
        }

        private static void ResolveMobActionSpeechColors(
            int chatBalloon,
            int floatNotice,
            float alpha,
            out Color backgroundColor,
            out Color borderColor,
            out Color textColor)
        {
            float clampedAlpha = MathHelper.Clamp(alpha, 0f, 1f);
            if (IsMobActionSpeechFloatNotice(floatNotice))
            {
                backgroundColor = new Color(32, 28, 18) * (0.90f * clampedAlpha);
                borderColor = new Color(255, 214, 91) * clampedAlpha;
                textColor = new Color(255, 244, 198) * clampedAlpha;
                return;
            }

            if (IsMobActionSpeechScreenChat(chatBalloon))
            {
                backgroundColor = new Color(31, 31, 31) * (0.88f * clampedAlpha);
                borderColor = new Color(246, 246, 246) * clampedAlpha;
                textColor = Color.White * clampedAlpha;
                return;
            }

            backgroundColor = new Color(255, 255, 245) * (0.92f * clampedAlpha);
            borderColor = new Color(72, 72, 72) * clampedAlpha;
            textColor = Color.Black * clampedAlpha;
        }
    }
}
