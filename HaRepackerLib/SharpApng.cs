/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace SharpApng
{
    public class Frame : IDisposable
    {
        private int m_num;
        private int m_den;
        private Bitmap m_bmp;

        public void Dispose()
        {
            m_bmp.Dispose();
        }

        public Frame(Bitmap bmp, int num, int den)
        {
            this.m_num = num;
            this.m_den = den;
            this.m_bmp = bmp;
        }

        public int DelayNum
        {
            get
            {
                return m_num;
            }
            set
            {
                m_num = value;
            }
        }

        public int DelayDen
        {
            get
            {
                return m_den;
            }
            set
            {
                m_den = value;
            }
        }

        public Bitmap Bitmap
        {
            get
            {
                return m_bmp;
            }
            set
            {
                m_bmp = value;
            }
        }
    }

    public class Apng : IDisposable
    {
        private List<Frame> m_frames = new List<Frame>();

        public Apng()
        {
        }

        public void Dispose()
        {
            foreach (Frame frame in m_frames)
                frame.Dispose();
            m_frames.Clear();
        }

        public Frame this[int index]
        {
            get
            {
                if (index < m_frames.Count) return m_frames[index];
                else return null;
            }
            set
            {
                if (index < m_frames.Count) m_frames[index] = value;
            }
        }

        public void AddFrame(Frame frame)
        {
            m_frames.Add(frame);
        }

        public void AddFrame(Bitmap bmp, int num, int den)
        {
            m_frames.Add(new Frame(bmp, num, den));
        }

        private Bitmap ExtendImage(Bitmap source, Size newSize)
        {
            Bitmap result = new Bitmap(newSize.Width, newSize.Height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImageUnscaled(source, 0, 0);
            }
            return result;
        }

        public void WriteApng(string path, bool firstFrameHidden, bool disposeAfter)
        {
            Size maxSize = new Size();
            foreach (Frame frame in m_frames)
            {
                if (frame.Bitmap.Width > maxSize.Width) maxSize.Width = frame.Bitmap.Width;
                if (frame.Bitmap.Height > maxSize.Height) maxSize.Height = frame.Bitmap.Height;
            }
            for (int i = 0; i < m_frames.Count; i++)
            {
                Frame frame = m_frames[i];
                if (frame.Bitmap.Width != maxSize.Width || frame.Bitmap.Height != maxSize.Height)
                    frame.Bitmap = ExtendImage(frame.Bitmap, maxSize);
                ApngBasicWrapper.CreateFrameManaged(frame.Bitmap, frame.DelayNum, frame.DelayDen, i);
            }
            ApngBasicWrapper.SaveApngManaged(path, m_frames.Count, maxSize.Width, maxSize.Height, firstFrameHidden);
            if (disposeAfter) Dispose();
        }
    }

    public static class ApngBasicWrapper
    {
        public const int PIXEL_DEPTH = 4;

        public static IntPtr MarshalString(string source)
        {
            byte[] toMarshal = Encoding.ASCII.GetBytes(source);
            int size = Marshal.SizeOf(source[0]) * source.Length;
            IntPtr pnt = Marshal.AllocHGlobal(size);
            Marshal.Copy(toMarshal, 0, pnt, source.Length);
            Marshal.Copy(new byte[] { 0 }, 0, new IntPtr(pnt.ToInt32() + size), 1);
            return pnt;
        }

        public static IntPtr MarshalByteArray(byte[] source)
        {
            int size = Marshal.SizeOf(source[0]) * source.Length;
            IntPtr pnt = Marshal.AllocHGlobal(size);
            Marshal.Copy(source, 0, pnt, source.Length);
            return pnt;
        }

        public static void ReleaseData(IntPtr ptr)
        {
            Marshal.FreeHGlobal(ptr);
        }

        public static unsafe byte[] TranslateImage(Bitmap image)
        {
            byte[] result = new byte[image.Width * image.Height * PIXEL_DEPTH];
            BitmapData data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte* p = (byte*)data.Scan0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    result[(y * image.Width + x) * PIXEL_DEPTH] = p[x * PIXEL_DEPTH];
                    result[(y * image.Width + x) * PIXEL_DEPTH + 1] = p[x * PIXEL_DEPTH + 1];
                    result[(y * image.Width + x) * PIXEL_DEPTH + 2] = p[x * PIXEL_DEPTH + 2];
                    result[(y * image.Width + x) * PIXEL_DEPTH + 3] = p[x * PIXEL_DEPTH + 3];
                }
                p += data.Stride;
            }
            image.UnlockBits(data);
            return result;
        }

        public static void CreateFrameManaged(Bitmap source, int num, int den, int i)
        {
            IntPtr ptr = MarshalByteArray(TranslateImage(source));
            CreateFrame(ptr, num, den, i, source.Width * source.Height * PIXEL_DEPTH);
            ReleaseData(ptr);
        }

        public static void SaveApngManaged(string path, int frameCount, int width, int height, bool firstFrameHidden)
        {
            IntPtr pathPtr = MarshalString(path);
            byte firstFrame = firstFrameHidden ? (byte)1 : (byte)0;
            SaveAPNG(pathPtr, frameCount, width, height, PIXEL_DEPTH, firstFrame);
            ReleaseData(pathPtr);
        }

        [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true, SetLastError=true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CreateFrameDelegate(IntPtr pdata, int num, int den, int i, int len);
        public static readonly CreateFrameDelegate CreateFrame;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SaveAPNGDelegate(IntPtr path, int frameCount, int width, int height, int bytesPerPixel, byte firstFrameHidden);
        public static readonly SaveAPNGDelegate SaveAPNG;

        static ApngBasicWrapper()
        {
            CreateFrame = null;
            SaveAPNG = null;
            IntPtr apnglib = LoadLibrary(Environment.Is64BitProcess ? "apng64.dll" : "apng32.dll");
            if (apnglib != IntPtr.Zero)
            {
                IntPtr createFramePtr = GetProcAddress(apnglib, "CreateFrame");
                if (createFramePtr != null)
                    CreateFrame = (CreateFrameDelegate)Marshal.GetDelegateForFunctionPointer(createFramePtr, typeof(CreateFrameDelegate));
                IntPtr saveApngPtr = GetProcAddress(apnglib, "SaveAPNG");
                if (saveApngPtr != null)
                    SaveAPNG = (SaveAPNGDelegate)Marshal.GetDelegateForFunctionPointer(saveApngPtr, typeof(SaveAPNGDelegate));
            }
        }
    }
}
