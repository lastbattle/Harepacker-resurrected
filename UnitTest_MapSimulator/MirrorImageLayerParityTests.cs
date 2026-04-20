using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class MirrorImageLayerParityTests
{
    [Fact]
    public void ShouldUseLiveMirrorInsertCanvas_WhenSourceLayerTimeAdvances()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 10,
            lastInsertCanvasLayerObjectId: 10,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourceLayerCurrentTime: 180,
            lastInsertCanvasSourceLayerCurrentTime: 120,
            sourcePartsObjectId: 20,
            lastInsertCanvasSourcePartsObjectId: 20,
            sourceSignature: 30,
            lastInsertedSourceSignature: 30,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1000,
            currentTime: 1020);

        Assert.True(admitted);
    }

    [Fact]
    public void ShouldUseLiveMirrorInsertCanvas_WhenSourceLayerTimeRegresses()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 10,
            lastInsertCanvasLayerObjectId: 10,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourceLayerCurrentTime: 30,
            lastInsertCanvasSourceLayerCurrentTime: 180,
            sourcePartsObjectId: 20,
            lastInsertCanvasSourcePartsObjectId: 20,
            sourceSignature: 30,
            lastInsertedSourceSignature: 30,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1000,
            currentTime: 1010);

        Assert.True(admitted);
    }

    [Fact]
    public void ShouldNotUseLiveMirrorInsertCanvas_WhenSourceLayerTimeAndIdentityAreUnchanged()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 10,
            lastInsertCanvasLayerObjectId: 10,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourceLayerCurrentTime: 120,
            lastInsertCanvasSourceLayerCurrentTime: 120,
            sourcePartsObjectId: 20,
            lastInsertCanvasSourcePartsObjectId: 20,
            sourceSignature: 30,
            lastInsertedSourceSignature: 30,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1000,
            currentTime: 1010);

        Assert.False(admitted);
    }

    [Fact]
    public void ShouldRejectLiveMirrorInsertCanvas_WhenSourceLayerMetadataMismatches()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 10,
            lastInsertCanvasLayerObjectId: 10,
            sourceLayer: AvatarRenderLayer.OverCharacter,
            sourceLayerCurrentTime: 140,
            lastInsertCanvasSourceLayerCurrentTime: 120,
            sourcePartsObjectId: 20,
            lastInsertCanvasSourcePartsObjectId: 20,
            sourceSignature: 30,
            lastInsertedSourceSignature: 30,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1000,
            currentTime: 1010);

        Assert.False(admitted);
    }

    [Fact]
    public void ShouldPreserveLastInsertSourceLayerCurrentTime_WhenNoLiveSourceCanvasUpdate()
    {
        int resolved = PlayerCharacter.ResolveMirrorImageLastInsertCanvasSourceLayerCurrentTime(
            existingSourceLayerCurrentTime: 150,
            sourceLayerCurrentTime: 220,
            hasSourceCanvas: false);

        Assert.Equal(150, resolved);
    }
}
