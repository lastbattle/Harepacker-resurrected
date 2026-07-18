using HaCreator.GUI.Input;
using HaCreator.GUI.InstanceEditor;
using HaCreator.MapEditor;
using HaCreator.Wz;
using HaRepacker;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.MapStructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HaSharedLibrary.GUI;
using CheckBox = HaCreator.GUI.InfoEditorControls.CheckBox;
using NumericUpDown = HaCreator.GUI.InfoEditorControls.NumericUpDown;

namespace HaCreator.GUI
{
    public partial class InfoEditor : EditorBase
    {
        public MapInfo info;
        private readonly MultiBoard multiBoard;
        private readonly Board board;
        private readonly System.Windows.Controls.TabItem tabItem;

        public InfoEditor(Board board, MapInfo info, MultiBoard multiBoard, System.Windows.Controls.TabItem tabItem)
        {
            InitializeComponent();
            EditorPanels.EditorPanelLocalizer.Attach(this);

            this.board = board;
            this.info = info;
            this.multiBoard = multiBoard;
            this.tabItem = tabItem;

            foreach (string optionKey in new[]
            {
                "MapOption_Clouds", "MapOption_Snow", "MapOption_Rain", "MapOption_Swim", "MapOption_Fly",
                "MapOption_Town", "MapOption_PartyOnly", "MapOption_ExpeditionOnly", "MapOption_NoMapCommands",
                "MapOption_HideMinimap", "MapOption_MinimapToggle", "MapOption_PersonalShops",
                "MapOption_EntrustedShops", "MapOption_NoRegeneration", "MapOption_BlockPartyBossChange",
                "MapOption_Everlast", "MapOption_DamageCheckFree", "MapOption_DisableScrolls",
                "MapOption_FlySkillRequired", "MapOption_ZakumAntiHack", "MapOption_CheckAllMoves",
                "MapOption_UseVrAsLimits", "MapOption_MirrorBottom"
            })
                optionsList.AddOption(EditorPanels.EditorPanelLocalizer.Text(optionKey));

            foreach (string type in new[] { "Default (0)", "Snowball (1)", "Continent Move (2)", "Tournament (3)", "Coconut (4)", "OX Quiz (5)", "Personal Time Limit (6)", "Waiting Room (7)", "Guild Boss (8)", "Limited View (9)", "Monster Carnival (A)", "Monster Carnival Revive (B)", "Zakum (C)", "Ariant Coliseum (D)", "Mu Lung Dojo (E)", "Monster Carnival 2 (F)", "Monster Carnival Waiting Room (10)", "Cookie House (11)", "Balrog (12)", "Sheep VS Wolf (13)", "Space Gaga (14)", "Witch Tower (15)", "Aran Tutorial (16)", "Mob Massacre (17)", "Mob Massacre Result (18)", "Party Raid (19)", "Party Raid Boss (1A)", "Party Raid Result (1B)", "No Dragon (1C)", "Dynamic Foothold (1D)", "Escort (1E)", "Escort Result (1F)", "Hunting Ad Balloon (20)", "Chaos Zakum (21)", "Kill Count (22)", "Wedding (3C)", "Wedding Photo (3D)", "Fishing King (4A)", "Showa Bath (51h)", "Beginner Camp (52h)", "Snowman (1000d)", "Showa Spa (1001d)", "Horntail PQ (1013d)", "Crimsonwood PQ (1014d)" })
                fieldType.Items.Add(type);

            timeLimitEnable.Tag = timeLimit;
            lvLimitEnable.Tag = lvLimit;
            lvForceMoveUse.Tag = lvForceMove;
            firstUserEnable.Tag = firstUserEnter;
            userEnterEnable.Tag = userEnter;
            fieldTypeEnable.Tag = fieldType;
            moveLimitEnable.Tag = moveLimit;
            mapNameEnable.Tag = mapName;
            mapDescEnable.Tag = mapDesc;
            streetNameEnable.Tag = streetNameBox;
            effectEnable.Tag = effectBox;
            dropExpireEnable.Tag = dropExpire;
            dropRateEnable.Tag = dropRate;
            recoveryEnable.Tag = recovery;
            reactorShuffle.Tag = new Control[] { reactorNameShuffle, reactorNameBox };
            reactorNameShuffle.Tag = reactorNameBox;
            fsEnable.Tag = fsBox;
            massEnable.Tag = new Control[] { createMobInterval, fixedMobCapacity };
            hpDecEnable.Tag = new Control[] { decHP, listBox_protectItem, decIntervalEnable, decInterval, protectEnable };
            protectEnable.Tag = new Control[] { listBox_protectItem };
            decIntervalEnable.Tag = decInterval;
            helpEnable.Tag = helpBox;
            timedMobEnable.Tag = new Control[] { timedMobEnd, timedMobStart };
            summonMobEnable.Tag = new Control[] { timedMobId, timedMobEnable, timedMobMessage };
            autoLieDetectorEnable.Tag = new Control[] { autoLieEnd, autoLieInterval, autoLieProp, autoLieStart };
            allowedItemsEnable.Tag = new Control[] { allowedItems, allowedItemsAdd, allowedItemsRemove };
            //mirror_Bottom_Enabled.Tag = mirror

            this.fieldType.SelectedIndex = 0;

            xBox.Value = board.MapSize.X;
            yBox.Value = board.MapSize.Y;

            List<string> sortedBGMs = new List<string>();
            foreach (KeyValuePair<string, WzInformationManager.BgmEntry> bgm in Program.InfoManager.BGMs)
                sortedBGMs.Add(bgm.Key);
            sortedBGMs.Sort();
            foreach (string bgm in sortedBGMs)
                bgmBox.Items.Add(bgm);
            bgmBox.SelectedItem = info.bgm;

            List<string> sortedMarks = new List<string>();
            foreach (KeyValuePair<string, Bitmap> mark in Program.InfoManager.MapMarks)
                sortedMarks.Add(mark.Key);
            sortedMarks.Sort();
            foreach (string mark in sortedMarks)
                markBox.Items.Add(mark);
            markBox.SelectedIndex = 0;

            switch (info.mapType)
            {
                case MapType.CashShopPreview:
                    IDLabel.Text = "CashShopPreview";
                    break;
                case MapType.MapLogin:
                    IDLabel.Text = "MapLogin";
                    break;
                case MapType.ITCPreview:
                    IDLabel.Text = "ITCPreview";
                    break;
                case MapType.RegularMap:
                    if (info.id == -1) IDLabel.Text = "";
                    else IDLabel.Text = info.id.ToString();
                    break;
            }
            nameBox.Text = info.strMapName;
            streetBox.Text = info.strStreetName;
            categoryBox.Text = info.strCategoryName;
            markBox.SelectedItem = info.mapMark;
            if (info.returnMap == info.id)
                cannotReturnCBX.IsChecked = true;
            else returnBox.Text = info.returnMap.ToString();
            if (info.forcedReturn == 999999999)
                returnHereCBX.IsChecked = true;
            else forcedRet.Text = info.forcedReturn.ToString();
            mobRate.Value = (decimal)info.mobRate;

            //LoadOptionalInt(info.link, linkBox);
            LoadOptionalInt(info.timeLimit, timeLimit, timeLimitEnable);
            LoadOptionalInt(info.lvLimit, lvLimit, lvLimitEnable);
            LoadOptionalInt(info.lvForceMove, lvForceMove, lvForceMoveUse);
            LoadOptionalString(info.onFirstUserEnter, firstUserEnter, firstUserEnable);
            LoadOptionalString(info.onUserEnter, userEnter, userEnterEnable);
            LoadOptionalString(info.mapName, mapName, mapNameEnable);
            LoadOptionalString(info.mapDesc, mapDesc, mapDescEnable);
            LoadOptionalString(info.streetName, streetNameBox, streetNameEnable);
            LoadOptionalString(info.effect, effectBox, effectEnable);
            LoadOptionalInt(info.moveLimit, moveLimit, moveLimitEnable);
            LoadOptionalInt(info.dropExpire, dropExpire, dropExpireEnable);
            LoadOptionalFloat(info.dropRate, dropRate, dropRateEnable);
            LoadOptionalFloat(info.recovery, recovery, recoveryEnable);
            reactorShuffle.IsChecked = info.reactorShuffle;
            LoadOptionalString(info.reactorShuffleName, reactorNameBox, reactorNameShuffle);
            LoadOptionalFloat(info.fs, fsBox, fsEnable);
            LoadOptionalInt(info.createMobInterval, createMobInterval, massEnable);
            LoadOptionalInt(info.fixedMobCapacity, fixedMobCapacity, massEnable);
            LoadOptionalInt(info.decHP, decHP, hpDecEnable);
            LoadOptionalInt(info.decInterval, decInterval, decIntervalEnable);
            LoadOptionalIntArray(info.protectItem, listBox_protectItem, protectEnable);

            // Help
            helpEnable.IsChecked = info.help != null;
            if (info.help != null)
                helpBox.Text = info.help.Replace(@"\n", "\r\n");

            // Time mobs
            if (info.timeMob != null)
            {
                TimeMob tMob = (TimeMob)info.timeMob;
                summonMobEnable.IsChecked = true;
                LoadOptionalInt(tMob.startHour, timedMobStart, timedMobEnable);
                LoadOptionalInt(tMob.endHour, timedMobEnd, timedMobEnable);
                timedMobId.Value = tMob.id;
                timedMobMessage.Text = tMob.message.Replace(@"\n", "\r\n");
            }

            // Lie detector
            if (info.autoLieDetector != null)
            {
                AutoLieDetector ald = (AutoLieDetector)info.autoLieDetector;
                autoLieDetectorEnable.IsChecked = true;
                autoLieStart.Value = ald.startHour;
                autoLieEnd.Value = ald.endHour;
                autoLieInterval.Value = ald.interval;
                autoLieProp.Value = ald.prop;
            }

            // Allowed item
            if (info.allowedItem != null)
            {
                allowedItemsEnable.IsChecked = true;
                foreach (int id in info.allowedItem)
                    allowedItems.Items.Add(id.ToString());
            }

            optionsList.SetChecked(0, info.cloud);
            optionsList.SetChecked(1, info.snow);
            optionsList.SetChecked(2, info.rain);
            optionsList.SetChecked(3, info.swim);
            optionsList.SetChecked(4, info.fly);
            optionsList.SetChecked(5, info.town);
            optionsList.SetChecked(6, info.partyOnly);
            optionsList.SetChecked(7, info.expeditionOnly);
            optionsList.SetChecked(8, info.noMapCmd);
            optionsList.SetChecked(9, info.hideMinimap);
            optionsList.SetChecked(10, info.miniMapOnOff);
            optionsList.SetChecked(11, info.personalShop);
            optionsList.SetChecked(12, info.entrustedShop);
            optionsList.SetChecked(13, info.noRegenMap);
            optionsList.SetChecked(14, info.blockPBossChange);
            optionsList.SetChecked(15, info.everlast);
            optionsList.SetChecked(16, info.damageCheckFree);
            optionsList.SetChecked(17, info.scrollDisable);
            optionsList.SetChecked(18, info.needSkillForFly);
            optionsList.SetChecked(19, info.zakum2Hack);
            optionsList.SetChecked(20, info.allMoveCheck);
            optionsList.SetChecked(21, info.VRLimit);
            optionsList.SetChecked(22, info.mirror_Bottom);

            // Populate field limit items
            // automatically populated via fieldLimitPanel1.Loaed
            fieldLimitPanel1.PopulateDefaultListView();
            fieldLimitPanel1.UpdateFieldLimitCheckboxes((ulong)info.fieldLimit);

            if (info.fieldType != null)/* fieldType.SelectedIndex = -1;
            else*/
            {
                fieldType.SelectedIndex = 0;
                if ((int)info.fieldType <= 0x22)
                    fieldType.SelectedIndex = (int)info.fieldType;
                else switch (info.fieldType)
                    {
                        case FieldType.FIELDTYPE_WEDDING:
                            fieldType.SelectedIndex = 0x23;
                            break;
                        case FieldType.FIELDTYPE_WEDDINGPHOTO:
                            fieldType.SelectedIndex = 0x24;
                            break;
                        case FieldType.FIELDTYPE_FISHINGKING:
                            fieldType.SelectedIndex = 0x25;
                            break;
                        case FieldType.FIELDTYPE_SHOWABATH:
                            fieldType.SelectedIndex = 0x26;
                            break;
                        case FieldType.FIELDTYPE_BEGINNERCAMP:
                            fieldType.SelectedIndex = 0x27;
                            break;
                        case FieldType.FIELDTYPE_SNOWMAN:
                            fieldType.SelectedIndex = 0x28;
                            break;
                        case FieldType.FIELDTYPE_SHOWASPA:
                            fieldType.SelectedIndex = 0x29;
                            break;
                        case FieldType.FIELDTYPE_HORNTAILPQ:
                            fieldType.SelectedIndex = 0x2A;
                            break;
                        case FieldType.FIELDTYPE_CRIMSONWOODPQ:
                            fieldType.SelectedIndex = 0x2B;
                            break;
                    }
            }
            foreach (WzImageProperty prop in info.additionalProps)
            {
                TreeViewItem node = new() { Header = prop.Name };
                unknownProps.Items.Add(node);
                node.Tag = prop;
                if (prop.WzProperties != null && prop.WzProperties.Count > 0)
                    ExtractPropList(prop.WzProperties, node);
            }
        }

