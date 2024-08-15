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

using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HaCreator.GUI
{
    /// <summary>
    /// Interaction logic for QuestEditor.xaml
    /// </summary>
    public partial class QuestEditor : Window, INotifyPropertyChanged
    {
        // etc
        private bool _isLoading = false;
        private bool _unsavedChanges = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditor()
        {
            InitializeComponent();

            
            _isLoading = true;
            try
            {
                DataContext = this;

                LoadQuestsData();
            } finally
            {
                _isLoading = false;
            }
        }

        #region Binding datas
        private QuestEditorModel _selectedQuest;
        public QuestEditorModel SelectedQuest
        {
            get => _selectedQuest;
            set
            {
                _selectedQuest = value;
                OnPropertyChanged(nameof(SelectedQuest));
            }
        }

        private ObservableCollection<QuestEditorModel> _quests = new ObservableCollection<QuestEditorModel>();
        public ObservableCollection<QuestEditorModel> Quests
        {
            get { return _quests; }
            set
            {
                this._quests = value;
                OnPropertyChanged(nameof(Quests));
            }
        }
        private ObservableCollection<QuestEditorModel> _filteredQuests = new ObservableCollection<QuestEditorModel>();
        public ObservableCollection<QuestEditorModel> FilteredQuests
        {
            get { return _filteredQuests; }
            set
            {
                this._filteredQuests = value;
                OnPropertyChanged(nameof(FilteredQuests));
            }
        }
        #endregion

        #region Overrides
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_unsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save the Quest.wz file before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        Repack r = new Repack();
                        r.ShowDialog();
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        break;
                    case MessageBoxResult.No:
                        break;
                }
            }

            base.OnClosing(e);
        }
        #endregion


        /// <summary>
        /// Data from Quest.wz
        /// </summary>
        private void LoadQuestsData()
        {
            foreach (KeyValuePair<string, WzSubProperty> kvp in Program.InfoManager.QuestInfos)
            {
                string key = kvp.Key;
                WzSubProperty questProp = kvp.Value;

#if DEBUG
                foreach (WzImageProperty questImgProp in questProp.WzProperties)
                {
                    switch (questImgProp.Name)
                    {
                        case "name":
                        case "0":
                        case "1":
                        case "2":
                        case "parent":
                        case "area":
                        case "order":
                        case "blocked":
                        case "autoStart":
                        case "autoPreComplete":
                        case "autoComplete":
                        case "selectedMob":
                        case "autoCancel":
                        case "disableAtStartTab":
                        case "disableAtPerformTab":
                        case "disableAtCompleteTab":
                        case "demandSummary":
                        case "rewardSummary":
                        case "showLayerTag":
                        case "oneShot":
                        case "summary":
                            break;
                        default:
                            string error = string.Format("Unhandled quest image property. Name='{0}', QuestId={1}", questImgProp.Name, kvp.Key);
                            ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                            break;
                    }
                }
#endif

                // Quest name
                string questName = (questProp["name"] as WzStringProperty)?.Value;

                QuestEditorModel quest = new QuestEditorModel { 
                    Id = int.Parse(key), 
                    Name = questName == null ? string.Empty: questName,
                };

                // parse quest desc
                quest.QuestInfoDesc0 = (questProp["0"] as WzStringProperty)?.Value ?? string.Empty;
                quest.QuestInfoDesc1 = (questProp["1"] as WzStringProperty)?.Value ?? string.Empty;
                quest.QuestInfoDesc2 = (questProp["2"] as WzStringProperty)?.Value ?? string.Empty;

                // parent
                quest.Parent = (questProp["parent"] as WzStringProperty)?.Value;

                // area, order
                quest.Area = (questProp["area"] as WzIntProperty)?.Value ?? 0;
                quest.Order = (questProp["order"] as WzIntProperty)?.Value ?? 0;

                // parse autoStart, autoPreComplete
                quest.Blocked = (questProp["blocked"] as WzIntProperty)?.Value > 0;
                quest.AutoStart = (questProp["autoStart"] as WzIntProperty)?.Value > 0;
                quest.AutoPreComplete = (questProp["autoPreComplete"] as WzIntProperty)?.Value > 0;
                quest.AutoComplete = (questProp["autoComplete"] as WzIntProperty)?.Value > 0;
                quest.SelectedMob = (questProp["selectedMob"] as WzIntProperty)?.Value > 0;
                quest.AutoCancel = (questProp["autoCancel"] as WzIntProperty)?.Value > 0;
                quest.OneShot = (questProp["oneShot"] as WzIntProperty)?.Value > 0;

                quest.DisableAtStartTab = (questProp["disableAtStartTab"] as WzIntProperty)?.Value > 0;
                quest.DisableAtPerformTab = (questProp["disableAtPerformTab"] as WzIntProperty)?.Value > 0;
                quest.DisableAtCompleteTab = (questProp["disableAtCompleteTab"] as WzIntProperty)?.Value > 0;

                // demand summary, reward summary
                quest.Summary = (questProp["summary"] as WzStringProperty)?.Value;
                quest.DemandSummary = (questProp["demandSummary"] as WzStringProperty)?.Value;
                quest.RewardSummary = (questProp["rewardSummary"] as WzStringProperty)?.Value;

                // misc properties
                quest.ShowLayerTag = (questProp["showLayerTag"] as WzStringProperty)?.Value;

                // Parse quest Say.img
                // the NPC conversations
                if (Program.InfoManager.QuestSays.ContainsKey(key)) // sometimes it does not exist in the Quest.wz/Say.img
                {
                    WzSubProperty questSayProp = Program.InfoManager.QuestSays[key];

                    WzSubProperty questSayStart0Prop = (WzSubProperty)questSayProp["0"];
                    WzSubProperty questSayEnd0Prop = (WzSubProperty)questSayProp["1"];

                    if (questSayStart0Prop != null)
                    {
                        var loadedModels = parseQuestSayConversations(questSayStart0Prop, quest);
                        foreach (QuestEditorSayModel sayModel in loadedModels)
                        {
                            quest.SayInfoStartQuest.Add(sayModel);
                        }
                    }
                    if (questSayEnd0Prop != null)
                    {
                        var loadedModels = parseQuestSayConversations(questSayEnd0Prop, quest);
                        foreach (QuestEditorSayModel sayModel in loadedModels)
                        {
                            quest.SayInfoEndQuest.Add(sayModel);
                        }
                    }
                } else
                {
                    // add empty placeholders
                }

                // add
                Quests.Add(quest);
            }
            FilteredQuests = Quests;

            if (Quests.Count > 0)
            {
                SelectedQuest = Quests[0];
            }

            /*var quest1000 = new QuestEditorModel
            {
                Id = 1000,
                Name = "Borrowing Sera's Mirror",
                Area = 20,
                Parent = "Sera's Mirror",
                Blocked = true,
                Order = 1,
                AutoStart = true,
                AutoPreComplete = false,
            };
            quest1000.QuestInfoDesc.Add("Let's go to Heena.");
            quest1000.QuestInfoDesc.Add("I ran into Heena who was worrying about her face getting irritated by the strong sunlight. I have to get a mirror for Heena from her sister, Sarah.");
            quest1000.QuestInfoDesc.Add("Heena asked me to go to her sister and get a mirror for her. I walked my way to Sarah.");

            quest1000.SayInfo.Add(new QuestEditorSayModel
            {
                Type = "YesNo",
                Messages = new ObservableCollection<string>
                {
                    "You must be the new traveler. Still foreign to this, huh? I'll be giving you important information here and there so please listen carefully and follow along. First if you want to talk to us, #bdouble-click#k us with the mouse.",
                    "#bLeft, right arrow#k will allow you to move. Press #bSpace Bar#k to jump. Jump diagonally by combining it with the directional cursors. Try it later.",
                    "Man... the sun is literally burning my beautiful skin! It's a scorching day today. Can I ask you for a favor? Can you get me a #bmirror#k from #r#p2100##k, please?"
                },
                Yes = "Thank you... #r#p2100##k will be on the hill down on the east side hanging up the laundry. The mirror looks like this #i4031003#.",
                No = "Don't want to? Hmmm... come back when you change your mind.",
                Stop = "Haven't met #r#p2100##k yet? She should be on a hill down on east side...it's pretty close from here so it will be easy to spot her..."
            });
            quest1000.CheckInfo.Add(new QuestEditorCheckInfoModel());
            quest1000.ActInfo.Add(new QuestEditorActInfoModel());

            Quests.Add(quest1000);

            // other quests
            Quests.Add(new QuestEditorModel { Id = 10000, Name = "A Strange Offer?!", Area = 50 });
            Quests.Add(new QuestEditorModel { Id = 10001, Name = "Co-op with Special Agent O", Area = 50 });
            Quests.Add(new QuestEditorModel { Id = 10002, Name = "Retrieve Special Agent Badge", Area = 50 });
            
            SelectedQuest = quest1000;*/
        }


        private ObservableCollection<QuestEditorSayModel> parseQuestSayConversations(WzSubProperty questSayStart0Prop, QuestEditorModel quest)
        {
            var sayInfo = new ObservableCollection<QuestEditorSayModel>();
            QuestEditorSayModel questEditorSayModel = null;

            for (int z = 0; z < questSayStart0Prop.WzProperties.Count; z++)
            { // has to be by order
                WzImageProperty questConvProp = questSayStart0Prop.WzProperties[z];

                int questConvName;
                if (int.TryParse(questConvProp.Name, out questConvName) && questConvName < 200) // is conversation property "0" "1" "2" "3"
                {
                    questEditorSayModel = new QuestEditorSayModel();
                    questEditorSayModel.NpcConversation = (questConvProp as WzStringProperty).Value;

                    sayInfo.Add(questEditorSayModel);
                }
                else
                {
                    if (questConvProp.Name == "yes" || questConvProp.Name == "no") // is "yes" "no" property
                    {
                        if (questEditorSayModel == null)
                            continue; // wz formatting error
                        if (questConvProp.Name == "yes")
                        {
                            int a = 0;
                            WzStringProperty textProp;
                            while ((textProp = questConvProp[a.ToString()] as WzStringProperty) != null)
                            {
                                questEditorSayModel.YesResponses.Add(new QuestEditorSayResponseModel() { Text = textProp.Value });
                                a++;
                            }
                        }
                        else
                        {
                            int a = 0;
                            WzStringProperty textProp;
                            while ((textProp = questConvProp[a.ToString()] as WzStringProperty) != null)
                            {
                                questEditorSayModel.NoResponses.Add(new QuestEditorSayResponseModel() { Text = textProp.Value });
                                a++;
                            }
                        }
                    }
                    else if (questConvProp.Name == "stop")
                    {

                    }
                }
            }
            return sayInfo;
        }
        #region QuestInfo

        #endregion

        #region Quest Tabs
        /// <summary>
        /// Adds a new quest
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addNewQuest_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// On quest selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuestListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || e.AddedItems.Count <= 0)
            {
                return;
            }
            // TODO: detect unsaved quest

            SelectedQuest = e.AddedItems[0] as QuestEditorModel;
        }

        /// <summary>
        /// Searchbox on text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Create a temporary list first
            ObservableCollection<QuestEditorModel> tempFilteredQuests = new ObservableCollection<QuestEditorModel>();
            string searchTerm = searchBox.Text.ToLower();

            foreach (var quest in Quests)
            {
                if (quest.Name.ToLower().Contains(searchTerm) || quest.Id.ToString().Contains(searchTerm))
                {
                    tempFilteredQuests.Add(quest);
                }
            }
            // Replace the main list
            FilteredQuests = tempFilteredQuests;
        }
        #endregion

        #region Quest QuestInfo.img
        #endregion


        #region Quest Say.img
        /// <summary>
        /// Add a new 'yes' conversation response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addYesResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataContext = button.DataContext as QuestEditorSayModel;
            if (dataContext != null)
            {
                dataContext.YesResponses.Add(new QuestEditorSayResponseModel() { Text = "Add some text here." });
            }
        }

        /// <summary>
        /// Add a new no conversation response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addNoResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataContext = button.DataContext as QuestEditorSayModel;
            if (dataContext != null)
            {
                dataContext.NoResponses.Add(new QuestEditorSayResponseModel() { Text = "Add some text here." });
            }
        }

        /// <summary>
        /// Remove yes conversation response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteYesResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var response = button.DataContext as QuestEditorSayResponseModel;
            var questModel = FindAncestor<DataGridCell>(button)?.DataContext as QuestEditorSayModel;

            if (response != null && questModel != null)
            {
                questModel.YesResponses.Remove(response);
            }
        }

        /// <summary>
        /// Remove no conversation response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteNoResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var response = button.DataContext as QuestEditorSayResponseModel;
            var questModel = FindAncestor<DataGridCell>(button)?.DataContext as QuestEditorSayModel;

            if (response != null && questModel != null)
            {
                questModel.NoResponses.Remove(response);
            }
        }

        // Helper method to find ancestor of a specific type
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
        #endregion

        #region Save Delete quest
        /// <summary>
        /// Saves the quest to WZ images
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_saveQuest_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedQuest == null)
                return;

            QuestEditorModel quest = _selectedQuest;
            WzSubProperty questWzSubProp = new WzSubProperty(quest.Id.ToString());
            WzSubProperty questWzSubProperty_original = Program.InfoManager.QuestInfos[quest.Id.ToString()];

            // Save QuestInfo.img
            if (questWzSubProperty_original != null)
            {
                questWzSubProp.AddProperty(new WzStringProperty("name", quest.Name));

                if (quest.QuestInfoDesc0 != null && quest.QuestInfoDesc0 != string.Empty)
                    questWzSubProp.AddProperty(new WzStringProperty("0", quest.QuestInfoDesc0));
                if (quest.QuestInfoDesc1 != null && quest.QuestInfoDesc1 != string.Empty)
                    questWzSubProp.AddProperty(new WzStringProperty("1", quest.QuestInfoDesc1));
                if (quest.QuestInfoDesc2 != null && quest.QuestInfoDesc2 != string.Empty)
                    questWzSubProp.AddProperty(new WzStringProperty("2", quest.QuestInfoDesc2));


                // parent
                if (quest.Parent != null && quest.Parent != string.Empty)
                {
                    questWzSubProp.AddProperty(new WzStringProperty("parent", quest.Parent));
                }

                // area, order
                if (quest.Area != 0)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("area", quest.Area));
                }
                if (quest.Order != 0)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("order", quest.Order));
                }

                // autoStart, autoComplete, autoPreComplete
                if (quest.Blocked == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("blocked", 1));
                }
                if (quest.AutoStart == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("autoStart", 1));
                }
                if (quest.AutoPreComplete == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("autoPreComplete", 1));
                }
                if (quest.AutoComplete == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("autoComplete", 1));
                }
                if (quest.SelectedMob == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("selectedMob", 1));
                }
                if (quest.AutoCancel == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("autoCancel", 1));
                }
                if (quest.OneShot == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("oneShot", 1));
                }

                if (quest.DisableAtStartTab == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("disableAtStartTab", 1));
                }
                if (quest.DisableAtPerformTab == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("disableAtPerformTab", 1));
                }
                if (quest.DisableAtCompleteTab == true)
                {
                    questWzSubProp.AddProperty(new WzIntProperty("disableAtCompleteTab", 1));
                }

                // summary, demand summary, reward summary
                if (quest.Summary != null && quest.Summary != string.Empty)
                {
                    questWzSubProp.AddProperty(new WzStringProperty("summary", quest.Summary));
                }
                if (quest.DemandSummary != null && quest.DemandSummary != string.Empty)
                {
                    questWzSubProp.AddProperty(new WzStringProperty("demandSummary", quest.DemandSummary));
                }
                if (quest.RewardSummary != null && quest.RewardSummary != string.Empty)
                {
                    questWzSubProp.AddProperty(new WzStringProperty("rewardSummary", quest.RewardSummary));
                }

                // misc properties
                if (quest.ShowLayerTag != null && quest.ShowLayerTag != string.Empty)
                {
                    questWzSubProp.AddProperty(new WzStringProperty("showLayerTag", quest.ShowLayerTag));
                }

                // remove the original image
                WzImage questInfoParentImg = questWzSubProperty_original.Parent as WzImage;

                // remove previous quest wzImage
                if (questInfoParentImg[questWzSubProperty_original.ToString()] != null)
                    questWzSubProperty_original.Remove();

                // replace the old 
                Program.InfoManager.QuestInfos[quest.Id.ToString()] = questWzSubProp;

                // add back the newly created image
                questInfoParentImg.AddProperty(questWzSubProp);

                // flag unsaved changes bool
                _unsavedChanges = true;
                Program.WzManager.SetWzFileUpdated(questInfoParentImg.GetTopMostWzDirectory().Name /* "map" */, questInfoParentImg);
            }

            ///////////////////
            ////// Save Say.img
            ///////////////////
            WzSubProperty newSayWzProp = new WzSubProperty(quest.Id.ToString());
            WzSubProperty oldSayWzProp = Program.InfoManager.QuestSays.ContainsKey(quest.Id.ToString()) ? Program.InfoManager.QuestSays[quest.Id.ToString()] : null;

            // start quest
            WzSubProperty startQuestSubProperty = new WzSubProperty("0");
            WzSubProperty endQuestSubProperty = new WzSubProperty("1");

            newSayWzProp.AddProperty(startQuestSubProperty);
            newSayWzProp.AddProperty(endQuestSubProperty);

            saveQuestSayConversation(quest.SayInfoStartQuest, startQuestSubProperty);
            saveQuestSayConversation(quest.SayInfoEndQuest, endQuestSubProperty);

            // end quest
            // TODO

            // remove previous quest say wzImage
            WzImage questSayParentImg = oldSayWzProp.Parent as WzImage; // TODO: this may be null, need to track reference of Say.img parent somewhere
            if (oldSayWzProp != null)
                oldSayWzProp.Remove();

            questSayParentImg.AddProperty(newSayWzProp); // add new to the parent

            // replace the old 
            Program.InfoManager.QuestSays[quest.Id.ToString()] = newSayWzProp;

            // flag unsaved changes bool
            _unsavedChanges = true;
            Program.WzManager.SetWzFileUpdated(questSayParentImg.GetTopMostWzDirectory().Name /* "map" */, questSayParentImg);

        }

        private void saveQuestSayConversation(ObservableCollection<QuestEditorSayModel> questSayItems, WzSubProperty questSaySubProperty)
        {
            int i = 0;
            foreach (QuestEditorSayModel sayModel in questSayItems)
            {
                // the main conversation
                questSaySubProperty.AddProperty(new WzStringProperty(i.ToString(), sayModel.NpcConversation));

                // yes/ no if any
                if (sayModel.YesResponses.Count > 0)
                {
                    WzSubProperty yesResponseSubWzProp = new WzSubProperty("yes");
                    int z = 0;
                    foreach (QuestEditorSayResponseModel sayRespModel in sayModel.YesResponses)
                    {
                        yesResponseSubWzProp.AddProperty(new WzStringProperty(z.ToString(), sayRespModel.Text));
                        z++;
                    }
                    questSaySubProperty.AddProperty(yesResponseSubWzProp);
                }
                if (sayModel.NoResponses.Count > 0)
                {
                    WzSubProperty noResponseSubWzProp = new WzSubProperty("no");
                    int z = 0;
                    foreach (QuestEditorSayResponseModel sayRespModel in sayModel.NoResponses)
                    {
                        noResponseSubWzProp.AddProperty(new WzStringProperty(z.ToString(), sayRespModel.Text));
                        z++;
                    }
                    questSaySubProperty.AddProperty(noResponseSubWzProp);
                }
                i++;
            }
        }

        /// <summary>
        /// Delete this selected quest
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_deleteQuest_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedQuest == null)
                return;

            QuestEditorModel quest = _selectedQuest;
            WzSubProperty questWzSubProperty = Program.InfoManager.QuestInfos[quest.Id.ToString()];


            // remove it off local collections
            Quests.Remove(_selectedQuest);
            FilteredQuests.Remove(_selectedQuest);

            // remove it off WzDirectory in the WZ
            WzImage questInfoParentImg = questWzSubProperty.Parent as WzImage;
            questWzSubProperty.Remove();


            // flag unsaved changes bool
            _unsavedChanges = true;
            Program.WzManager.SetWzFileUpdated(questInfoParentImg.GetTopMostWzDirectory().Name /* "map" */, questInfoParentImg);
        }
        #endregion

        #region Property Changed Event

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// OnPropertyChanged
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
