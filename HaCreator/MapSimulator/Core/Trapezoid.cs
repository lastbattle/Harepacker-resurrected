using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Represents a trapezoid shape for hit detection.
    /// Used by skills like Assaulter, Band of Thieves, etc.
    /// Based on CMobPool hit detection in MapleStory client.
    ///
    /// The trapezoid is defined by:
    /// - Origin point (start of attack)
    /// - Direction (left or right facing)
    /// - Near width (width at origin)
    /// - Far width (width at maximum range)
    /// - Range (length of trapezoid)
    /// - Height offset (vertical range)
    /// </summary>
    public struct Trapezoid
    {
        /// <summary>X coordinate of the origin point</summary>
        public float OriginX;

        /// <summary>Y coordinate of the origin point</summary>
        public float OriginY;

        /// <summary>Width at the near (origin) end</summary>
        public float NearWidth;

        /// <summary>Width at the far end</summary>
        public float FarWidth;

        /// <summary>Horizontal range/length of the trapezoid</summary>
        public float Range;

        /// <summary>Vertical offset from origin (for height-based skills)</summary>
        public float HeightOffset;

        /// <summary>True if facing right, false if facing left</summary>
        public bool FacingRight;

        /// <summary>
        /// Create a trapezoid for hit detection
        /// </summary>
        /// <param name="originX">X coordinate of origin</param>
        /// <param name="originY">Y coordinate of origin</param>
        /// <param name="nearWidth">Width at origin (full height, split above/below)</param>
        /// <param name="farWidth">Width at far end (full height, split above/below)</param>
        /// <param name="range">Horizontal range</param>
        /// <param name="facingRight">Direction of attack</param>
        /// <param name="heightOffset">Vertical offset (positive = up)</param>
        public Trapezoid(float originX, float originY, float nearWidth, float farWidth, float range, bool facingRight, float heightOffset = 0)
        {
            OriginX = originX;
            OriginY = originY;
            NearWidth = nearWidth;
            FarWidth = farWidth;
            Range = range;
            FacingRight = facingRight;
            HeightOffset = heightOffset;
        }

        /// <summary>
        /// Create a trapezoid from a rectangle (uniform width)
        /// </summary>
        public static Trapezoid FromRectangle(float x, float y, float width, float height, bool facingRight)
        {
            return new Trapezoid(
                facingRight ? x : x + width,
                y + height / 2,
                height,
                height,
                width,
                facingRight
            );
        }

        /// <summary>
        /// Create a cone-shaped trapezoid (starts narrow, expands)
        /// </summary>
        public static Trapezoid CreateCone(float originX, float originY, float startWidth, float endWidth, float range, bool facingRight)
        {
            return new Trapezoid(originX, originY, startWidth, endWidth, range, facingRight);
        }

        /// <summary>
        /// Create a reverse cone (starts wide, narrows)
        /// </summary>
        public static Trapezoid CreateReverseCone(float originX, float originY, float startWidth, float endWidth, float range, bool facingRight)
        {
            return new Trapezoid(originX, originY, startWidth, endWidth, range, facingRight);
        }

        /// <summary>
        /// Check if a point is inside the trapezoid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsPoint(float px, float py)
        {
            // Calculate horizontal distance from origin
            float dx = FacingRight ? (px - OriginX) : (OriginX - px);

            // Point must be in front of origin (in attack direction)
            if (dx < 0 || dx > Range)
                return false;

            // Calculate the width at this distance (linear interpolation)
            float t = Range > 0 ? dx / Range : 0;
            float widthAtDistance = NearWidth + t * (FarWidth - NearWidth);
            float halfWidth = widthAtDistance / 2;

            // Adjust origin Y by height offset
            float adjustedOriginY = OriginY - HeightOffset;

            // Check vertical distance
            float dy = py - adjustedOriginY;
            return Math.Abs(dy) <= halfWidth;
        }

        /// <summary>
        /// Check if a rectangle intersects with this trapezoid
        /// </summary>
        public bool IntersectsRect(Rectangle rect)
        {
            // Quick bounds check first
            float minX = FacingRight ? OriginX : OriginX - Range;
            float maxX = FacingRight ? OriginX + Range : OriginX;
            float maxWidth = Math.Max(NearWidth, FarWidth);
            float minY = OriginY - HeightOffset - maxWidth / 2;
            float maxY = OriginY - HeightOffset + maxWidth / 2;

            // Check if bounding boxes overlap
            if (rect.Right < minX || rect.Left > maxX ||
                rect.Bottom < minY || rect.Top > maxY)
                return false;

            // Check center point of rectangle
            float centerX = rect.X + rect.Width / 2f;
            float centerY = rect.Y + rect.Height / 2f;
            if (ContainsPoint(centerX, centerY))
                return true;

            // Check corners
            if (ContainsPoint(rect.Left, rect.Top)) return true;
            if (ContainsPoint(rect.Right, rect.Top)) return true;
            if (ContainsPoint(rect.Left, rect.Bottom)) return true;
            if (ContainsPoint(rect.Right, rect.Bottom)) return true;

            // Check edge midpoints
            if (ContainsPoint(centerX, rect.Top)) return true;
            if (ContainsPoint(centerX, rect.Bottom)) return true;
            if (ContainsPoint(rect.Left, centerY)) return true;
            if (ContainsPoint(rect.Right, centerY)) return true;

            return false;
        }

        /// <summary>
        /// Get the bounding rectangle of this trapezoid
        /// </summary>
        public Rectangle GetBoundingRect()
        {
            float minX = FacingRight ? OriginX : OriginX - Range;
            float maxX = FacingRight ? OriginX + Range : OriginX;
            float maxWidth = Math.Max(NearWidth, FarWidth);
            float minY = OriginY - HeightOffset - maxWidth / 2;
            float maxY = OriginY - HeightOffset + maxWidth / 2;

            return new Rectangle(
                (int)minX,
                (int)minY,
                (int)(maxX - minX),
                (int)(maxY - minY)
            );
        }

        /// <summary>
        /// Get the four corner points of the trapezoid
        /// </summary>
        public (Vector2 nearTop, Vector2 nearBottom, Vector2 farTop, Vector2 farBottom) GetCorners()
        {
            float halfNearWidth = NearWidth / 2;
            float halfFarWidth = FarWidth / 2;
            float adjustedY = OriginY - HeightOffset;

            float farX = FacingRight ? OriginX + Range : OriginX - Range;

            return (
                new Vector2(OriginX, adjustedY - halfNearWidth),      // nearTop
                new Vector2(OriginX, adjustedY + halfNearWidth),      // nearBottom
                new Vector2(farX, adjustedY - halfFarWidth),          // farTop
                new Vector2(farX, adjustedY + halfFarWidth)           // farBottom
            );
        }

        /// <summary>
        /// Scale the trapezoid by a factor
        /// </summary>
        public Trapezoid Scale(float factor)
        {
            return new Trapezoid(
                OriginX,
                OriginY,
                NearWidth * factor,
                FarWidth * factor,
                Range * factor,
                FacingRight,
                HeightOffset * factor
            );
        }

        /// <summary>
        /// Offset the trapezoid by a delta
        /// </summary>
        public Trapezoid Offset(float dx, float dy)
        {
            return new Trapezoid(
                OriginX + dx,
                OriginY + dy,
                NearWidth,
                FarWidth,
                Range,
                FacingRight,
                HeightOffset
            );
        }

        public override string ToString()
        {
            return $"Trapezoid(Origin=({OriginX},{OriginY}), Near={NearWidth}, Far={FarWidth}, Range={Range}, Facing={( FacingRight ? "R" : "L")})";
        }
    }
}
