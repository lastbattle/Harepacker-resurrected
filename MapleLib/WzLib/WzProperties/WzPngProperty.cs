/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MapleLib.Converters;
using MapleLib.WzLib.Util;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains the information for a bitmap
    /// https://docs.microsoft.com/en-us/windows/win32/direct3d9/compressed-texture-resources
    /// https://code.google.com/archive/p/libsquish/
    /// https://github.com/svn2github/libsquish
    /// http://www.sjbrown.co.uk/2006/01/19/dxt-compression-techniques/
    /// https://en.wikipedia.org/wiki/S3_Texture_Compression
    /// </summary>
    public class WzPngProperty : WzImageProperty
    {
        #region Fields
        private int width, height, format, format2;
        internal byte[] compressedImageBytes;
        internal Bitmap png;
        internal WzObject parent;
        //internal WzImage imgParent;
        internal bool listWzUsed = false;

        internal WzBinaryReader wzReader;
        internal long offs;
        #endregion

        #region Inherited Members
        public override void SetValue(object value)
        {
            if (value is Bitmap)
                SetImage((Bitmap)value);
            else compressedImageBytes = (byte[])value;
        }

        public override WzImageProperty DeepClone()
        {
            WzPngProperty clone = new WzPngProperty();
            clone.SetImage(GetImage(false));
            return clone;
        }

        public override object WzValue { get { return GetImage(false); } }
        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /*/// <summary>
        /// The image that this property is contained in
        /// </summary>
        public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return "PNG"; } set { } }
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.PNG; } }
        public override void WriteValue(WzBinaryWriter writer)
        {
            throw new NotImplementedException("Cannot write a PngProperty");
        }
        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            compressedImageBytes = null;
            if (png != null)
            {
                png.Dispose();
                png = null;
            }
            //this.wzReader.Close(); // closes at WzFile
            this.wzReader = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The width of the bitmap
        /// </summary>
        public int Width { get { return width; } set { width = value; } }
        /// <summary>
        /// The height of the bitmap
        /// </summary>
        public int Height { get { return height; } set { height = value; } }
        /// <summary>
        /// The format of the bitmap
        /// </summary>
        public int Format
        {
            get { return format + format2; }
            set
            {
                format = value;
                format2 = 0;
            }
        }
        public int Format2
        {
            get { return format2; }
            set
            {
                format2 = value;
            }
        }

        /// <summary>
        /// Wz PNG format to Microsoft.Xna.Framework.Graphics.SurfaceFormat
        /// https://github.com/Kagamia/WzComparerR2/search?q=wzlibextension
        /// </summary>
        /// <param name="pngform"></param>
        /// <returns></returns>
        public SurfaceFormat GetXNASurfaceFormat()
        {
            switch (Format)
            {
                case 1: return SurfaceFormat.Bgra4444;
                case 2:
                case 3: return SurfaceFormat.Bgra32;
                case 513:
                case 517: return SurfaceFormat.Bgr565;
                case 1026: return SurfaceFormat.Dxt3;
                case 2050: return SurfaceFormat.Dxt5;
                default: return SurfaceFormat.Bgra32;
            }
        }


        public bool ListWzUsed
        {
            get
            {
                return listWzUsed;
            }
            set
            {
                if (value != listWzUsed)
                {
                    listWzUsed = value;
                    CompressPng(GetImage(false));
                }
            }
        }
        /// <summary>
        /// The actual bitmap
        /// </summary>
        public Bitmap PNG
        {
            set
            {
                this.png = value;
                CompressPng(this.png);
            }
        }

        /// <summary>
        /// Creates a blank WzPngProperty
        /// </summary>
        public WzPngProperty() { }

        /// <summary>
        /// Creates a blank WzPngProperty 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="parseNow"></param>
        internal WzPngProperty(WzBinaryReader reader, bool parseNow)
        {
            // Read compressed bytes
            width = reader.ReadCompressedInt();
            height = reader.ReadCompressedInt();
            format = reader.ReadCompressedInt();
            format2 = reader.ReadCompressedInt();
            reader.BaseStream.Position += 4;
            offs = reader.BaseStream.Position;
            int len = reader.ReadInt32() - 1;
            reader.BaseStream.Position += 1;

            lock (reader) // lock WzBinaryReader, allowing it to be loaded from multiple threads at once
            {
                if (len > 0)
                {
                    if (parseNow)
                    {
                        if (wzReader == null) // when saving the WZ file to a new encryption
                        {
                            compressedImageBytes = reader.ReadBytes(len);
                        }
                        else // when opening the Wz property
                        {
                            compressedImageBytes = wzReader.ReadBytes(len);
                        }
                        ParsePng(true);
                    }
                    else
                        reader.BaseStream.Position += len;
                }
                this.wzReader = reader;
            }
        }
        #endregion

        #region Parsing Methods
        public byte[] GetCompressedBytes(bool saveInMemory)
        {
            if (compressedImageBytes == null)
            {
                lock (wzReader)// lock WzBinaryReader, allowing it to be loaded from multiple threads at once
                {
                    long pos = this.wzReader.BaseStream.Position;
                    this.wzReader.BaseStream.Position = offs;
                    int len = this.wzReader.ReadInt32() - 1;
                    if (len <= 0) // possibility an image written with the wrong wzIv 
                        throw new Exception("The length of the image is negative. WzPngProperty. Wrong WzIV?");

                    this.wzReader.BaseStream.Position += 1;

                    if (len > 0)
                        compressedImageBytes = this.wzReader.ReadBytes(len);
                    this.wzReader.BaseStream.Position = pos;
                }

                if (!saveInMemory)
                {
                    //were removing the referance to compressedBytes, so a backup for the ret value is needed
                    byte[] returnBytes = compressedImageBytes;
                    compressedImageBytes = null;
                    return returnBytes;
                }
            }
            return compressedImageBytes;
        }

        public void SetImage(Bitmap png)
        {
            this.png = png;
            CompressPng(png);
        }

        public Bitmap GetImage(bool saveInMemory)
        {
            if (png == null)
            {
                ParsePng(saveInMemory);
            }
            return png;
        }

        internal byte[] Decompress(byte[] compressedBuffer, int decompressedSize)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                memStream.Write(compressedBuffer, 2, compressedBuffer.Length - 2);
                byte[] buffer = new byte[decompressedSize];
                memStream.Position = 0;

                using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Decompress))
                {
                    zip.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
            }
        }

        internal byte[] Compress(byte[] decompressedBuffer)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Compress, true))
                {
                    zip.Write(decompressedBuffer, 0, decompressedBuffer.Length);
                }
                memStream.Position = 0;
                byte[] buffer = new byte[memStream.Length + 2];
                memStream.Read(buffer, 2, buffer.Length - 2);

                System.Buffer.BlockCopy(new byte[] { 0x78, 0x9C }, 0, buffer, 0, 2);

                return buffer;
            }
        }

        public void ParsePng(bool saveInMemory, Texture2D texture2d = null)
        {
            byte[] rawBytes = GetRawImage(saveInMemory);
            if (rawBytes == null)
            {
                png = null;
                return;
            }
            try
            {
                Bitmap bmp = null;
                Rectangle rect_ = new Rectangle(0, 0, width, height);

                switch (Format)
                {
                    case 1:
                        {
                            bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImage_PixelDataBgra4444(rawBytes, width, height, bmp, bmpData);
                            break;
                        }
                    case 2:
                        {
                            bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case 3:
                        {
                            // New format 黑白缩略图
                            // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                            // you'll be remembered forever <3 
                            bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImageDXT3(rawBytes, width, height, bmp, bmpData);
                            break;
                        }
                    case 257: // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                        {
                            bmp = new Bitmap(width, height, PixelFormat.Format16bppArgb1555);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
                            // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                            CopyBmpDataWithStride(rawBytes, bmp.Width * 2, bmpData);

                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case 513: // nexon wizet logo
                        {
                            bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);

                            Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case 517:
                        {
                            bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);

                            DecompressImage_PixelDataForm517(rawBytes, width, height, bmp, bmpData);
                            break;
                        }
                    case 1026:
                        {
                            bmp = new Bitmap(this.width, this.height, PixelFormat.Format32bppArgb);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImageDXT3(rawBytes, this.width, this.height, bmp, bmpData);
                            break;
                        }
                    case 2050: // new
                        {
                            bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImageDXT5(rawBytes, Width, Height, bmp, bmpData);
                            break;
                        }
                    default:
                        Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, string.Format("Unknown PNG format {0} {1}", format, format2));
                        break;
                }
                if (bmp != null)
                {
                    if (texture2d != null)
                    {
                        Microsoft.Xna.Framework.Rectangle rect = new Microsoft.Xna.Framework.Rectangle(Microsoft.Xna.Framework.Point.Zero,
                            new Microsoft.Xna.Framework.Point(width, height));
                        texture2d.SetData(0, 0, rect, rawBytes, 0, rawBytes.Length);
                    }
                }

                png = bmp;
            }
            catch (InvalidDataException)
            {
                png = null;
            }
        }

        /// <summary>
        /// Parses the raw image bytes from WZ
        /// </summary>
        /// <returns></returns>
        internal byte[] GetRawImage(bool saveInMemory)
        {
            byte[] rawImageBytes = GetCompressedBytes(saveInMemory);

            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(rawImageBytes)))
                {
                    DeflateStream zlib;

                    ushort header = reader.ReadUInt16();
                    listWzUsed = header != 0x9C78 && header != 0xDA78 && header != 0x0178 && header != 0x5E78;
                    if (!listWzUsed)
                    {
                        zlib = new DeflateStream(reader.BaseStream, CompressionMode.Decompress);
                    }
                    else
                    {
                        reader.BaseStream.Position -= 2;
                        MemoryStream dataStream = new MemoryStream();
                        int blocksize = 0;
                        int endOfPng = rawImageBytes.Length;

                        // Read image into zlib
                        while (reader.BaseStream.Position < endOfPng)
                        {
                            blocksize = reader.ReadInt32();
                            for (int i = 0; i < blocksize; i++)
                            {
                                dataStream.WriteByte((byte)(reader.ReadByte() ^ ParentImage.reader.WzKey[i]));
                            }
                        }
                        dataStream.Position = 2;
                        zlib = new DeflateStream(dataStream, CompressionMode.Decompress);
                    }

                    int uncompressedSize = 0;
                    byte[] decBuf = null;

                    switch (format + format2)
                    {
                        case 1: // 0x1
                            {
                                uncompressedSize = width * height * 2;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 2: // 0x2
                            {
                                uncompressedSize = width * height * 4;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 3: // 0x2 + 1?
                            {
                                // New format 黑白缩略图
                                // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                                // you'll be remembered forever <3 

                                uncompressedSize = width * height * 4;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 257: // 0x100 + 1?
                            {
                                // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                                // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                                uncompressedSize = width * height * 2;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 513: // 0x200 nexon wizet logo
                            {
                                uncompressedSize = width * height * 2;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 517: // 0x200 + 5
                            {
                                uncompressedSize = width * height / 128;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 1026: // 0x400 + 2?
                            {
                                uncompressedSize = width * height * 4;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case 2050: // 0x800 + 2? new
                            {
                                uncompressedSize = width * height;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        default:
                            Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, string.Format("Unknown PNG format {0} {1}", format, format2));
                            break;
                    }

                    if (decBuf != null)
                    {
                        using (zlib)
                        {
                            zlib.Read(decBuf, 0, uncompressedSize);
                            return decBuf;
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
            }
            return null;
        }

        #region Decoders
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color RGB565ToColor(ushort val)
        {
            const int rgb565_mask_r = 0xf800;
            const int rgb565_mask_g = 0x07e0;
            const int rgb565_mask_b = 0x001f;
            
            int r = (val & rgb565_mask_r) >> 11;
            int g = (val & rgb565_mask_g) >> 5;
            int b = (val & rgb565_mask_b);
            var c = Color.FromArgb(
                (r << 3) | (r >> 2),
                (g << 2) | (g >> 4),
                (b << 3) | (b >> 2));
            return c;
        }

        /// <summary>
        /// For debugging: an example of this image may be found at "Effect.wz\\5skill.img\\character_delayed\\0"
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void DecompressImage_PixelDataBgra4444(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            int uncompressedSize = width * height * 2;
            byte[] decoded = new byte[uncompressedSize * 2];

            // Declare a pointer to the first element of the rawData array
            // This allows us to directly access the memory of the rawData array
            // without having to access it through the array indexer, which is slower
            fixed (byte* pRawData = rawData)
            {
                // Declare a pointer to the first element of the decoded array
                fixed (byte* pDecoded = decoded)
                {
                    // Iterate over the elements of the rawData array using the pointer
                    for (int i = 0; i < uncompressedSize; i++)
                    {
                        byte byteAtPosition = *(pRawData + i);

                        int lo = byteAtPosition & 0x0F;
                        byte b = (byte)(lo | (lo << 4));
                        *(pDecoded + i * 2) = b;

                        int hi = byteAtPosition & 0xF0;
                        byte g = (byte)(hi | (hi >> 4));
                        *(pDecoded + i * 2 + 1) = g;
                    }
                }
            }

            // Copy the decoded data to the bitmap using a pointer
            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        /// <summary>
        /// DXT3
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecompressImageDXT3(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            byte[] decoded = new byte[width * height * 4];

            if (SquishPNGWrapper.CheckAndLoadLibrary())
            {
                SquishPNGWrapper.DecompressImage(decoded, width, height, rawData, (int)SquishPNGWrapper.FlagsEnum.kDxt3);
            }
            else  // otherwise decode here directly, fallback.
            {
                Color[] colorTable = new Color[4];
                int[] colorIdxTable = new int[16];
                byte[] alphaTable = new byte[16];
                
                for (int y = 0; y < height; y += 4)
                {
                    for (int x = 0; x < width; x += 4)
                    {
                        int off = x * 4 + y * width;
                        ExpandAlphaTableDXT3(alphaTable, rawData, off);
                        ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                        ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                SetPixel(decoded,
                                    x + i,
                                    y + j,
                                    width,
                                    colorTable[colorIdxTable[j * 4 + i]],
                                    alphaTable[j * 4 + i]);
                            }
                        }
                    }
                }
            }
            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecompressImage_PixelDataForm517(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            byte[] decoded = new byte[width * height * 2];

            int lineIndex = 0;
            for (int j0 = 0, j1 = height / 16; j0 < j1; j0++)
            {
                var dstIndex = lineIndex;
                for (int i0 = 0, i1 = width / 16; i0 < i1; i0++)
                {
                    int idx = (i0 + j0 * i1) * 2;
                    byte b0 = rawData[idx];
                    byte b1 = rawData[idx + 1];
                    for (int k = 0; k < 16; k++)
                    {
                        decoded[dstIndex++] = b0;
                        decoded[dstIndex++] = b1;
                    }
                }
                for (int k = 1; k < 16; k++)
                {
                    Array.Copy(decoded, lineIndex, decoded, dstIndex, width * 2);
                    dstIndex += width * 2;
                }

                lineIndex += width * 32;
            }
            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        /// <summary>
        /// DXT5
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecompressImageDXT5(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            byte[] decoded = new byte[width * height * 4];

            if (SquishPNGWrapper.CheckAndLoadLibrary())
            {
                SquishPNGWrapper.DecompressImage(decoded, width, height, rawData, (int)SquishPNGWrapper.FlagsEnum.kDxt5);
            }
            else  // otherwise decode here directly, fallback
            {
                Color[] colorTable = new Color[4];
                int[] colorIdxTable = new int[16];
                byte[] alphaTable = new byte[8];
                int[] alphaIdxTable = new int[16];
                for (int y = 0; y < height; y += 4)
                {
                    for (int x = 0; x < width; x += 4)
                    {
                        int off = x * 4 + y * width;
                        ExpandAlphaTableDXT5(alphaTable, rawData[off + 0], rawData[off + 1]);
                        ExpandAlphaIndexTableDXT5(alphaIdxTable, rawData, off + 2);
                        ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                        ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                SetPixel(decoded,
                                    x + i,
                                    y + j,
                                    width,
                                    colorTable[colorIdxTable[j * 4 + i]],
                                    alphaTable[alphaIdxTable[j * 4 + i]]);
                            }
                        }
                    }
                }
            }
            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPixel(byte[] pixelData, int x, int y, int width, Color color, byte alpha)
        {
            int offset = (y * width + x) * 4;
            pixelData[offset + 0] = color.B;
            pixelData[offset + 1] = color.G;
            pixelData[offset + 2] = color.R;
            pixelData[offset + 3] = alpha;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyBmpDataWithStride(byte[] source, int stride, BitmapData bmpData)
        {
            if (bmpData.Stride == stride)
            {
                Marshal.Copy(source, 0, bmpData.Scan0, source.Length);
            }
            else
            {
                for (int y = 0; y < bmpData.Height; y++)
                {
                    Marshal.Copy(source, stride * y, bmpData.Scan0 + bmpData.Stride * y, stride);
                }
            }

        }
        #endregion

        #region DXT1 Color
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandColorTable(Color[] color, ushort c0, ushort c1)
        {
            color[0] = RGB565ToColor(c0);
            color[1] = RGB565ToColor(c1);
            if (c0 > c1)
            {
                color[2] = Color.FromArgb(0xff, (color[0].R * 2 + color[1].R + 1) / 3, (color[0].G * 2 + color[1].G + 1) / 3, (color[0].B * 2 + color[1].B + 1) / 3);
                color[3] = Color.FromArgb(0xff, (color[0].R + color[1].R * 2 + 1) / 3, (color[0].G + color[1].G * 2 + 1) / 3, (color[0].B + color[1].B * 2 + 1) / 3);
            }
            else
            {
                color[2] = Color.FromArgb(0xff, (color[0].R + color[1].R) / 2, (color[0].G + color[1].G) / 2, (color[0].B + color[1].B) / 2);
                color[3] = Color.FromArgb(0xff, Color.Black);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandColorIndexTable(int[] colorIndex, byte[] rawData, int offset)
        {
            for (int i = 0; i < 16; i += 4, offset++)
            {
                colorIndex[i + 0] = (rawData[offset] & 0x03);
                colorIndex[i + 1] = (rawData[offset] & 0x0c) >> 2;
                colorIndex[i + 2] = (rawData[offset] & 0x30) >> 4;
                colorIndex[i + 3] = (rawData[offset] & 0xc0) >> 6;
            }
        }
        #endregion

        #region DXT3/DXT5 Alpha
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTableDXT3(byte[] alpha, byte[] rawData, int offset)
        {
            for (int i = 0; i < 16; i += 2, offset++)
            {
                alpha[i + 0] = (byte)(rawData[offset] & 0x0f);
                alpha[i + 1] = (byte)((rawData[offset] & 0xf0) >> 4);
            }
            for (int i = 0; i < 16; i++)
            {
                alpha[i] = (byte)(alpha[i] | (alpha[i] << 4));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTableDXT5(byte[] alpha, byte a0, byte a1)
        {
            // get the two alpha values
            alpha[0] = a0;
            alpha[1] = a1;

            // compare the values to build the codebook
            if (a0 > a1)
            {
                for (int i = 2; i < 8; i++) // // use 7-alpha codebook
                {
                    alpha[i] = (byte)(((8 - i) * a0 + (i - 1) * a1 + 3) / 7);
                }
            }
            else
            {
                for (int i = 2; i < 6; i++) // // use 5-alpha codebook
                {
                    alpha[i] = (byte)(((6 - i) * a0 + (i - 1) * a1 + 2) / 5);
                }
                alpha[6] = 0;
                alpha[7] = 255;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaIndexTableDXT5(int[] alphaIndex, byte[] rawData, int offset)
        {
            // write out the indexed codebook values
            for (int i = 0; i < 16; i += 8, offset += 3)
            {
                int flags = rawData[offset]
                    | (rawData[offset + 1] << 8)
                    | (rawData[offset + 2] << 16);

                // unpack 8 3-bit values from it
                for (int j = 0; j < 8; j++)
                {
                    int mask = 0x07 << (3 * j);
                    alphaIndex[i + j] = (flags & mask) >> (3 * j); 
                }
            }
        }
        #endregion

        internal void CompressPng(Bitmap bmp)
        {
            byte[] buf = new byte[bmp.Width * bmp.Height * 8];
            format = 2;
            format2 = 0;
            width = bmp.Width;
            height = bmp.Height;
            
            //byte[] bmpBytes = bmp.BitmapToBytes();
            /* if (SquishPNGWrapper.CheckAndLoadLibrary())
                        {
                            byte[] bmpBytes = bmp.BitmapToBytes();
                            SquishPNGWrapper.CompressImage(bmpBytes, width, height, buf, (int)SquishPNGWrapper.FlagsEnum.kDxt1);
                        }
                        else
                        {*/
            unsafe
            {
                fixed (byte* pBuf = buf)
                {
                    byte* pCurPixel = pBuf;
                    for (int i = 0; i < height; i++)
                    {
                        for (int j = 0; j < width; j++)
                        {
                            Color curPixel = bmp.GetPixel(j, i);
                            *pCurPixel = curPixel.B;
                            *(pCurPixel + 1) = curPixel.G;
                            *(pCurPixel + 2) = curPixel.R;
                            *(pCurPixel + 3) = curPixel.A;
                            pCurPixel += 4;
                        }
                    }
                }
            }
            compressedImageBytes = Compress(buf);

            buf = null;

            if (listWzUsed)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    using (WzBinaryWriter writer = new WzBinaryWriter(memStream, WzTool.GetIvByMapleVersion(WzMapleVersion.GMS)))
                    {
                        writer.Write(2);
                        for (int i = 0; i < 2; i++)
                        {
                            writer.Write((byte)(compressedImageBytes[i] ^ writer.WzKey[i]));
                        }
                        writer.Write(compressedImageBytes.Length - 2);
                        for (int i = 2; i < compressedImageBytes.Length; i++)
                            writer.Write((byte)(compressedImageBytes[i] ^ writer.WzKey[i - 2]));
                        compressedImageBytes = memStream.GetBuffer();
                    }
                }
            }
        }
        #endregion

        #region Cast Values

        public override Bitmap GetBitmap()
        {
            return GetImage(false);
        }
        #endregion
    }
}