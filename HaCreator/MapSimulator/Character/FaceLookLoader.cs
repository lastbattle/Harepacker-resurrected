using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Client-owned face-look loader seam. This mirrors the cache and expression-plane
    /// build step that sits under runtime blink or packet emotion ownership.
    /// </summary>
    internal sealed class FaceLookLoader
    {
        private readonly record struct FaceLookCacheKey(
            SkinColor SkinColor,
            string ExpressionName,
            int FaceAccessoryItemId);

        private readonly FacePart _facePart;
        private readonly Dictionary<FaceLookCacheKey, FaceLookEntry> _lookCache = new();

        public FaceLookLoader(FacePart facePart)
        {
            _facePart = facePart ?? throw new ArgumentNullException(nameof(facePart));
        }

        public FaceLookEntry GetLook(string expression, SkinColor skinColor, CharacterPart faceAccessoryPart)
        {
            string normalizedExpression = NormalizeExpressionName(expression);
            int faceAccessoryItemId = faceAccessoryPart?.ItemId ?? 0;
            FaceLookCacheKey cacheKey = new(skinColor, normalizedExpression, faceAccessoryItemId);
            if (_lookCache.TryGetValue(cacheKey, out FaceLookEntry cachedLook))
            {
                return cachedLook;
            }

            CharacterAnimation faceAnimation = _facePart.GetExpression(normalizedExpression);
            CharacterAnimation accessoryAnimation = ResolveAccessoryExpression(faceAccessoryPart, normalizedExpression);
            int faceFrameCount = faceAnimation?.Frames?.Count ?? 0;
            int accessoryFrameCount = accessoryAnimation?.Frames?.Count ?? 0;
            int frameCount = Math.Max(faceFrameCount, accessoryFrameCount);
            if (frameCount == 0)
            {
                return null;
            }

            FaceLookEntry faceLook = new()
            {
                ExpressionName = normalizedExpression,
                SkinColor = skinColor,
                FaceItemId = _facePart.ItemId,
                FaceAccessoryItemId = faceAccessoryItemId
            };

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                CharacterFrame faceFrame = GetAnimationFrame(faceAnimation, frameIndex);
                CharacterFrame accessoryFrame = GetAnimationFrame(accessoryAnimation, frameIndex);
                int delay = ResolveFaceLookDelay(faceFrame, accessoryFrame);

                faceLook.Frames.Add(new FaceLookFrame
                {
                    FaceFrame = faceFrame,
                    AccessoryFrame = accessoryFrame,
                    Delay = delay
                });

                faceLook.TotalDuration += delay;
            }

            _lookCache[cacheKey] = faceLook;
            return faceLook;
        }

        public bool TryGetLookDuration(string expression, SkinColor skinColor, CharacterPart faceAccessoryPart, out int durationMs)
        {
            durationMs = 0;
            FaceLookEntry faceLook = GetLook(expression, skinColor, faceAccessoryPart);
            if (faceLook == null || faceLook.TotalDuration <= 0)
            {
                return false;
            }

            durationMs = faceLook.TotalDuration;
            return true;
        }

        private static string NormalizeExpressionName(string expression)
        {
            return string.IsNullOrWhiteSpace(expression)
                ? "default"
                : expression.Trim();
        }

        private static CharacterAnimation ResolveAccessoryExpression(CharacterPart faceAccessoryPart, string expression)
        {
            if (TryResolveAccessoryAnimation(faceAccessoryPart, expression, out CharacterAnimation animation))
            {
                return animation;
            }

            if (TryResolveAccessoryAnimation(faceAccessoryPart, "default", out animation))
            {
                return animation;
            }

            if (TryResolveAccessoryAnimation(faceAccessoryPart, "blink", out animation))
            {
                return animation;
            }

            if (faceAccessoryPart?.AvailableAnimations != null)
            {
                foreach (string authoredExpression in faceAccessoryPart.AvailableAnimations.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
                {
                    if (TryResolveAccessoryAnimation(faceAccessoryPart, authoredExpression, out animation))
                    {
                        return animation;
                    }
                }
            }

            if (faceAccessoryPart?.Animations != null)
            {
                foreach ((_, CharacterAnimation fallbackAnimation) in faceAccessoryPart.Animations)
                {
                    if (fallbackAnimation?.Frames?.Count > 0)
                    {
                        return fallbackAnimation;
                    }
                }
            }

            return null;
        }

        private static bool TryResolveAccessoryAnimation(CharacterPart faceAccessoryPart, string expression, out CharacterAnimation animation)
        {
            animation = null;
            if (faceAccessoryPart == null || string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            if (faceAccessoryPart.Animations.TryGetValue(expression, out animation))
            {
                return animation?.Frames?.Count > 0;
            }

            if (faceAccessoryPart.AvailableAnimations != null
                && faceAccessoryPart.AvailableAnimations.Count > 0
                && !faceAccessoryPart.AvailableAnimations.Contains(expression))
            {
                return false;
            }

            animation = faceAccessoryPart.GetAnimation(expression);
            return animation?.Frames?.Count > 0;
        }

        private static CharacterFrame GetAnimationFrame(CharacterAnimation animation, int frameIndex)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return null;
            }

            return animation.Frames[frameIndex % animation.Frames.Count];
        }

        private static int ResolveFaceLookDelay(CharacterFrame faceFrame, CharacterFrame accessoryFrame)
        {
            if (faceFrame?.Delay > 0)
            {
                return faceFrame.Delay;
            }

            if (accessoryFrame?.Delay > 0)
            {
                return accessoryFrame.Delay;
            }

            return 100;
        }
    }
}
