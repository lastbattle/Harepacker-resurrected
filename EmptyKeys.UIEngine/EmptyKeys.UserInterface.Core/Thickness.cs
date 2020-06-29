using System;
using System.Text;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Thickness is describing area of rectangle (border, margin, padding)
    /// </summary>
    public struct Thickness : IEquatable<Thickness>
    {
        private static Thickness zero = new Thickness(0f);

        /// <summary>
        /// The zero thickness
        /// </summary>
        /// <value>
        /// The zero.
        /// </value>
        public static Thickness Zero
        {
            get
            {
                return zero;
            }
        }

        /// <summary>
        /// The left
        /// </summary>
        public float Left;

        /// <summary>
        /// The top
        /// </summary>
        public float Top;

        /// <summary>
        /// The right
        /// </summary>
        public float Right;

        /// <summary>
        /// The bottom
        /// </summary>
        public float Bottom;

        /// <summary>
        /// Initializes a new instance of the <see cref="Thickness"/> structure
        /// </summary>
        /// <param name="uniformLength">Length of the uniform.</param>
        public Thickness(float uniformLength)
        {
            Left = Top = Right = Bottom = uniformLength;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Thickness"/> struct.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="top">The top.</param>
        /// <param name="right">The right.</param>
        /// <param name="bottom">The bottom.</param>
        public Thickness(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Thickness other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
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
            if (obj is Thickness)
            {
                return this == (Thickness)obj;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return Left.GetHashCode() ^ Top.GetHashCode() ^ Right.GetHashCode() ^ Bottom.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder(64);
            result.AppendFormat("{0}, {1}, {2}, {3}", Left, Top, Right, Bottom);
            return result.ToString();
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="t1">The t1.</param>
        /// <param name="t2">The t2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(Thickness t1, Thickness t2)
        {
            return ((t1.Left == t2.Left || (float.IsNaN(t1.Left) && float.IsNaN(t2.Left)))
                    && (t1.Top == t2.Top || (float.IsNaN(t1.Top) && float.IsNaN(t2.Top)))
                    && (t1.Right == t2.Right || (float.IsNaN(t1.Right) && float.IsNaN(t2.Right)))
                    && (t1.Bottom == t2.Bottom || (float.IsNaN(t1.Bottom) && float.IsNaN(t2.Bottom))));
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="t1">The t1.</param>
        /// <param name="t2">The t2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(Thickness t1, Thickness t2)
        {
            return (!(t1 == t2));
        }

        /// <summary>
        /// Implements the operator +.
        /// </summary>
        /// <param name="t1">The t1.</param>
        /// <param name="t2">The t2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Thickness operator +(Thickness t1, Thickness t2)
        {            
            return new Thickness(
                t1.Left + t2.Left,
                t1.Top + t2.Top,
                t1.Right + t2.Right,
                t1.Bottom + t2.Bottom);                
        }

        /// <summary>
        /// Implements the operator -.
        /// </summary>
        /// <param name="t1">The t1.</param>
        /// <param name="t2">The t2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Thickness operator -(Thickness t1, Thickness t2)
        {
            return new Thickness(
                t1.Left - t2.Left,
                t1.Top - t2.Top,
                t1.Right - t2.Right,
                t1.Bottom - t2.Bottom);
        }
    }
}
