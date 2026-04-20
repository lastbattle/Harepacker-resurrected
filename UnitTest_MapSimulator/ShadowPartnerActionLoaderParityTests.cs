using System.Linq;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class ShadowPartnerActionLoaderParityTests
    {
        [Fact]
        public void FistBuiltInPiecePlan_PreservesClientInitDelaysAndFlip()
        {
            var plan = ShadowPartnerClientActionResolver.GetPiecedShadowPartnerActionPlan("fist");

            Assert.Equal(25, plan.Count);

            ShadowPartnerClientActionResolver.ShadowPartnerActionPiece flippedPiece = plan.Single(piece => piece.SlotIndex == 23);
            Assert.Equal("swingOF", flippedPiece.PieceActionName);
            Assert.Equal(2, flippedPiece.SourceFrameIndex);
            Assert.Equal(90, flippedPiece.DelayOverrideMs);
            Assert.True(flippedPiece.Flip);
            Assert.Equal(450, ShadowPartnerClientActionResolver.ResolveClientActionManInitEventDelayMs(plan));
        }

        [Fact]
        public void BambooBuiltInPiecePlan_PreservesClientInitDelaysAndFlip()
        {
            var plan = ShadowPartnerClientActionResolver.GetPiecedShadowPartnerActionPlan("bamboo");

            Assert.Equal(31, plan.Count);

            ShadowPartnerClientActionResolver.ShadowPartnerActionPiece flippedPiece = plan.Single(piece => piece.SlotIndex == 5);
            Assert.Equal("swingT2", flippedPiece.PieceActionName);
            Assert.Equal(0, flippedPiece.SourceFrameIndex);
            Assert.Equal(-120, flippedPiece.DelayOverrideMs);
            Assert.True(flippedPiece.Flip);
            Assert.Equal(3360, ShadowPartnerClientActionResolver.ResolveClientActionManInitEventDelayMs(plan));
        }
    }
}