        private void ExtractPropList(List<WzImageProperty> properties, TreeViewItem parent)
        {
            foreach (WzImageProperty prop in properties)
            {
                TreeViewItem node = new() { Header = prop.Name };
                parent.Items.Add(node);
                node.Tag = prop;
                if (prop.WzProperties != null && prop.WzProperties.Count > 0)
                    ExtractPropList(prop.WzProperties, node);
            }
        }

        /// <summary>
        /// Loads optional int into the NumericUpDown target
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="checkBox"></param>
        private void LoadOptionalInt(int? source, NumericUpDown target, CheckBox checkBox)
        {
            checkBox.IsChecked = source != null;
            if (source != null) target.Value = source.Value;
        }

        /// <summary>
        /// Loads an optional int array into a ComboBox
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="checkBox"></param>
        private void LoadOptionalIntArray(List<int> source, ComboBox target, CheckBox checkBox)
        {
            checkBox.IsChecked = source != null && source.Count > 0;
            if (checkBox.IsChecked == true)
            {
                foreach (int val in source)
                {
                    target.Items.Add(val.ToString());
                }
            }
        }

        /// <summary>
        /// Loads an optional int array into a ListBox
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="checkBox"></param>
        private void LoadOptionalIntArray(List<int> source, ListBox target, CheckBox checkBox)
        {
            checkBox.IsChecked = source != null && source.Count > 0;
            if (checkBox.IsChecked == true)
            {
                foreach (int val in source)
                {
                    target.Items.Add(val.ToString());
                }
            }
        }

