using HaSharedLibrary.Render;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Cached boundary checker for mirror/reflection calculations.
    /// Optimizes boundary checks by caching results and only recalculating when position changes significantly.
    /// Used by MobItem, NpcItem, and other entities that need reflection boundary checks.
    /// </summary>
    public class CachedBoundaryChecker
    {
        private ReflectionDrawableBoundary _cachedBoundary = null;
        private int _lastCheckX = int.MinValue;
        private int _lastCheckY = int.MinValue;
        private readonly int _threshold;

        /// <summary>
        /// Default threshold: only recheck if moved more than 50 pixels
        /// </summary>
        public const int DEFAULT_THRESHOLD = 50;

        /// <summary>
        /// Gets the cached boundary result
        /// </summary>
        public ReflectionDrawableBoundary CachedBoundary => _cachedBoundary;

        /// <summary>
        /// Creates a new cached boundary checker
        /// </summary>
        /// <param name="threshold">Distance threshold before recalculating (default: 50 pixels)</param>
        public CachedBoundaryChecker(int threshold = DEFAULT_THRESHOLD)
        {
            _threshold = threshold;
        }

        /// <summary>
        /// Updates the cached boundary if the position has changed significantly.
        /// </summary>
        /// <param name="currentX">Current X position</param>
        /// <param name="currentY">Current Y position</param>
        /// <param name="mirrorBottomRect">Mirror bottom rectangle</param>
        /// <param name="mirrorBottomReflection">Mirror bottom reflection info</param>
        /// <param name="checkMirrorFieldData">Function to check mirror field data boundaries</param>
        /// <returns>True if boundary was recalculated, false if cached value was used</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UpdateBoundary(int currentX, int currentY,
            Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData)
        {
            // Skip threshold check on first call to avoid int.MinValue overflow
            if (_lastCheckX != int.MinValue)
            {
                int dx = Math.Abs(currentX - _lastCheckX);
                int dy = Math.Abs(currentY - _lastCheckY);
                if (dx < _threshold && dy < _threshold)
                    return false; // Use cached value
            }

            _lastCheckX = currentX;
            _lastCheckY = currentY;

            // Check mirror boundaries
            _cachedBoundary = null;
            if (mirrorBottomReflection != null && mirrorBottomRect.Contains(new Point(currentX, currentY)))
            {
                _cachedBoundary = mirrorBottomReflection;
            }
            else if (checkMirrorFieldData != null)
            {
                _cachedBoundary = checkMirrorFieldData(currentX, currentY);
            }

            return true; // Boundary was recalculated
        }

        /// <summary>
        /// Forces a boundary recalculation on the next update
        /// </summary>
        public void Invalidate()
        {
            _lastCheckX = int.MinValue;
            _lastCheckY = int.MinValue;
            _cachedBoundary = null;
        }

        /// <summary>
        /// Resets the cached boundary without forcing recalculation
        /// </summary>
        public void ClearCache()
        {
            _cachedBoundary = null;
        }
    }
}
