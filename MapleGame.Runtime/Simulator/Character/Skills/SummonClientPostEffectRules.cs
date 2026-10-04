using System;
using HaCreator.MapSimulator.Animation;
using Microsoft.Xna.Framework;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills;

internal static class SummonClientPostEffectRules
{
    private const int ClientConfirmedPhoenixLegacySkillId = 3121006;
    private const int ClientConfirmedPhoenixCurrentSkillId = 3120010;
    private const int ClientConfirmedSilverHawkLegacySkillId = 3111005;
    private const int ClientConfirmedFrostpreyLegacySkillId = 3221005;
    private const int ClientConfirmedFrostpreyCurrentSkillId = 3211005;
    private const int ClientConfirmedShadowMesoReactiveSkillId = 4111007;
    private const int ClientConfirmedShadowMesoReactiveSkillAliasId = 4211007;
    private const int ClientConfirmedTeslaCoilSkillId = 35111002;
    private const int ClientSummonedAttackDelayBaselineMs = 300;
    private const float ClientConfirmedReactiveChainSourceOffsetX = 25f;
    private const float ClientConfirmedReactiveChainSourceOffsetY = -25f;
    private const float ClientConfirmedReactiveChainMaxDistance = 600f;
    private const int ClientConfirmedReactiveChainTargetInset = 10;

    public static bool ShouldRegisterAttackTileOverlay(
        int skillId,
        SkillData skillData,
        SkillAnimation zoneAnimation,
        int skillLevel,
        int ownerCharacterLevel)
    {
        if (zoneAnimation?.Frames.Count <= 0
            && !HasAuthoredTileOverlayPath(skillData, skillLevel, ownerCharacterLevel))
        {
            return false;
        }

        return skillId == ClientConfirmedPhoenixLegacySkillId
               || skillId == ClientConfirmedPhoenixCurrentSkillId
               || skillId == ClientConfirmedSilverHawkLegacySkillId
               || skillId == ClientConfirmedFrostpreyLegacySkillId
               || skillId == ClientConfirmedFrostpreyCurrentSkillId
               || HasAuthoredTileOverlayPath(skillData, skillLevel, ownerCharacterLevel);
    }

    public static bool ShouldRegisterReactiveAttackChainEffect(int skillId, SkillData skillData)
    {
        return IsReactiveAttackChainSkill(skillId)
               && HasClientOwnedReactiveAttackChainBallVisual(skillData?.Projectile);
    }

    public static SkillAnimation ResolveAttackTileOverlayAnimation(
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel)
    {
        return skillData?.ZoneEffect?.ResolveAnimationVariant(
                   Math.Max(1, skillLevel),
                   Math.Max(1, ownerCharacterLevel),
                   skillData.MaxLevel)
               ?? skillData?.ZoneAnimation;
    }

    public static string ResolveAttackTileOverlayAnimationPath(
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel)
    {
        if (skillData == null)
        {
            return null;
        }

        return skillData.ZoneEffect?.ResolveTileUolPath(
                   Math.Max(1, skillLevel),
                   Math.Max(1, ownerCharacterLevel))
               ?? skillData.ZoneEffect?.ResolveAnimationVariantPath(
                   Math.Max(1, skillLevel),
                   Math.Max(1, ownerCharacterLevel),
                   skillData.MaxLevel);
    }

    public static int ResolveAttackTileOverlayEffectDistance(
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel)
    {
        return skillData?.ZoneEffect?.ResolveEffectDistanceVariant(
                   Math.Max(1, skillLevel),
                   Math.Max(1, ownerCharacterLevel))
               ?? 0;
    }

    public static bool IsReactiveAttackChainSkill(int skillId)
    {
        return skillId == ClientConfirmedShadowMesoReactiveSkillId
               || skillId == ClientConfirmedShadowMesoReactiveSkillAliasId;
    }

    public static int ResolvePostAttackEffectDelayMs(SkillData skillData, string branchName = null)
    {
        return Math.Max(0, skillData?.ResolveExplicitSummonAttackAfterMs(branchName) ?? 0);
    }

    public static int ResolveRemainingPostAttackEffectDelayMs(
        SkillData skillData,
        string branchName,
        int attackStartedAt,
        int currentTime)
    {
        int attackDelayMs = ResolvePostAttackEffectDelayMs(skillData, branchName);
        int elapsedMs = Math.Max(0, currentTime - attackStartedAt);
        return Math.Max(0, attackDelayMs - elapsedMs);
    }

