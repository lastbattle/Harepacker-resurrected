/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HaCreator.GUI.EditorPanels
{
    public partial class LifePanel : DockContent
    {
        List<string> reactors = new List<string>();
        List<string> npcs = new List<string>();
        List<string> mobs = new List<string>();

        private HaCreatorStateManager hcsm;

        public LifePanel(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            InitializeComponent();

            foreach (KeyValuePair<string, ReactorInfo> entry in Program.InfoManager.Reactors)
            {
                reactors.Add(entry.Value.ID);
            }
            foreach (KeyValuePair<string, string> entry in Program.InfoManager.NPCs)
            {
                npcs.Add(entry.Key + " - " + entry.Value);
            }
            foreach (KeyValuePair<string, string> entry in Program.InfoManager.Mobs)
            {
                mobs.Add(entry.Key + " - " + entry.Value);
            }

            ReloadLifeList();
        }

        private void lifeModeChanged(object sender, EventArgs e)
        {
            ReloadLifeList();
        }

        public static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) != -1;
        }

        private void ReloadLifeList()
        {
            string searchText = lifeSearchBox.Text;
            bool getAll = searchText == "";
            lifeListBox.Items.Clear();
            List<string> items = new List<string>();
            if (reactorRButton.Checked)
            {
                items.AddRange(getAll ? reactors : reactors.Where(x => ContainsIgnoreCase(x, searchText)));
            }
            else if (npcRButton.Checked)
            {
                items.AddRange(getAll ? npcs : npcs.Where(x => ContainsIgnoreCase(x, searchText)));
            }
            else if (mobRButton.Checked)
            {
                items.AddRange(getAll ? mobs : mobs.Where(x => ContainsIgnoreCase(x, searchText)));
            }
            items.Sort();
            lifeListBox.Items.AddRange(items.Cast<object>().ToArray());
        }

        private void lifeListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                lifePictureBox.Image = new Bitmap(1, 1);
                if (lifeListBox.SelectedItem == null) return;
                if (reactorRButton.Checked)
                {
                    ReactorInfo info = Program.InfoManager.Reactors[(string)lifeListBox.SelectedItem];
                    lifePictureBox.Image = new Bitmap(info.Image);
                    hcsm.EnterEditMode(ItemTypes.Reactors);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                }
                else if (npcRButton.Checked)
                {
                    string id = ((string)lifeListBox.SelectedItem).Substring(0, ((string)lifeListBox.SelectedItem).IndexOf(" - "));
                    NpcInfo info = WzInfoTools.GetNpcInfoById(id);
                    if (info == null)
                    {
                        lifePictureBox.Image = null;
                        return;
                    }
                    lifePictureBox.Image = new Bitmap(info.Image);
                    hcsm.EnterEditMode(ItemTypes.NPCs);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                }
                else if (mobRButton.Checked)
                {
                    string id = ((string)lifeListBox.SelectedItem).Substring(0, ((string)lifeListBox.SelectedItem).IndexOf(" - "));
                    MobInfo info = WzInfoTools.GetMobInfoById(id);
                    if (info == null)
                    {
                        lifePictureBox.Image = null;
                        return;
                    }
                    lifePictureBox.Image = new Bitmap(info.Image);
                    hcsm.EnterEditMode(ItemTypes.Mobs);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                }
            }
        }
    }
}
