/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using HaSharedLibrary.SystemInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HaSharedLibrary.Util {

    public class MemoryScannerHelper {


        /// <summary>
        /// Searches the memory for byte array pattern
        /// </summary>
        /// <param name="processHandle">Process.GetCurrentProcess().Handle</param>
        /// <param name="startAddress"></param>
        /// <param name="endAddress"></param>
        /// <param name="searchPattern">i.e [AC ?? 00 00 EE 5F ??]</param>
        /// <returns></returns>
        public static IntPtr ScanCurrentProcessMemory(IntPtr processHandle, long startAddress, long endAddress, string searchPattern) {
            try {
                int bufferSize = 4096;
                byte[] buffer = new byte[bufferSize];

                for (long currentAddress = startAddress; currentAddress < endAddress; currentAddress += bufferSize) {
                    IntPtr bytesRead;
                    if (!kernel32.ReadProcessMemory(processHandle, (IntPtr)currentAddress, buffer, bufferSize, out bytesRead)) {
                        continue; // Skip if we can't read this memory region
                    }

                    int actualBytesRead = bytesRead.ToInt32();

                    byte?[] pattern = ParseSearchPattern(searchPattern);
                    int result = SearchPattern(buffer, actualBytesRead, pattern);

                    if (result != -1) {
                        return (IntPtr)(currentAddress + result);
                    }
                }

                return IntPtr.Zero; // Pattern not found
            }
            catch (Exception ex) {
                Debug.WriteLine($"An error occurred: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Parses an array-of-byte [AOB] search pattern
        /// </summary>
        /// <param name="pattern">i.e [AC ?? 00 00 EE 5F ??]</param>
        /// <returns></returns>
        private static byte?[] ParseSearchPattern(string pattern) {
            return pattern.Split(' ')
                .Select(b => b == "??" ? (byte?)null : Convert.ToByte(b, 16))
                .ToArray();
        }

        private static int SearchPattern(byte[] haystack, int haystackLength, byte?[] needle) {
            int needleLength = needle.Length;
            for (int i = 0; i <= haystackLength - needleLength; i++) {
                bool found = true;
                for (int j = 0; j < needleLength; j++) {
                    if (needle[j].HasValue && haystack[i + j] != needle[j].Value) {
                        found = false;
                        break;
                    }
                }
                if (found) {
                    return i;
                }
            }
            return -1;
        }
    }
}