using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedSummonUpdateRulesTests
    {
        [Fact]
        public void ResolvePacketAttackFacingRight_UsesMoveActionForReactiveDamageSummons()
        {
            ActiveSummon summon = CreateSummon(
                4111007,
                minionAbility: "PADReflect&&MADReflect",
                condition: "damaged");

            bool facingRight = PacketOwnedSummonUpdateRules.ResolvePacketAttackFacingRight(
                summon,
                moveActionRaw: 1,
                packetFacingLeft: false,
                fallbackFacingRight: true);

            Assert.False(facingRight);
        }

        [Fact]
        public void ResolvePacketAttackFacingRight_UsesPacketFacingForNonReactiveSummons()
        {
            ActiveSummon summon = CreateSummon(3211005);

            bool facingRight = PacketOwnedSummonUpdateRules.ResolvePacketAttackFacingRight(
                summon,
                moveActionRaw: 1,
                packetFacingLeft: false,
                fallbackFacingRight: false);

            Assert.True(facingRight);
        }

        [Fact]
        public void ShouldRegisterClientOwnedAttackTileOverlay_RequiresConfirmedSkillAndZoneFrames()
        {
            ActiveSummon summon = CreateSummon(
                3120010,
                zoneAnimation: CreateSingleFrameAnimation());

            bool shouldRegister = PacketOwnedSummonUpdateRules.ShouldRegisterClientOwnedAttackTileOverlay(summon);

            Assert.True(shouldRegister);
        }

        [Fact]
        public void BuildClientOwnedAttackTileOverlayArea_UsesAuthoredRangeAndClientBottomPadding()
        {
            ActiveSummon summon = CreateSummon(
                3211005,
                zoneAnimation: CreateSingleFrameAnimation());
            summon.SkillData.SummonAttackRangeLeft = -180;
            summon.SkillData.SummonAttackRangeRight = 180;
            summon.SkillData.SummonAttackRangeTop = -100;
            summon.SkillData.SummonAttackRangeBottom = 100;

            Rectangle area = PacketOwnedSummonUpdateRules.BuildClientOwnedAttackTileOverlayArea(
                new Vector2(400f, 300f),
                summon);

            Assert.Equal(new Rectangle(220, 200, 360, 300), area);
        }

        private static ActiveSummon CreateSummon(
            int skillId,
            string minionAbility = null,
            string condition = null,
            SkillAnimation zoneAnimation = null)
        {
            return new ActiveSummon
            {
                SkillId = skillId,
                SkillData = new SkillData
                {
                    SkillId = skillId,
                    MinionAbility = minionAbility,
                    SummonCondition = condition,
                    ZoneAnimation = zoneAnimation
                }
            };
        }

        private static SkillAnimation CreateSingleFrameAnimation()
        {
            SkillAnimation animation = new SkillAnimation
            {
                Frames = new List<SkillFrame>
                {
                    new()
                    {
                        Texture = new TestDxObject(),
                        Origin = Point.Zero,
                        Delay = 100
                    }
                }
            };

            animation.CalculateDuration();
            return animation;
        }

        private sealed class TestDxObject : IDXObject
        {
            public int Delay => 100;
            public int X => 0;
            public int Y => 0;
            public int Width => 16;
            public int Height => 16;
            public object Tag { get; set; } = new object();
            public Texture2D Texture => null!;

            public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }

            public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
            {
            }
        }
    }
}
