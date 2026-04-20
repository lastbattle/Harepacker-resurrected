using System.Collections.Generic;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class AnimationDisplayerUserStateLadderParityTests
    {
        [Theory]
        [InlineData("ladder", true)]
        [InlineData("rope2", true)]
        [InlineData("stand1", false)]
        [InlineData(null, false)]
        public void IsAnimationDisplayerSpecificUserStateLadderActionName_ResolvesExpectedValues(
            string actionName,
            bool expected)
        {
            bool resolved = MapSimulator.IsAnimationDisplayerSpecificUserStateLadderActionName(actionName);

            Assert.Equal(expected, resolved);
        }

        [Fact]
        public void TryResolveAnimationDisplayerSpecificUserStateFrames_LadderAction_PrefersLadderRepeatAndFinishFirst()
        {
            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state = BuildState();

            bool resolved = MapSimulator.TryResolveAnimationDisplayerSpecificUserStateFrames(
                new[] { state },
                ownerActionName: "ladder",
                out _,
                out _,
                out List<IDXObject> repeatFrames,
                out List<IDXObject> endFrames);

            Assert.True(resolved);
            Assert.Equal("ladder-repeat", repeatFrames[0].Tag);
            Assert.Equal("ladder-finish", endFrames[0].Tag);
        }

        [Fact]
        public void TryResolveAnimationDisplayerSpecificUserStateFrames_GroundAction_PrefersGroundRepeatAndFinishFirst()
        {
            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state = BuildState();

            bool resolved = MapSimulator.TryResolveAnimationDisplayerSpecificUserStateFrames(
                new[] { state },
                ownerActionName: "stand1",
                out _,
                out _,
                out List<IDXObject> repeatFrames,
                out List<IDXObject> endFrames);

            Assert.True(resolved);
            Assert.Equal("ground-repeat", repeatFrames[0].Tag);
            Assert.Equal("ground-finish", endFrames[0].Tag);
        }

        private static RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState BuildState()
        {
            var skill = new SkillData
            {
                SkillId = 33121006,
                AvatarLadderEffect = BuildAnimation("ladder-repeat"),
                AvatarOverlayEffect = BuildAnimation("ground-repeat"),
                AvatarLadderFinishEffect = BuildAnimation("ladder-finish"),
                AvatarOverlayFinishEffect = BuildAnimation("ground-finish")
            };

            return new RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState
            {
                SkillId = skill.SkillId,
                Skill = skill,
                OverlayAnimation = skill.AvatarOverlayEffect
            };
        }

        private static SkillAnimation BuildAnimation(string tag)
        {
            return new SkillAnimation
            {
                Frames = new List<SkillFrame>
                {
                    new()
                    {
                        Delay = 80,
                        Texture = new TestDxObject(tag)
                    }
                }
            };
        }

        private sealed class TestDxObject : IDXObject
        {
            public TestDxObject(string tag)
            {
                Tag = tag;
            }

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public int Delay => 80;
            public int X => 0;
            public int Y => 0;
            public int Width => 1;
            public int Height => 1;
            public object Tag { get; set; }
            public Texture2D Texture => null;
        }
    }
}
