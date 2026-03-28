using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum LocalOverlayBalloonAnchorMode
    {
        Avatar,
        World
    }

    internal sealed class LocalOverlayBalloonState
    {
        public string Text { get; private set; } = string.Empty;
        public int RequestedWidth { get; private set; }
        public int ExpiresAt { get; private set; } = int.MinValue;
        public LocalOverlayBalloonAnchorMode AnchorMode { get; private set; }
        public Point WorldAnchor { get; private set; }

        public bool IsActive(int currentTickCount) =>
            !string.IsNullOrWhiteSpace(Text) &&
            unchecked(currentTickCount - ExpiresAt) < 0;

        public void ShowAvatar(string text, int requestedWidth, int lifetimeMs, int currentTickCount)
        {
            Text = SanitizeText(text);
            RequestedWidth = Math.Max(0, requestedWidth);
            AnchorMode = LocalOverlayBalloonAnchorMode.Avatar;
            WorldAnchor = Point.Zero;
            ExpiresAt = currentTickCount + Math.Max(0, lifetimeMs);
        }

        public void ShowWorld(string text, int requestedWidth, int lifetimeMs, Point worldAnchor, int currentTickCount)
        {
            Text = SanitizeText(text);
            RequestedWidth = Math.Max(0, requestedWidth);
            AnchorMode = LocalOverlayBalloonAnchorMode.World;
            WorldAnchor = worldAnchor;
            ExpiresAt = currentTickCount + Math.Max(0, lifetimeMs);
        }

        public void Update(int currentTickCount)
        {
            if (!IsActive(currentTickCount))
            {
                Clear();
            }
        }

        public void Clear()
        {
            Text = string.Empty;
            RequestedWidth = 0;
            ExpiresAt = int.MinValue;
            WorldAnchor = Point.Zero;
        }

        private static string SanitizeText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace("\r", string.Empty).Trim();
        }
    }

    internal sealed class LocalOverlayBalloonSkin
    {
        public Texture2D NorthWest { get; init; }
        public Texture2D NorthEast { get; init; }
        public Texture2D SouthWest { get; init; }
        public Texture2D SouthEast { get; init; }
        public Texture2D North { get; init; }
        public Texture2D South { get; init; }
        public Texture2D West { get; init; }
        public Texture2D East { get; init; }
        public Texture2D Center { get; init; }
        public Texture2D Arrow { get; init; }
        public Color TextColor { get; init; } = Color.Black;

        public bool IsLoaded =>
            NorthWest != null &&
            NorthEast != null &&
            SouthWest != null &&
            SouthEast != null &&
            North != null &&
            South != null &&
            West != null &&
            East != null &&
            Center != null &&
            Arrow != null;
    }
}
