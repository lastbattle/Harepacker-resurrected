/* Copyright (C) 2020 LastBattle
* https://github.com/lastbattle/Harepacker-resurrected
* 
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.IO;
using MapleLib.WzLib.Util;
using System.Diagnostics;
using HaRepacker.GUI.Panels;
using System.Text.RegularExpressions;
using MapleLib.PacketLib;

namespace HaRepacker.GUI
{
    public partial class CustomWZEncryptionInputBox : Form
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="panel"></param>
        public CustomWZEncryptionInputBox()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Form load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveForm_Load(object sender, EventArgs e)
        { 
            // Load from settings
            string storedCustomEnc = Program.ConfigurationManager.ApplicationSettings.MapleVersion_EncryptionBytes;
            string[] splitBytes = storedCustomEnc.Split(' ');

            bool parsed = true;
            if (splitBytes.Length == 4)
            {
                foreach (string byte_ in splitBytes)
                {
                    if (!CheckHexDigits(byte_))
                    {
                        parsed = false;
                        break;
                    }
                }
            }
            else
                parsed = false;

            if (!parsed)
            {
                // do nothing.. default, could be corrupted anyway
                Program.ConfigurationManager.ApplicationSettings.MapleVersion_EncryptionBytes = "00 00 00 00";
                Program.ConfigurationManager.Save();
            }
            else
            {
                int i = 0;
                foreach (string byte_ in splitBytes)
                {
                    switch (i)
                    {
                        case 0:
                            textBox_byte0.Text = byte_;
                            break;
                        case 1:
                            textBox_byte1.Text = byte_;
                            break;
                        case 2:
                            textBox_byte2.Text = byte_;
                            break;
                        case 3:
                            textBox_byte3.Text = byte_;
                            break;
                    }
                    i++;
                }
            }
        }

        /// <summary>
        /// On save button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveButton_Click(object sender, EventArgs e)
        {
            string strByte0 = textBox_byte0.Text;
            string strByte1 = textBox_byte1.Text;
            string strByte2 = textBox_byte2.Text;
            string strByte3 = textBox_byte3.Text;

            if (!CheckHexDigits(strByte0) || !CheckHexDigits(strByte1) || !CheckHexDigits(strByte2) || !CheckHexDigits(strByte3))
            {
                MessageBox.Show("Wrong format. Please check the input bytes.", "Error");
                return;
            }

            Program.ConfigurationManager.ApplicationSettings.MapleVersion_EncryptionBytes =
                string.Format("{0} {1} {2} {3}",
                strByte0,
                strByte1,
                strByte2,
                strByte3);
            Program.ConfigurationManager.Save();
            Close();
        }

        /// <summary>
        /// Checks the input hex string i.e "0x5E" if its valid or not.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private bool CheckHexDigits(string input)
        {
            if (input.Length >= 1 && input.Length <= 2)
            {
                for (int i = 0; i < input.Length; i++)
                {
                    if (!HexEncoding.IsHexDigit(input[i]))
                    {
                        return false;
                    }
                }
            }
            else
                return false;
            return true;
        }
    }
}
