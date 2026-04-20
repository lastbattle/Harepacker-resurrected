using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public sealed class WeddingFieldParityTests
{
    [Fact]
    public void OnWeddingCeremonyEnd_PhotoOwnerDoesNotForceCardOrCelebration()
    {
        var field = new WeddingField();
        field.BindWeddingPhotoSceneOwner(
            mapId: 680000300,
            sourceDescription: "test photo owner",
            viewport: null);

        field.OnWeddingProgress(step: 0, groomId: 0, brideId: 0, currentTimeMs: 100);
        Assert.True(field.IsCeremonyTextOverlayActive);
        Assert.False(field.IsCeremonyCardOverlayActive);
        Assert.False(field.IsCeremonyCelebrationActive);

        field.OnWeddingCeremonyEnd(currentTimeMs: 200);

        Assert.False(field.IsCeremonyTextOverlayActive);
        Assert.False(field.IsCeremonyCardOverlayActive);
        Assert.False(field.IsCeremonyCelebrationActive);
    }

    [Fact]
    public void OnWeddingCeremonyEnd_PhotoOwnerPreservesActiveCardAndCelebration()
    {
        var field = new WeddingField();
        field.BindWeddingPhotoSceneOwner(
            mapId: 680000300,
            sourceDescription: "test photo owner",
            viewport: null);

        field.OnWeddingProgress(step: 2, groomId: 0, brideId: 0, currentTimeMs: 100);
        Assert.True(field.IsCeremonyCardOverlayActive);
        Assert.True(field.IsCeremonyCelebrationActive);

        field.OnWeddingCeremonyEnd(currentTimeMs: 200);

        Assert.True(field.IsCeremonyCardOverlayActive);
        Assert.True(field.IsCeremonyCelebrationActive);
    }
}
