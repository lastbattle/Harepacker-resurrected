using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character.Skills;

public static class SummonImpactPresentationResolver
{
    private const float SourceVerticalOffset = 25f;
    private const float MinimumFallbackTargetHeightOffset = 20f;
    private const float TargetEdgeInset = 10f;
    private const float MaxTargetDistance = 600f;
    private const int SourceAnchorPositionCode = 2;
    private const int TargetCenterPositionCode = 3;

    public static Vector2 ResolveImpactPosition(
        SummonImpactPresentation presentation,
        Rectangle targetHitbox,
        Vector2 source,
        Vector2 fallbackTargetPosition)
    {
        if (presentation?.PositionCode == SourceAnchorPositionCode)
        {
            return source;
        }

        Vector2 sourceAnchor = ResolveSourceAnchor(source);
        if (targetHitbox.IsEmpty)
        {
            return ApplyDistanceCap(sourceAnchor, fallbackTargetPosition);
        }

        float centerX = targetHitbox.Left + targetHitbox.Width * 0.5f;
        float clampedY = MathHelper.Clamp(sourceAnchor.Y, targetHitbox.Top, targetHitbox.Bottom);
        float targetX = ResolveTargetAnchorX(presentation?.PositionCode, targetHitbox, source.X, centerX);

        return ApplyDistanceCap(sourceAnchor, new Vector2(targetX, clampedY));
    }

    public static Vector2 ResolveSourceAnchor(Vector2 source)
    {
        return new Vector2(source.X, source.Y - SourceVerticalOffset);
    }

    public static Vector2 ResolveFallbackTargetPosition(Vector2 targetPosition, float visualHeight)
    {
        return new Vector2(
            targetPosition.X,
            targetPosition.Y - Math.Max(MinimumFallbackTargetHeightOffset, visualHeight * 0.5f));
    }

    private static float ResolveTargetAnchorX(int? positionCode, Rectangle targetHitbox, float sourceX, float centerX)
    {
        if (positionCode == TargetCenterPositionCode)
        {
            return centerX;
        }

        float inset = Math.Min(TargetEdgeInset, targetHitbox.Width * 0.5f);
        return sourceX > centerX
            ? Math.Max(centerX, targetHitbox.Right - inset)
            : Math.Min(centerX, targetHitbox.Left + inset);
    }

    private static Vector2 ApplyDistanceCap(Vector2 sourceAnchor, Vector2 resolvedTarget)
    {
        Vector2 delta = resolvedTarget - sourceAnchor;
        float maxDistanceSq = MaxTargetDistance * MaxTargetDistance;
        float distanceSq = delta.LengthSquared();
        if (distanceSq <= 0f || distanceSq <= maxDistanceSq)
        {
            return resolvedTarget;
        }

        return sourceAnchor + Vector2.Normalize(delta) * MaxTargetDistance;
    }
}
