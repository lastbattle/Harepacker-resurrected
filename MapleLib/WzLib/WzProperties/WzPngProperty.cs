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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using MapleLib.WzLib.Util;
using System.Runtime.CompilerServices;

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains the information for a bitmap
    /// </summary>
    public class WzPngProperty : WzImageProperty
    {
        #region Fields
        internal int width, height, nPixFormat = 1, nMagLevel;
        internal byte[] compressedBytes;
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
            if (value is Bitmap) SetPNG((Bitmap)value);
            else compressedBytes = (byte[])value;
        }

        public override WzImageProperty DeepClone()
        {
            WzPngProperty clone = new WzPngProperty();
            clone.SetPNG(GetPNG(false));
            return clone;
        }

        public override object WzValue { get { return GetPNG(false); } }
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
            compressedBytes = null;
            if (png != null)
            {
                png.Dispose();
                png = null;
            }
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The width of the bitmap
        /// </summary>
        public int Width
        {
            get
            {
                return width;
            }
            private set
            {
                width = value;
            }
        }
        /// <summary>
        /// The height of the bitmap
        /// </summary>
        public int Height
        {
            get
            {
                return height;
            }
            private set
            {
                height = value;
            }
        }
        /// <summary>
        /// The format of the bitmap
        /// </summary>
        public int sFormat
        {
            get
            {
                return nPixFormat + nMagLevel;
            }
            set
            {
                nPixFormat = value;
                nMagLevel = 0;
            }
        }

        public bool ListWzUsed {
            get {
                return listWzUsed;
            }
            set {
                if (value != listWzUsed) {
                    listWzUsed = value;
                    CompressPng(GetPNG(false), false);
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
                png = value;
                CompressPng(value, false);
            }
        }

        /// <summary>
        /// Creates a blank WzPngProperty
        /// </summary>
        public WzPngProperty() { }
        internal WzPngProperty(WzBinaryReader reader, bool parseNow)
        {
            this.wzReader = reader;

            // Width Height
            width = reader.ReadCompressedInt();
            height = reader.ReadCompressedInt();
            if (this.width >= 0x10000 || this.height >= 0x10000) // copy pasta eric <3
            {
                throw new ArgumentException(string.Format("Invalid WzPngProperty in Wz. Width: {0}, Height: {1}", width, height));
            }

            // Image format
            nPixFormat = reader.ReadCompressedInt();
            nMagLevel = reader.ReadByte();

            // Other crap
            reader.BaseStream.Position += 4;
            offs = reader.BaseStream.Position;
            int len = reader.ReadInt32() - 1;
            reader.BaseStream.Position += 1;

            if (len > 0)
            {
                if (parseNow)
                {
                    compressedBytes = wzReader.ReadBytes(len);
                    ParsePng();
                }
                else
                    reader.BaseStream.Position += len;
            }
            wzReader = reader;
        }
        #endregion

        #region Parsing Methods
        public byte[] GetCompressedBytes(bool saveInMemory)
        {
            if (compressedBytes == null)
            {
                long pos = wzReader.BaseStream.Position;
                wzReader.BaseStream.Position = offs;
                int len = wzReader.ReadInt32() - 1;
                wzReader.BaseStream.Position += 1;
                if (len > 0)
                    compressedBytes = wzReader.ReadBytes(len);
                wzReader.BaseStream.Position = pos;
                if (!saveInMemory)
                {
                    //were removing the referance to compressedBytes, so a backup for the ret value is needed
                    byte[] returnBytes = compressedBytes;
                    compressedBytes = null;
                    return returnBytes;
                }
            }
            return compressedBytes;
        }

        public void SetPNG(Bitmap png)
        {
            this.png = png;
            CompressPng(png, true);
        }

        public Bitmap GetPNG(bool saveInMemory)
        {
            if (png == null)
            {
                long pos = wzReader.BaseStream.Position;
                wzReader.BaseStream.Position = offs;
                int len = wzReader.ReadInt32() - 1;
                wzReader.BaseStream.Position += 1;
                if (len > 0)
                    compressedBytes = wzReader.ReadBytes(len);
                ParsePng();
                wzReader.BaseStream.Position = pos;
                if (!saveInMemory)
                {
                    Bitmap pngImage = png;
                    png = null;
                    compressedBytes = null;
                    return pngImage;
                }
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
                }
                return buffer;
            }
        }

        internal byte[] Compress(byte[] decompressedBuffer)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Compress, true))
                {
                    zip.Write(decompressedBuffer, 0, decompressedBuffer.Length);
                    zip.Close();
                }
                memStream.Position = 0;
                byte[] buffer = new byte[memStream.Length + 2];
                memStream.Read(buffer, 2, buffer.Length - 2);

                System.Buffer.BlockCopy(new byte[] { 0x78, 0x9C }, 0, buffer, 0, 2);
                return buffer;
            }
        }

        internal void ParsePng()
        {
            DeflateStream zlib;
            int uncompressedSize = 0;
            Bitmap bmp = null;
            BitmapData bmpData;
            WzImage imgParent = ParentImage;
            byte[] decBuf;

            BinaryReader reader = new BinaryReader(new MemoryStream(compressedBytes));
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
                int endOfPng = compressedBytes.Length;

                while (reader.BaseStream.Position < endOfPng)
                {
                    blocksize = reader.ReadInt32();
                    for (int i = 0; i < blocksize; i++)
                    {
                        dataStream.WriteByte((byte)(reader.ReadByte() ^ imgParent.reader.WzKey[i]));
                    }
                }
                dataStream.Position = 2;
                zlib = new DeflateStream(dataStream, CompressionMode.Decompress);
            }

            // System.Diagnostics.Debug.WriteLine("nPixFormat {0}, this.nMagLevel {1}", nPixFormat, this.nMagLevel);
            switch (nPixFormat + this.nMagLevel)
            {
                case 1:
                    bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uncompressedSize = width * height * 2;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);

                    byte[] argb = new Byte[uncompressedSize * 2]; // argb
                    for (int i = 0; i < uncompressedSize; i++)
                    {
                        int low = decBuf[i] & 0x0F;
                        int high = decBuf[i] & 0xF0;

                        argb[i * 2] = (byte)(low | (low << 4));
                        argb[i * 2 + 1] = (byte)(high | (high >> 4));
                    }
                    Marshal.Copy(argb, 0, bmpData.Scan0, argb.Length);
                    bmp.UnlockBits(bmpData);

                    break;

                case 2:
                    bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uncompressedSize = width * height * 4;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);
                    Marshal.Copy(decBuf, 0, bmpData.Scan0, decBuf.Length);
                    bmp.UnlockBits(bmpData);
                    break;

                case 3:
                    // New format 黑白缩略图
                    // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                    // you'll be remembered forever <3 
                    bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    bmpData = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uncompressedSize = width * height * 4;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);


                    int[] argb2 = new int[this.width * this.height];
                    {
                        int index;
                        int index2;
                        int p;
                        int w = ((int)Math.Ceiling(this.width / 4.0));
                        int h = ((int)Math.Ceiling(this.height / 4.0));
                        for (int y_ = 0; y_ < h; y_++)
                        {
                            for (int x_ = 0; x_ < w; x_++)
                            {
                                index = (x_ + y_ * w) * 2; //原像素索引
                                index2 = x_ * 4 + y_ * this.width * 4; //目标像素索引
                                p = (decBuf[index] & 0x0F) | ((decBuf[index] & 0x0F) << 4);
                                p |= ((decBuf[index] & 0xF0) | ((decBuf[index] & 0xF0) >> 4)) << 8;
                                p |= ((decBuf[index + 1] & 0x0F) | ((decBuf[index + 1] & 0x0F) << 4)) << 16;
                                p |= ((decBuf[index + 1] & 0xF0) | ((decBuf[index] & 0xF0) >> 4)) << 24;

                                for (int i = 0; i < 4; i++)
                                {
                                    if (x_ * 4 + i < this.width)
                                    {
                                        argb2[index2 + i] = p;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            //复制行
                            index2 = y_ * this.width * 4;
                            for (int j = 1; j < 4; j++)
                            {
                                if (y_ * 4 + j < this.height)
                                {
                                    Array.Copy(argb2, index2, argb2, index2 + j * this.width, this.width);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                    Marshal.Copy(decBuf, 0, bmpData.Scan0, argb2.Length);
                    bmp.UnlockBits(bmpData);
                    break;

                case 513:
                    bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
                    bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);
                    uncompressedSize = width * height * 2;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);
                    Marshal.Copy(decBuf, 0, bmpData.Scan0, decBuf.Length);
                    bmp.UnlockBits(bmpData);
                    break;

                case 517:
                    bmp = new Bitmap(width, height);
                    uncompressedSize = width * height / 128;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);
                    byte iB = 0;
                    int x = 0;
                    int y = 0;

                    for (int i = 0; i < uncompressedSize; i++)
                    {
                        for (byte j = 0; j < 8; j++)
                        {
                            iB = Convert.ToByte(((decBuf[i] & (0x01 << (7 - j))) >> (7 - j)) * 0xFF);
                            for (int k = 0; k < 16; k++)
                            {
                                if (x == width)
                                {
                                    x = 0;
                                    y++;
                                }
                                bmp.SetPixel(x, y, Color.FromArgb(0xFF, iB, iB, iB));
                                x++;
                            }
                        }
                    }
                    break;

                case 1026:
                    bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uncompressedSize = width * height;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);
                    decBuf = GetPixelDataDXT3(decBuf, Width, Height);
                    Marshal.Copy(decBuf, 0, bmpData.Scan0, decBuf.Length);
                    bmp.UnlockBits(bmpData);
                    break;


                case 2050: // new
                    bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uncompressedSize = width * height;
                    decBuf = new byte[uncompressedSize];
                    zlib.Read(decBuf, 0, uncompressedSize);
                    decBuf = GetPixelDataDXT5(decBuf, Width, Height);
                    Marshal.Copy(decBuf, 0, bmpData.Scan0, decBuf.Length);
                    bmp.UnlockBits(bmpData);
                    break;

                default:
                    Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, string.Format("Unknown PNG format. nPixFormat: {0}, nMagLevel: {1}", this.nPixFormat, this.nMagLevel));
                    break;
            }
            png = bmp;
        }
        #endregion

        #region Cast Values

        public override Bitmap GetBitmap()
        {
            return GetPNG(false);
        }
        #endregion

        #region DXT format compressor
        internal void CompressPng(Bitmap bmp, bool fromImport)
        {
            this.width = bmp.Width;
            this.height = bmp.Height;

            if (fromImport) // rescan pixel format
            {
                if (bmp.PixelFormat == PixelFormat.Format32bppArgb)
                {
                    nPixFormat = 2;
                }
            }

            // https://github.com/eaxvac/Harepacker-resurrected 
            // http://forum.ragezone.com/f921/release-harepacker-resurrected-1149521/
            switch (nPixFormat + nMagLevel)
            {
                case 1:
                    {
                        byte[] buf = new byte[bmp.Width * bmp.Height * 2];
                        int curPos = 0;

                        for (int row = 0; row < height; row++)
                        {
                            for (int col = 0; col < width; col++)
                            {
                                Color curPixel = bmp.GetPixel(col, row); // 4 bytes argb

                                int B = (curPixel.B >> 4) & 0x0F;
                                int G = (curPixel.G << 4) & 0xF0;
                                int R = (curPixel.R >> 4) & 0x0F;
                                int A = (curPixel.A << 4) & 0xF0; // confirmed

                                // 1 byte for every argb
                                buf[curPos] = (byte)(B | G);
                                buf[curPos + 1] = (byte)(R | A);

                                curPos += 2; // BG
                            }
                        }
                        compressedBytes = Compress(buf);
                        break;
                    }
                case 2:
                default: // defaults to pixel format 2. Unsupported for now.
                    {
                        byte[] buf = new byte[bmp.Width * bmp.Height * 4];
                        this.nPixFormat = 2;
                        this.nMagLevel = 0;

                        int curPos = 0;
                        for (int row = 0; row < height; row++)
                        {
                            for (int col = 0; col < width; col++)
                            {
                                Color curPixel = bmp.GetPixel(col, row); // 4 bytes 
                                buf[curPos] = curPixel.B;
                                buf[curPos + 1] = curPixel.G;
                                buf[curPos + 2] = curPixel.R;
                                buf[curPos + 3] = curPixel.A;

                                curPos += 4; // ARGB
                            }
                        }
                        compressedBytes = Compress(buf);
                        break;
                    }
            }

            if (listWzUsed)
            {
                MemoryStream memStream = new MemoryStream();
                WzBinaryWriter writer = new WzBinaryWriter(memStream, ((WzDirectory)parent).WzIv);
                writer.Write(2);
                for (int i = 0; i < 2; i++)
                {
                    writer.Write((byte)(compressedBytes[i] ^ writer.WzKey[i]));
                }
                writer.Write(compressedBytes.Length - 2);
                for (int i = 2; i < compressedBytes.Length; i++)
                    writer.Write((byte)(compressedBytes[i] ^ writer.WzKey[i - 2]));
                compressedBytes = memStream.GetBuffer();
                writer.Close();
            }
        }
        #endregion

        #region DXT Format Parser
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetPixelDataDXT3(byte[] rawData, int width, int height)
        {
            byte[] pixel = new byte[width * height * 4];

            Color[] colorTable = new Color[4];
            int[] colorIdxTable = new int[16];
            byte[] alphaTable = new byte[16];
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    int off = x * 4 + y * width;
                    ExpandAlphaTable(alphaTable, rawData, off);
                    ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                    ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                    ExpandColorTable(colorTable, u0, u1);
                    ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                    for (int j = 0; j < 4; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SetPixel(pixel,
                                x + i,
                                y + j,
                                width,
                                colorTable[colorIdxTable[j * 4 + i]],
                                alphaTable[j * 4 + i]);
                        }
                    }
                }
            }
            return pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetPixelDataDXT5(byte[] rawData, int width, int height)
        {
            byte[] pixel = new byte[width * height * 4];

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
                            SetPixel(pixel,
                                           x + i,
                                           y + j,
                                           width,
                                           colorTable[colorIdxTable[j * 4 + i]],
                                           alphaTable[alphaIdxTable[j * 4 + i]]);
                        }
                    }
                }
            }

            return pixel;
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
        private static void ExpandColorTable(Color[] color, ushort u0, ushort u1)
        {
            color[0] = RGB565ToColor(u0);
            color[1] = RGB565ToColor(u1);
            color[2] = System.Drawing.Color.FromArgb(0xff, (color[0].R * 2 + color[1].R + 1) / 3, (color[0].G * 2 + color[1].G + 1) / 3, (color[0].B * 2 + color[1].B + 1) / 3);
            color[3] = System.Drawing.Color.FromArgb(0xff, (color[0].R + color[1].R * 2 + 1) / 3, (color[0].G + color[1].G * 2 + 1) / 3, (color[0].B + color[1].B * 2 + 1) / 3);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTable(byte[] alpha, byte[] rawData, int offset)
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
            alpha[0] = a0;
            alpha[1] = a1;
            if (a0 > a1)
            {
                for (int i = 2; i < 8; i++)
                {
                    alpha[i] = (byte)(((8 - i) * a0 + (i - 1) * a1 + 3) / 7);
                }
            }
            else
            {
                for (int i = 2; i < 6; i++)
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
            for (int i = 0; i < 16; i += 8, offset += 3)
            {
                int flags = rawData[offset]
                    | (rawData[offset + 1] << 8)
                    | (rawData[offset + 2] << 16);
                for (int j = 0; j < 8; j++)
                {
                    int mask = 0x07 << (3 * j);
                    alphaIndex[i + j] = (flags & mask) >> (3 * j);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color RGB565ToColor(ushort val)
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
        #endregion
    }
}