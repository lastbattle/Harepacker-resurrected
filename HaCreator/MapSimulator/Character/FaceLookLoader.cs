using System;
using System.Collections.Generic;
using System.Linq;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
                FaceAccessoryItemId = faceAccessoryItemId,
                AuthoredDuration = faceAnimation?.AuthoredDuration
            };

            int resolvedDuration = 0;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                CharacterFrame faceFrame = GetAnimationFrame(faceAnimation, frameIndex);
                CharacterFrame accessoryFrame = GetAnimationFrame(accessoryAnimation, frameIndex);
                int delay = ResolveFaceLookDelay(faceFrame, accessoryFrame);

                faceLook.Frames.Add(new FaceLookFrame
                {
                    FaceFrame = faceFrame,
                    AccessoryFrame = accessoryFrame,
                    CompositeFrame = CreateCompositeFrame(faceFrame, accessoryFrame, delay),
                    Delay = delay
                });

                resolvedDuration += delay;
            }

            faceLook.TotalDuration = ResolveFaceLookDuration(faceLook.AuthoredDuration, resolvedDuration);
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

        private static int ResolveFaceLookDuration(int? authoredDuration, int resolvedDuration)
        {
            if (authoredDuration.GetValueOrDefault() > 0)
            {
                return authoredDuration.Value;
            }

            return resolvedDuration;
        }

        private static CharacterFrame CreateCompositeFrame(CharacterFrame faceFrame, CharacterFrame accessoryFrame, int delay)
        {
            if (faceFrame == null)
            {
                return null;
            }

            if (accessoryFrame == null)
            {
                CharacterFrame clonedFaceFrame = faceFrame.Clone();
                clonedFaceFrame.Delay = delay;
                return clonedFaceFrame;
            }

            if (!TryResolveCompositeLayout(faceFrame, accessoryFrame, out CompositeFaceLayout layout))
            {
                return null;
            }

            CharacterFrame compositeFrame = new()
            {
                Delay = delay,
                Z = "face",
                Origin = layout.BrowPoint,
                Bounds = new Rectangle(layout.Left, layout.Top, layout.Width, layout.Height),
                FrameUol = faceFrame.FrameUol ?? accessoryFrame.FrameUol
            };

            MergeMapPoints(compositeFrame.Map, faceFrame.Map, faceFrame.GetMapPoint("brow"), layout);
            MergeMapPoints(compositeFrame.Map, accessoryFrame.Map, accessoryFrame.GetMapPoint("brow"), layout);
            compositeFrame.Map["brow"] = layout.BrowPoint;

            if (TryComposeTexture(faceFrame, accessoryFrame, layout, out IDXObject compositeTexture))
            {
                compositeFrame.Texture = compositeTexture;
            }

            return compositeFrame;
        }

        private static bool TryResolveCompositeLayout(
            CharacterFrame faceFrame,
            CharacterFrame accessoryFrame,
            out CompositeFaceLayout layout)
        {
            layout = default;

            Point faceBrow = faceFrame.GetMapPoint("brow");
            Point accessoryBrow = accessoryFrame.GetMapPoint("brow");

            if (!TryResolveFrameSize(faceFrame, out int faceWidth, out int faceHeight)
                || !TryResolveFrameSize(accessoryFrame, out int accessoryWidth, out int accessoryHeight))
            {
                return false;
            }

            int faceLeft = -faceBrow.X;
            int faceTop = -faceBrow.Y;
            int accessoryLeft = -accessoryBrow.X;
            int accessoryTop = -accessoryBrow.Y;

            int left = Math.Min(faceLeft, accessoryLeft);
            int top = Math.Min(faceTop, accessoryTop);
            int right = Math.Max(faceLeft + faceWidth, accessoryLeft + accessoryWidth);
            int bottom = Math.Max(faceTop + faceHeight, accessoryTop + accessoryHeight);
            int width = right - left;
            int height = bottom - top;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            layout = new CompositeFaceLayout(
                left,
                top,
                width,
                height,
                new Point(-left, -top));
            return true;
        }

        private static void MergeMapPoints(
            Dictionary<string, Point> destination,
            Dictionary<string, Point> source,
            Point sourceBrow,
            CompositeFaceLayout layout)
        {
            if (destination == null || source == null || source.Count == 0)
            {
                return;
            }

            foreach ((string name, Point value) in source)
            {
                Point translatedPoint = new(
                    layout.BrowPoint.X + (value.X - sourceBrow.X),
                    layout.BrowPoint.Y + (value.Y - sourceBrow.Y));
                destination[name] = translatedPoint;
            }
        }

        private static bool TryComposeTexture(
            CharacterFrame faceFrame,
            CharacterFrame accessoryFrame,
            CompositeFaceLayout layout,
            out IDXObject compositeTexture)
        {
            compositeTexture = null;

            Texture2D faceTexture = faceFrame.Texture?.Texture;
            Texture2D accessoryTexture = accessoryFrame.Texture?.Texture;
            GraphicsDevice graphicsDevice = faceTexture?.GraphicsDevice ?? accessoryTexture?.GraphicsDevice;
            if (faceTexture == null || accessoryTexture == null || graphicsDevice == null)
            {
                return false;
            }

            try
            {
                Color[] compositePixels = CreateTransparentPixelBuffer(layout.Width, layout.Height);
                foreach ((CharacterFrame Frame, Texture2D Texture) layer in EnumerateCompositeLayers(
                    faceFrame,
                    faceTexture,
                    accessoryFrame,
                    accessoryTexture))
                {
                    DrawCompositeLayer(compositePixels, layout.Width, layout.Height, layer.Frame, layer.Texture, layout);
                }

                Texture2D composedTexture = new(graphicsDevice, layout.Width, layout.Height, false, SurfaceFormat.Color);
                composedTexture.SetData(compositePixels);
                compositeTexture = new DXObject(0, 0, composedTexture, delay: 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void DrawCompositeLayer(
            Color[] compositePixels,
            int compositeWidth,
            int compositeHeight,
            CharacterFrame frame,
            Texture2D texture,
            CompositeFaceLayout layout)
        {
            if (compositePixels == null || frame == null || texture == null)
            {
                return;
            }

            Point brow = frame.GetMapPoint("brow");
            int drawX = layout.BrowPoint.X - brow.X;
            int drawY = layout.BrowPoint.Y - brow.Y;
            Color[] sourcePixels = new Color[texture.Width * texture.Height];
            texture.GetData(sourcePixels);

            for (int sourceY = 0; sourceY < texture.Height; sourceY++)
            {
                int targetY = drawY + sourceY;
                if ((uint)targetY >= (uint)compositeHeight)
                {
                    continue;
                }

                for (int sourceX = 0; sourceX < texture.Width; sourceX++)
                {
                    int targetX = drawX + sourceX;
                    if ((uint)targetX >= (uint)compositeWidth)
                    {
                        continue;
                    }

                    int sourceIndex = sourceY * texture.Width + sourceX;
                    int targetIndex = targetY * compositeWidth + targetX;
                    compositePixels[targetIndex] = AlphaBlend(compositePixels[targetIndex], sourcePixels[sourceIndex]);
                }
            }
        }

        private static IEnumerable<(CharacterFrame Frame, Texture2D Texture)> EnumerateCompositeLayers(
            CharacterFrame faceFrame,
            Texture2D faceTexture,
            CharacterFrame accessoryFrame,
            Texture2D accessoryTexture)
        {
            return new[]
                {
                    (Frame: faceFrame, Texture: faceTexture, ZIndex: ZMapReference.GetZIndex(faceFrame?.Z), TieBreak: 1),
                    (Frame: accessoryFrame, Texture: accessoryTexture, ZIndex: ZMapReference.GetZIndex(accessoryFrame?.Z), TieBreak: 0)
                }
                .Where(static layer => layer.Frame != null && layer.Texture != null)
                .OrderBy(static layer => layer.ZIndex)
                .ThenBy(static layer => layer.TieBreak)
                .Select(static layer => (layer.Frame, layer.Texture));
        }

        private static Color[] CreateTransparentPixelBuffer(int width, int height)
        {
            return new Color[Math.Max(0, width) * Math.Max(0, height)];
        }

        private static Color AlphaBlend(Color destination, Color source)
        {
            if (source.A <= 0)
            {
                return destination;
            }

            if (source.A >= byte.MaxValue || destination.A <= 0)
            {
                return source;
            }

            float sourceAlpha = source.A / 255f;
            float destinationAlpha = destination.A / 255f;
            float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
            if (outputAlpha <= 0f)
            {
                return Color.Transparent;
            }

            float outputRed = ((source.R / 255f) * sourceAlpha + (destination.R / 255f) * destinationAlpha * (1f - sourceAlpha)) / outputAlpha;
            float outputGreen = ((source.G / 255f) * sourceAlpha + (destination.G / 255f) * destinationAlpha * (1f - sourceAlpha)) / outputAlpha;
            float outputBlue = ((source.B / 255f) * sourceAlpha + (destination.B / 255f) * destinationAlpha * (1f - sourceAlpha)) / outputAlpha;

            return new Color(
                (byte)Math.Clamp((int)Math.Round(outputRed * 255f), 0, 255),
                (byte)Math.Clamp((int)Math.Round(outputGreen * 255f), 0, 255),
                (byte)Math.Clamp((int)Math.Round(outputBlue * 255f), 0, 255),
                (byte)Math.Clamp((int)Math.Round(outputAlpha * 255f), 0, 255));
        }

        private static bool TryResolveFrameSize(CharacterFrame frame, out int width, out int height)
        {
            width = frame?.Texture?.Width ?? frame?.Bounds.Width ?? 0;
            height = frame?.Texture?.Height ?? frame?.Bounds.Height ?? 0;
            return width > 0 && height > 0;
        }

        private readonly record struct CompositeFaceLayout(
            int Left,
            int Top,
            int Width,
            int Height,
            Point BrowPoint);
    }
}
