using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements MonoGame specific texture
    /// </summary>
    public class MonoGameTexture : TextureBase
    {
        private Texture2D texture;

        /// <summary>
        /// Gets the width.
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        public override int Width
        {
            get { return texture == null ? 0 : texture.Width; }
        }

        /// <summary>
        /// Gets the height.
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        public override int Height
        {
            get { return texture == null ? 0 : texture.Height; }
        }

        /// <summary>
        /// Gets the format.
        /// </summary>
        /// <value>
        /// The format.
        /// </value>
        public override TextureSurfaceFormat Format
        {
            get { return (TextureSurfaceFormat)(int)texture.Format; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameTexture"/> class.
        /// </summary>
        /// <param name="nativeTexture">The native texture.</param>
        public MonoGameTexture(object nativeTexture)
            : base(nativeTexture)
        {
            texture = nativeTexture as Texture2D;
        }

        /// <summary>
        /// Gets the native texture.
        /// </summary>
        /// <returns></returns>
        public override object GetNativeTexture()
        {
            return texture;
        }

        /// <summary>
        /// Generates the one to one white texture
        /// </summary>
        public override void GenerateOneToOne()
        {
            if (Height != 1 || Width != 1)
            {
                return;
            }

            texture.SetData<Color>(new Color[] { Color.White });
        }

        /// <summary>
        /// Generates the solid color texture content
        /// </summary>
        /// <param name="borderThickness">The border thickness.</param>
        /// <param name="isBorder">if set to <c>true</c> [is border].</param>
        public override void GenerateSolidColor(Thickness borderThickness, bool isBorder)
        {
            Color[] data = new Color[Width * Height];

            for (int i = 0; i < data.Length; i++)
            {
                int y = (i / Width);
                int x = i - (y * Width);
                if ((borderThickness.Top > y || borderThickness.Bottom >= Height - y) ||
                    (borderThickness.Left > x || borderThickness.Right >= Width - x))
                {
                    data[i] = new Color { PackedValue = uint.MaxValue };
                }
                else
                {
                    data[i] = new Color { PackedValue = 0 };
                }
            }

            texture.SetData<Color>(data);
        }

        /// <summary>
        /// Generates the linear gradient texture content
        /// </summary>
        /// <param name="lineStart">The line start.</param>
        /// <param name="lineEnd">The line end.</param>
        /// <param name="borderThickness">The border thickness.</param>
        /// <param name="sortedStops">The sorted stops.</param>
        /// <param name="spread">The spread.</param>
        /// <param name="isBorder">if set to <c>true</c> [is border].</param>
        public override void GenerateLinearGradient(PointF lineStart, PointF lineEnd, Thickness borderThickness, List<GradientStop> sortedStops,
            GradientSpreadMethod spread, bool isBorder)
        {
            Color[] data = new Color[Width * Height];
            Color startColor = Color.TransparentBlack;
            Color endColor = Color.TransparentBlack;
            PointF point = new PointF();
            float length = GetLength(lineStart, lineEnd);

            int index = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    point.Y = y;
                    point.X = x;
                    if ((borderThickness.Top > point.Y || borderThickness.Bottom >= Height - point.Y) ||
                        (borderThickness.Left > point.X || borderThickness.Right >= Width - point.X)
                        || !isBorder)
                    {
                        PointF projectPoint = ProjectToLine(lineStart, lineEnd, point, Width);
                        float finalOffset = GetLength(projectPoint, lineStart) / length;

                        switch (spread)
                        {
                            case GradientSpreadMethod.Pad:
                                if (projectPoint.X <= lineStart.X && projectPoint.Y <= lineStart.Y)
                                {
                                    finalOffset = 0;
                                }

                                if (projectPoint.X >= lineEnd.X && projectPoint.Y >= lineEnd.Y)
                                {
                                    finalOffset = 1;
                                }

                                break;
                            case GradientSpreadMethod.Reflect:
                                if (projectPoint.X <= lineStart.X && projectPoint.Y <= lineStart.Y ||
                                    projectPoint.X >= lineEnd.X && projectPoint.Y >= lineEnd.Y)
                                {
                                    if (finalOffset > 1)
                                    {
                                        finalOffset = ((float)Math.Ceiling(finalOffset) - finalOffset);
                                    }
                                }                                

                                break;
                            case GradientSpreadMethod.Repeat:

                                if (projectPoint.X <= lineStart.X && projectPoint.Y <= lineStart.Y)
                                {
                                    if (finalOffset > 0)
                                    {
                                        finalOffset = ((float)Math.Ceiling(finalOffset) - finalOffset);
                                    }
                                }

                                if (projectPoint.X >= lineEnd.X && projectPoint.Y >= lineEnd.Y)
                                {
                                    if (finalOffset > 1)
                                    {
                                        finalOffset = (finalOffset - (float)Math.Floor(finalOffset));
                                    }
                                }

                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }

                        GradientStop startStop = GetStartStop(finalOffset, sortedStops);
                        startColor = startStop != null ? new Color { PackedValue = startStop.Color.PackedValue } : Color.TransparentBlack;
                        GradientStop endStop = GetEndStop(finalOffset, sortedStops, spread);
                        endColor = endStop != null ? new Color { PackedValue = endStop.Color.PackedValue } : Color.TransparentBlack;
                        if (endStop != null && startStop != null)
                        {
                            finalOffset = (finalOffset - startStop.Offset) * (1f / (endStop.Offset - startStop.Offset));
                        }

                        if (float.IsInfinity(finalOffset))
                        {
                            finalOffset = 0;
                        }

                        if (float.IsNaN(finalOffset))
                        {
                            finalOffset = 1;
                        }

                        Color finalColor = Color.Lerp(startColor, endColor, finalOffset);
                        data[index] = finalColor;
                    }
                    else
                    {
                        data[index] = Color.TransparentBlack;
                    }

                    index++;
                }
            }

            texture.SetData<Color>(data);
        }

        private static float GetLength(PointF start, PointF end)
        {
            float x = end.X - start.X;
            float y = end.Y - start.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static GradientStop GetStartStop(float offset, List<GradientStop> stops)
        {
            for (int i = stops.Count - 1; i >= 0; i--)
            {
                GradientStop stop = stops[i];
                if (offset >= stop.Offset)
                {
                    return stop;
                }
            }

            if (stops.Count != 0)
            {
                return stops[0];
            }

            return null;
        }

        private static GradientStop GetEndStop(float offset, List<GradientStop> stops, GradientSpreadMethod spreadMethod)
        {
            foreach (var gStop in stops)
            {
                if (gStop.Offset > offset)
                {
                    return gStop;
                }
            }

            if (stops.Count != 0)
            {
                return stops[stops.Count - 1];
            }

            return null;
        }

        private static PointF ProjectToLine(PointF lineStart, PointF lineEnd, PointF toProject, int width)
        {
            float n1 = lineStart.X;
            if (lineStart.X == lineEnd.X)
            {
                n1 -= 1f / width;
            }

            double m = (double)(lineEnd.Y - lineStart.Y) / (lineEnd.X - n1);
            double b = (double)lineStart.Y - (m * lineStart.X);

            double x = (m * (double)toProject.Y + (double)toProject.X - m * b) / (m * m + 1);
            double y = (m * m * (double)toProject.Y + m * (double)toProject.X + b) / (m * m + 1);

            return new PointF((float)x, (float)y);
        }

        /// <summary>
        /// Generates the check box texture content
        /// </summary>
        public override void GenerateCheckbox()
        {
            Color[] data = new Color[Width * Height];
            for (int i = 0; i < data.Length; i++)
            {
                float y = (i / Width);
                float x = i - (y * Width);
                float right = (Width - 2) - (float)Math.Round(y / 2f, MidpointRounding.AwayFromZero);
                float left = y - (float)Math.Round(Width / 2f, MidpointRounding.AwayFromZero) - 1;
                if (x == right || x - 1 == right)
                {
                    data[i] = Color.White;
                }
                else if (x == left || x - 1 == left)
                {
                    data[i] = Color.White;
                }
            }

            texture.SetData<Color>(data);
        }

        /// <summary>
        /// Generates the arrow.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="startX">The start x point</param>
        /// <param name="lineSize">Size of the line.</param>
        public override void GenerateArrow(ArrowDirection direction, int startX, int lineSize)
        {
            int prevY = 0;
            Color[] data = new Color[Width * Height];
            for (int i = 0; i < data.Length; i++)
            {
                int y = (i / Width);
                int x = i - (y * Width);

                if (x > startX && startX + lineSize > x)
                {
                    data[i] = Color.White;
                }

                if (prevY != y)
                {
                    switch (direction)
                    {
                        case ArrowDirection.Up:
                            startX--;
                            lineSize += 2;
                            break;
                        case ArrowDirection.Down:
                            startX++;
                            lineSize -= 2;
                            break;
                        case ArrowDirection.Left:
                            if (Height / 2f >= y)
                            {
                                lineSize++;
                                startX--;
                            }
                            else
                            {
                                lineSize--;
                                startX++;
                            }
                            break;
                        case ArrowDirection.Right:
                            float half = Height / 2f;
                            if (half >= y)
                            {
                                lineSize++;
                            }
                            else
                            {
                                lineSize--;
                            }
                            break;
                        default:
                            break;
                    }
                    prevY = y;
                }
            }

            texture.SetData<Color>(data);
        }

        /// <summary>
        /// Sets the color data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <exception cref="System.ArgumentException">Wrong data size</exception>
        public override void SetColorData(uint[] data)
        {
            if (data.Length != Width * Height)
            {
                throw new ArgumentException("Wrong data size");
            }

            Color[] buffer = new Color[Width * Height];
            for (int i = 0; i < data.Length; i++)
            {
                buffer[i] = new Color { PackedValue = data[i] };
            }

            texture.SetData<Color>(buffer);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            if (!texture.IsDisposed)
            {
                texture.Dispose();
            }
        }        
    }
}