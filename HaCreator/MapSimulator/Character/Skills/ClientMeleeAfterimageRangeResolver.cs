using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character.Skills
{
    public static class ClientMeleeAfterimageRangeResolver
    {
        private const int RedirectedRawActionCode = 57;
        private const int RedirectTargetRawActionCode = 41;
        private const int HardcodedRangeRawActionCode = 74;

        // Confirmed in CActionMan::GetMeleeAttackRange:
        // action 57 redirects to 41, and action 74 uses a hardcoded rectangle.
        private static readonly Rectangle HardcodedRange = new(-88, -62, 70, 56);

        public static int NormalizeRawActionCodeForRange(int rawActionCode)
        {
            return rawActionCode == RedirectedRawActionCode
                ? RedirectTargetRawActionCode
                : rawActionCode;
        }

        public static bool TryResolveRangeOverride(int? rawActionCode, bool facingRight, out Rectangle range)
        {
            range = Rectangle.Empty;
            if (!rawActionCode.HasValue)
            {
                return false;
            }

            switch (NormalizeRawActionCodeForRange(rawActionCode.Value))
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
            int? rawActionCode,
            bool facingRight)
        {
            if (!TryResolveRangeOverride(rawActionCode, facingRight, out Rectangle overrideRange))
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
