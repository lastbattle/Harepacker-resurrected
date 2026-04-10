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
    private const float ClientConfirmedReactiveChainSourceOffsetX = 25f;
    private const float ClientConfirmedReactiveChainSourceOffsetY = -25f;
    private const float ClientConfirmedReactiveChainMaxDistance = 600f;
    private const int ClientConfirmedReactiveChainTargetInset = 10;

    public static bool ShouldRegisterAttackTileOverlay(int skillId, SkillAnimation zoneAnimation)
    {
        if (zoneAnimation?.Frames.Count <= 0)
        {
            return false;
        }

        return skillId == ClientConfirmedPhoenixLegacySkillId
               || skillId == ClientConfirmedPhoenixCurrentSkillId
               || skillId == ClientConfirmedSilverHawkLegacySkillId
               || skillId == ClientConfirmedFrostpreyLegacySkillId
               || skillId == ClientConfirmedFrostpreyCurrentSkillId;
    }

    public static bool ShouldRegisterReactiveAttackChainEffect(int skillId, SkillData skillData)
    {
        return IsReactiveAttackChainSkill(skillId)
               && HasClientOwnedReactiveAttackChainVisual(skillData);
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

    private static bool HasClientOwnedReactiveAttackChainVisual(SkillData skillData)
    {
        if (skillData == null)
        {
            return false;
        }

        if (skillData.Projectile?.Animation?.Frames.Count > 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(skillData.Projectile?.BallUolPath)
            || !string.IsNullOrWhiteSpace(skillData.Projectile?.FlipBallUolPath)
            || !string.IsNullOrWhiteSpace(skillData.Projectile?.AnimationPath))
        {
            return true;
        }

        if (skillData.SummonProjectileAnimations?.Count > 0)
        {
            return true;
        }

        return skillData.SummonProjectileAnimationsByBranch?.Values.Any(
            static animations => animations?.Count > 0) == true;
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
