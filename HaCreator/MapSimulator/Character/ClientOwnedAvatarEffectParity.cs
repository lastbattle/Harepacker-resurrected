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
