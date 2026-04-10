using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    internal static class AvatarActionLayerCoordinator
    {
        internal const int MaxClientActionFrameDelay = 1_000_000_000;
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
            int frameIndex)
        {
            int delay = Math.Max(0, authoredDelay);
            if (heldShootAction && IsHeldShootActionFrame(actionName, frameIndex))
            {
                return MaxClientActionFrameDelay;
            }

            if (UsesWalkSpeedTiming(actionName))
            {
                int clampedWalkSpeed = Math.Clamp(walkSpeed, 70, 140);
                return delay * 100 / clampedWalkSpeed;
            }

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
            if (frame?.Parts == null
                || frame.Parts.Count == 0
                || characterFrame == null
                || tamingMobFrame == null
                || !tamingMobFrame.Map.TryGetValue("navel", out Point tamingMobNavel)
                || !characterFrame.Map.TryGetValue("navel", out Point characterNavel))
            {
                return;
            }

            Point bodyRelMove = new(tamingMobNavel.X - characterNavel.X, tamingMobNavel.Y - characterNavel.Y);
            if (!facingRight)
            {
                bodyRelMove.X = -bodyRelMove.X;
            }

            if (bodyRelMove == Point.Zero)
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

        internal static bool IsRawActionAllowed(int rawActionCode, bool isMorphAvatar)
        {
            return rawActionCode >= 0 && rawActionCode < (isMorphAvatar ? 49 : 273);
        }

        private static bool UsesWalkSpeedTiming(string actionName)
        {
            if (!CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return false;
            }

            return rawActionCode < 2 || rawActionCode == 124;
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

        private static void OffsetMapPoint(Dictionary<string, Point> mapPoints, string name, Point offset)
        {
            if (mapPoints == null || !mapPoints.TryGetValue(name, out Point point))
            {
                return;
            }

            mapPoints[name] = new Point(point.X + offset.X, point.Y + offset.Y);
        }
    }
}
