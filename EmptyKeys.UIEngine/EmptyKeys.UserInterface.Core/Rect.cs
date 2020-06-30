using System;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Implements primitive float Rectangle
    /// </summary>
    public struct Rect : IEquatable<Rect>
    {
        private readonly static Rect empty = new Rect(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);

        /// <summary>
        /// Gets the empty rectangle
        /// </summary>
        /// <value>
        /// The empty rectangle
        /// </value>
        public static Rect Empty
        {
            get
            {
                return empty;
            }
        }

        /// <summary>
        /// The x
        /// </summary>
        public float X;

        /// <summary>
        /// The y
        /// </summary>
        public float Y;

        /// <summary>
        /// The width
        /// </summary>
        public float Width;

        /// <summary>
        /// The height
        /// </summary>
        public float Height;        

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        /// <value>
        /// The location.
        /// </value>
        public PointF Location
        {
            get
            {
                return new PointF(X, Y);
            }
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>
        /// The size.
        /// </value>
        public Size Size
        {
            get
            {
                return new Size(Width, Height);
            }

            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is empty.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is empty; otherwise, <c>false</c>.
        /// </value>
        public bool IsEmpty
        {
            get
            {
                return Width < 0;
            }
        }

        /// <summary>
        /// Gets the right.
        /// </summary>
        /// <value>
        /// The right.
        /// </value>
        public float Right
        {
            get
            {
                if (IsEmpty)
                {
                    return float.NegativeInfinity;
                }

                return X + Width;
            }
        }

        /// <summary>
        /// Gets the left.
        /// </summary>
        /// <value>
        /// The left.
        /// </value>
        public float Left
        {
            get
            {
                return X;
            }
        }

        /// <summary>
        /// Gets the top.
        /// </summary>
        /// <value>
        /// The top.
        /// </value>
        public float Top
        {
            get
            {
                return Y;
            }
        }

        /// <summary>
        /// Gets the bottom.
        /// </summary>
        /// <value>
        /// The bottom.
        /// </value>
        public float Bottom
        {
            get
            {
                if (IsEmpty)
                {
                    return float.NegativeInfinity;
                }

                return Y + Height;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> struct.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public Rect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> struct.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public Rect(double x, double y, double width, double height)
        {
            X = (float)x;
            Y = (float)y;
            Width = (float)width;
            Height = (float)height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> struct.
        /// </summary>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        public Rect(PointF point1, PointF point2)
        {
            X = Math.Min(point1.X, point2.X);
            Y = Math.Min(point1.Y, point2.Y);
            Width = Math.Max(Math.Max(point1.X, point2.X) - X, 0);
            Height = Math.Max(Math.Max(point1.Y, point2.Y) - Y, 0);
        }

        /// <summary>
        /// Determines whether [contains] [the specified point].
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns></returns>
        public bool Contains(PointF point)
        {
            return ((((X <= point.X) && (point.X < (X + Width))) && (Y <= point.Y)) && (point.Y < (Y + Height)));
        }

        /// <summary>
        /// Determines whether the specified rectangle intersects with this rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <returns></returns>
        public bool Intersects(Rect rectangle)
        {
            return rectangle.Left < Right && Left < rectangle.Right && rectangle.Top < Bottom && Top < rectangle.Bottom;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Rect))
            {
                return false;
            }

            return Equals((Rect)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Width.GetHashCode() ^ Height.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0},{1},{2},{3}", X, Y, Width, Height);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Rect other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="rect1">The rect1.</param>
        /// <param name="rect2">The rect2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(Rect rect1, Rect rect2)
        {
            return rect1.X == rect2.X && rect1.Y == rect2.Y && rect1.Width == rect2.Width && rect1.Height == rect2.Height;
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="rect1">The rect1.</param>
        /// <param name="rect2">The rect2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(Rect rect1, Rect rect2)
        {
            return !(rect1 == rect2);
        }
    }
}