    public static int ResolveSummonedAttackImpactDelayMs(
        int skillId,
        int attackAfterMs,
        int summonedAttackDelayMs,
        Func<int, int> randomNextExclusive)
    {
        int resolvedAttackAfterMs = Math.Max(0, attackAfterMs);
        if (skillId != ClientConfirmedTeslaCoilSkillId)
        {
            return resolvedAttackAfterMs;
        }

        int jitterWindowMs = Math.Max(0, summonedAttackDelayMs - ClientSummonedAttackDelayBaselineMs);
        if (jitterWindowMs <= 0)
        {
            return resolvedAttackAfterMs;
        }

        int jitterMs = randomNextExclusive?.Invoke(jitterWindowMs) ?? 0;
        return resolvedAttackAfterMs + Math.Clamp(jitterMs, 0, jitterWindowMs - 1);
    }

    public static SkillAnimation ResolvePassiveEffectAnimation(SkillData skillData)
    {
        return ResolveSummonNamedEffectAnimation(skillData, secondary: false)
               ?? skillData?.Effect
               ?? skillData?.AffectedEffect;
    }

    public static SkillAnimation ResolvePassiveSecondaryEffectAnimation(SkillData skillData)
    {
        return ResolveSummonNamedEffectAnimation(skillData, secondary: true)
               ?? skillData?.EffectSecondary
               ?? skillData?.AffectedSecondaryEffect;
    }

    public static (Vector2 Source, Vector2 Target) ResolveReactiveAttackChainEndpoints(
        Vector2 summonPosition,
        Rectangle targetHitbox,
        bool facingRight)
    {
        Vector2 sourceAnchor = new(summonPosition.X, summonPosition.Y + ClientConfirmedReactiveChainSourceOffsetY);
        Vector2 chainSource = new(
            sourceAnchor.X + (facingRight ? ClientConfirmedReactiveChainSourceOffsetX : -ClientConfirmedReactiveChainSourceOffsetX),
            sourceAnchor.Y);
        Vector2 chainTarget = sourceAnchor;

        if (targetHitbox.Width > 0 && targetHitbox.Height > 0)
        {
            float centerX = (targetHitbox.Left + targetHitbox.Right) * 0.5f;
            float insetLeft = targetHitbox.Left + ClientConfirmedReactiveChainTargetInset;
            float insetRight = targetHitbox.Right - ClientConfirmedReactiveChainTargetInset;
            float targetX = centerX < summonPosition.X
                ? Math.Max(centerX, insetRight)
                : Math.Min(centerX, insetLeft);
            float targetY = MathHelper.Clamp(sourceAnchor.Y, targetHitbox.Top, targetHitbox.Bottom);
            chainTarget = new Vector2(targetX, targetY);

            Vector2 delta = chainTarget - sourceAnchor;
            float distance = delta.Length();
            if (distance > ClientConfirmedReactiveChainMaxDistance && distance > 0f)
            {
                chainTarget = sourceAnchor + delta / distance * ClientConfirmedReactiveChainMaxDistance;
            }
        }

        return (chainSource, chainTarget);
    }

    public static SkillAnimation ResolveReactiveAttackChainAnimation(
        int skillId,
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel,
        bool flip = false)
    {
        if (!IsReactiveAttackChainSkill(skillId))
        {
            return null;
        }

        return skillData?.Projectile?.ResolveGetBallLikeAnimation(
            Math.Max(1, skillLevel),
            Math.Max(1, ownerCharacterLevel),
            flip,
            skillData.MaxLevel);
    }

    public static string ResolveReactiveAttackChainAnimationPath(
        int skillId,
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel,
        bool flip = false)
    {
        if (!IsReactiveAttackChainSkill(skillId))
        {
            return null;
        }

        return skillData?.Projectile?.ResolveGetBallLikeUolPath(
                   Math.Max(1, skillLevel),
                   Math.Max(1, ownerCharacterLevel),
                   flip,
                   skillData.MaxLevel)
               ?? skillData?.Projectile?.ResolveGetBallLikeAnimationPath(
                   Math.Max(1, skillLevel),
                   Math.Max(1, ownerCharacterLevel),
                   skillData.MaxLevel);
    }

    public static bool ShouldUseGenericReactiveAttackChainFallback(
        int skillId,
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel,
        bool flip = false)
    {
        if (!IsReactiveAttackChainSkill(skillId))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(ResolveReactiveAttackChainAnimationPath(
            skillId,
            skillData,
            skillLevel,
            ownerCharacterLevel,
            flip));
    }

