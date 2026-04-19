using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class AnimationDisplayerParityTests
    {
        [Fact]
        public void ActiveEffectMotionBlurDefinition_ClampsDelayIntervalAndAlpha()
        {
            ActiveEffectItemMotionBlurDefinition definition = ActiveEffectItemMotionBlurResolver.CreateDefinition(
                itemId: 5010044,
                spectrum: true,
                follow: false,
                delayMs: 0,
                intervalMs: -25,
                alpha: 300);

            Assert.True(definition.IsValid);
            Assert.Equal(1, definition.DelayMs);
            Assert.Equal(1, definition.IntervalMs);
            Assert.Equal(byte.MaxValue, definition.Alpha);
        }

        [Fact]
        public void ActiveEffectMotionBlurSnapshotRetention_UsesWzDelayWindow()
        {
            ActiveEffectItemMotionBlurDefinition definition = ActiveEffectItemMotionBlurResolver.CreateDefinition(
                itemId: 5010044,
                spectrum: true,
                follow: false,
                delayMs: 600,
                intervalMs: 100,
                alpha: 224);

            Assert.True(ActiveEffectItemMotionBlurResolver.ShouldRetainSnapshot(1000, 1599, definition));
            Assert.False(ActiveEffectItemMotionBlurResolver.ShouldRetainSnapshot(1000, 1600, definition));
        }

        [Fact]
        public void ActiveEffectMotionBlurSnapshotAlpha_UsesSharedLinearFadeShape()
        {
            const byte baseAlpha = 224;
            const int delayMs = 600;

            byte localAlpha = SecondaryMotionBlurAnimation.ResolveSnapshotAlpha(ageMs: 300, retentionMs: delayMs, baseAlpha);
            byte remoteAlpha = RemoteUserActorPool.ResolveRemoteActiveEffectMotionBlurSnapshotAlpha(ageMs: 300, delayMs, baseAlpha);

            Assert.Equal((byte)112, localAlpha);
            Assert.Equal(localAlpha, remoteAlpha);
            Assert.Equal((byte)0, SecondaryMotionBlurAnimation.ResolveSnapshotAlpha(ageMs: 600, retentionMs: delayMs, baseAlpha));
            Assert.Equal((byte)0, RemoteUserActorPool.ResolveRemoteActiveEffectMotionBlurSnapshotAlpha(ageMs: 600, delayMs, baseAlpha));
        }

        [Fact]
        public void RemoteMotionBlurLayerSnapshot_UsesFiveAvatarLayersInClientDrawOrder()
        {
            var frame = new AssembledFrame
            {
                AvatarRenderLayers = AssembledFrame.CreateEmptyAvatarRenderLayers()
            };

            frame.AvatarRenderLayers[(int)AvatarRenderLayer.UnderCharacter] = new[] { CreatePart(AvatarRenderLayer.UnderCharacter, offsetX: 1, offsetY: 2) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.OverCharacter] = new[] { CreatePart(AvatarRenderLayer.OverCharacter, offsetX: 3, offsetY: 4) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.UnderFace] = new[] { CreatePart(AvatarRenderLayer.UnderFace, offsetX: 5, offsetY: 6) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.Face] = new[] { CreatePart(AvatarRenderLayer.Face, offsetX: 7, offsetY: 8) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.OverFace] = new[] { CreatePart(AvatarRenderLayer.OverFace, offsetX: 9, offsetY: 10) };

            bool created = RemoteUserActorPool.TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
                frame,
                out List<RemoteUserActorPool.RemoteActiveEffectMotionBlurLayerSnapshot> snapshots);

            Assert.True(created);
            Assert.Equal(5, snapshots.Count);
            Assert.Equal(0, snapshots[0].DrawOrder);
            Assert.Equal(1, snapshots[1].DrawOrder);
            Assert.Equal(2, snapshots[2].DrawOrder);
            Assert.Equal(3, snapshots[3].DrawOrder);
            Assert.Equal(4, snapshots[4].DrawOrder);
            Assert.Equal(AvatarRenderLayer.UnderCharacter, snapshots[0].SourceLayer);
            Assert.Equal(AvatarRenderLayer.OverCharacter, snapshots[1].SourceLayer);
            Assert.Equal(AvatarRenderLayer.UnderFace, snapshots[2].SourceLayer);
            Assert.Equal(AvatarRenderLayer.Face, snapshots[3].SourceLayer);
            Assert.Equal(AvatarRenderLayer.OverFace, snapshots[4].SourceLayer);
            Assert.Equal(4, snapshots[0].SourceLayerCaptureOrder);
            Assert.Equal(3, snapshots[1].SourceLayerCaptureOrder);
            Assert.Equal(2, snapshots[2].SourceLayerCaptureOrder);
            Assert.Equal(0, snapshots[3].SourceLayerCaptureOrder);
            Assert.Equal(1, snapshots[4].SourceLayerCaptureOrder);
        }

        [Fact]
        public void RemoteMotionBlurLayerSnapshot_PreservesSimulatedHandleIdentity()
        {
            var frame = new AssembledFrame
            {
                AvatarRenderLayers = AssembledFrame.CreateEmptyAvatarRenderLayers()
            };

            frame.AvatarRenderLayers[(int)AvatarRenderLayer.Face] = new[] { CreatePart(AvatarRenderLayer.Face, offsetX: 1, offsetY: 1) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.OverFace] = new[] { CreatePart(AvatarRenderLayer.OverFace, offsetX: 2, offsetY: 2) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.UnderFace] = new[] { CreatePart(AvatarRenderLayer.UnderFace, offsetX: 3, offsetY: 3) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.OverCharacter] = new[] { CreatePart(AvatarRenderLayer.OverCharacter, offsetX: 4, offsetY: 4) };
            frame.AvatarRenderLayers[(int)AvatarRenderLayer.UnderCharacter] = new[] { CreatePart(AvatarRenderLayer.UnderCharacter, offsetX: 5, offsetY: 5) };

            var simulatedHandles = new Dictionary<AvatarRenderLayer, int>
            {
                [AvatarRenderLayer.Face] = 9101,
                [AvatarRenderLayer.OverFace] = 9102,
                [AvatarRenderLayer.UnderFace] = 9103,
                [AvatarRenderLayer.OverCharacter] = 9104,
                [AvatarRenderLayer.UnderCharacter] = 9105
            };

            bool created = RemoteUserActorPool.TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
                frame,
                simulatedHandles,
                out List<RemoteUserActorPool.RemoteActiveEffectMotionBlurLayerSnapshot> snapshots);

            Assert.True(created);
            Assert.Equal(5, snapshots.Count);
            Assert.Equal(9105, snapshots[0].SimulatedLayerHandleId);
            Assert.Equal(9104, snapshots[1].SimulatedLayerHandleId);
            Assert.Equal(9103, snapshots[2].SimulatedLayerHandleId);
            Assert.Equal(9101, snapshots[3].SimulatedLayerHandleId);
            Assert.Equal(9102, snapshots[4].SimulatedLayerHandleId);
        }

        [Fact]
        public void RemoteMotionBlurLayerSnapshot_FallsBackToPartsAndSkipsInvisibleEntries()
        {
            AssembledPart visible = CreatePart(AvatarRenderLayer.Face, offsetX: 11, offsetY: 12);
            AssembledPart invisible = CreatePart(AvatarRenderLayer.Face, offsetX: 13, offsetY: 14);
            invisible.IsVisible = false;

            var frame = new AssembledFrame
            {
                Parts = new List<AssembledPart> { visible, invisible }
            };

            bool created = RemoteUserActorPool.TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
                frame,
                out List<RemoteUserActorPool.RemoteActiveEffectMotionBlurLayerSnapshot> snapshots);

            Assert.True(created);
            Assert.Single(snapshots);
            Assert.Equal(3, snapshots[0].DrawOrder);
            Assert.Single(snapshots[0].Parts);
            Assert.Equal(11, snapshots[0].Parts[0].OffsetX);
            Assert.Equal(12, snapshots[0].Parts[0].OffsetY);
        }

        [Fact]
        public void SpecificUserStateFrames_EndPhaseAggregatesAllAuthoredCleanupBranches()
        {
            var state = new RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState
            {
                SkillId = 33121006,
                Skill = new SkillData
                {
                    AvatarOverlayFinishEffect = CreateAnimationWithSingleFrame("finish"),
                    AvatarUnderFaceFinishEffect = CreateAnimationWithSingleFrame("finish0"),
                    AvatarLadderFinishEffect = CreateAnimationWithSingleFrame("back_finish"),
                    KeydownEndEffect = CreateAnimationWithSingleFrame("keydownend"),
                    StopEffect = CreateAnimationWithSingleFrame("stopEffect")
                }
            };

            bool resolved = MapSimulator.TryResolveAnimationDisplayerSpecificUserStateFrames(
                new[] { state },
                out int resolvedSkillId,
                out _,
                out _,
                out List<IDXObject> endFrames);

            Assert.True(resolved);
            Assert.Equal(33121006, resolvedSkillId);
            Assert.Equal(5, endFrames.Count);
            Assert.Equal(
                new[] { "finish", "finish0", "back_finish", "keydownend", "stopEffect" },
                endFrames.Select(frame => frame.Tag as string).ToArray());
        }

        private static SkillAnimation CreateAnimationWithSingleFrame(string tag)
        {
            return new SkillAnimation
            {
                Frames = new List<SkillFrame>
                {
                    new SkillFrame
                    {
                        Texture = new TestDxObject(tag),
                        Delay = 100
                    }
                }
            };
        }

        private static AssembledPart CreatePart(AvatarRenderLayer layer, int offsetX, int offsetY)
        {
            return new AssembledPart
            {
                Texture = new TestDxObject("part"),
                OffsetX = offsetX,
                OffsetY = offsetY,
                IsVisible = true,
                RenderLayer = layer
            };
        }

        private sealed class TestDxObject : IDXObject
        {
            public TestDxObject(string tag)
            {
                Tag = tag;
            }

            public int Delay => 100;
            public int X => 0;
            public int Y => 0;
            public int Width => 1;
            public int Height => 1;
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
