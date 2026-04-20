using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MeleeRangedMagicResolutionParityTests
    {
        [Fact]
        public void TryResolveMountedClientBodyRelMoveYFromFallbackFrames_PrefersAssembledFramesBeforeMountedFrames()
        {
            IReadOnlyList<AssembledFrame> assembledFrames = new[]
            {
                new AssembledFrame { MapPoints = new Dictionary<string, Microsoft.Xna.Framework.Point> { ["navel"] = new Microsoft.Xna.Framework.Point(1, -54) } },
                new AssembledFrame { MapPoints = new Dictionary<string, Microsoft.Xna.Framework.Point> { ["navel"] = new Microsoft.Xna.Framework.Point(4, -51) } }
            };
            IReadOnlyList<CharacterFrame> mountedFrames = new[]
            {
                new CharacterFrame { Map = new Dictionary<string, Microsoft.Xna.Framework.Point> { ["navel"] = new Microsoft.Xna.Framework.Point(1, -54) } },
                new CharacterFrame { Map = new Dictionary<string, Microsoft.Xna.Framework.Point> { ["navel"] = new Microsoft.Xna.Framework.Point(7, -40) } }
            };

            bool resolved = PlayerCharacter.TryResolveMountedClientBodyRelMoveYFromFallbackFrames(
                assembledFrames,
                assembledFrameIndex: 1,
                mountedFrames,
                mountedFrameIndex: 1,
                out int bodyRelMoveY);

            Assert.True(resolved);
            Assert.Equal(3, bodyRelMoveY);
        }

        [Fact]
        public void TryResolveMountedClientBodyRelMoveYFromFallbackFrames_FallsBackToMountedFramesWhenAssembledFramesUnavailable()
        {
            IReadOnlyList<CharacterFrame> mountedFrames = new[]
            {
                new CharacterFrame { Map = new Dictionary<string, Microsoft.Xna.Framework.Point> { ["navel"] = new Microsoft.Xna.Framework.Point(1, -54) } },
                new CharacterFrame { Map = new Dictionary<string, Microsoft.Xna.Framework.Point> { ["navel"] = new Microsoft.Xna.Framework.Point(7, -40) } }
            };

            bool resolved = PlayerCharacter.TryResolveMountedClientBodyRelMoveYFromFallbackFrames(
                assembledFrames: null,
                assembledFrameIndex: 0,
                mountedFrames,
                mountedFrameIndex: 1,
                out int bodyRelMoveY);

            Assert.True(resolved);
            Assert.Equal(14, bodyRelMoveY);
        }

        [Fact]
        public void TryResolveMountedClientBodyRelMoveYFromFallbackFrames_ReturnsFalseWhenBothFallbackSourcesAreMissing()
        {
            bool resolved = PlayerCharacter.TryResolveMountedClientBodyRelMoveYFromFallbackFrames(
                assembledFrames: null,
                assembledFrameIndex: 0,
                mountedFrames: null,
                mountedFrameIndex: 0,
                out int bodyRelMoveY);

            Assert.False(resolved);
            Assert.Equal(0, bodyRelMoveY);
        }

        [Fact]
        public void TryResolveClientBodyRelMoveY_UsesOriginDeltaWhenNavelMapPointIsMissing()
        {
            IReadOnlyList<AssembledFrame> frames = new[]
            {
                new AssembledFrame { Origin = new Microsoft.Xna.Framework.Point(0, 10) },
                new AssembledFrame { Origin = new Microsoft.Xna.Framework.Point(0, 4) }
            };

            bool resolved = PlayerCharacter.TryResolveClientBodyRelMoveY(frames, frameIndex: 1, out int bodyRelMoveY);

            Assert.True(resolved);
            Assert.Equal(-6, bodyRelMoveY);
        }
    }
}
