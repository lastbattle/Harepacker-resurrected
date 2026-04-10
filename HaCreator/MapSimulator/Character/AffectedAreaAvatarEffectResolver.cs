using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Character
{
    internal static class AffectedAreaAvatarEffectResolver
    {
        internal static bool HasPromotableAffectedBranch(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills = null)
        {
            return TryResolveAuthoredAuraOwnerSkills(
                sourceSkill,
                supportSkills,
                out _);
        }

        internal static bool TryBuildLoopingAvatarEffectSkill(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills,
            out SkillData effectSkill,
            out int signature)
        {
            effectSkill = null;
            signature = 0;
            if (!TryResolveAuthoredAuraOwnerSkills(
                    sourceSkill,
                    supportSkills,
                    out IReadOnlyList<SkillData> ownerSkills))
            {
                return false;
            }

            SkillAnimation overlayAnimation = null;
            SkillAnimation overlaySecondaryAnimation = null;
            SkillAnimation underFaceAnimation = null;
            SkillAnimation underFaceSecondaryAnimation = null;

            foreach (SkillData ownerSkill in ownerSkills)
            {
                AssignAffectedAreaAvatarEffectPlane(
                    CreateLoopingAvatarEffect(ownerSkill.AffectedEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignAffectedAreaAvatarEffectPlane(
                    CreateLoopingAvatarEffect(ownerSkill.AffectedSecondaryEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
            }

            effectSkill = new SkillData
            {
                SkillId = sourceSkill?.SkillId ?? 0,
                AvatarOverlayEffect = overlayAnimation,
                AvatarOverlaySecondaryEffect = overlaySecondaryAnimation,
                AvatarUnderFaceEffect = underFaceAnimation,
                AvatarUnderFaceSecondaryEffect = underFaceSecondaryAnimation
            };
            signature = SkillManager.ComputePersistentAvatarEffectSignature(effectSkill);
            return true;
        }

        private static bool TryResolveAuthoredAuraOwnerSkills(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills,
            out IReadOnlyList<SkillData> ownerSkills)
        {
            var resolvedOwnerSkills = new List<SkillData>();

            foreach (SkillData skill in EnumerateEffectSourceSkills(sourceSkill, supportSkills))
            {
                if (skill?.AffectedEffect?.Frames?.Count > 0
                    || skill?.AffectedSecondaryEffect?.Frames?.Count > 0)
                {
                    resolvedOwnerSkills.Add(skill);
                }
            }

            ownerSkills = resolvedOwnerSkills;
            return resolvedOwnerSkills.Count > 0;
        }

        private static IEnumerable<SkillData> EnumerateEffectSourceSkills(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills)
        {
            var visitedSkillIds = new HashSet<int>();

            if (sourceSkill != null
                && (sourceSkill.SkillId <= 0 || visitedSkillIds.Add(sourceSkill.SkillId)))
            {
                yield return sourceSkill;
            }

            if (supportSkills == null)
            {
                yield break;
            }

            foreach (SkillData supportSkill in supportSkills)
            {
                if (supportSkill == null)
                {
                    continue;
                }

                if (supportSkill.SkillId > 0 && !visitedSkillIds.Add(supportSkill.SkillId))
                {
                    continue;
                }

                yield return supportSkill;
            }
        }

        private static SkillAnimation CreateLoopingAvatarEffect(SkillAnimation animation)
        {
            if (animation?.Frames?.Count <= 0)
            {
                return null;
            }

            return new SkillAnimation
            {
                Name = animation.Name,
                Frames = new List<SkillFrame>(animation.Frames),
                Loop = true,
                Origin = animation.Origin,
                ZOrder = animation.ZOrder,
                PositionCode = animation.PositionCode
            };
        }

        private static void AssignAffectedAreaAvatarEffectPlane(
            SkillAnimation animation,
            ref SkillAnimation overlayAnimation,
            ref SkillAnimation overlaySecondaryAnimation,
            ref SkillAnimation underFaceAnimation,
            ref SkillAnimation underFaceSecondaryAnimation)
        {
            if (animation == null)
            {
                return;
            }

            bool prefersUnderFace = animation.ZOrder < 0;
            if (prefersUnderFace)
            {
                underFaceAnimation ??= animation;
                underFaceSecondaryAnimation ??= ReferenceEquals(animation, underFaceAnimation) ? null : animation;
                return;
            }

            overlayAnimation ??= animation;
            overlaySecondaryAnimation ??= ReferenceEquals(animation, overlayAnimation) ? null : animation;
        }
    }
}
