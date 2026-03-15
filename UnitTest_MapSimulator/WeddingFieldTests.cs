using HaCreator.MapSimulator.Effects;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class WeddingFieldTests
    {
        [Fact]
        public void OnWeddingProgress_UsesWeddingNpcFallbackTextForCathedralSteps()
        {
            WeddingField field = new();
            field.Enable(680000110);

            field.OnWeddingProgress(1, 100, 200, 1000);

            Assert.Equal(
                "Very well! I pronounce you Husband and Wife. You may kiss the bride!",
                field.CurrentDialogMessage);
            Assert.Equal(WeddingDialogMode.Text, field.CurrentDialogMode);
        }

        [Fact]
        public void OnWeddingProgress_ChapelGuestStepTwoShowsBlessPrompt()
        {
            WeddingField field = new();
            field.Enable(680000210);
            field.SetLocalPlayerState(999, new Vector2(320f, 180f));

            field.OnWeddingProgress(2, 100, 200, 1000);

            Assert.Equal(WeddingParticipantRole.Guest, field.LocalParticipantRole);
            Assert.True(field.IsGuestBlessPromptActive);
            Assert.Equal("Would you like to give your blessing to the couple?", field.CurrentDialogMessage);
        }

        [Fact]
        public void OnWeddingProgress_ChapelParticipantStepTwoSuppressesGuestPrompt()
        {
            WeddingField field = new();
            field.Enable(680000210);
            field.SetLocalPlayerState(100, new Vector2(320f, 180f));

            field.OnWeddingProgress(2, 100, 200, 1000);

            Assert.Equal(WeddingParticipantRole.Groom, field.LocalParticipantRole);
            Assert.Null(field.CurrentDialogMessage);
            Assert.False(field.IsGuestBlessPromptActive);
        }

        [Fact]
        public void SetBlessEffect_AnchorsToLocalParticipantPosition()
        {
            WeddingField field = new();
            field.Enable(680000110);
            field.OnWeddingProgress(0, 42, 77, 1000);
            field.SetLocalPlayerState(42, new Vector2(400f, 200f));

            field.SetBlessEffect(true, 1000);

            Assert.Equal(WeddingParticipantRole.Groom, field.LocalParticipantRole);
            Assert.True(field.BlessEffectWorldCenter.HasValue);
            Assert.True(Math.Abs(field.BlessEffectWorldCenter.Value.X - 420f) < 0.01f);
            Assert.True(Math.Abs(field.BlessEffectWorldCenter.Value.Y - 180f) < 0.01f);
        }
    }
}
