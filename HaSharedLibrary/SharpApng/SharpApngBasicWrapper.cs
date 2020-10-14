/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.SharpApng
{
    public static class SharpApngBasicWrapper
    {
        public const int PIXEL_DEPTH = 4;

        static SharpApngBasicWrapper()
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
            else
                throw new Exception("apng64.dll or apng32.dll not found.");
        }

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

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CreateFrameDelegate(IntPtr pdata, int num, int den, int i, int len);
        public static readonly CreateFrameDelegate CreateFrame;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SaveAPNGDelegate(IntPtr path, int frameCount, int width, int height, int bytesPerPixel, byte firstFrameHidden);
        public static readonly SaveAPNGDelegate SaveAPNG;
    }

}