using HaCreator.MapSimulator.Character;
using Xunit;

public class MirrorImageLayerParityTests
{
    [Fact]
    public void ShouldUseLiveMirrorInsert_ReturnsFalse_WhenSameLayerAndSourceMetadataUnchanged()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 3,
            lastInsertCanvasLayerObjectId: 3,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourcePartsObjectId: 1001,
            lastInsertCanvasSourcePartsObjectId: 1001,
            sourceSignature: 2002,
            lastInsertedSourceSignature: 2002,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1234);

        Assert.False(admitted);
    }

    [Fact]
    public void ShouldUseLiveMirrorInsert_ReturnsTrue_WhenSourcePartsIdentityChanged()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 3,
            lastInsertCanvasLayerObjectId: 3,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourcePartsObjectId: 3003,
            lastInsertCanvasSourcePartsObjectId: 1001,
            sourceSignature: 2002,
            lastInsertedSourceSignature: 2002,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1234);

        Assert.True(admitted);
    }

    [Fact]
    public void ShouldUseLiveMirrorInsert_ReturnsTrue_WhenSourceSignatureChanged()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 3,
            lastInsertCanvasLayerObjectId: 3,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourcePartsObjectId: 1001,
            lastInsertCanvasSourcePartsObjectId: 1001,
            sourceSignature: 5005,
            lastInsertedSourceSignature: 2002,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1234);

        Assert.True(admitted);
    }

    [Fact]
    public void ShouldUseLiveMirrorInsert_ReturnsTrue_WhenInsertTimeMetadataMissing()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 3,
            lastInsertCanvasLayerObjectId: 3,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourcePartsObjectId: 1001,
            lastInsertCanvasSourcePartsObjectId: 1001,
            sourceSignature: 2002,
            lastInsertedSourceSignature: 2002,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: int.MinValue);

        Assert.True(admitted);
    }

    [Fact]
    public void ShouldUseLiveMirrorInsert_ReturnsFalse_WhenRecordedSourceLayerMismatchesIncomingLayer()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 3,
            lastInsertCanvasLayerObjectId: 3,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourcePartsObjectId: 1001,
            lastInsertCanvasSourcePartsObjectId: 1001,
            sourceSignature: 2002,
            lastInsertedSourceSignature: 2002,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.OverCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasTime: 1234);

        Assert.False(admitted);
    }

    [Fact]
    public void ShouldUseLiveMirrorInsert_ReturnsFalse_WhenRecordedOverlayLayerMismatchesIncomingLayer()
    {
        bool admitted = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
            preparedLayerObjectId: 3,
            lastInsertCanvasLayerObjectId: 3,
            sourceLayer: AvatarRenderLayer.UnderCharacter,
            sourcePartsObjectId: 1001,
            lastInsertCanvasSourcePartsObjectId: 1001,
            sourceSignature: 2002,
            lastInsertedSourceSignature: 2002,
            lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
            overlayTargetLayer: AvatarRenderLayer.UnderFace,
            lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.Face,
            lastInsertCanvasTime: 1234);

        Assert.False(admitted);
    }
}
