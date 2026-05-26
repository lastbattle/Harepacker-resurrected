using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    public static class ClientOwnedAvatarEffectParity
    {
        private const int FaceOwnedPlanePositionCode = 1;
        private const int OverFaceOwnedPlanePositionCode = 2;
        private const int EnergyChargeAdditionalLayerRefCount = 1;
        private const int EnergyChargeParentUnderFaceRefCount = 1;
        private const int EnergyChargeListNodeRefCount = 1;
        private static readonly HashSet<int> RotateSensitiveRawActionCodes = new()
        {
            101,
            82,
            114,
            149,
            151,
            193
        };

        private static readonly HashSet<string> RotateSensitiveActionNames =
            BuildRotateSensitiveActionNames();

        public enum EnergyChargeAdditionalLayerMutationKind
        {
            CaptureOwnerReference = 0,
            CaptureParentUnderFaceReference = 1,
            ReparentToUnderFace = 2,
            RestoreAlpha = 3,
            AnimateRepeat = 4,
            ReleaseParentUnderFaceReference = 5,
            ReleaseOwnerReference = 6
        }

        public readonly record struct EnergyChargeAdditionalLayerMutation(
            EnergyChargeAdditionalLayerMutationKind Kind,
            int AdditionalLayerIndex,
            int SimulatedLayerHandleId,
            int SimulatedListNodeId,
            int SimulatedParentUnderFaceLayerHandleId,
            int SimulatedLayerHandleRefCount,
            int SimulatedListNodeRefCount,
            int SimulatedParentUnderFaceLayerRefCount,
            int Alpha,
            string AnimationMode,
            string SourceEffectName)
        {
            public static EnergyChargeAdditionalLayerMutation CaptureOwnerReference(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.CaptureOwnerReference,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId: 0,
                    layerRefCount: EnergyChargeAdditionalLayerRefCount,
                    listNodeRefCount: EnergyChargeListNodeRefCount,
                    parentRefCount: 0,
                    alpha: 0,
                    animationMode: null,
                    sourceEffectName);
            }

            public static EnergyChargeAdditionalLayerMutation CaptureParentUnderFaceReference(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                int parentUnderFaceLayerHandleId,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.CaptureParentUnderFaceReference,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId,
                    layerRefCount: EnergyChargeAdditionalLayerRefCount,
                    listNodeRefCount: EnergyChargeListNodeRefCount,
                    parentRefCount: EnergyChargeParentUnderFaceRefCount,
                    alpha: 0,
                    animationMode: null,
                    sourceEffectName);
            }

            public static EnergyChargeAdditionalLayerMutation ReparentToUnderFace(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                int parentUnderFaceLayerHandleId,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.ReparentToUnderFace,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId,
                    layerRefCount: EnergyChargeAdditionalLayerRefCount,
                    listNodeRefCount: EnergyChargeListNodeRefCount,
                    parentRefCount: EnergyChargeParentUnderFaceRefCount,
                    alpha: 0,
                    animationMode: null,
                    sourceEffectName);
            }

            public static EnergyChargeAdditionalLayerMutation RestoreAlpha(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                int parentUnderFaceLayerHandleId,
                int alpha,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.RestoreAlpha,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId,
                    layerRefCount: EnergyChargeAdditionalLayerRefCount,
                    listNodeRefCount: EnergyChargeListNodeRefCount,
                    parentRefCount: EnergyChargeParentUnderFaceRefCount,
                    alpha,
                    animationMode: null,
                    sourceEffectName);
            }

            public static EnergyChargeAdditionalLayerMutation AnimateRepeat(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                int parentUnderFaceLayerHandleId,
                string animationMode,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.AnimateRepeat,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId,
                    layerRefCount: EnergyChargeAdditionalLayerRefCount,
                    listNodeRefCount: EnergyChargeListNodeRefCount,
                    parentRefCount: EnergyChargeParentUnderFaceRefCount,
                    alpha: 0,
                    animationMode,
                    sourceEffectName);
            }

            public static EnergyChargeAdditionalLayerMutation ReleaseParentUnderFaceReference(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                int parentUnderFaceLayerHandleId,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.ReleaseParentUnderFaceReference,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId,
                    layerRefCount: EnergyChargeAdditionalLayerRefCount,
                    listNodeRefCount: EnergyChargeListNodeRefCount,
                    parentRefCount: 0,
                    alpha: 0,
                    animationMode: null,
                    sourceEffectName);
            }

            public static EnergyChargeAdditionalLayerMutation ReleaseOwnerReference(
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                string sourceEffectName)
            {
                return Create(
                    EnergyChargeAdditionalLayerMutationKind.ReleaseOwnerReference,
                    additionalLayerIndex,
                    layerHandleId,
                    listNodeId,
                    parentUnderFaceLayerHandleId: 0,
                    layerRefCount: 0,
                    listNodeRefCount: 0,
                    parentRefCount: 0,
                    alpha: 0,
                    animationMode: null,
                    sourceEffectName);
            }

            private static EnergyChargeAdditionalLayerMutation Create(
                EnergyChargeAdditionalLayerMutationKind kind,
                int additionalLayerIndex,
                int layerHandleId,
                int listNodeId,
                int parentUnderFaceLayerHandleId,
                int layerRefCount,
                int listNodeRefCount,
                int parentRefCount,
                int alpha,
                string animationMode,
                string sourceEffectName)
            {
                return new EnergyChargeAdditionalLayerMutation(
                    kind,
                    Math.Max(0, additionalLayerIndex),
                    Math.Max(0, layerHandleId),
                    Math.Max(0, listNodeId),
                    Math.Max(0, parentUnderFaceLayerHandleId),
                    Math.Max(0, layerRefCount),
                    Math.Max(0, listNodeRefCount),
                    Math.Max(0, parentRefCount),
                    MathHelper.Clamp(alpha, 0, 255),
                    animationMode ?? string.Empty,
                    sourceEffectName ?? string.Empty);
            }
        }

        public static bool ShouldHideDuringPlayerAction(params string[] actionNames)
        {
            return ShouldHideDuringPlayerAction(null, actionNames);
        }

        public static bool ShouldHideDuringPlayerAction(int? rawActionCode, params string[] actionNames)
        {
            if (rawActionCode.HasValue && RotateSensitiveRawActionCodes.Contains(rawActionCode.Value))
            {
                return true;
            }

            if (actionNames == null || actionNames.Length == 0)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < actionNames.Length; i++)
            {
                string actionName = actionNames[i];
                if (string.IsNullOrWhiteSpace(actionName) || !seen.Add(actionName))
                {
                    continue;
                }

                if (RotateSensitiveActionNames.Contains(actionName))
                {
                    return true;
                }

                if (TryParseSyntheticRawActionCode(actionName, out int syntheticRawActionCode)
                    && RotateSensitiveRawActionCodes.Contains(syntheticRawActionCode))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool PrefersUnderFaceAvatarEffectPlane(Skills.SkillAnimation animation)
        {
            return animation != null
                   && PrefersUnderFaceAvatarEffectPlane(animation.PositionCode, animation.ZOrder);
        }

        internal static bool PrefersUnderFaceAvatarEffectPlane(Skills.SkillAnimation animation, bool isClientMovementOwner)
        {
            return animation != null
                   && PrefersUnderFaceAvatarEffectPlane(animation.PositionCode, animation.ZOrder, isClientMovementOwner);
        }

        internal static bool PrefersUnderFaceAvatarEffectPlane(int? positionCode, int zOrder)
        {
            return PrefersUnderFaceAvatarEffectPlane(positionCode, zOrder, isClientMovementOwner: false);
        }

        internal static bool PrefersUnderFaceAvatarEffectPlane(int? positionCode, int zOrder, bool isClientMovementOwner)
        {
            return positionCode == FaceOwnedPlanePositionCode
                   || zOrder < 0
                   || (isClientMovementOwner && !positionCode.HasValue && zOrder == 0);
        }

        internal static bool PrefersOverFaceAvatarEffectPlane(Skills.SkillAnimation animation)
        {
            return animation != null
                   && PrefersOverFaceAvatarEffectPlane(animation.PositionCode, animation.ZOrder);
        }

        internal static bool PrefersOverFaceAvatarEffectPlane(int? positionCode, int zOrder)
        {
            return positionCode == OverFaceOwnedPlanePositionCode
                   && zOrder >= 0;
        }

        internal static bool TryResolveFaceOwnedAvatarEffectAnchor(
            AssembledFrame assembledFrame,
            bool facingRight,
            int screenX,
            int screenY,
            int? positionCode,
            out int anchorX,
            out int anchorY)
        {
            anchorX = screenX;
            anchorY = screenY;

            if (positionCode != FaceOwnedPlanePositionCode
                || assembledFrame?.MapPoints == null)
            {
                return false;
            }

            if (assembledFrame.MapPoints.TryGetValue(AvatarActionLayerCoordinator.ClientFaceOriginMapPoint, out Point mountedFaceOrigin))
            {
                anchorX = screenX + mountedFaceOrigin.X;
                anchorY = screenY - assembledFrame.FeetOffset + mountedFaceOrigin.Y;
                return true;
            }

            if (!assembledFrame.MapPoints.TryGetValue("brow", out Point anchorPoint))
            {
                return false;
            }

            anchorX = facingRight
                ? screenX + anchorPoint.X
                : screenX - anchorPoint.X;
            anchorY = screenY - assembledFrame.FeetOffset + anchorPoint.Y;
            return true;
        }

        internal static int ResolveUnsignedTickElapsedMs(int currentTime, int startTime)
        {
            if (currentTime == int.MinValue || startTime == int.MinValue)
            {
                return 0;
            }

            long elapsed = unchecked((uint)(currentTime - startTime));
            return elapsed >= int.MaxValue
                ? int.MaxValue
                : (int)elapsed;
        }

        internal static bool HasUnsignedTickReached(int currentTime, int targetTime)
        {
            if (currentTime == int.MinValue || targetTime == int.MinValue)
            {
                return false;
            }

            return unchecked((uint)(currentTime - targetTime)) < int.MaxValue;
        }

        private static HashSet<string> BuildRotateSensitiveActionNames()
        {
            var actionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "doubleJump",
                "backspin",
                "rollingSpin",
                "darkSpin",
                "screw",
                "somersault",
                "finalCut"
            };

            foreach (int rawActionCode in RotateSensitiveRawActionCodes)
            {
                if (CharacterPart.TryGetActionStringFromCode(rawActionCode, out string actionName)
                    && !string.IsNullOrWhiteSpace(actionName))
                {
                    actionNames.Add(actionName);
                }
            }

            return actionNames;
        }

        private static bool TryParseSyntheticRawActionCode(string actionName, out int rawActionCode)
        {
            rawActionCode = 0;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            ReadOnlySpan<char> actionSpan = actionName.AsSpan().Trim();
            int markerIndex = actionSpan.LastIndexOf('*');
            if (markerIndex < 0 || markerIndex >= actionSpan.Length - 1)
            {
                return false;
            }

            ReadOnlySpan<char> rawActionSpan = actionSpan[(markerIndex + 1)..];
            return int.TryParse(
                       rawActionSpan,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out rawActionCode)
                   && rawActionCode > 0;
        }
    }
}