        /// <summary>
        /// Gets an optional int from a NumericUpDown
        /// </summary>
        /// <param name="textbox"></param>
        /// <param name="checkBox"></param>
        /// <returns></returns>
        private int? GetOptionalInt(NumericUpDown textbox, CheckBox checkBox)
        {
            return checkBox.IsChecked == true ? (int?)textbox.Value : null;
        }

        /// <summary>
        /// Gets an optional int array from a ComboBox
        /// </summary>
        /// <param name="comboBox"></param>
        /// <param name="checkBox"></param>
        /// <returns>null if none is selected</returns>
        private List<int> GetOptionalIntArrayFromList(ComboBox comboBox, CheckBox checkBox)
        {
            List<int> ret = new List<int>();
            if (checkBox.IsChecked == true)
            {
                if (comboBox.Items.Count > 0)
                {
                    foreach (string itemId in comboBox.Items)
                    {
                        ret.Add(int.Parse(itemId));
                    }
                    return ret;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets an optional int array from a ListBox
        /// </summary>
        /// <param name="comboBox"></param>
        /// <param name="checkBox"></param>
        /// <returns>null if none is selected</returns>
        private List<int> GetOptionalIntArrayFromList(ListBox listBox, CheckBox checkBox)
        {
            List<int> ret = new List<int>();
            if (checkBox.IsChecked == true)
            {
                foreach (string itemId in listBox.Items)
                {
                    ret.Add(int.Parse(itemId));
                }
                return ret;
            }
            return null;
        }

        private void LoadOptionalFloat(float? source, NumericUpDown target, CheckBox checkBox)
        {
            checkBox.IsChecked = source != null;
            if (source != null) target.Value = (decimal)source.Value;
        }

        private float? GetOptionalFloat(NumericUpDown textbox, CheckBox checkBox)
        {
            return checkBox.IsChecked == true ? (float?)textbox.Value : null;
        }

        private void LoadOptionalString(string source, TextBox target, CheckBox checkBox)
        {
            checkBox.IsChecked = source != null;
            if (source != null) target.Text = source;
        }

        private string GetOptionalString(TextBox textbox, CheckBox checkBox)
        {
            return checkBox.IsChecked == true ? textbox.Text : null;
        }

        private void bgmBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            string bgm = (string)bgmBox.SelectedItem;
            WzBinaryProperty soundProperty = Program.InfoManager.GetBgm(bgm);
            if (soundProperty != null)
            {
                soundPlayer1.SoundProperty = soundProperty;
            } else
            {
                string error = string.Format("[InfoEditor] Missing BGM [{0}] in Sound.wz for the Field ID [{1}]", bgm, this.info.id);
                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
            }
        }

        /// <summary>
        /// On add protect item Id
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addProtectItem_Click(object sender, RoutedEventArgs e)
        {
            string name;
            int? value;

            if (!IntInputBox.Show(EditorPanels.EditorPanelLocalizer.Text("Prompt_EnterItemId", "Enter item ID"), "0", 0, out name, out value, true))
            {
                return;
            }
            if (value == 0)
            {
                MessageBox.Show(EditorPanels.EditorPanelLocalizer.Text("Error_ValueNotZero", "Value must not be 0."));
                return;
            }
            listBox_protectItem.Items.Add(value.ToString());
        }

        /// <summary>
        /// Remove protect item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeProtectItem_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_protectItem.SelectedIndex != -1)
                listBox_protectItem.Items.RemoveAt(listBox_protectItem.SelectedIndex);
        }

        private void InfoEditor_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            soundPlayer1.SoundProperty = null;
        }

