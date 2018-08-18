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
using Win32;

namespace HaRepacker.GUI
{
    /// <summary>
    /// ZLZ WZ encryption keys 
    /// Credits: http://forum.ragezone.com/f921/release-gms-key-retriever-895646/index2.html 
    /// </summary>
    public partial class ZLZPacketEncryptionKeyForm : Form
    {
        public ZLZPacketEncryptionKeyForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens the ZLZ.DLL file
        /// Harepacker needs to be compiled under x86 for this to work!
        /// </summary>
        /// <returns></returns>
        public bool OpenZLZDllFile()
        {
            if (IntPtr.Size != 4)
            {
                MessageBox.Show("Unable to load ZLZ.dll. Please ensure that you're running the x86 version (32-bit) of HaRepacker.", "Error");
                return false;
            }

            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog1.Filter = "ZLZ file (.dll)|*.dll";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            DialogResult userClickedOK = openFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == DialogResult.OK)
            {
                FileInfo fileinfo = new FileInfo(openFileDialog1.FileName);

                bool setDLLDirectory = kernel32.SetDllDirectory(fileinfo.Directory.FullName);
                IntPtr module;
                if (((int)(module = kernel32.LoadLibrary(fileinfo.FullName))) == 0)
                {
                    uint lastError = kernel32.GetLastError();

                    MessageBox.Show("Unable to load DLL Library. Kernel32 GetLastError() : " + lastError, "Error");
                }
                else
                {
                    try
                    {
                        var Method = Marshal.GetDelegateForFunctionPointer((IntPtr)(module.ToInt32() + 0x1340), typeof(GenerateKey)) as GenerateKey;
                        Method();

                        ShowKey(module);

                        return true;
                    }
                    catch (Exception exp)
                    {
                        MessageBox.Show("Invalid KeyGen position. This version of MapleStory may be unsupported.\r\n" + exp.ToString(), "Error");
                    }
                    finally
                    {
                        kernel32.FreeLibrary(module);
                    }
                }
            }
            return false;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GenerateKey();


        /// <summary>
        /// Display the Aes key used for the maplestory encryption.
        /// </summary>
        private void ShowKey(IntPtr module)
        {
            const int KeyPos = 0x14020;
            const int KeyGen = 0x1340;

            // OdinMS format
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 128; i += 16)
            {
                short value = (short)Marshal.ReadInt32((IntPtr)(module.ToInt32() + KeyPos + i));
                sb.Append("(byte) 0x" + value.ToString("X") + ", (byte) 0x00, (byte) 0x00, (byte) 0x00");
                sb.Append(", ");
            }
            sb = sb.Remove(sb.Length - 2, 2);

            // Mapleshark format
            StringBuilder sb2 = new StringBuilder();
            StringBuilder sb_sharkCombined = new StringBuilder();
            for (int i = 0; i < 128; i += 4)
            {
                byte value = Marshal.ReadByte((IntPtr)(module.ToInt32() + KeyPos + i));
                string hexValue = value.ToString("X").PadLeft(2, '0');

                sb2.Append("0x" + hexValue);
                sb2.Append(", ");

                sb_sharkCombined.Append(hexValue);
            }
            sb2 = sb2.Remove(sb2.Length - 2, 2);

            textBox_aesOdin.Text = sb.ToString();
            textBox_aesOthers.Text = sb2.ToString() 
                + Environment.NewLine + Environment.NewLine 
                + sb_sharkCombined.ToString(); ;
        }
    }
}
