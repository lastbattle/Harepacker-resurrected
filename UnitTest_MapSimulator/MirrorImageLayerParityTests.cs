using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MirrorImageLayerParityTests
    {
        [Fact]
        public void LiveInsertCanvasGate_RejectsOverlayTargetMismatch()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 7,
                lastInsertCanvasLayerObjectId: 7,
                sourceLayer: AvatarRenderLayer.Face,
                sourcePartsObjectId: 21,
                lastInsertCanvasSourcePartsObjectId: 21,
                sourceSignature: 145,
                lastInsertedSourceSignature: 145,
                lastInsertCanvasSourceLayer: AvatarRenderLayer.Face,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.OverFace,
                lastInsertCanvasTime: 1200);

            Assert.False(usesLiveSource);
        }

        [Fact]
        public void LiveInsertCanvasGate_RejectsMissingInsertCanvasTimeWhenLayerObjectWasTracked()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 7,
                lastInsertCanvasLayerObjectId: 7,
                sourceLayer: AvatarRenderLayer.UnderCharacter,
                sourcePartsObjectId: 31,
                lastInsertCanvasSourcePartsObjectId: 31,
                sourceSignature: 22,
                lastInsertedSourceSignature: 22,
                lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderCharacter,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasTime: int.MinValue);

            Assert.False(usesLiveSource);
        }

        [Fact]
        public void LiveInsertCanvasGate_AcceptsMatchingLayerSourceAndOverlayMetadata()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 11,
                lastInsertCanvasLayerObjectId: 11,
                sourceLayer: AvatarRenderLayer.OverCharacter,
                sourcePartsObjectId: 99,
                lastInsertCanvasSourcePartsObjectId: 99,
                sourceSignature: 503,
                lastInsertedSourceSignature: 503,
                lastInsertCanvasSourceLayer: AvatarRenderLayer.OverCharacter,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasTime: 3200);

            Assert.True(usesLiveSource);
        }

        [Fact]
        public void LiveInsertCanvasGate_AcceptsInitialLivePathWhenNoInsertCanvasLayerObjectWasRecordedYet()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 5,
                lastInsertCanvasLayerObjectId: 0,
                sourceLayer: AvatarRenderLayer.Face,
                sourcePartsObjectId: 0,
                lastInsertCanvasSourcePartsObjectId: 0,
                sourceSignature: 0,
                lastInsertedSourceSignature: 0,
                lastInsertCanvasSourceLayer: null,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: null,
                lastInsertCanvasTime: int.MinValue);

            Assert.True(usesLiveSource);
        }

        [Fact]
        public void LiveInsertCanvasGate_RejectsSourcePartsObjectIdMismatch()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 9,
                lastInsertCanvasLayerObjectId: 9,
                sourceLayer: AvatarRenderLayer.OverFace,
                sourcePartsObjectId: 314,
                lastInsertCanvasSourcePartsObjectId: 159,
                sourceSignature: 77,
                lastInsertedSourceSignature: 11,
                lastInsertCanvasSourceLayer: AvatarRenderLayer.OverFace,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasTime: 5500);

            Assert.False(usesLiveSource);
        }

        [Fact]
        public void LiveInsertCanvasGate_AcceptsWhenSourcePartsObjectIdMatches()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 13,
                lastInsertCanvasLayerObjectId: 13,
                sourceLayer: AvatarRenderLayer.UnderFace,
                sourcePartsObjectId: 8,
                lastInsertCanvasSourcePartsObjectId: 8,
                sourceSignature: 8,
                lastInsertedSourceSignature: 8,
                lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderFace,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasTime: 7777);

            Assert.True(usesLiveSource);
        }

        [Fact]
        public void LiveInsertCanvasGate_AcceptsSourcePartsObjectIdMismatchWhenSourceSignatureMatches()
        {
            bool usesLiveSource = PlayerCharacter.ShouldUseLiveMirrorImageSourceLayerForInsertCanvas(
                preparedLayerObjectId: 13,
                lastInsertCanvasLayerObjectId: 13,
                sourceLayer: AvatarRenderLayer.UnderFace,
                sourcePartsObjectId: 101,
                lastInsertCanvasSourcePartsObjectId: 8,
                sourceSignature: 444,
                lastInsertedSourceSignature: 444,
                lastInsertCanvasSourceLayer: AvatarRenderLayer.UnderFace,
                overlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasOverlayTargetLayer: AvatarRenderLayer.UnderFace,
                lastInsertCanvasTime: 7777);

            Assert.True(usesLiveSource);
        }

        [Fact]
        public void LiveSourceLayerGate_AcceptsActionFrameAndFacingDriftWhenLiveCanvasExists()
        {
            var sourceParts = new[]
            {
                new AssembledPart
                {
                    Texture = new StubDxObject(8, 8),
                    IsVisible = true
                }
            };

            bool usesLiveSource = PlayerCharacter.CanUseLiveMirrorImageSourceLayer(
                preparedActionName: "stand1",
                currentActionName: "stabD1",
                preparedFrameIndex: 0,
                currentFrameIndex: 3,
                preparedFacingRight: true,
                currentFacingRight: false,
                preparedSourceSignature: 123,
                liveSourceParts: sourceParts);

            Assert.True(usesLiveSource);
        }

        private sealed class StubDxObject : IDXObject
        {
            public StubDxObject(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Delay => 100;
            public int X => 0;
            public int Y => 0;
            public int Width { get; }
            public int Height { get; }
            public object Tag { get; set; }
            public Texture2D Texture => null;

            public void DrawObject(
                SpriteBatch sprite,
                SkeletonMeshRenderer meshRenderer,
                GameTime gameTime,
                int mapShiftX,
                int mapShiftY,
                bool flip,
                ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawBackground(
                SpriteBatch sprite,
                SkeletonMeshRenderer meshRenderer,
                GameTime gameTime,
                int x,
                int y,
                Color color,
                bool flip,
                ReflectionDrawableBoundary drawReflectionInfo)
            {
            }
        }
    }
}
