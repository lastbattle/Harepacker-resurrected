using HaCreator.MapSimulator;
using HaSharedLibrary.Render;
using Xunit;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator
{
    /// <summary>
    /// Unit tests for CachedBoundaryChecker
    /// </summary>
    public class CachedBoundaryCheckerTests
    {
        private static ReflectionDrawableBoundary CreateTestBoundary()
        {
            // Constructor: (ushort gradient, ushort alpha, string objectForOverlay, bool reflection, bool alphaTest)
            return new ReflectionDrawableBoundary(0, 100, "", true, false);
        }

        private static ReflectionDrawableBoundary CreateAltBoundary()
        {
            return new ReflectionDrawableBoundary(50, 200, "overlay", true, true);
        }

        [Fact]
        public void Constructor_InitializesWithNullBoundary()
        {
            var checker = new CachedBoundaryChecker();

            Assert.Null(checker.CachedBoundary);
        }

        [Fact]
        public void UpdateBoundary_CachesBoundaryOnFirstCall()
        {
            var checker = new CachedBoundaryChecker();
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 200, 200);

            bool recalculated = checker.UpdateBoundary(100, 100, rect, boundary, null);

            Assert.True(recalculated);
            Assert.Equal(boundary, checker.CachedBoundary);
        }

        [Fact]
        public void UpdateBoundary_UsesCacheWhenPositionUnchanged()
        {
            var checker = new CachedBoundaryChecker(threshold: 50);
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 200, 200);

            checker.UpdateBoundary(100, 100, rect, boundary, null);
            bool recalculated = checker.UpdateBoundary(110, 105, rect, boundary, null);

            Assert.False(recalculated);
        }

        [Fact]
        public void UpdateBoundary_RecalculatesWhenPositionChangesSignificantly()
        {
            var checker = new CachedBoundaryChecker(threshold: 50);
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 200, 200);

            checker.UpdateBoundary(100, 100, rect, boundary, null);
            bool recalculated = checker.UpdateBoundary(200, 100, rect, boundary, null);

            Assert.True(recalculated);
        }

        [Fact]
        public void UpdateBoundary_SetsNullWhenOutsideBoundary()
        {
            var checker = new CachedBoundaryChecker();
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 50, 50); // Position 100,100 is outside this rect

            checker.UpdateBoundary(100, 100, rect, boundary, null);

            Assert.Null(checker.CachedBoundary);
        }

        [Fact]
        public void UpdateBoundary_UsesFallbackFunction()
        {
            var checker = new CachedBoundaryChecker();
            var fallbackBoundary = CreateAltBoundary();
            var rect = new XnaRectangle(0, 0, 50, 50); // Position is outside rect

            checker.UpdateBoundary(100, 100, rect, null, (x, y) => fallbackBoundary);

            Assert.Equal(fallbackBoundary, checker.CachedBoundary);
        }

        [Fact]
        public void Invalidate_ForcesCacheReset()
        {
            var checker = new CachedBoundaryChecker();
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 200, 200);

            checker.UpdateBoundary(100, 100, rect, boundary, null);
            checker.Invalidate();

            Assert.Null(checker.CachedBoundary);
        }

        [Fact]
        public void ClearCache_RemovesCachedBoundary()
        {
            var checker = new CachedBoundaryChecker();
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 200, 200);

            checker.UpdateBoundary(100, 100, rect, boundary, null);
            checker.ClearCache();

            Assert.Null(checker.CachedBoundary);
        }

        [Fact]
        public void UpdateBoundary_RecalculatesAfterInvalidate()
        {
            var checker = new CachedBoundaryChecker(threshold: 50);
            var boundary = CreateTestBoundary();
            var rect = new XnaRectangle(0, 0, 200, 200);

            checker.UpdateBoundary(100, 100, rect, boundary, null);
            checker.Invalidate();

            bool recalculated = checker.UpdateBoundary(105, 105, rect, boundary, null);

            Assert.True(recalculated);
        }
    }
}
