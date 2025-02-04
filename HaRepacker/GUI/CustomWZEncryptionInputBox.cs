﻿/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

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

using System;
using System.ComponentModel;
using System.Windows.Forms;
using MapleLib.PacketLib;
using MapleLib.MapleCryptoLib;
using MapleLib.Configuration;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;

namespace HaRepacker.GUI
{
    public partial class CustomWZEncryptionInputBox : Form
    {
        private EncryptionKey _currentSelectedEncryptionKey;
        private BindingList<EncryptionKey> _encryptionKeys;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="panel"></param>
        public CustomWZEncryptionInputBox()
        {
            InitializeComponent();

            _encryptionKeys = new BindingList<EncryptionKey>(Program.ConfigurationManager.CustomKeys);

            nameBox.DataSource = _encryptionKeys;
            nameBox.DisplayMember = "Name";
        }


        /// <summary>
        /// Form load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveForm_Load(object sender, EventArgs e)
        {
            ApplicationSettings appSettings = Program.ConfigurationManager.ApplicationSettings;

            // AES IV
            string storedCustomEnc = appSettings.MapleVersion_CustomEncryptionBytes;
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
                appSettings.MapleVersion_CustomEncryptionBytes = "00 00 00 00";
                Program.ConfigurationManager.Save();
            }
            else
            {
                textBox_byte0.Text = splitBytes[0];
                textBox_byte1.Text = splitBytes[1];
                textBox_byte2.Text = splitBytes[2];
                textBox_byte3.Text = splitBytes[3];
            }

            // AES User key
            if (appSettings.MapleVersion_CustomAESUserKey == string.Empty)  // set default if there's none
            {
                SetDefaultTextBoxAESUserKey();
            } 
            else
            {
                string storedCustomAESKey = appSettings.MapleVersion_CustomAESUserKey;
                string[] splitAESKeyBytes = storedCustomAESKey.Split(' ');

                bool parsed2 = true;
                if (splitAESKeyBytes.Length == 32)
                {
                    foreach (string byte_ in splitAESKeyBytes)
                    {
                        if (!CheckHexDigits(byte_))
                        {
                            parsed2 = false;
                            break;
                        }
                    }
                }
                else
                    parsed2 = false;

                if (!parsed2)
                {
                    // do nothing.. default, could be corrupted anyway
                    appSettings.MapleVersion_CustomAESUserKey = string.Empty;
                    Program.ConfigurationManager.Save();
                }
                else
                {
                   UserKey2TextBoxes(splitAESKeyBytes);
                }
            }
            
            // load the custom user key from ApplicationSettings.txt
            var iv = Program.ConfigurationManager.GetCusomWzIVEncryption();
            Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
            var currentLoadedKey = WzKeyGenerator.GenerateWzKey(iv, MapleCryptoConstants.UserKey_WzLib);
            
            // find the matched one
            var matchedIndex = Program.ConfigurationManager.CustomKeys.FindIndex(k => k.WzKey == currentLoadedKey);

            if (matchedIndex != -1) {
                nameBox.SelectedIndex = matchedIndex;
            }
            else {
                DefaultData();
            }   
        }

        private void DefaultData() {
            _currentSelectedEncryptionKey = null;
            nameBox.Text = "Custom Key " + (Program.ConfigurationManager.CustomKeys.Count + 1);
            SetDefaultTextBoxIV();
            SetDefaultTextBoxAESUserKey();
        }

        private void nameBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (nameBox.SelectedItem is null)
                return;

            _currentSelectedEncryptionKey = (EncryptionKey)nameBox.SelectedItem;

