using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character.Skills
{
    public static class ClientMeleeAfterimageRangeResolver
    {
        private const int BlastSkillId = 1221009;
        private const int BlastRangeRawActionCode = 17;
        private const int RedirectedRawActionCode = 57;
        private const int RedirectTargetRawActionCode = 41;
        private const int HardcodedRangeRawActionCode = 74;

        // Confirmed in CActionMan::GetMeleeAttackRange:
        // skill 1221009 forces raw action 17 before lookup, action 57 redirects to 41,
        // and action 74 uses a hardcoded rectangle.
        private static readonly Rectangle HardcodedRange = new(-88, -62, 70, 56);

        public static int? ResolveRawActionCodeForRange(int skillId, int? rawActionCode)
        {
            if (skillId == BlastSkillId)
            {
                return BlastRangeRawActionCode;
            }

            if (!rawActionCode.HasValue)
            {
                return null;
            }

            return rawActionCode.Value == RedirectedRawActionCode
                ? RedirectTargetRawActionCode
                : rawActionCode.Value;
        }

        public static bool TryResolveRangeOverride(int skillId, int? rawActionCode, bool facingRight, out Rectangle range)
        {
            range = Rectangle.Empty;
            int? resolvedRawActionCode = ResolveRawActionCodeForRange(skillId, rawActionCode);
            if (!resolvedRawActionCode.HasValue)
            {
                return false;
            }

            switch (resolvedRawActionCode.Value)
            {
                case HardcodedRangeRawActionCode:
                    range = facingRight
                        ? HardcodedRange
                        : new Rectangle(
                            -(HardcodedRange.X + HardcodedRange.Width),
                            HardcodedRange.Y,
                            HardcodedRange.Width,
                            HardcodedRange.Height);
                    return true;

                default:
                    return false;
            }
        }

        public static MeleeAfterImageAction ApplyRangeOverride(
            MeleeAfterImageAction action,
            int skillId,
            int? rawActionCode,
            bool facingRight)
        {
            if (!TryResolveRangeOverride(skillId, rawActionCode, facingRight, out Rectangle overrideRange))
            {
                return action;
            }

            return new MeleeAfterImageAction
            {
                FrameSets = action?.FrameSets ?? new(),
                Range = overrideRange
            };
        }
    }
}
