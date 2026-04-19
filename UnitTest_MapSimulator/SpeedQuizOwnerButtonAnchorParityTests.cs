using HaCreator;
using Microsoft.Xna.Framework;
using System.Reflection;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class SpeedQuizOwnerButtonAnchorParityTests
    {
        [Fact]
        public void ResolveSpeedQuizOwnerButtonBounds_UsesWzAnchorMetricsWhenAvailable()
        {
            Rectangle ownerBounds = new(200, 120, 265, 422);
            Rectangle fallbackBounds = new(433, 500, 40, 16);

            Rectangle resolved = ResolveSpeedQuizOwnerButtonBounds(
                ownerBounds,
                hasAnchorMetrics: true,
                anchorOrigin: new Point(-105, -393),
                anchorSize: new Point(40, 16),
                fallbackBounds);

            Assert.Equal(new Rectangle(305, 513, 40, 16), resolved);
        }

        [Fact]
        public void ResolveSpeedQuizOwnerButtonBounds_FallsBackWhenAnchorMetricsMissing()
        {
            Rectangle ownerBounds = new(64, 96, 265, 422);
            Rectangle fallbackBounds = new(190, 476, 40, 16);

            Rectangle resolved = ResolveSpeedQuizOwnerButtonBounds(
                ownerBounds,
                hasAnchorMetrics: false,
                anchorOrigin: Point.Zero,
                anchorSize: Point.Zero,
                fallbackBounds);

            Assert.Equal(fallbackBounds, resolved);
        }

        [Fact]
        public void ResolveSpeedQuizOwnerButtonBounds_FallsBackWhenAnchorSizeInvalid()
        {
            Rectangle ownerBounds = new(64, 96, 265, 422);
            Rectangle fallbackBounds = new(190, 476, 40, 16);

            Rectangle resolved = ResolveSpeedQuizOwnerButtonBounds(
                ownerBounds,
                hasAnchorMetrics: true,
                anchorOrigin: new Point(-149, -393),
                anchorSize: new Point(0, 16),
                fallbackBounds);

            Assert.Equal(fallbackBounds, resolved);
        }

        private static Rectangle ResolveSpeedQuizOwnerButtonBounds(
            Rectangle ownerBounds,
            bool hasAnchorMetrics,
            Point anchorOrigin,
            Point anchorSize,
            Rectangle fallbackBounds)
        {
            MethodInfo method = typeof(Program).Assembly
                .GetType("HaCreator.MapSimulator.MapSimulator", throwOnError: true)
                .GetMethod(
                    "ResolveSpeedQuizOwnerButtonBounds",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            object result = method.Invoke(
                null,
                new object[] { ownerBounds, hasAnchorMetrics, anchorOrigin, anchorSize, fallbackBounds });
            Assert.IsType<Rectangle>(result);
            return (Rectangle)result;
        }
    }
}
