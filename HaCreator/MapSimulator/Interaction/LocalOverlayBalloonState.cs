using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum LocalOverlayBalloonAnchorMode
    {
        Avatar,
        World
    }

    internal sealed class LocalOverlayBalloonState
    {
        private LocalOverlayBalloonMessage _avatarMessage;
        private readonly List<LocalOverlayBalloonMessage> _fieldMessages = new();

        public bool HasAvatarMessage => _avatarMessage != null;
        public int FieldMessageCount => _fieldMessages.Count;

        public bool IsActive(int currentTickCount)
        {
            if (_avatarMessage?.IsActive(currentTickCount) == true)
            {
                return true;
            }

            for (int i = 0; i < _fieldMessages.Count; i++)
            {
                if (_fieldMessages[i].IsActive(currentTickCount))
                {
                    return true;
                }
            }

            return false;
        }

        public LocalOverlayBalloonMessage ShowAvatar(string text, int requestedWidth, int lifetimeMs, int currentTickCount)
        {
            DisposeMessage(_avatarMessage);
            _avatarMessage = CreateMessage(
                text,
                requestedWidth,
                lifetimeMs,
                currentTickCount,
                LocalOverlayBalloonAnchorMode.Avatar,
                Point.Zero);
            return _avatarMessage;
        }

        public LocalOverlayBalloonMessage ShowWorld(string text, int requestedWidth, int lifetimeMs, Point worldAnchor, int currentTickCount)
        {
            LocalOverlayBalloonMessage message = CreateMessage(
                text,
                requestedWidth,
                lifetimeMs,
                currentTickCount,
                LocalOverlayBalloonAnchorMode.World,
                worldAnchor);
            if (message == null)
            {
                return null;
            }

            _fieldMessages.Add(message);
            return message;
        }

        public void Update(int currentTickCount)
        {
            if (_avatarMessage?.IsActive(currentTickCount) != true)
            {
                DisposeMessage(_avatarMessage);
                _avatarMessage = null;
            }

            for (int i = _fieldMessages.Count - 1; i >= 0; i--)
            {
                if (!_fieldMessages[i].IsActive(currentTickCount))
                {
                    DisposeMessage(_fieldMessages[i]);
                    _fieldMessages.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            DisposeMessage(_avatarMessage);
            _avatarMessage = null;
            for (int i = 0; i < _fieldMessages.Count; i++)
            {
                DisposeMessage(_fieldMessages[i]);
            }

            _fieldMessages.Clear();
        }

        public LocalOverlayBalloonMessage GetAvatarMessage(int currentTickCount)
        {
            return _avatarMessage?.IsActive(currentTickCount) == true
                ? _avatarMessage
                : null;
        }

        public IReadOnlyList<LocalOverlayBalloonMessage> GetFieldMessages(int currentTickCount)
        {
            if (_fieldMessages.Count == 0)
            {
                return Array.Empty<LocalOverlayBalloonMessage>();
            }

            var active = new List<LocalOverlayBalloonMessage>(_fieldMessages.Count);
            for (int i = 0; i < _fieldMessages.Count; i++)
            {
                LocalOverlayBalloonMessage message = _fieldMessages[i];
                if (message.IsActive(currentTickCount))
                {
                    active.Add(message);
                }
            }

            return active;
        }

        private static LocalOverlayBalloonMessage CreateMessage(
            string text,
            int requestedWidth,
            int lifetimeMs,
            int currentTickCount,
            LocalOverlayBalloonAnchorMode anchorMode,
            Point worldAnchor)
        {
            string sanitizedText = SanitizeText(text);
            int expiresAt = currentTickCount + Math.Max(0, lifetimeMs);
            return string.IsNullOrWhiteSpace(sanitizedText) || expiresAt <= currentTickCount
                ? null
                : new LocalOverlayBalloonMessage(
                    sanitizedText,
                    Math.Max(0, requestedWidth),
                    expiresAt,
                    anchorMode,
                    worldAnchor);
        }

        private static string SanitizeText(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("\r", string.Empty);
        }

        private static void DisposeMessage(LocalOverlayBalloonMessage message)
        {
            message?.DisposeVisual();
        }
    }

    internal sealed class LocalOverlayBalloonMessage
    {
        public LocalOverlayBalloonMessage(string text, int requestedWidth, int expiresAt, LocalOverlayBalloonAnchorMode anchorMode, Point worldAnchor)
        {
            Text = text ?? string.Empty;
            RequestedWidth = requestedWidth;
            ExpiresAt = expiresAt;
            AnchorMode = anchorMode;
            WorldAnchor = worldAnchor;
        }

        public string Text { get; }
        public int RequestedWidth { get; }
        public int ExpiresAt { get; }
        public LocalOverlayBalloonAnchorMode AnchorMode { get; }
        public Point WorldAnchor { get; }
        public Texture2D CachedBodyTexture { get; private set; }
        public Point CachedBodySize { get; private set; }

        public bool IsActive(int currentTickCount) =>
            !string.IsNullOrWhiteSpace(Text) &&
            unchecked(currentTickCount - ExpiresAt) < 0;

        public bool HasCachedBodyTexture(int width, int height) =>
            CachedBodyTexture != null &&
            !CachedBodyTexture.IsDisposed &&
            CachedBodySize.X == width &&
            CachedBodySize.Y == height;

        public void SetCachedBodyTexture(Texture2D texture)
        {
            DisposeVisual();
            CachedBodyTexture = texture;
            CachedBodySize = texture == null || texture.IsDisposed
                ? Point.Zero
                : new Point(texture.Width, texture.Height);
        }

        public void DisposeVisual()
        {
            if (CachedBodyTexture != null && !CachedBodyTexture.IsDisposed)
            {
                CachedBodyTexture.Dispose();
            }

            CachedBodyTexture = null;
            CachedBodySize = Point.Zero;
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
        public LocalOverlayBalloonArrowSprite Arrow { get; init; }
        public LocalOverlayBalloonArrowSprite SecondaryArrow { get; init; }
        public LocalOverlayBalloonArrowSprite SouthEastArrow { get; init; }
        public LocalOverlayBalloonArrowSprite SouthWestArrow { get; init; }
        public LocalOverlayBalloonArrowSprite NorthWestArrow { get; init; }
        public LocalOverlayBalloonArrowSprite NorthEastArrow { get; init; }
        public LocalOverlayBalloonArrowSprite SouthEastLongArrow { get; init; }
        public LocalOverlayBalloonArrowSprite SouthWestLongArrow { get; init; }
        public LocalOverlayBalloonArrowSprite NorthWestLongArrow { get; init; }
        public LocalOverlayBalloonArrowSprite NorthEastLongArrow { get; init; }
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
            Center != null;

        public bool HasArrowSprites =>
            Arrow?.IsLoaded == true ||
            SecondaryArrow?.IsLoaded == true ||
            SouthEastArrow?.IsLoaded == true ||
            SouthWestArrow?.IsLoaded == true ||
            NorthWestArrow?.IsLoaded == true ||
            NorthEastArrow?.IsLoaded == true ||
            SouthEastLongArrow?.IsLoaded == true ||
            SouthWestLongArrow?.IsLoaded == true ||
            NorthWestLongArrow?.IsLoaded == true ||
            NorthEastLongArrow?.IsLoaded == true;
    }

    internal sealed class LocalOverlayBalloonArrowSprite
    {
        public Texture2D Texture { get; init; }
        public Point Origin { get; init; }

        public bool IsLoaded => Texture != null;
    }
}