        private void markBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            markImage.Source = ToBitmapSource(Program.InfoManager.MapMarks[(string)markBox.SelectedItem]);
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            lock (multiBoard)
            {
                if (info.mapType != MapType.CashShopPreview && info.mapType != MapType.ITCPreview)
                {
                    info.bgm = (string)bgmBox.SelectedItem;
                    info.mapMark = (string)markBox.SelectedItem;
                }
                if (info.mapType == MapType.RegularMap)
                {
                    // No need to change String.wz here, we will do that upon saving
                    info.strMapName = nameBox.Text;
                    info.strStreetName = streetBox.Text;
                    info.strCategoryName = categoryBox.Text;

                    // We do, however, need to change the tab's name/info
                    ((TabItemContainer)tabItem.Tag).Text = info.strMapName;

                    tabItem.Header = MapLoader.GetFormattedMapNameForTabItem(info.id, info.strStreetName, info.strMapName);
                }
                info.returnMap = cannotReturnCBX.IsChecked == true ? info.id : (int)returnBox.Value;
                info.forcedReturn = returnHereCBX.IsChecked == true ? 999999999 : (int)forcedRet.Value;
                info.mobRate = (float)mobRate.Value;
                info.timeLimit = GetOptionalInt(timeLimit, timeLimitEnable);
                info.lvLimit = GetOptionalInt(lvLimit, lvLimitEnable);
                info.lvForceMove = GetOptionalInt(lvForceMove, lvForceMoveUse);
                info.onFirstUserEnter = GetOptionalString(firstUserEnter, firstUserEnable);
                info.onUserEnter = GetOptionalString(userEnter, userEnterEnable);
                info.mapName = GetOptionalString(mapName, mapNameEnable);
                info.mapDesc = GetOptionalString(mapDesc, mapDescEnable);
                info.streetName = GetOptionalString(streetNameBox, streetNameEnable);
                info.effect = GetOptionalString(effectBox, effectEnable);
                info.moveLimit = GetOptionalInt(moveLimit, moveLimitEnable);
                info.dropExpire = GetOptionalInt(dropExpire, dropExpireEnable);
                info.dropRate = GetOptionalFloat(dropRate, dropRateEnable);
                info.recovery = GetOptionalFloat(recovery, recoveryEnable);
                info.reactorShuffle = reactorShuffle.IsChecked == true;
                info.reactorShuffleName = GetOptionalString(reactorNameBox, reactorNameShuffle);
                info.fs = GetOptionalFloat(fsBox, fsEnable);
                info.createMobInterval = GetOptionalInt(createMobInterval, massEnable);
                info.fixedMobCapacity = GetOptionalInt(fixedMobCapacity, massEnable);
                info.decHP = GetOptionalInt(decHP, hpDecEnable);
                info.decInterval = GetOptionalInt(decInterval, decIntervalEnable);
                info.protectItem = GetOptionalIntArrayFromList(listBox_protectItem, protectEnable);

                if (helpEnable.IsChecked == true) info.help = helpBox.Text.Replace("\r\n", @"\n");
                if (summonMobEnable.IsChecked == true)
                    info.timeMob = new TimeMob(
                        GetOptionalInt(timedMobStart, timedMobEnable),
                        GetOptionalInt(timedMobEnd, timedMobEnable),
                        (int)timedMobId.Value,
                        timedMobMessage.Text.Replace("\r\n", @"\n"));
                if (autoLieDetectorEnable.IsChecked == true)
                    info.autoLieDetector = new AutoLieDetector(
                        (int)autoLieStart.Value,
                        (int)autoLieEnd.Value,
                        (int)autoLieInterval.Value,
                        (int)autoLieProp.Value);
                if (allowedItemsEnable.IsChecked == true)
                {
                    info.allowedItem = new List<int>();
                    foreach (string id in allowedItems.Items)
                        info.allowedItem.Add(int.Parse(id));
                }
                info.cloud = optionsList.Checked(0);
                info.snow = optionsList.Checked(1);
                info.rain = optionsList.Checked(2);
                info.swim = optionsList.Checked(3);
                info.fly = optionsList.Checked(4);
                info.town = optionsList.Checked(5);
                info.partyOnly = optionsList.Checked(6);
                info.expeditionOnly = optionsList.Checked(7);
                info.noMapCmd = optionsList.Checked(8);
                info.hideMinimap = optionsList.Checked(9);
                info.miniMapOnOff = optionsList.Checked(10);
                info.personalShop = optionsList.Checked(11);
                info.entrustedShop = optionsList.Checked(12);
                info.noRegenMap = optionsList.Checked(13);
                info.blockPBossChange = optionsList.Checked(14);
                info.everlast = optionsList.Checked(15);
                info.damageCheckFree = optionsList.Checked(16);
                info.scrollDisable = optionsList.Checked(17);
                info.needSkillForFly = optionsList.Checked(18);
                info.zakum2Hack = optionsList.Checked(19);
                info.allMoveCheck = optionsList.Checked(20);
                info.VRLimit = optionsList.Checked(21);
                info.mirror_Bottom = optionsList.Checked(22);

                info.fieldLimit = (long)fieldLimitPanel1.FieldLimit;

                if (fieldType.SelectedIndex <= 0x22)
                    info.fieldType = (FieldType)fieldType.SelectedIndex;
                else
                {
                    switch (fieldType.SelectedIndex)
                    {
                        case 0x23:
                            info.fieldType = FieldType.FIELDTYPE_WEDDING;
                            break;
                        case 0x24:
                            info.fieldType = FieldType.FIELDTYPE_WEDDINGPHOTO;
                            break;
                        case 0x25:
                            info.fieldType = FieldType.FIELDTYPE_FISHINGKING;
                            break;
                        case 0x26:
                            info.fieldType = FieldType.FIELDTYPE_SHOWABATH;
                            break;
                        case 0x27:
                            info.fieldType = FieldType.FIELDTYPE_BEGINNERCAMP;
                            break;
                        case 0x28:
                            info.fieldType = FieldType.FIELDTYPE_SNOWMAN;
                            break;
                        case 0x29:
                            info.fieldType = FieldType.FIELDTYPE_SHOWASPA;
                            break;
                        case 0x2A:
                            info.fieldType = FieldType.FIELDTYPE_HORNTAILPQ;
                            break;
                        case 0x2B:
                            info.fieldType = FieldType.FIELDTYPE_CRIMSONWOODPQ;
                            break;
                    }
                }

                if (board.MapSize.X != (int)xBox.Value || board.MapSize.Y != (int)yBox.Value)
                {
                    board.MapSize = new Microsoft.Xna.Framework.Point((int)xBox.Value, (int)yBox.Value);
                }
            }
            Close();
        }

