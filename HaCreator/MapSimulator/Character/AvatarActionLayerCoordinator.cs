using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    internal static class AvatarActionLayerCoordinator
    {
        internal const int MaxClientActionFrameDelay = 1_000_000_000;
        internal const string ClientBodyOriginMapPoint = "clientBodyOrigin";
        internal const string ClientFaceOriginMapPoint = "clientFaceOrigin";
        internal const string ClientMuzzleOriginMapPoint = "clientMuzzleOrigin";
        internal const string ClientTamingMobNavelOriginMapPoint = "clientTamingMobNavelOrigin";
        internal const string ClientTamingMobHeadOriginMapPoint = "clientTamingMobHeadOrigin";
        internal const string ClientTamingMobMuzzleOriginMapPoint = "clientTamingMobMuzzleOrigin";
        private const int MechanicTankModeSkillId = 35121005;

        private static readonly IReadOnlyDictionary<int, int> MechanicTankOneTimeActionRewrites =
            new Dictionary<int, int>
            {
                [240] = 260,
                [241] = 261,
                [259] = 262
            };

        internal static string ResolvePreparedActionName(string actionName, bool isMorphAvatar)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            if (!isMorphAvatar && IsExplicitMountedTransitionActionName(actionName))
            {
                return actionName;
            }

            if (!CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return actionName;
            }

            return IsRawActionAllowed(rawActionCode, isMorphAvatar)
                ? actionName
                : CharacterPart.GetActionString(CharacterAction.Stand1);
        }

        internal static string ResolveMechanicTankOneTimeActionName(string actionName, int mechanicModeSkillId)
        {
            if (mechanicModeSkillId != MechanicTankModeSkillId
                || string.IsNullOrWhiteSpace(actionName)
                || !CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return actionName;
            }

            if (rawActionCode is 59 or 239)
            {
                return null;
            }

            return MechanicTankOneTimeActionRewrites.TryGetValue(rawActionCode, out int rewrittenRawActionCode)
                   && CharacterPart.TryGetActionStringFromCode(rewrittenRawActionCode, out string rewrittenActionName)
                ? rewrittenActionName
                : actionName;
        }

        internal static int ResolvePreparedFrameDelay(
            string actionName,
            int authoredDelay,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            int frameIndex,
            bool isMorphAvatar = false,
            bool isSuperManMorph = false)
        {
            int delay = Math.Max(0, authoredDelay);
            if (heldShootAction && IsHeldShootActionFrame(actionName, frameIndex))
            {
                return MaxClientActionFrameDelay;
            }

            if (isMorphAvatar)
            {
                return ShouldAdaptMorphActionSpeed(actionName, isSuperManMorph)
                    ? ResolveActionSpeedDelay(delay, actionSpeed)
                    : delay;
            }

            if (UsesWalkSpeedTiming(actionName))
            {
                int clampedWalkSpeed = Math.Clamp(walkSpeed, 70, 140);
                return delay * 100 / clampedWalkSpeed;
            }

            return ResolveActionSpeedDelay(delay, actionSpeed);
        }

        internal static int ResolvePreparedAnimationDuration(
            CharacterAnimation animation,
            string actionName,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar = false,
            bool isSuperManMorph = false)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return 0;
            }

            long duration = 0;
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                duration += ResolvePreparedFrameDelay(
                    actionName,
                    animation.Frames[i]?.Delay ?? 0,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    i,
                    isMorphAvatar,
                    isSuperManMorph);
                if (duration >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }

            return (int)duration;
        }

        private static int ResolveActionSpeedDelay(int delay, int actionSpeed)
        {
            int clampedActionSpeed = Math.Clamp(actionSpeed, 2, 10);
            return delay * (clampedActionSpeed + 10) / 16;
        }

        internal static void ApplyMountedOriginRelocation(
            AssembledFrame frame,
            string actionName,
            CharacterFrame characterFrame,
            CharacterFrame tamingMobFrame,
            bool facingRight)
        {
            if (frame == null
                || characterFrame == null
                || tamingMobFrame == null)
            {
                return;
            }

            MountedOriginRelocation relocation = ResolveMountedOriginRelocation(
                characterFrame,
                tamingMobFrame,
                facingRight);
            if (!relocation.HasBodyRelMove)
            {
                return;
            }

            WriteClientMountedOriginMapPoints(frame.MapPoints, relocation);

            Point bodyRelMove = relocation.BodyRelMove;
            if (bodyRelMove == Point.Zero)
            {
                return;
            }

            if (frame.Parts == null)
            {
                return;
            }

            for (int i = 0; i < frame.Parts.Count; i++)
            {
                AssembledPart part = frame.Parts[i];
                if (part == null || IsTamingMobLayer(part))
                {
                    continue;
                }

                part.OffsetX += bodyRelMove.X;
                part.OffsetY += bodyRelMove.Y;
            }

            OffsetMapPoint(frame.MapPoints, "navel", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "neck", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "hand", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "handMove", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "brow", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "muzzle", bodyRelMove);
        }

        internal static MountedOriginRelocation ResolveMountedOriginRelocation(
            CharacterFrame characterFrame,
            CharacterFrame tamingMobFrame,
            bool facingRight)
        {
            var relocation = new MountedOriginRelocation();
            if (characterFrame == null || tamingMobFrame == null)
            {
                return relocation;
            }

            if (!TryGetMapPoint(characterFrame, "navel", out Point characterNavel)
                || !TryGetMapPoint(tamingMobFrame, "navel", out Point tamingMobNavel))
            {
                return relocation;
            }

            relocation.HasBodyRelMove = true;
            relocation.BodyRelMove = ApplyClientFlip(
                new Point(tamingMobNavel.X - characterNavel.X, tamingMobNavel.Y - characterNavel.Y),
                facingRight);

            if (TryGetMapPoint(characterFrame, "brow", out Point faceOrigin))
            {
                relocation.FaceOrigin = ApplyClientFlip(faceOrigin, facingRight);
            }

            if (TryGetMapPoint(characterFrame, "muzzle", out Point muzzleOrigin))
            {
                relocation.MuzzleOrigin = ApplyClientFlip(muzzleOrigin, facingRight);
            }

            relocation.TamingMobNavelOrigin = ApplyClientFlip(tamingMobNavel, facingRight);

            if (TryGetMapPoint(tamingMobFrame, "head", out Point tamingMobHeadOrigin))
            {
                relocation.TamingMobHeadOrigin = ApplyClientFlip(tamingMobHeadOrigin, facingRight);
            }

            if (TryGetMapPoint(tamingMobFrame, "muzzle", out Point tamingMobMuzzleOrigin))
            {
                relocation.TamingMobMuzzleOrigin = ApplyClientFlip(tamingMobMuzzleOrigin, facingRight);
            }

            return relocation;
        }

        internal static bool IsRawActionAllowed(int rawActionCode, bool isMorphAvatar)
        {
            return rawActionCode >= 0 && rawActionCode < (isMorphAvatar ? 49 : 273);
        }

        private static bool IsExplicitMountedTransitionActionName(string actionName)
        {
            return string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class MountedOriginRelocation
        {
            public bool HasBodyRelMove { get; set; }
            public Point BodyRelMove { get; set; }
            public Point? FaceOrigin { get; set; }
            public Point? MuzzleOrigin { get; set; }
            public Point? TamingMobNavelOrigin { get; set; }
            public Point? TamingMobHeadOrigin { get; set; }
            public Point? TamingMobMuzzleOrigin { get; set; }
        }

        private static bool UsesWalkSpeedTiming(string actionName)
        {
            if (!CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return false;
            }

            return rawActionCode < 2 || rawActionCode == 124;
        }

        private static bool ShouldAdaptMorphActionSpeed(string actionName, bool isSuperManMorph)
        {
            return isSuperManMorph
                   && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                   && rawActionCode != 0;
        }

        private static bool IsHeldShootActionFrame(string actionName, int frameIndex)
        {
            if (frameIndex < 0
                || string.IsNullOrWhiteSpace(actionName)
                || !CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                || !IsShootActionName(actionName))
            {
                return false;
            }

            return rawActionCode switch
            {
                122 => frameIndex == 0 || frameIndex == 4,
                31 => frameIndex == 2 || frameIndex == 4,
                _ => frameIndex == 4
            };
        }

        private static bool IsShootActionName(string actionName)
        {
            return actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                   || actionName.Contains("shot", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "demolition", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "windspear", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "windshot", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rapidfire", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "cannon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "torpedo", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTamingMobLayer(AssembledPart part)
        {
            return part.PartType == CharacterPartType.TamingMob
                   || part.SourcePart?.Slot == EquipSlot.TamingMob;
        }

        private static void WriteClientMountedOriginMapPoints(
            Dictionary<string, Point> mapPoints,
            MountedOriginRelocation relocation)
        {
            if (mapPoints == null || relocation == null)
            {
                return;
            }

            mapPoints[ClientBodyOriginMapPoint] = relocation.BodyRelMove;
            WriteOptionalMapPoint(mapPoints, ClientFaceOriginMapPoint, relocation.FaceOrigin);
            WriteOptionalMapPoint(mapPoints, ClientMuzzleOriginMapPoint, relocation.MuzzleOrigin);
            WriteOptionalMapPoint(mapPoints, ClientTamingMobNavelOriginMapPoint, relocation.TamingMobNavelOrigin);
            WriteOptionalMapPoint(mapPoints, ClientTamingMobHeadOriginMapPoint, relocation.TamingMobHeadOrigin);
            WriteOptionalMapPoint(mapPoints, ClientTamingMobMuzzleOriginMapPoint, relocation.TamingMobMuzzleOrigin);
        }

        private static void WriteOptionalMapPoint(Dictionary<string, Point> mapPoints, string name, Point? point)
        {
            if (point.HasValue)
            {
                mapPoints[name] = point.Value;
            }
        }

        private static void OffsetMapPoint(Dictionary<string, Point> mapPoints, string name, Point offset)
        {
            if (mapPoints == null || !mapPoints.TryGetValue(name, out Point point))
            {
                return;
            }

            mapPoints[name] = new Point(point.X + offset.X, point.Y + offset.Y);
        }

        private static bool TryGetMapPoint(CharacterFrame frame, string name, out Point point)
        {
            point = Point.Zero;
            return frame?.Map != null && frame.Map.TryGetValue(name, out point);
        }

        private static Point ApplyClientFlip(Point point, bool facingRight)
        {
            return facingRight ? point : new Point(-point.X, point.Y);
        }
    }
}
