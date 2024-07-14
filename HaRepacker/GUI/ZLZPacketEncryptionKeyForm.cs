using HaSharedLibrary.SystemInterop;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaRepacker.GUI {
    /// <summary>
    /// ZLZ WZ encryption keys 
    /// Credits: http://forum.ragezone.com/f921/release-gms-key-retriever-895646/index2.html 
    /// </summary>
    public partial class ZLZPacketEncryptionKeyForm : Form {
        public ZLZPacketEncryptionKeyForm() {
            InitializeComponent();
        }

        #region 64Bit ZLZ
        /// <summary>
        /// Opens the ZLZ64.DLL file
        /// </summary>
        /// <returns></returns>
        public bool OpenZLZDllFile_64Bit(string filePath) {
            FileInfo fileinfo = new FileInfo(filePath);
            bool setDLLDirectory = kernel32.SetDllDirectory(fileinfo.Directory.FullName);

            // Flags for LoadLibraryEx
            const uint DONT_RESOLVE_DLL_REFERENCES = 0x00000001;
            const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
            const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

            IntPtr module = kernel32.LoadLibraryEx(fileinfo.FullName, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);

            if (module == IntPtr.Zero) {
                uint lastError = kernel32.GetLastError();
                MessageBox.Show($"Unable to load DLL Library. Kernel32 GetLastError() : {lastError}", "Error");
            }
            else {
                try {
                    System.IntPtr functionAddress = (System.IntPtr)(module.ToInt64() + 0x3A40); // sub_0000000180003A40

                    var Method = Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(ZLZGenerateKey.GenerateKey)) as ZLZGenerateKey.GenerateKey;
                    Method();

                    ShowKey_64Bit(module, 0x74030);
                    return true;
                }
                catch (Exception exp) {
                    MessageBox.Show($"Invalid KeyGen position. This version of MapleStory may be unsupported.\r\n{exp}", "Error");
                }
                finally {
                    kernel32.FreeLibrary(module);
                }
            }
            return false;
        }

        /// <summary>
        /// Display the Aes key used for the maplestory encryption.
        /// </summary>
        private void ShowKey_64Bit(IntPtr module, int baseKeyPosition) {
            // OdinMS format
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 128; i += 4) {
                IntPtr readAddress = (IntPtr)(module.ToInt64() + baseKeyPosition + i);

                //MessageBox.Show("Read address: " + readAddress.ToString("X8"));

                byte value = (byte)Marshal.ReadByte(readAddress);
                sb.Append("(byte) 0x" + value.ToString("X"));
                sb.Append(", ");
            }
            sb = sb.Remove(sb.Length - 2, 2);

            // Mapleshark format
            StringBuilder sb2 = new StringBuilder();
            StringBuilder sb_sharkCombined = new StringBuilder();
            for (int i = 0; i < 128; i += 4) {
                IntPtr readAddress = (IntPtr)(module.ToInt64() + baseKeyPosition + i);

                byte value = Marshal.ReadByte(readAddress);
                string hexValue = value.ToString("X").PadLeft(2, '0');

                sb2.Append("0x" + hexValue);
                sb2.Append(", ");

                sb_sharkCombined.Append(hexValue);
            }
            sb2 = sb2.Remove(sb2.Length - 2, 2);

            textBox_aesOdin.Text = sb.ToString();
            textBox_aesOthers.Text = sb2.ToString()
                + Environment.NewLine + Environment.NewLine
                + sb_sharkCombined.ToString();
        }

        /*
         * Trace it from ZLZ64.dll _7ZInflator first
         * .rdata:00000001800606D8 ; const ZInflator::`vftable'
         * .rdata:00000001800606D8 ??_7ZInflator@@6B@ dq offset sub_18000DC20
         * .rdata:00000001800606D8                                         ; DATA XREF: sub_18000D940+9↑o
         * .rdata:00000001800606D8                                         ; sub_18000DC20+F↑o ...
         * .rdata:00000001800606E0                 dq offset processData
         * .rdata:00000001800606E8                 dq offset sub_18000E9F0
         * .rdata:00000001800606F0                 dq offset sub_18000E820
         * .rdata:00000001800606F8                 dq offset sub_18000E190
         * .rdata:0000000180060700                 dq offset sub_18000E810
         * .rdata:0000000180060708                 dq offset sub_18000EAE0
         * .rdata:0000000180060710                 dq offset sub_18000E000
         * .rdata:0000000180060718                 dq offset sub_18000DEA0 
         * 
         * From the first function, 
         * __int64 __fastcall processData(__int64 a1, __int64 a2, int a3)
{
  __int64 result; // rax
  unsigned int v6; // edx
  unsigned int v7; // ecx
  int v8; // eax
  int v9; // eax
  __int64 v10; // rcx
  unsigned int v11; // esi
  __int64 v12; // rax
  int v13; // edx
  __int64 v14; // rcx
  int v15; // eax
  int v16; // esi
  _QWORD *v17; // rbp
  int v18; // edi
  int v19; // eax
  __int64 v20; // rax
  int v21; // er8
  unsigned int v22; // edi
  int v23; // eax
  __int64 v24; // rax
  int v25; // edx
  __int64 v26; // rcx
  int v27; // eax
  unsigned int v28; // eax
  char v29[16]; // [rsp+30h] [rbp-28h] BYREF
  int v30; // [rsp+70h] [rbp+18h] BYREF

  if ( !a3 )
    return 0i64;
  *(_QWORD *)(a1 + 48) = a2;
  *(_DWORD *)(a1 + 56) = a3;
  while ( 1 )                                   // while (true)
  {
    if ( !*(_DWORD *)(a1 + 40) )
    {                                           // if (*(int32_t*)(context + 40) == 0) // If no data in buffer
      v6 = *(_DWORD *)(a1 + 136);               // uint32_t remainingBytes = *(uint32_t*)(context + 136);
      if ( *(_DWORD *)(a1 + 124) )              // if (*(int32_t*)(context + 124) != 0) // Some condition
      {
        v7 = 0;
        if ( v6 > 4 )
          v7 = v6 - 4;                          // uint32_t chunkSize = (remainingBytes > 4) ? (remainingBytes - 4) : 0;
        v8 = 0x10000;                           //  chunkSize = (chunkSize < 0x10000) ? chunkSize : 0x10000;
        if ( v7 < 0x10000 )
          v8 = v7;
        v30 = v8;
        if ( v8 )                               // if (chunkSize > 0)
        {
          v9 = *(_DWORD *)(a1 + 16);
          if ( (v9 & 0xF) != 0 )
          {
            sub_18000D830(v29, 5i64);           // readChunkSize(context, &chunkSize);
            sub_1800124AC(v29, &_TI1_AVZException__);// processChunk(context, chunkData, chunkSize);
            __debugbreak();
          }
          v10 = *(_QWORD *)(a1 + 8);
          *(_DWORD *)(a1 + 16) = v9 | 0x10;
          (*(void (__fastcall **)(__int64, int *, __int64))(*(_QWORD *)v10 + 8i64))(v10, &v30, 4i64);
          v11 = v30;
          v12 = sub_18000ED00((_QWORD *)(a1 + 128));
          v13 = *(_DWORD *)(a1 + 16);
          if ( (v13 & 0xF) != 0 )
          {
            sub_18000D830(v29, 5i64);
            sub_1800124AC(v29, &_TI1_AVZException__);
            __debugbreak();
          }
          v14 = *(_QWORD *)(a1 + 8);
          *(_DWORD *)(a1 + 16) = v13 | 0x10;
          v15 = (*(__int64 (__fastcall **)(__int64, __int64, _QWORD))(*(_QWORD *)v14 + 8i64))(v14, v12, v11);
          v16 = v30;
          *(_DWORD *)(a1 + 40) = v15;
          v17 = (_QWORD *)(a1 + 24);
          v18 = sub_18000ED00((_QWORD *)(a1 + 128));
          v19 = sub_18000ED00((_QWORD *)(a1 + 24));
          sub_180003BA0(v19, v18, v16, (unsigned int)&unk_180074010, 0);
        }
        else
        {
          *(_DWORD *)(a1 + 40) = 0;
          v17 = (_QWORD *)(a1 + 24);
        }
        v20 = sub_18000ED00(v17);
        v21 = *(_DWORD *)(a1 + 136);
        *(_QWORD *)(a1 + 32) = v20;
        if ( v21 != -1 )
          *(_DWORD *)(a1 + 136) = v21 - (*(_DWORD *)(a1 + 40) != 0 ? 4 : 0) - *(_DWORD *)(a1 + 40);
      }
      else
      {
        v22 = 0x10000;
        if ( v6 < 0x10000 )
          v22 = *(_DWORD *)(a1 + 136);          //  uint32_t bytesToProcess = (remainingBytes < 0x10000) ? remainingBytes : 0x10000;
        if ( v22 )                              // if (bytesToProcess > 0)
        {
          v24 = sub_18000ED00((_QWORD *)(a1 + 24));
          v25 = *(_DWORD *)(a1 + 16);
          if ( (v25 & 0xF) != 0 )
          {
            sub_18000D830(v29, 5i64);           //  int64_t data = readData(context);
            sub_1800124AC(v29, &_TI1_AVZException__);// int32_t processedBytes = processData(context, data, bytesToProcess);
            JUMPOUT(0x18000E715i64);
          }
          v26 = *(_QWORD *)(a1 + 8);
          *(_DWORD *)(a1 + 16) = v25 | 0x10;
          v23 = (*(__int64 (__fastcall **)(__int64, __int64, _QWORD))(*(_QWORD *)v26 + 8i64))(v26, v24, v22);
        }
        else
        {
          v23 = 0;
        }
        *(_DWORD *)(a1 + 40) = v23;
        *(_QWORD *)(a1 + 32) = sub_18000ED00((_QWORD *)(a1 + 24));//  *(int64_t*)(context + 32) = readData(context);
        v27 = *(_DWORD *)(a1 + 136);            // int32_t remainingBytes = *(int32_t*)(context + 136);
        if ( v27 != -1 )                        // if (remainingBytes != -1)
          *(_DWORD *)(a1 + 136) = v27 - *(_DWORD *)(a1 + 40);// *(int32_t*)(context + 136) = remainingBytes - *(int32_t*)(context + 40);
      }
    }
    v28 = sub_180008390(a1 + 32, 2i64);
    if ( v28 )
      break;
    if ( !*(_DWORD *)(a1 + 56) )
      goto LABEL_27;
  }
  if ( v28 != 1 && v28 != -5 )
    sub_18000E8A0(a1, v28);
LABEL_27:
  result = (unsigned int)(a3 - *(_DWORD *)(a1 + 56));
  *(_DWORD *)(a1 + 56) = 0;
  return result;
}

        * 
        * Inside sub_180003BA0(v19, v18, v16, (unsigned int)&unk_180074010, 0);
        * __int128 *__fastcall sub_180003BA0(__int64 a1, int a2, int a3, _DWORD *a4, int a5)
{
  __int64 v9; // rbx
  unsigned int v10; // edi
  __int64 v11; // rbx
  __int128 *result; // rax
  __int64 v13; // rdx
  __int64 v14; // rbx
  unsigned int v15[4]; // [rsp+30h] [rbp-188h] BYREF
  __int128 v16[2]; // [rsp+40h] [rbp-178h] BYREF
  unsigned int v17; // [rsp+60h] [rbp-158h]
  int v18; // [rsp+64h] [rbp-154h] BYREF
  char v19[280]; // [rsp+68h] [rbp-150h] BYREF

  if ( a5 )
    init_key();
  v9 = 0i64;
  v18 = 8;
  v15[0] = 0;
  sub_180004230(&dword_180074030, v19);
  v17 = 0;
  v10 = 0;
  if ( a4 )
  {
    LODWORD(v16[0]) = *a4;
    DWORD1(v16[0]) = v16[0];
    DWORD2(v16[0]) = v16[0];
    HIDWORD(v16[0]) = v16[0];
  }
  else
  {
    v16[0] = xmmword_18005D4B0;
  }
  if ( a3 )
  {
    sub_180003E50((unsigned int)v16, a2, a3, a1, (__int64)v15);
    v9 = v15[0];
    v10 = v17;
  }
  v11 = a1 + v9;
  result = (__int128 *)sub_180002B80(&v18, v16);
  if ( v10 )
  {
    v13 = v10;
    v14 = v11 - (_QWORD)v16;
    result = v16;
    do
    {
      *((_BYTE *)result + v14) = *(_BYTE *)result ^ *((_BYTE *)result + 16);
      result = (__int128 *)((char *)result + 1);
      --v13;
    }
    while ( v13 );
  }
  return result;
}

        * 
        * First function
        * void init_key()
{
  if ( !dword_1800760F4 )
  {
    dword_180074030 = 0xF;
    dword_180074034 = 0x14;
    dword_180074038 = 5;
    dword_18007403C = 0x65;
    dword_180074040 = 27;
    dword_180074044 = 40;
    dword_180074048 = 97;
    dword_18007404C = 201;
    dword_180074050 = 197;
    dword_180074054 = 231;
    dword_180074058 = 44;
    dword_18007405C = 142;
    dword_180074060 = 70;
    dword_180074064 = 54;
    dword_180074068 = 8;
    dword_18007406C = 220;
    dword_180074070 = 243;
    dword_180074074 = 168;
    dword_180074078 = 141;
    dword_18007407C = 254;
    dword_180074080 = 190;
    dword_180074084 = 242;
    dword_180074088 = 235;
    dword_18007408C = 113;
    dword_180074090 = 255;
    dword_180074094 = 160;
    dword_180074098 = 208;
    dword_18007409C = 59;
    dword_1800740A0 = 117;
    dword_1800740A4 = 6;
    dword_1800740A8 = 140;
    dword_1800740AC = 126;
    dword_1800760F4 = 1;
  }
}

        OR simply search 13h, 52h, 2Ah, 5Bh, 8, 2, 10h, 60h, 6, 2, 43h, 0Fh 
        and find the address that sets this value.
         */
        #endregion


        #region 32Bit ZLZ
        /// <summary>
        /// Opens the ZLZ.DLL file
        /// Harepacker needs to be compiled under x86 for this to work!
        /// </summary>
        /// <returns></returns>
        public bool OpenZLZDllFile_32Bit(string filePath) {
            FileInfo fileinfo = new FileInfo(filePath);

            bool setDLLDirectory = kernel32.SetDllDirectory(fileinfo.Directory.FullName);
            IntPtr module;
            if (((int)(module = kernel32.LoadLibrary(fileinfo.FullName))) == 0) {
                uint lastError = kernel32.GetLastError();

                MessageBox.Show("Unable to load DLL Library. Kernel32 GetLastError() : " + lastError, "Error");
            }
            else {
                try {
                    System.IntPtr functionAddress = (System.IntPtr)(module.ToInt32() + 0x1340); // sub_10001340

                    var Method = Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(ZLZGenerateKey.GenerateKey)) as ZLZGenerateKey.GenerateKey;
                    Method();

                    ShowKey_32Bit(module, 0x14020); // 0x1340

                    return true;
                }
                catch (Exception exp) {
                    MessageBox.Show("Invalid KeyGen position. This version of MapleStory may be unsupported.\r\n" + exp.ToString(), "Error");
                }
                finally {
                    kernel32.FreeLibrary(module);
                }
            }
            return false;
        }


        /// <summary>
        /// Display the Aes key used for the maplestory encryption.
        /// </summary>
        private void ShowKey_32Bit(IntPtr module, int baseKeyPosition) {
            // OdinMS format
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 128; i += 16) {
                IntPtr readAddress = (IntPtr)(module.ToInt32() + baseKeyPosition + i);

                short value = (short)Marshal.ReadInt32(readAddress);
                sb.Append("(byte) 0x" + value.ToString("X") + ", (byte) 0x00, (byte) 0x00, (byte) 0x00");
                sb.Append(", ");
            }
            sb = sb.Remove(sb.Length - 2, 2);

            // Mapleshark format
            StringBuilder sb2 = new StringBuilder();
            StringBuilder sb_sharkCombined = new StringBuilder();
            for (int i = 0; i < 128; i += 4) {
                IntPtr readAddress = (IntPtr)(module.ToInt32() + baseKeyPosition + i);

                byte value = Marshal.ReadByte(readAddress);
                string hexValue = value.ToString("X").PadLeft(2, '0');

                sb2.Append("0x" + hexValue);
                sb2.Append(", ");

                sb_sharkCombined.Append(hexValue);
            }
            sb2 = sb2.Remove(sb2.Length - 2, 2);

            textBox_aesOdin.Text = sb.ToString();
            textBox_aesOthers.Text = sb2.ToString()
                + Environment.NewLine + Environment.NewLine
                + sb_sharkCombined.ToString();
        }
        #endregion
    }
}
