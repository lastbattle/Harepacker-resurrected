using System;
using System.Runtime.Serialization;
using System.Text;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements RGBA color structure
    /// </summary>
    [DataContract]
    public struct ColorW : IEquatable<ColorW>
    {
        private static readonly ColorW white = new ColorW(uint.MaxValue);
        private static readonly ColorW black = new ColorW((uint)Colors.Black);
        private static readonly ColorW transparent = new ColorW(0);

        /// <summary>
        /// Gets the white Color
        /// </summary>
        /// <value>
        /// The white color
        /// </value>
        public static ColorW White { get { return white; } }

        /// <summary>
        /// Gets the black color
        /// </summary>
        /// <value>
        /// The black color
        /// </value>
        public static ColorW Black { get { return black; } }

        /// <summary>
        /// Gets the transparent black color
        /// </summary>
        /// <value>
        /// The transparent black color
        /// </value>
        public static ColorW TransparentBlack { get { return transparent; } }

        private uint packedValue;

        /// <summary>
        /// Gets or sets the red component
        /// </summary>
        /// <value>
        /// The red component
        /// </value>
        [DataMember(Order = 1)]
        public byte R
        {
            get
            {
                unchecked
                {
                    return (byte)this.packedValue;
                }
            }
            set
            {
                this.packedValue = (this.packedValue & 0xffffff00) | value;
            }
        }

        /// <summary>
        /// Gets or sets the green component
        /// </summary>
        /// <value>
        /// The green component
        /// </value>
        [DataMember(Order = 2)]
        public byte G
        {
            get
            {
                unchecked
                {
                    return (byte)(this.packedValue >> 8);
                }
            }
            set
            {
                this.packedValue = (this.packedValue & 0xffff00ff) | ((uint)value << 8);
            }
        }

        /// <summary>
        /// Gets or sets the blue component
        /// </summary>
        /// <value>
        /// The blue component
        /// </value>
        [DataMember(Order = 3)]
        public byte B
        {
            get
            {
                unchecked
                {
                    return (byte)(this.packedValue >> 16);
                }
            }
            set
            {
                this.packedValue = (this.packedValue & 0xff00ffff) | ((uint)value << 16);
            }
        }        

        /// <summary>
        /// Gets or sets alpha
        /// </summary>
        /// <value>
        /// Alpha
        /// </value>
        [DataMember(Order = 4)]
        public byte A
        {
            get
            {
                unchecked
                {
                    return (byte)(this.packedValue >> 24);
                }
            }
            set
            {
                this.packedValue = (this.packedValue & 0x00ffffff) | ((uint)value << 24);
            }
        }

        /// <summary>
        /// Gets or sets the packed value of the color
        /// </summary>
        /// <value>
        /// The packed value.
        /// </value>
        public uint PackedValue
        {
            get { return packedValue; }
            set { packedValue = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorW"/> class.
        /// </summary>
        /// <param name="packedValue">The packed value.</param>
        public ColorW(uint packedValue)
        {
            this.packedValue = packedValue;
        }        

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorW"/> struct.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        public ColorW(int r, int g, int b)
        {
            packedValue = 0;
            R = (byte)Clamp(r, Byte.MinValue, Byte.MaxValue);
            G = (byte)Clamp(g, Byte.MinValue, Byte.MaxValue);
            B = (byte)Clamp(b, Byte.MinValue, Byte.MaxValue);
            A = (byte)255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorW" /> class.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        /// <param name="alpha">The alpha.</param>
        public ColorW(int r, int g, int b, int alpha)
        {
            packedValue = 0;
            R = (byte)Clamp(r, Byte.MinValue, Byte.MaxValue);
            G = (byte)Clamp(g, Byte.MinValue, Byte.MaxValue);
            B = (byte)Clamp(b, Byte.MinValue, Byte.MaxValue);
            A = (byte)Clamp(alpha, Byte.MinValue, Byte.MaxValue);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorW"/> struct.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        public ColorW(float r, float g, float b)
        {
            packedValue = 0;
            R = (byte)Clamp(r * 255, Byte.MinValue, Byte.MaxValue);
            G = (byte)Clamp(g * 255, Byte.MinValue, Byte.MaxValue);
            B = (byte)Clamp(b * 255, Byte.MinValue, Byte.MaxValue);
            A = (byte)255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorW" /> struct.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        /// <param name="alpha">The alpha.</param>
        public ColorW(float r, float g, float b, float alpha)
        {
            packedValue = 0;
            R = (byte)Clamp(r * 255, Byte.MinValue, Byte.MaxValue);
            G = (byte)Clamp(g * 255, Byte.MinValue, Byte.MaxValue);
            B = (byte)Clamp(b * 255, Byte.MinValue, Byte.MaxValue);
            A = (byte)Clamp(alpha * 255, Byte.MinValue, Byte.MaxValue);
        }

        private static float Clamp(float value, float min, float max)
        {            
            value = (value > max) ? max : value;            
            value = (value < min) ? min : value;
            return value;
        }

        private static float Clamp(int value, int min, int max)
        {
            value = (value > max) ? max : value;
            value = (value < min) ? min : value;
            return value;
        }

        /// <summary>
        /// Implements the operator *.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static ColorW operator *(ColorW value, float scale)
        {
            return new ColorW((int)(value.R * scale), (int)(value.G * scale), (int)(value.B * scale), (int)(value.A * scale));
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="color1">A.</param>
        /// <param name="color2">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(ColorW color1, ColorW color2)
        {
            return (color1.A == color2.A && color1.R == color2.R && color1.G == color2.G && color1.B == color2.B);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="color1">A.</param>
        /// <param name="color2">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(ColorW color1, ColorW color2)
        {
            return !(color1 == color2);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return packedValue.GetHashCode();
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
            return ((obj is ColorW) && this.Equals((ColorW)obj));
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(ColorW other)
        {
            return PackedValue == other.PackedValue;
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder(9);
            result.AppendFormat("#{0:X2}{1:X2}{2:X2}{3:X2}", A, R, G, B);
            return result.ToString();
        }

        /// <summary>
        /// Lerp between value1 and value2 by amount
        /// </summary>
        /// <param name="value1">The value1.</param>
        /// <param name="value2">The value2.</param>
        /// <param name="amount">The amount.</param>
        /// <returns></returns>
        public static ColorW Lerp(ColorW value1, ColorW value2, float amount)
        {
            amount = Clamp(amount, 0, 1);
            return new ColorW((int)Lerp(value1.R, value2.R, amount), (int)Lerp(value1.G, value2.G, amount), (int)Lerp(value1.B, value2.B, amount), (int)Lerp(value1.A, value2.A, amount));
        }

        private static float Lerp(float value1, float value2, float amount)
        {
            return value1 + (value2 - value1) * amount;
        }
    }
}
