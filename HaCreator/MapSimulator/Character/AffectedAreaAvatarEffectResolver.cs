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
            return ResolveAuthoredAuraOwnerSkill(sourceSkill, supportSkills) != null;
        }

        internal static SkillData BuildLoopingAvatarEffectSkill(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills = null)
        {
            SkillData ownerSkill = ResolveAuthoredAuraOwnerSkill(sourceSkill, supportSkills);
            if (ownerSkill == null)
            {
                return null;
            }

            SkillAnimation overlayAnimation = null;
            SkillAnimation overlaySecondaryAnimation = null;
            SkillAnimation underFaceAnimation = null;
            SkillAnimation underFaceSecondaryAnimation = null;

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

            return new SkillData
            {
                SkillId = sourceSkill?.SkillId ?? 0,
                AvatarOverlayEffect = overlayAnimation,
                AvatarOverlaySecondaryEffect = overlaySecondaryAnimation,
                AvatarUnderFaceEffect = underFaceAnimation,
                AvatarUnderFaceSecondaryEffect = underFaceSecondaryAnimation
            };
        }

        internal static SkillData ResolveAuthoredAuraOwnerSkill(
            SkillData sourceSkill,
            IReadOnlyCollection<SkillData> supportSkills = null)
        {
            foreach (SkillData skill in EnumerateEffectSourceSkills(sourceSkill, supportSkills))
            {
                if (skill?.AffectedEffect?.Frames?.Count > 0
                    || skill?.AffectedSecondaryEffect?.Frames?.Count > 0)
                {
                    return skill;
                }
            }

            return null;
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
                ZOrder = animation.ZOrder
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
