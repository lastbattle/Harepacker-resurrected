using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Implements Vector structure
    /// </summary>
    [DataContract]
    public struct Vector : IEquatable<Vector>
    {
        private static Vector zero = new Vector(0f, 0f);

        /// <summary>
        /// Gets the zero vector.
        /// </summary>
        /// <value>
        /// The zero vector.
        /// </value>
        public static Vector Zero
        {
            get { return zero; }
        }

        /// <summary>
        /// The X
        /// </summary>
        [DataMember(Order = 1)]
        public float X;

        /// <summary>
        /// The Y
        /// </summary>
        [DataMember(Order = 2)]
        public float Y;

        /// <summary>
        /// Gets the length.
        /// </summary>
        /// <value>
        /// The length.
        /// </value>
        public float Length
        {
            get
            {
                return (float)Math.Sqrt(X * X + Y * Y);
            }
        }

        /// <summary>
        /// Gets the length squared.
        /// </summary>
        /// <value>
        /// The length squared.
        /// </value>
        public float LengthSquared
        {
            get
            {
                return X * X + Y * Y;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vector"/> struct.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public Vector(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vector"/> struct.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public Vector(double x, double y)
        {
            X = (float)x;
            Y = (float)y;
        }

        /// <summary>
        /// Normalizes the specified vector.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns></returns>
        public static Vector Normalize(Vector vector)
        {
            vector /= Math.Max(Math.Abs(vector.X), Math.Abs(vector.Y));
            vector /= vector.Length;
            return vector;
        }

        /// <summary>
        /// Normalizes this instance.
        /// </summary>
        public void Normalize()
        {            
            this /= Math.Max(Math.Abs(X), Math.Abs(X));
            this /= Length;
        }

        /// <summary>
        /// Returns a dot product of two vectors.
        /// </summary>
        /// <param name="value1">The value1.</param>
        /// <param name="value2">The value2.</param>
        /// <returns></returns>
        public static float Dot(Vector value1, Vector value2)
        {
            return (value1.X * value2.X) + (value1.Y * value2.Y);
        }

        /// <summary>
        /// Implements the operator -.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Vector operator -(Vector vector)
        {
            return new Vector(-vector.X, -vector.Y);
        }

        /// <summary>
        /// Negates this instance.
        /// </summary>
        public void Negate()
        {
            X = -X;
            Y = -Y;
        }

        /// <summary>
        /// Implements the operator +.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Vector operator +(Vector vector1, Vector vector2)
        {
            return new Vector(vector1.X + vector2.X,
                              vector1.Y + vector2.Y);
        }

        /// <summary>
        /// Adds the specified vector1s.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns></returns>
        public static Vector Add(Vector vector1, Vector vector2)
        {
            return new Vector(vector1.X + vector2.X,
                              vector1.Y + vector2.Y);
        }

        /// <summary>
        /// Implements the operator -.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Vector operator -(Vector vector1, Vector vector2)
        {
            return new Vector(vector1.X - vector2.X,
                              vector1.Y - vector2.Y);
        }

        /// <summary>
        /// Subtracts the specified vectors.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns></returns>
        public static Vector Subtract(Vector vector1, Vector vector2)
        {
            return new Vector(vector1.X - vector2.X,
                              vector1.Y - vector2.Y);
        }

        /// <summary>
        /// Implements the operator +.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static PointF operator +(Vector vector, PointF point)
        {
            return new PointF(point.X + vector.X, point.Y + vector.Y);
        }

        /// <summary>
        /// Adds the specified vectors.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="point">The point.</param>
        /// <returns></returns>
        public static PointF Add(Vector vector, PointF point)
        {
            return new PointF(point.X + vector.X, point.Y + vector.Y);
        }

        /// <summary>
        /// Implements the operator *.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scalar">The scalar.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Vector operator *(Vector vector, float scalar)
        {
            return new Vector(vector.X * scalar,
                              vector.Y * scalar);
        }

        /// <summary>
        /// Multiplies the specified vector.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scalar">The scalar.</param>
        /// <returns></returns>
        public static Vector Multiply(Vector vector, float scalar)
        {
            return new Vector(vector.X * scalar,
                              vector.Y * scalar);
        }

        /// <summary>
        /// Implements the operator *.
        /// </summary>
        /// <param name="scalar">The scalar.</param>
        /// <param name="vector">The vector.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Vector operator *(float scalar, Vector vector)
        {
            return new Vector(vector.X * scalar,
                              vector.Y * scalar);
        }

        /// <summary>
        /// Multiplies the specified scalar.
        /// </summary>
        /// <param name="scalar">The scalar.</param>
        /// <param name="vector">The vector.</param>
        /// <returns></returns>
        public static Vector Multiply(float scalar, Vector vector)
        {
            return new Vector(vector.X * scalar,
                              vector.Y * scalar);
        }

        /// <summary>
        /// Implements the operator /.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scalar">The scalar.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static Vector operator /(Vector vector, float scalar)
        {
            return vector * (1.0f / scalar);
        }

        /// <summary>
        /// Divides the specified vector.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scalar">The scalar.</param>
        /// <returns></returns>
        public static Vector Divide(Vector vector, float scalar)
        {
            return vector * (1.0f / scalar);
        }

        /// <summary>
        /// Implements the operator *.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static float operator *(Vector vector1, Vector vector2)
        {
            return vector1.X * vector2.X + vector1.Y * vector2.Y;
        }

        /// <summary>
        /// Multiplies the specified vectors
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns></returns>
        public static float Multiply(Vector vector1, Vector vector2)
        {
            return vector1.X * vector2.X + vector1.Y * vector2.Y;
        }

        /// <summary>
        /// Determinant of specified vectors.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns></returns>
        public static float Determinant(Vector vector1, Vector vector2)
        {
            return vector1.X * vector2.Y - vector1.Y * vector2.X;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Vector"/> to <see cref="Size"/>.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator Size(Vector vector)
        {
            return new Size(Math.Abs(vector.X), Math.Abs(vector.Y));
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Vector"/> to <see cref="PointF"/>.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator PointF(Vector vector)
        {
            return new PointF(vector.X, vector.Y);
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(Vector vector1, Vector vector2)
        {
            return vector1.X == vector2.X && vector1.Y == vector2.Y;
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="vector1">The vector1.</param>
        /// <param name="vector2">The vector2.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(Vector vector1, Vector vector2)
        {
            return !(vector1 == vector2);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Vector other)
        {
            return (X == other.X) && (Y == other.Y);
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
            if (obj is Vector)
            {
                return Equals((Vector)obj);
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
            return X.GetHashCode() + Y.GetHashCode();
        }
    }
}