        private void enablingCheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            CheckBox cbx = (CheckBox)sender;
            bool featureActivated = cbx.IsChecked == true && cbx.IsEnabled;
            if (cbx.Tag is Control)
                ((Control)cbx.Tag).IsEnabled = featureActivated;
            else
            {
                foreach (Control control in (Control[])cbx.Tag)
                    control.IsEnabled = featureActivated;
                foreach (Control control in (Control[])cbx.Tag) if (control is CheckBox)
                        enablingCheckBox_CheckChanged(control, e);
            }
        }

        private int lastret = 0;
        private int lastforcedret = 0;

        private void cannotReturnCBX_CheckedChanged(object sender, RoutedEventArgs e)
        {
            returnBox.IsEnabled = cannotReturnCBX.IsChecked != true;
            if (cannotReturnCBX.IsChecked == true) { lastret = (int)returnBox.Value; returnBox.Value = info.id; }
            else returnBox.Value = lastret;
        }

        private void returnHereCBX_CheckedChanged(object sender, RoutedEventArgs e)
        {
            forcedRet.IsEnabled = returnHereCBX.IsChecked != true;
            if (returnHereCBX.IsChecked == true) { lastforcedret = (int)forcedRet.Value; forcedRet.Value = 999999999; }
            else forcedRet.Value = lastforcedret;
        }

        private void allowedItemsRemove_Click(object sender, RoutedEventArgs e)
        {
            if (allowedItems.SelectedIndex != -1)
                allowedItems.Items.RemoveAt(allowedItems.SelectedIndex);
        }

        private void allowedItemsAdd_Click(object sender, RoutedEventArgs e)
        {
            allowedItems.Items.Add(Microsoft.VisualBasic.Interaction.InputBox(
                EditorPanels.EditorPanelLocalizer.Text("Prompt_InsertItemId", "Insert item ID"),
                EditorPanels.EditorPanelLocalizer.Text("Title_AddAllowedItem", "Add Allowed Item"), "", -1, -1));
        }

        /// <summary>
        /// Select field ID for return map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_selectReturnMap_Click(object sender, RoutedEventArgs e)
        {
            using var adapter = new System.Windows.Forms.NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = returnBox.Value };
            LoadMapSelector selector = new LoadMapSelector(adapter);
            if (selector.ShowDialog() == true) returnBox.Value = adapter.Value;
        }

        /// <summary>
        /// Select field ID for forced return map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_selectForcedReturnMap_Click(object sender, RoutedEventArgs e)
        {
            using var adapter = new System.Windows.Forms.NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Value = forcedRet.Value };
            LoadMapSelector selector = new LoadMapSelector(adapter);
            if (selector.ShowDialog() == true) forcedRet.Value = adapter.Value;
        }

        private void CancelButton_WpfClick(object sender, RoutedEventArgs e) => cancelButton_Click(sender, EventArgs.Empty);

        private void OkButton_WpfClick(object sender, RoutedEventArgs e) => okButton_Click(sender, EventArgs.Empty);

        private static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = stream;
            source.EndInit();
            source.Freeze();
            return source;
        }
    }
}
