namespace HaCreator.MapSimulator.Interaction
{
    using System;
    using Microsoft.Xna.Framework;

    internal enum PacketQuestResultUtilDialogButtonVisualState
    {
        Normal = 0,
        MouseOver = 1,
        Pressed = 2,
        Disabled = 3,
        KeyFocused = 4
    }

    internal static class PacketQuestResultUtilDialogLayout
    {
        // UIWindow2.img/UtilDlgEx exposes the v95-era quest-result util dialog shell
        // as top/center/bottom slices sized 519x28, 519x13, and 519x44.
        internal const int DefaultWindowWidth = 519;
        internal const int DefaultTopHeight = 28;
        internal const int DefaultCenterHeight = 13;
        internal const int DefaultBottomHeight = 44;
        internal const int DefaultCenterRepeatCount = 10;
        internal const int SpeakerBodyTextWidth = 341;
        internal const int SpeakerBodyRightMargin = 24;
        internal const int SpeakerBodyTopMargin = 24;
        internal const int SpeakerBodyBottomMargin = 90;
        internal const int SpeakerPortraitLeftMargin = 18;
        internal const int SpeakerPortraitTopMargin = 18;
        internal const int SpeakerPortraitBottomMargin = 64;
        internal const int SpeakerPortraitRightGap = 16;
        internal const int SpeakerNameBarBottomMargin = 10;
        internal const int DefaultWindowHeight =
            DefaultTopHeight + (DefaultCenterHeight * DefaultCenterRepeatCount) + DefaultBottomHeight;

        internal static Rectangle GetBodyTextRectangle(Rectangle windowRect, bool hasSpeakerPortrait)
        {
            if (!hasSpeakerPortrait)
            {
                return new Rectangle(
                    windowRect.X + 34,
                    windowRect.Y + SpeakerBodyTopMargin,
                    windowRect.Width - 68,
                    windowRect.Height - SpeakerBodyBottomMargin);
            }

            int width = Math.Min(SpeakerBodyTextWidth, Math.Max(0, windowRect.Width - 68));
            int x = windowRect.Right - SpeakerBodyRightMargin - width;
            return new Rectangle(
                x,
                windowRect.Y + SpeakerBodyTopMargin,
                width,
                windowRect.Height - SpeakerBodyBottomMargin);
        }

        internal static Rectangle GetSpeakerPortraitBounds(Rectangle windowRect, Rectangle bodyTextRect)
        {
            int x = windowRect.X + SpeakerPortraitLeftMargin;
            int y = windowRect.Y + SpeakerPortraitTopMargin;
            int width = Math.Max(0, bodyTextRect.X - x - SpeakerPortraitRightGap);
            int height = Math.Max(0, windowRect.Height - SpeakerPortraitTopMargin - SpeakerPortraitBottomMargin);
            return new Rectangle(x, y, width, height);
        }

        internal static Rectangle GetSpeakerNameBarBounds(
            Rectangle portraitBounds,
            int barWidth,
            int barHeight)
        {
            if (portraitBounds.Width <= 0 ||
                portraitBounds.Height <= 0 ||
                barWidth <= 0 ||
                barHeight <= 0)
            {
                return Rectangle.Empty;
            }

            int width = Math.Min(barWidth, portraitBounds.Width);
            int x = portraitBounds.X + Math.Max(0, (portraitBounds.Width - width) / 2);
            int y = portraitBounds.Bottom - barHeight - SpeakerNameBarBottomMargin;
            return new Rectangle(x, y, width, barHeight);
        }

        internal static Rectangle GetSpeakerFrameBounds(
            Rectangle portraitBounds,
            Point origin,
            int frameWidth,
            int frameHeight)
        {
            if (portraitBounds.Width <= 0 ||
                portraitBounds.Height <= 0 ||
                frameWidth <= 0 ||
                frameHeight <= 0)
            {
                return Rectangle.Empty;
            }

            float scale = Math.Min(
                portraitBounds.Width / (float)frameWidth,
                portraitBounds.Height / (float)frameHeight);
            int width = Math.Max(1, (int)Math.Round(frameWidth * scale));
            int height = Math.Max(1, (int)Math.Round(frameHeight * scale));
            int anchoredX = portraitBounds.X + ((portraitBounds.Width - width) / 2);
            int anchoredY = portraitBounds.Bottom - height;

            if (origin.X > 0 || origin.Y > 0)
            {
                anchoredX = (int)Math.Round((portraitBounds.X + (portraitBounds.Width / 2f)) - (origin.X * scale));
                anchoredY = (int)Math.Round(portraitBounds.Bottom - (origin.Y * scale));
            }

            int minX = portraitBounds.X;
            int maxX = portraitBounds.Right - width;
            int minY = portraitBounds.Y;
            int maxY = portraitBounds.Bottom - height;
            return new Rectangle(
                Math.Clamp(anchoredX, minX, Math.Max(minX, maxX)),
                Math.Clamp(anchoredY, minY, Math.Max(minY, maxY)),
                width,
                height);
        }

        internal static string ResolveNextButtonText(bool hasNextPage)
        {
            return hasNextPage ? "Next" : "OK";
        }

        internal static PacketQuestResultUtilDialogButtonVisualState ResolveButtonVisualState(
            bool enabled,
            bool isPressed,
            bool isHovered,
            bool isKeyFocused)
        {
            if (!enabled)
            {
                return PacketQuestResultUtilDialogButtonVisualState.Disabled;
            }

            if (isPressed)
            {
                return PacketQuestResultUtilDialogButtonVisualState.Pressed;
            }

            if (isHovered)
            {
                return PacketQuestResultUtilDialogButtonVisualState.MouseOver;
            }

            return isKeyFocused
                ? PacketQuestResultUtilDialogButtonVisualState.KeyFocused
                : PacketQuestResultUtilDialogButtonVisualState.Normal;
        }
    }
}
