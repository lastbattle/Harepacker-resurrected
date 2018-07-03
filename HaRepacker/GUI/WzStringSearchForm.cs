/* Copyright (C) 2018 LastBattle
    https://github.com/eaxvac/Harepacker-resurrected

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

using MapleLib.Helpers;
using System;
using System.Collections.Generic;
using System.Windows.Forms;


namespace HaRepacker.GUI
{
    public partial class WzStringSearchForm : Form
    {

        // WZ Searcher
        public static Dictionary<int, KeyValuePair<string, string>>
            HexJumpList = new Dictionary<int, KeyValuePair<string, string>>();
        public static Dictionary<int, int> JumpList_Map = new Dictionary<int, int>();
        private WzStringSearchFormDataCache WzDataCache;

        private string loadedWzVersion;

        public WzStringSearchForm(WzStringSearchFormDataCache WzDataCache, string loadedWzVersion)
        {
            InitializeComponent();

            this.WzDataCache = WzDataCache;
            this.loadedWzVersion = loadedWzVersion;
        }

        #region Wz data ID searcher
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void in_textchanged(object sender, EventArgs e)
        {
            int data = 0;
            try
            {
                data = Int32.Parse(textBox6.Text);
            }
            catch (Exception) { }
            textBox5.Text = BitConverter.ToString(ByteUtils.IntegerToLittleEndian(data)).Replace("-", " ");

            Clipboard.SetText(textBox5.Text);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_hexlist_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selecIndex = comboBox_hexlist.SelectedIndex;
            if (selecIndex >= 0)
            {
                int count = 0;

                foreach (KeyValuePair<int, KeyValuePair<string, string>> data in HexJumpList)
                {
                    if (count == selecIndex)
                    {
                        textBox6.Text = data.Key.ToString();

                        label_itemname.Text = data.Value.Key;
                        label_itemdesc.Text = data.Value.Value;
                        break;
                    }
                    count++;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox_itemidFind_TextChanged(object sender, EventArgs e)
        {
            string query = textBox_itemidFind.Text;
            if (query.Length <= 2 && query != string.Empty) // if its string empty, include everything..
                return;

            HexJumpList.Clear();
            comboBox_hexlist.Items.Clear();

            // Items
            if (checkBox_searcheq.Checked)
            {
                WzDataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Eqp, query, HexJumpList);
            }
            if (checkBox_searchuse.Checked)
            {
                WzDataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Use, query, HexJumpList);
            }
            if (checkBox_searchsetup.Checked)
            {
                WzDataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Setup, query, HexJumpList);
            }
            if (checkBox_searchetc.Checked)
            {
                WzDataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Etc, query, HexJumpList);
            }
            if (checkBox_searchcash.Checked)
            {
                WzDataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Cash, query, HexJumpList);
            }
            // Quests
            if (checkBox_searchquest.Checked)
            {
                WzDataCache.LookupQuest(query, HexJumpList);
            }

            // NPC
            if (checkBox_searchNPC.Checked)
            {
                WzDataCache.LookupNPCs(query, HexJumpList);
            }
            // Maps
            if (checkBox_searchMaps.Checked)
            {
                WzDataCache.LookupMaps(query, HexJumpList);
            }
            // Skills
            if (checkbox_searchSkill.Checked)
            {
                WzDataCache.LookupSkills(query, HexJumpList);
            }
            // Jobs
            if (checkBox_searchJobs.Checked)
            {
                WzDataCache.LookupJobs(query, HexJumpList);
            }


            if (HexJumpList.Count == 0)
            {
                textBox6.Text = "0";
                label_itemname.Text = "Not found";
                label_itemdesc.Text = "Not found";
            }
            else
            {
                bool first = true;
                foreach (KeyValuePair<int, KeyValuePair<string, string>> data in HexJumpList)
                {
                    if (first)
                    {
                        textBox6.Text = data.Key.ToString();

                        label_itemname.Text = data.Value.Key;
                        label_itemdesc.Text = data.Value.Value;

                        first = false;
                    }
                    comboBox_hexlist.Items.Add(String.Format("{0} - {1}", data.Value.Key, data.Value.Value));
                }
            }
        }
        #endregion
    }
}