    public static Rectangle BuildAttackTileOverlayArea(
        Vector2 anchor,
        SkillData skillData,
        string branchName = null)
    {
        Rectangle authoredRange = Rectangle.Empty;
        if (skillData != null)
        {
            skillData.TryGetSummonAttackRange(
                facingRight: true,
                branchName,
                out authoredRange);
        }

        int left = authoredRange.X;
        int top = authoredRange.Y;
        int width = authoredRange.Width;
        int height = authoredRange.Height;

        if (width <= 0 || height <= 0)
        {
            int fallbackLeft = skillData?.SummonAttackRangeLeft ?? 0;
            int fallbackRight = skillData?.SummonAttackRangeRight ?? 0;
            int fallbackTop = skillData?.SummonAttackRangeTop ?? 0;
            int fallbackBottom = skillData?.SummonAttackRangeBottom ?? 0;
            left = fallbackLeft;
            top = fallbackTop;
            width = Math.Max(1, fallbackRight - fallbackLeft);
            height = Math.Max(1, fallbackBottom - fallbackTop);
        }

        int x = (int)MathF.Round(anchor.X) + left;
        int y = (int)MathF.Round(anchor.Y) + top;
        return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height + 100));
    }

    private static bool HasClientOwnedReactiveAttackChainBallVisual(ProjectileData projectile)
    {
        if (projectile == null)
        {
            return false;
        }

        if (projectile.Animation?.Frames.Count > 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(projectile.BallUolPath)
            || !string.IsNullOrWhiteSpace(projectile.FlipBallUolPath)
            || !string.IsNullOrWhiteSpace(projectile.AnimationPath))
        {
            return true;
        }

        if (projectile.VariantAnimations?.Any(static animation => animation?.Frames.Count > 0) == true)
        {
            return true;
        }

        if (projectile.VariantAnimationPaths?.Any(static path => !string.IsNullOrWhiteSpace(path)) == true)
        {
            return true;
        }

        if (projectile.CharacterLevelVariantAnimations?.Values.Any(
                static animations => animations?.Any(animation => animation?.Frames.Count > 0) == true) == true)
        {
            return true;
        }

        if (projectile.CharacterLevelVariantAnimationPaths?.Values.Any(
                static paths => paths?.Any(path => !string.IsNullOrWhiteSpace(path)) == true) == true)
        {
            return true;
        }

        if (projectile.CharacterLevelBallUolPaths?.Values.Any(static path => !string.IsNullOrWhiteSpace(path)) == true
            || projectile.CharacterLevelFlipBallUolPaths?.Values.Any(static path => !string.IsNullOrWhiteSpace(path)) == true)
        {
            return true;
        }

        if (projectile.LevelVariantAnimations?.Values.Any(
                static animations => animations?.Any(animation => animation?.Frames.Count > 0) == true) == true)
        {
            return true;
        }

        if (projectile.LevelVariantAnimationPaths?.Values.Any(
                static paths => paths?.Any(path => !string.IsNullOrWhiteSpace(path)) == true) == true)
        {
            return true;
        }

        return projectile.LevelBallUolPaths?.Values.Any(static path => !string.IsNullOrWhiteSpace(path)) == true
               || projectile.LevelFlipBallUolPaths?.Values.Any(static path => !string.IsNullOrWhiteSpace(path)) == true;
    }

    private static bool HasAuthoredTileOverlayPath(
        SkillData skillData,
        int skillLevel,
        int ownerCharacterLevel)
    {
        if (skillData?.ZoneEffect == null)
        {
            return false;
        }

        int resolvedSkillLevel = Math.Max(1, skillLevel);
        int resolvedOwnerCharacterLevel = Math.Max(1, ownerCharacterLevel);
        string tileUolPath = skillData.ZoneEffect.ResolveTileUolPath(resolvedSkillLevel, resolvedOwnerCharacterLevel);
        if (HasTileFamilyMarker(tileUolPath))
        {
            return true;
        }

        string variantPath = skillData.ZoneEffect.ResolveAnimationVariantPath(
            resolvedSkillLevel,
            resolvedOwnerCharacterLevel,
            skillData.MaxLevel);
        return HasTileFamilyMarker(variantPath);
    }

    private static bool HasTileFamilyMarker(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalizedPath = path.Replace('\\', '/');
        return normalizedPath.Contains("/tile/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.EndsWith("/tile", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedPath, "tile", StringComparison.OrdinalIgnoreCase);
    }

    private static SkillAnimation ResolveSummonNamedEffectAnimation(SkillData skillData, bool secondary)
    {
        if (skillData?.SummonNamedAnimations == null || skillData.SummonNamedAnimations.Count == 0)
        {
            return null;
        }

        string[] preferredBranchNames = secondary
            ? new[] { "effect0", "repeat0" }
            : new[] { "effect", "repeat" };
        foreach (string branchName in preferredBranchNames)
        {
            if (skillData.SummonNamedAnimations.TryGetValue(branchName, out SkillAnimation animation)
                && animation?.Frames.Count > 0)
            {
                return animation;
            }
        }

        return null;
    }
}