            FillBoxesWithSelectedKey();
        }

        private void FillBoxesWithSelectedKey() {

            if (_currentSelectedEncryptionKey == null) {
                SetDefaultTextBoxIV();
                SetDefaultTextBoxAESUserKey();
                return;
            }

            // IV
            var iv = _currentSelectedEncryptionKey.Iv.Split(' ');
            textBox_byte0.Text = iv[0];
            textBox_byte1.Text = iv[1];
            textBox_byte2.Text = iv[2];
            textBox_byte3.Text = iv[3];

            // AES User Key
            var aesUserKey = _currentSelectedEncryptionKey.AesUserKey.Split(' ');
            UserKey2TextBoxes(aesUserKey);
        }

        private void createButton_Click(object sender, EventArgs e) {
            DefaultData();
        }

        private void deleteButton_Click(object sender, EventArgs e) {
            if (nameBox.Items.Count <= 1) {
                MessageBox.Show("Can't delete. Must have at least one Custom Key.", "Error");
                return;
            }

            if (_currentSelectedEncryptionKey == null) // "Create New" button is just clicked, not a valid selection
            {
                MessageBox.Show("Nothing to delete.", "Error");
                return;
            }

            if (DialogResult.Yes != MessageBox.Show($"Are you sure you want to delete [{_currentSelectedEncryptionKey.Name}]?", "Warning", MessageBoxButtons.YesNo))
                return;

            int removeIndex = nameBox.SelectedIndex;
            _encryptionKeys.RemoveAt(removeIndex);

            if (removeIndex == _encryptionKeys.Count)
            {
                // If the last item is removed, nameBox.SelectedItem automatically becomes the previous item (index - 1).
                // The SelectedIndexChanged event will be triggered, causing all boxes to update accordingly.
            }
            else
            {
                // Otherwise we need to manually update the selected key
                nameBox_SelectedIndexChanged(null, null);
            }

            Program.ConfigurationManager.Save();
        }

        /// <summary>
        /// On save button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) {
                MessageBox.Show("Please enter a name.", "Error");
                return;
            }

            // IV 
            string[] ivBytes = new string[4]
            {
                textBox_byte0.Text,
                textBox_byte1.Text,
                textBox_byte2.Text,
                textBox_byte3.Text
            };

            if (!CheckHexDigits(ivBytes[0]) || !CheckHexDigits(ivBytes[1]) || !CheckHexDigits(ivBytes[2]) || !CheckHexDigits(ivBytes[3]))
            {
                MessageBox.Show("Wrong format for AES IV. Please check the input bytes.", "Error");
                return;
            }

            // AES User Key
            // AES User Key (using the new TextBoxes2UserKey method)
            string[] userKeys = TextBoxes2UserKey();

            for (int i = 0; i < userKeys.Length; i++)
            {
                if (!CheckHexDigits(userKeys[i]))
                {
                    MessageBox.Show("Wrong format for AES User Key. Please check the input bytes.", "Error");
                    return;
                }
            }

            // Save
            Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionName = nameBox.Text;
            Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionBytes = string.Join(" ", ivBytes);
            Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomAESUserKey = string.Join(" ", userKeys);

            // Set the UserKey in memory.
            Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();

            if (_currentSelectedEncryptionKey == null) {
                // add
                _currentSelectedEncryptionKey = new EncryptionKey {
                    Name = nameBox.Text,
                    Iv = Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionBytes,
                    AesUserKey = Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomAESUserKey,
                    MapleVersion = WzMapleVersion.CUSTOM,
                };
                Program.ConfigurationManager.CustomKeys.Add(_currentSelectedEncryptionKey);
            }
            else {
                //update
                _currentSelectedEncryptionKey.Name = nameBox.Text;
                _currentSelectedEncryptionKey.Iv = Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionBytes;
                _currentSelectedEncryptionKey.AesUserKey = Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomAESUserKey;
            }

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

        /// <summary>
        /// On clicked reset AES User Key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_resetAESUserKey_Click(object sender, EventArgs e)
        {
            SetDefaultTextBoxAESUserKey();
        }

        private void SetDefaultTextBoxIV() {
            textBox_byte0.Text = "00";
            textBox_byte1.Text = "00";
            textBox_byte2.Text = "00";
            textBox_byte3.Text = "00";
        }

        private void SetDefaultTextBoxAESUserKey()
        {
            byte[] AESUserKey = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT;

            textBox_AESUserKey1.Text = HexTool.ToString(AESUserKey[0 * 4]);
            textBox_AESUserKey2.Text = HexTool.ToString(AESUserKey[1 * 4]);
            textBox_AESUserKey3.Text = HexTool.ToString(AESUserKey[2 * 4]);
            textBox_AESUserKey4.Text = HexTool.ToString(AESUserKey[3 * 4]);
            textBox_AESUserKey5.Text = HexTool.ToString(AESUserKey[4 * 4]);
            textBox_AESUserKey6.Text = HexTool.ToString(AESUserKey[5 * 4]);
            textBox_AESUserKey7.Text = HexTool.ToString(AESUserKey[6 * 4]);
            textBox_AESUserKey8.Text = HexTool.ToString(AESUserKey[7 * 4]);
            textBox_AESUserKey9.Text = HexTool.ToString(AESUserKey[8 * 4]);
            textBox_AESUserKey10.Text = HexTool.ToString(AESUserKey[9 * 4]);
            textBox_AESUserKey11.Text = HexTool.ToString(AESUserKey[10 * 4]);
            textBox_AESUserKey12.Text = HexTool.ToString(AESUserKey[11 * 4]);
            textBox_AESUserKey13.Text = HexTool.ToString(AESUserKey[12 * 4]);
            textBox_AESUserKey14.Text = HexTool.ToString(AESUserKey[13 * 4]);
            textBox_AESUserKey15.Text = HexTool.ToString(AESUserKey[14 * 4]);
            textBox_AESUserKey16.Text = HexTool.ToString(AESUserKey[15 * 4]);
            textBox_AESUserKey17.Text = HexTool.ToString(AESUserKey[16 * 4]);
            textBox_AESUserKey18.Text = HexTool.ToString(AESUserKey[17 * 4]);
            textBox_AESUserKey19.Text = HexTool.ToString(AESUserKey[18 * 4]);
            textBox_AESUserKey20.Text = HexTool.ToString(AESUserKey[19 * 4]);
            textBox_AESUserKey21.Text = HexTool.ToString(AESUserKey[20 * 4]);
            textBox_AESUserKey22.Text = HexTool.ToString(AESUserKey[21 * 4]);
            textBox_AESUserKey23.Text = HexTool.ToString(AESUserKey[22 * 4]);
            textBox_AESUserKey24.Text = HexTool.ToString(AESUserKey[23 * 4]);
            textBox_AESUserKey25.Text = HexTool.ToString(AESUserKey[24 * 4]);
            textBox_AESUserKey26.Text = HexTool.ToString(AESUserKey[25 * 4]);
            textBox_AESUserKey27.Text = HexTool.ToString(AESUserKey[26 * 4]);
            textBox_AESUserKey28.Text = HexTool.ToString(AESUserKey[27 * 4]);
            textBox_AESUserKey29.Text = HexTool.ToString(AESUserKey[28 * 4]);
            textBox_AESUserKey30.Text = HexTool.ToString(AESUserKey[29 * 4]);
            textBox_AESUserKey31.Text = HexTool.ToString(AESUserKey[30 * 4]);
            textBox_AESUserKey32.Text = HexTool.ToString(AESUserKey[31 * 4]);
        }

        private void UserKey2TextBoxes(string[] aesUserKey)
        {
            textBox_AESUserKey1.Text = aesUserKey[0];
            textBox_AESUserKey2.Text = aesUserKey[1];
            textBox_AESUserKey3.Text = aesUserKey[2];
            textBox_AESUserKey4.Text = aesUserKey[3];
            textBox_AESUserKey5.Text = aesUserKey[4];
            textBox_AESUserKey6.Text = aesUserKey[5];
            textBox_AESUserKey7.Text = aesUserKey[6];
            textBox_AESUserKey8.Text = aesUserKey[7];
            textBox_AESUserKey9.Text = aesUserKey[8];
            textBox_AESUserKey10.Text = aesUserKey[9];
            textBox_AESUserKey11.Text = aesUserKey[10];
            textBox_AESUserKey12.Text = aesUserKey[11];
            textBox_AESUserKey13.Text = aesUserKey[12];
            textBox_AESUserKey14.Text = aesUserKey[13];
            textBox_AESUserKey15.Text = aesUserKey[14];
            textBox_AESUserKey16.Text = aesUserKey[15];
            textBox_AESUserKey17.Text = aesUserKey[16];
            textBox_AESUserKey18.Text = aesUserKey[17];
            textBox_AESUserKey19.Text = aesUserKey[18];
            textBox_AESUserKey20.Text = aesUserKey[19];
            textBox_AESUserKey21.Text = aesUserKey[20];
            textBox_AESUserKey22.Text = aesUserKey[21];
            textBox_AESUserKey23.Text = aesUserKey[22];
            textBox_AESUserKey24.Text = aesUserKey[23];
            textBox_AESUserKey25.Text = aesUserKey[24];
            textBox_AESUserKey26.Text = aesUserKey[25];
            textBox_AESUserKey27.Text = aesUserKey[26];
            textBox_AESUserKey28.Text = aesUserKey[27];
            textBox_AESUserKey29.Text = aesUserKey[28];
            textBox_AESUserKey30.Text = aesUserKey[29];
            textBox_AESUserKey31.Text = aesUserKey[30];
            textBox_AESUserKey32.Text = aesUserKey[31];
        }
        
        private string[] TextBoxes2UserKey()
        {
            string[] aesUserKey = new string[32];
    
            aesUserKey[0] = textBox_AESUserKey1.Text;
            aesUserKey[1] = textBox_AESUserKey2.Text;
            aesUserKey[2] = textBox_AESUserKey3.Text;
            aesUserKey[3] = textBox_AESUserKey4.Text;
            aesUserKey[4] = textBox_AESUserKey5.Text;
            aesUserKey[5] = textBox_AESUserKey6.Text;
            aesUserKey[6] = textBox_AESUserKey7.Text;
            aesUserKey[7] = textBox_AESUserKey8.Text;
            aesUserKey[8] = textBox_AESUserKey9.Text;
            aesUserKey[9] = textBox_AESUserKey10.Text;
            aesUserKey[10] = textBox_AESUserKey11.Text;
            aesUserKey[11] = textBox_AESUserKey12.Text;
            aesUserKey[12] = textBox_AESUserKey13.Text;
            aesUserKey[13] = textBox_AESUserKey14.Text;
            aesUserKey[14] = textBox_AESUserKey15.Text;
            aesUserKey[15] = textBox_AESUserKey16.Text;
            aesUserKey[16] = textBox_AESUserKey17.Text;
            aesUserKey[17] = textBox_AESUserKey18.Text;
            aesUserKey[18] = textBox_AESUserKey19.Text;
            aesUserKey[19] = textBox_AESUserKey20.Text;
            aesUserKey[20] = textBox_AESUserKey21.Text;
            aesUserKey[21] = textBox_AESUserKey22.Text;
            aesUserKey[22] = textBox_AESUserKey23.Text;
            aesUserKey[23] = textBox_AESUserKey24.Text;
            aesUserKey[24] = textBox_AESUserKey25.Text;
            aesUserKey[25] = textBox_AESUserKey26.Text;
            aesUserKey[26] = textBox_AESUserKey27.Text;
            aesUserKey[27] = textBox_AESUserKey28.Text;
            aesUserKey[28] = textBox_AESUserKey29.Text;
            aesUserKey[29] = textBox_AESUserKey30.Text;
            aesUserKey[30] = textBox_AESUserKey31.Text;
            aesUserKey[31] = textBox_AESUserKey32.Text;

            return aesUserKey;
        }

        /// <summary>
        /// On hyperlink click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            const string link = "http://forum.ragezone.com/f921/maplestorys-aes-userkey-1116849/";

            System.Diagnostics.Process.Start(link);
        }
    }
}
