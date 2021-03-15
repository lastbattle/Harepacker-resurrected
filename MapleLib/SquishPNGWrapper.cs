/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2021 lastbattle
   
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;

namespace MapleLib
{
    /// <summary>
    /// The C# wrapper class for the squish.dll png library
    /// </summary>
    public class SquishPNGWrapper
    {
        private enum SquishLibLoadingState
        {
            Uninitialised = 0x0,
            Initialised = 0x1,
            WrongProcessorArchitecture = 0x2,
            UnknownError = 0x3,
        }

        private static SquishLibLoadingState _SquishLibLoadingState = SquishLibLoadingState.Uninitialised;

        /// <summary>
        /// Returns true if the squish library can be used on this HaRepacker assembly
        /// </summary>
        public static bool CheckAndLoadLibrary()
        {
            if (_SquishLibLoadingState == SquishLibLoadingState.Initialised)
                return true; // shouldnt check again once its loaded anyway.

            else if (_SquishLibLoadingState == SquishLibLoadingState.Uninitialised) // not init yet
            {
                //similarly to find process architecture  
                var assemblyArchitecture = Assembly.GetExecutingAssembly().GetName().ProcessorArchitecture;
                if (!(assemblyArchitecture == ProcessorArchitecture.X86 || assemblyArchitecture == ProcessorArchitecture.Amd64))
                {
                    _SquishLibLoadingState = SquishLibLoadingState.WrongProcessorArchitecture;

                    System.Diagnostics.Debug.WriteLine("squish.dll library not loaded. MSIL assembly detected.");
                    return false;
                }
                // Load library here
                IntPtr errorCode = LoadLibrary("squish.dll");
                if (errorCode != IntPtr.Zero)
                {
                    IntPtr FixFlagsPtr = GetProcAddress(errorCode, "_DLLEXPORT_FixFlags");
                    if (FixFlagsPtr != null)
                        FixFlags = (_DLLEXPORT_FixFlags)Marshal.GetDelegateForFunctionPointer(FixFlagsPtr, typeof(_DLLEXPORT_FixFlags));

                    IntPtr CompressPtr = GetProcAddress(errorCode, "_DLLEXPORT_Compress");
                    if (CompressPtr != null)
                        Compress = (_DLLEXPORT_Compress)Marshal.GetDelegateForFunctionPointer(CompressPtr, typeof(_DLLEXPORT_Compress));

                    IntPtr CompressMaskedPtr = GetProcAddress(errorCode, "_DLLEXPORT_CompressMasked");
                    if (CompressMaskedPtr != null)
                        CompressMasked = (_DLLEXPORT_CompressMasked)Marshal.GetDelegateForFunctionPointer(CompressMaskedPtr, typeof(_DLLEXPORT_CompressMasked));

                    IntPtr DecompressPtr = GetProcAddress(errorCode, "_DLLEXPORT_Decompress");
                    if (DecompressPtr != null)
                        Decompress = (_DLLEXPORT_Decompress)Marshal.GetDelegateForFunctionPointer(DecompressPtr, typeof(_DLLEXPORT_Decompress));

                    IntPtr StorageRequirementsPtr = GetProcAddress(errorCode, "_DLLEXPORT_GetStorageRequirements");
                    if (StorageRequirementsPtr != null)
                        GetStorageRequirements = (_DLLEXPORT_GetStorageRequirements)Marshal.GetDelegateForFunctionPointer(StorageRequirementsPtr, typeof(_DLLEXPORT_GetStorageRequirements));

                    IntPtr CompressImagePtr = GetProcAddress(errorCode, "_DLLEXPORT_CompressImage");
                    if (CompressImagePtr != null)
                        CompressImage = (_DLLEXPORT_CompressImage)Marshal.GetDelegateForFunctionPointer(CompressImagePtr, typeof(_DLLEXPORT_CompressImage));

                    IntPtr DecompressImagePtr = GetProcAddress(errorCode, "_DLLEXPORT_DecompressImage");
                    if (DecompressImagePtr != null)
                        DecompressImage = (_DLLEXPORT_DecompressImage)Marshal.GetDelegateForFunctionPointer(DecompressImagePtr, typeof(_DLLEXPORT_DecompressImage));


                    _SquishLibLoadingState = SquishLibLoadingState.Initialised; // flag as initialised

                    System.Diagnostics.Debug.WriteLine("Loaded squish.dll library.");

                    return true;
                }
                else
                {
                    _SquishLibLoadingState = SquishLibLoadingState.UnknownError;

                    System.Diagnostics.Debug.WriteLine("Error loaded squish.dll library, code = " + errorCode);
                    return false;
                    //throw new Exception("squish.dll not found in the program directory.");
                }
            }
            return false;
        }

        #region Imports
        public static _DLLEXPORT_FixFlags FixFlags;
        public static _DLLEXPORT_Compress Compress;
        public static _DLLEXPORT_CompressMasked CompressMasked;
        public static _DLLEXPORT_Decompress Decompress;
        public static _DLLEXPORT_GetStorageRequirements GetStorageRequirements;
        public static _DLLEXPORT_CompressImage CompressImage;
        public static _DLLEXPORT_DecompressImage DecompressImage;


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int _DLLEXPORT_FixFlags(int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _DLLEXPORT_Compress(byte[] rgba, byte[] block, int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _DLLEXPORT_CompressMasked(byte[] rgba, int mask, byte[] block, int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _DLLEXPORT_Decompress(byte[] rgba, byte[] block, int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int _DLLEXPORT_GetStorageRequirements(int width, int height, int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _DLLEXPORT_CompressImage(byte[] rgba_src, int width, int height, byte[] blocks, int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _DLLEXPORT_DecompressImage(byte[] rgba, int width, int height, byte[] blocks, int flags);

        #endregion

        #region Kernel32DLL Import
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        #endregion


        public enum FlagsEnum
        {
            //! Use DXT1 compression.
            kDxt1 = (1 << 0),

            //! Use DXT3 compression.
            kDxt3 = (1 << 1),

            //! Use DXT5 compression.
            kDxt5 = (1 << 2),

            //! Use a very slow but very high quality colour compressor.
            kColourIterativeClusterFit = (1 << 8),

            //! Use a slow but high quality colour compressor (the default).
            kColourClusterFit = (1 << 3),

            //! Use a fast but low quality colour compressor.
            kColourRangeFit = (1 << 4),

            //! Use a perceptual metric for colour error (the default).
            kColourMetricPerceptual = (1 << 5),

            //! Use a uniform metric for colour error.
            kColourMetricUniform = (1 << 6),

            //! Weight the colour by alpha during cluster fit (disabled by default).
            kWeightColourByAlpha = (1 << 7)
        };

    }
}
