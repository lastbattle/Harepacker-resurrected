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
        private int _nextOwnerIdentity = 1;

        public bool HasAvatarMessage => _avatarMessage != null;
        public int FieldMessageCount => _fieldMessages.Count;

        public bool IsActive(int currentTickCount)
        {
            Update(currentTickCount);
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

        public LocalOverlayBalloonMessage ShowAvatar(string text, int requestedWidth, int lifetimeMs, int currentTickCount, Point avatarOriginOffset)
        {
            DisposeMessage(_avatarMessage);
            _avatarMessage = CreateMessage(
                text,
                requestedWidth,
                lifetimeMs,
                currentTickCount,
                LocalOverlayBalloonAnchorMode.Avatar,
                Point.Zero,
                avatarOriginOffset,
                AllocateOwnerIdentity());
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
                worldAnchor,
                Point.Zero,
                AllocateOwnerIdentity());
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

        public int LastOwnerIdentityForClientParity =>
            _fieldMessages.Count > 0
                ? _fieldMessages[^1].OwnerIdentity
                : _avatarMessage?.OwnerIdentity ?? 0;

        public LocalOverlayBalloonMessage GetAvatarMessage(int currentTickCount)
        {
            Update(currentTickCount);
            return _avatarMessage?.IsActive(currentTickCount) == true
                ? _avatarMessage
                : null;
        }

        public IReadOnlyList<LocalOverlayBalloonMessage> GetFieldMessages(int currentTickCount)
        {
            Update(currentTickCount);
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
            Point worldAnchor,
            Point anchorOffset,
            int ownerIdentity)
        {
            string sanitizedText = SanitizeText(text);
            int normalizedLifetimeMs = Math.Max(0, lifetimeMs);
            int expiresAt = currentTickCount + normalizedLifetimeMs;
            return string.IsNullOrEmpty(sanitizedText) || expiresAt <= currentTickCount
                ? null
                : new LocalOverlayBalloonMessage(
                    sanitizedText,
                    Math.Max(0, requestedWidth),
                    currentTickCount,
                    normalizedLifetimeMs,
                    expiresAt,
                    anchorMode,
                    worldAnchor,
                    anchorOffset,
                    ownerIdentity);
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

        private int AllocateOwnerIdentity()
        {
            int ownerIdentity = _nextOwnerIdentity;
            _nextOwnerIdentity++;
            if (_nextOwnerIdentity <= 0)
            {
                _nextOwnerIdentity = 1;
            }

            return ownerIdentity;
        }
    }

    internal sealed class LocalOverlayBalloonMessage
    {
        private readonly Dictionary<LocalOverlayBalloonVisualCacheKey, Texture2D> _cachedVisuals = new();
        private Texture2D _cachedBodyTexture;
        private int _cachedBodyWidth;
        private int _cachedBodyHeight;

        public LocalOverlayBalloonMessage(
            string text,
            int requestedWidth,
            int startedAt,
            int lifetimeMs,
            int expiresAt,
            LocalOverlayBalloonAnchorMode anchorMode,
            Point worldAnchor,
            Point anchorOffset,
            int ownerIdentity)
        {
            Text = text ?? string.Empty;
            RequestedWidth = requestedWidth;
            StartedAt = startedAt;
            LifetimeMs = lifetimeMs;
            ExpiresAt = expiresAt;
            AnchorMode = anchorMode;
            WorldAnchor = worldAnchor;
            AnchorOffset = anchorOffset;
            OwnerIdentity = ownerIdentity;
        }

        public string Text { get; }
        public int RequestedWidth { get; }
        public int StartedAt { get; }
        public int LifetimeMs { get; }
        public int ExpiresAt { get; }
        public LocalOverlayBalloonAnchorMode AnchorMode { get; }
        public Point WorldAnchor { get; }
        public Point AnchorOffset { get; }
        public int OwnerIdentity { get; }
        public Texture2D CachedBodyTexture => _cachedBodyTexture != null && !_cachedBodyTexture.IsDisposed ? _cachedBodyTexture : null;

        public bool IsActive(int currentTickCount) =>
            !string.IsNullOrEmpty(Text) &&
            unchecked(currentTickCount - ExpiresAt) < 0;

        public bool TryGetCachedVisualTexture(int bodyWidth, int bodyHeight, int variantId, out Texture2D texture)
        {
            LocalOverlayBalloonVisualCacheKey key = new(bodyWidth, bodyHeight, variantId);
            if (_cachedVisuals.TryGetValue(key, out texture) &&
                texture != null &&
                !texture.IsDisposed)
            {
                return true;
            }

            texture = null;
            return false;
        }

        public void SetCachedVisualTexture(int bodyWidth, int bodyHeight, int variantId, Texture2D texture)
        {
            LocalOverlayBalloonVisualCacheKey key = new(bodyWidth, bodyHeight, variantId);
            if (_cachedVisuals.TryGetValue(key, out Texture2D existingTexture) &&
                existingTexture != null &&
                !existingTexture.IsDisposed)
            {
                existingTexture.Dispose();
            }

            if (texture == null || texture.IsDisposed)
            {
                _cachedVisuals.Remove(key);
                return;
            }

            _cachedVisuals[key] = texture;
        }

        public bool HasCachedBodyTexture(int bodyWidth, int bodyHeight)
        {
            return _cachedBodyTexture != null
                && !_cachedBodyTexture.IsDisposed
                && _cachedBodyWidth == bodyWidth
                && _cachedBodyHeight == bodyHeight;
        }

        public void SetCachedBodyTexture(Texture2D texture, int bodyWidth, int bodyHeight)
        {
            if (_cachedBodyTexture != null && !_cachedBodyTexture.IsDisposed)
            {
                _cachedBodyTexture.Dispose();
            }

            _cachedBodyTexture = texture != null && !texture.IsDisposed ? texture : null;
            _cachedBodyWidth = _cachedBodyTexture == null ? 0 : bodyWidth;
            _cachedBodyHeight = _cachedBodyTexture == null ? 0 : bodyHeight;
        }

        public void DisposeVisual()
        {
            if (_cachedBodyTexture != null && !_cachedBodyTexture.IsDisposed)
            {
                _cachedBodyTexture.Dispose();
            }

            _cachedBodyTexture = null;
            _cachedBodyWidth = 0;
            _cachedBodyHeight = 0;
            foreach ((LocalOverlayBalloonVisualCacheKey _, Texture2D texture) in _cachedVisuals)
            {
                if (texture != null && !texture.IsDisposed)
                {
                    texture.Dispose();
                }
            }

            _cachedVisuals.Clear();
        }
    }

    internal readonly record struct LocalOverlayBalloonVisualCacheKey(int BodyWidth, int BodyHeight, int VariantId);

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
        public bool IsScreenChat { get; init; }

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
        public Point TopMountPoint { get; init; }
        public Point BottomMountPoint { get; init; }

        public bool IsLoaded => Texture != null;
    }
}
