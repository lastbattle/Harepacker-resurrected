using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal enum SkillTooltipAnchorOwner
    {
        LegacyPanel,
        SkillBook,
        QuickSlot,
        StatusBarCooldownTray,
        StatusBarOffBarCooldownTray
    }

    internal static class SkillTooltipFrameLayout
    {
        internal readonly record struct FrameGeometry(int Width, int Height, Point Origin);
        internal const int ClientTooltipWidth = 320;
        internal const int ClientTooltipBaseHeight = 114;
        internal const int ClientTooltipTitleX = 10;
        internal const int ClientTooltipTitleY = 10;
        internal const int ClientTooltipIconX = 10;
        internal const int ClientTooltipIconY = 32;
        internal const int ClientTooltipTextX = 87;
        internal const int ClientTooltipTextY = 32;
        internal const int ClientTooltipRightPadding = 20;
        internal static readonly Color PlainTooltipBackgroundColor = new(0, 0, 0, 235);
        private const int LegacyTooltipOffsetX = 12;
        private const int LegacyTooltipOffsetY = -4;
        private const int SkillBookCursorYOffset = 20;
        private const int QuickSlotCursorYOffset = 20;
        private const int StatusBarCooldownTrayCursorYOffset = -128;
        private const int StatusBarOffBarCooldownTrayCursorXOffset = 20;
        private const int StatusBarOffBarCooldownTrayCursorYOffset = 20;
        private const int MountedSkillTooltipFrameWidth = 193;
        private const int MountedSkillTooltipFrameHeight = 102;

        internal static void DrawPlainTooltipBackground(SpriteBatch sprite, Texture2D fillTexture, Rectangle rect)
        {
            if (sprite == null || fillTexture == null || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            sprite.Draw(fillTexture, rect, PlainTooltipBackgroundColor);
        }

        internal static Point ResolveSameFamilyOriginFallback(
            Point authoredOrigin,
            int authoredWidth,
            int authoredHeight,
            Point fallbackOrigin,
            int fallbackWidth,
            int fallbackHeight)
        {
            if (authoredOrigin != Point.Zero
                || fallbackOrigin == Point.Zero
                || authoredWidth != fallbackWidth
                || authoredHeight != fallbackHeight
                || authoredWidth != MountedSkillTooltipFrameWidth
                || authoredHeight != MountedSkillTooltipFrameHeight)
            {
                return authoredOrigin;
            }

            return fallbackOrigin;
        }

        internal static Point ResolveTooltipAnchorFromCursor(Point cursorPosition, SkillTooltipAnchorOwner owner)
        {
            // v95 client evidence:
            // - CUISkill::OnMouseMove and CUIKeyConfig::OnMouseMove call SetToolTip_Skill at (mouseX, mouseY + 20).
            // - CUIStatusBar::OnMouseMove calls SetToolTip_Skill at (mouseX, mouseY - 128) for shortcut tray entries.
            // - CUIStatusBar::ProcessToolTip uses (mouseX + 20, mouseY + 20) for status-bar owned tooltip routing.
            return owner switch
            {
                SkillTooltipAnchorOwner.SkillBook => new Point(cursorPosition.X, cursorPosition.Y + SkillBookCursorYOffset),
                SkillTooltipAnchorOwner.QuickSlot => new Point(cursorPosition.X, cursorPosition.Y + QuickSlotCursorYOffset),
                SkillTooltipAnchorOwner.StatusBarCooldownTray => new Point(cursorPosition.X, cursorPosition.Y + StatusBarCooldownTrayCursorYOffset),
                SkillTooltipAnchorOwner.StatusBarOffBarCooldownTray => new Point(
                    cursorPosition.X + StatusBarOffBarCooldownTrayCursorXOffset,
                    cursorPosition.Y + StatusBarOffBarCooldownTrayCursorYOffset),
                _ => new Point(cursorPosition.X + LegacyTooltipOffsetX, cursorPosition.Y + LegacyTooltipOffsetY)
            };
        }

        internal static Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<FrameGeometry> frameGeometries,
            ReadOnlySpan<int> framePreference,
            int edgePadding,
            out int tooltipFrameIndex)
        {
            Rectangle bestRect = Rectangle.Empty;
            int bestFrame = framePreference.Length > 0 ? framePreference[0] : 0;
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < framePreference.Length; i++)
            {
                int frameIndex = framePreference[i];
                Rectangle candidate = CreateTooltipRectFromAnchor(
                    anchorPoint,
                    tooltipWidth,
                    tooltipHeight,
                    frameIndex,
                    frameGeometries);
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight, edgePadding);
                if (overflow == 0)
                {
                    tooltipFrameIndex = frameIndex;
                    return candidate;
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestFrame = frameIndex;
                    bestRect = candidate;
                }
            }

            tooltipFrameIndex = bestFrame;
            return ClampTooltipRect(bestRect, renderWidth, renderHeight, edgePadding);
        }

        internal static FrameGeometry[] BuildFrameGeometries(
            Texture2D[] frames,
            Point[] origins)
        {
            int frameCount = Math.Max(frames?.Length ?? 0, origins?.Length ?? 0);
            if (frameCount <= 0)
            {
                return Array.Empty<FrameGeometry>();
            }

            FrameGeometry[] geometries = new FrameGeometry[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                Texture2D frame = frames != null && i < frames.Length ? frames[i] : null;
                Point origin = origins != null && i < origins.Length ? origins[i] : Point.Zero;
                geometries[i] = new FrameGeometry(
                    frame?.Width ?? 0,
                    frame?.Height ?? 0,
                    origin);
            }

            return geometries;
        }

        private static Rectangle CreateTooltipRectFromAnchor(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int frameIndex,
            ReadOnlySpan<FrameGeometry> frameGeometries)
        {
            if ((uint)frameIndex < (uint)frameGeometries.Length)
            {
                FrameGeometry geometry = frameGeometries[frameIndex];
                if (geometry.Width > 0 && geometry.Height > 0 && geometry.Origin != Point.Zero)
                {
                    float scaleX = tooltipWidth / (float)geometry.Width;
                    float scaleY = tooltipHeight / (float)geometry.Height;
                    return new Rectangle(
                        anchorPoint.X - (int)Math.Round(geometry.Origin.X * scaleX),
                        anchorPoint.Y - (int)Math.Round(geometry.Origin.Y * scaleY),
                        tooltipWidth,
                        tooltipHeight);
                }
            }

            return frameIndex switch
            {
                0 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y, tooltipWidth, tooltipHeight),
                _ => new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight)
            };
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight, int edgePadding)
        {
            int overflow = 0;
            if (rect.Left < edgePadding)
            {
                overflow += edgePadding - rect.Left;
            }

            if (rect.Top < edgePadding)
            {
                overflow += edgePadding - rect.Top;
            }

            if (rect.Right > renderWidth - edgePadding)
            {
                overflow += rect.Right - (renderWidth - edgePadding);
            }

            if (rect.Bottom > renderHeight - edgePadding)
            {
                overflow += rect.Bottom - (renderHeight - edgePadding);
            }

            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight, int edgePadding)
        {
            int minX = edgePadding;
            int minY = edgePadding;
            int maxX = Math.Max(minX, renderWidth - edgePadding - rect.Width);
            int maxY = Math.Max(minY, renderHeight - edgePadding - rect.Height);

            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }
    }
}
