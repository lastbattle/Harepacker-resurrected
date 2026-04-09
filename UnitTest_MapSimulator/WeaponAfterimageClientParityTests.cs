using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class WeaponAfterimageClientParityTests
    {
        [Fact]
        public void ApplyRangeOverride_CreatesRangeOnlyAction_ForShowdownRawAction()
        {
            MeleeAfterImageAction action = ClientMeleeAfterimageRangeResolver.ApplyRangeOverride(
                null,
                skillId: 0,
                rawActionCode: 74,
                facingRight: true);

            Assert.NotNull(action);
            Assert.True(action.HasRange);
            Assert.Empty(action.FrameSets);
            Assert.Equal(new Rectangle(-88, -62, 70, 56), action.Range);
        }

        [Fact]
        public void ApplyRangeOverride_MirrorsShowdownRange_ForLeftFacingPlayback()
        {
            MeleeAfterImageAction action = ClientMeleeAfterimageRangeResolver.ApplyRangeOverride(
                null,
                skillId: 0,
                rawActionCode: 74,
                facingRight: false);

            Assert.NotNull(action);
            Assert.Equal(new Rectangle(18, -62, 70, 56), action.Range);
        }

        [Fact]
        public void ResolveRawActionCodeForRange_RedirectsBurster2ToRain()
        {
            int? resolvedRawActionCode = ClientMeleeAfterimageRangeResolver.ResolveRawActionCodeForRange(
                skillId: 0,
                rawActionCode: 57);

            Assert.Equal(41, resolvedRawActionCode);
        }

        [Fact]
        public void ResolveRawActionCodeForRange_ForcesBlastToStabO2RangeCode()
        {
            int? resolvedRawActionCode = ClientMeleeAfterimageRangeResolver.ResolveRawActionCodeForRange(
                skillId: 1221009,
                rawActionCode: 73);

            Assert.Equal(17, resolvedRawActionCode);
        }
    }
}
