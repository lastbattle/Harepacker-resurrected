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

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains the information for a bitmap
    /// </summary>
    public class WzPngProperty : WzImageProperty
    {
        #region Fields
        internal int width, height, format, format2;
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
        public int Width { get { return width; } set { width = value; } }
        /// <summary>
        /// The height of the bitmap
        /// </summary>
        public int Height { get { return height; } set { height = value; } }
        /// <summary>
        /// The format of the bitmap
        /// </summary>
        public int Format { get { return format + format2; } set { format = value; format2 = 0; } }

        public bool ListWzUsed { get { return listWzUsed; } set { if (value != listWzUsed) { listWzUsed = value; CompressPng(GetPNG(false)); } } }
        /// <summary>
        /// The actual bitmap
        /// </summary>
        public Bitmap PNG
        {
            set
            {
                png = value;
                CompressPng(value);
            }
        }

        [Obsolete("To enable more control over memory usage, this property was superseded by the GetCompressedBytes method and will be removed in the future")]
        public byte[] CompressedBytes
        {
            get
            {
                return GetCompressedBytes(false);
            }
        }

        /// <summary>
        /// Creates a blank WzPngProperty
        /// </summary>
        public WzPngProperty() { }
        internal WzPngProperty(WzBinaryReader reader, bool parseNow)
        {
            // Read compressed bytes
            width = reader.ReadCompressedInt();
            height = reader.ReadCompressedInt();
            format = reader.ReadCompressedInt();
            format2 = reader.ReadByte();
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
            CompressPng(png);
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
            MemoryStream memStream = new MemoryStream();
            memStream.Write(compressedBuffer, 2, compressedBuffer.Length - 2);
            byte[] buffer = new byte[decompressedSize];
            memStream.Position = 0;
            DeflateStream zip = new DeflateStream(memStream, CompressionMode.Decompress);
            zip.Read(buffer, 0, buffer.Length);
            zip.Close();
            zip.Dispose();
            memStream.Close();
            memStream.Dispose();
            return buffer;
        }

        internal byte[] Compress(byte[] decompressedBuffer)
        {
            MemoryStream memStream = new MemoryStream();
            DeflateStream zip = new DeflateStream(memStream, CompressionMode.Compress, true);
            zip.Write(decompressedBuffer, 0, decompressedBuffer.Length);
            zip.Close();
            memStream.Position = 0;
            byte[] buffer = new byte[memStream.Length + 2];
            memStream.Read(buffer, 2, buffer.Length - 2);
            memStream.Close();
            memStream.Dispose();
            zip.Dispose();
            System.Buffer.BlockCopy(new byte[] { 0x78, 0x9C }, 0, buffer, 0, 2);
            return buffer;
        }

        internal void ParsePng()
        {
            try
            {
                DeflateStream zlib;
                int uncompressedSize = 0;
                int x = 0, y = 0, b = 0, g = 0;
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

                switch (format + format2)
                {
                    case 1:
                        bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                        uncompressedSize = width * height * 2;
                        decBuf = new byte[uncompressedSize];
                        zlib.Read(decBuf, 0, uncompressedSize);
                        byte[] argb = new Byte[uncompressedSize * 2];
                        for (int i = 0; i < uncompressedSize; i++)
                        {
                            b = decBuf[i] & 0x0F; 
                            b |= (b << 4); 

                            argb[i * 2] = (byte)b;

                            g = decBuf[i] & 0xF0; 
                            g |= (g >> 4); 

                            argb[i * 2 + 1] = (byte)g;
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
                        for (int i = 0; i < uncompressedSize; i++)
                        {
                            for (byte j = 0; j < 8; j++)
                            {
                                iB = Convert.ToByte(((decBuf[i] & (0x01 << (7 - j))) >> (7 - j)) * 0xFF);
                                for (int k = 0; k < 16; k++)
                                {
                                    if (x == width) { x = 0; y++; }
                                    bmp.SetPixel(x, y, Color.FromArgb(0xFF, iB, iB, iB));
                                    x++;
                                }
                            }
                        }
                        break;
                    /* case 1026:
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
                         break;*/
                    default:
                        Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, string.Format("Unknown PNG format {0} {1}", format, format2));
                        break;
                }
                png = bmp;
            }catch (InvalidDataException exp) { png = null; }
        }
        internal void CompressPng(Bitmap bmp)
        {
            byte[] buf = new byte[bmp.Width * bmp.Height * 8];
            format = 2;
            format2 = 0;
            width = bmp.Width;
            height = bmp.Height;

            int curPos = 0;
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    Color curPixel = bmp.GetPixel(j, i);
                    buf[curPos] = curPixel.B;
                    buf[curPos + 1] = curPixel.G;
                    buf[curPos + 2] = curPixel.R;
                    buf[curPos + 3] = curPixel.A;
                    curPos += 4;
                }
            compressedBytes = Compress(buf);
            if (listWzUsed)
            {
                MemoryStream memStream = new MemoryStream();
                WzBinaryWriter writer = new WzBinaryWriter(memStream, WzTool.GetIvByMapleVersion(WzMapleVersion.GMS));
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

        #region Cast Values

        public override Bitmap GetBitmap()
        {
            return GetPNG(false);
        }
        #endregion
    }
}