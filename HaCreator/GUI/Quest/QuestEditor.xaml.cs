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

using HaCreator.GUI.InstanceEditor;
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

namespace HaCreator.GUI.Quest
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
            }
            finally
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

        #region Loader
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
                            string error = string.Format("[QuestEditor] Unhandled quest image property. Name='{0}', QuestId={1}", questImgProp.Name, kvp.Key);
                            ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                            break;
                    }
                }
#endif

                // Quest name
                string questName = (questProp["name"] as WzStringProperty)?.Value;

                QuestEditorModel quest = new QuestEditorModel
                {
                    Id = int.Parse(key),
                    Name = questName == null ? string.Empty : questName,
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
                        foreach (QuestEditorSayModel sayModel in loadedModels.Item1)
                        {
                            quest.SayInfoStartQuest.Add(sayModel);
                        }
                        foreach (QuestEditorSayEndQuestModel sayStopModel in loadedModels.Item2)
                        {
                            quest.SayInfoStop_StartQuest.Add(sayStopModel);
                        }
                    }
                    if (questSayEnd0Prop != null)
                    {
                        var loadedModels = parseQuestSayConversations(questSayEnd0Prop, quest);
                        foreach (QuestEditorSayModel sayModel in loadedModels.Item1)
                        {
                            quest.SayInfoEndQuest.Add(sayModel);
                        }
                        foreach (QuestEditorSayEndQuestModel sayStopModel in loadedModels.Item2)
                        {
                            quest.SayInfoStop_EndQuest.Add(sayStopModel);
                        }
                    }

                    /*QuestEditorSayEndQuestModel test1 = new QuestEditorSayEndQuestModel()
                    {
                        ConversationType = QuestEditorStopConversationType.Default
                    };
                    test1.Responses.Add(new QuestEditorSayResponseModel() { Text = "You haven&apos;t collected 9 #b#t3994199#s#k yet." });
                    test1.Responses.Add(new QuestEditorSayResponseModel() { Text = "Now #b go back to check the wreckage of the carriage, There&apos;s bound to have some clue out there." });
                    quest.SayInfoStopQuest.Add(test1);

                    QuestEditorSayEndQuestModel test2 = new QuestEditorSayEndQuestModel()
                    {
                        ConversationType = QuestEditorStopConversationType.Item
                    };
                    test2.Responses.Add(new QuestEditorSayResponseModel() { Text = "You haven&apos;t collected 9 #b#t3994199#s#k yet." });
                    test2.Responses.Add(new QuestEditorSayResponseModel() { Text = "Now #b go back to check the wreckage of the carriage, There&apos;s bound to have some clue out there." });
                    quest.SayInfoStopQuest.Add(test2);*/
                }
                else
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


        /// <summary>
        /// Parses quest say, and say stop conversations into a list.
        /// </summary>
        /// <param name="questSayStart0Prop"></param>
        /// <param name="quest"></param>
        /// <returns></returns>
        private Tuple<
            ObservableCollection<QuestEditorSayModel>, 
            ObservableCollection<QuestEditorSayEndQuestModel>> parseQuestSayConversations(WzSubProperty questSayStart0Prop, QuestEditorModel quest)
        {
            var sayInfo = new ObservableCollection<QuestEditorSayModel>();

            var sayStop = new ObservableCollection<QuestEditorSayEndQuestModel>();

            QuestEditorSayModel questEditorSayModel = null;

            for (int z = 0; z < questSayStart0Prop.WzProperties.Count; z++) // this has to be parsed by its order!! whatever comes first parses first
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
                        else if (questConvProp.Name == "no")
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
                    else if (questConvProp.Name == "ask")
                    {
                        if (questEditorSayModel == null)
                            continue; // wz formatting error

                        quest.IsAskConversation = (questConvProp as WzIntProperty).Value > 0;
                    }
                    else if (questConvProp.Name == "lost") // lost quest item
                    {
                        /*
                         * <imgdir name="lost">
                         * <string name="0" value="Oh no... you lost the letter? Well, it&apos;s not hard for me to write another on, though. Here it is, and please give this to #b#p2101001##k."/>
                         * <imgdir name="yes">
                         * </imgdir>
                         * </imgdir>
                         */
                    }
                    else if (questConvProp.Name == "stop") // | stop is the options for ask.
                    {
                        // TODO
                        foreach (WzImageProperty questStopProp in questConvProp.WzProperties)
                        {
                            if (questStopProp.Name == "item" || // if not enough item
                                    questStopProp.Name == "mob" || questStopProp.Name == "monster" || // if the hunt amount have not reached threshold
                                    questStopProp.Name == "npc" || // if npc is not in the map, or the user talks to the NPC that issued the quest and not the one required to complete it.
                                    questStopProp.Name == "quest" || // if quest pre-requisite requirement has not reached 
                                    questStopProp.Name == "default" || // 'everything else chat' if any of the  pre-requisite requirement has not reached [i.e not enough ETC slot]
                                    questStopProp.Name == "info")
                                    {
                                for (int a = 0; a < questStopProp.WzProperties.Count; a++) // this has to be parsed by its order!! whatever comes first parses first
                                {
                                    // TODO
                                    // Quest_000.wz\Say.img\57106\1\stop\default\illustration 
                                    // sometimes may be a WzSubProperty in the later version of MapleStory. (v170++)
                                    WzStringProperty questStopPropItem = questStopProp.WzProperties[a] as WzStringProperty;

                                    if (Enum.TryParse(StringUtility.CapitalizeFirstCharacter(questStopProp.Name), true, out QuestEditorStopConversationType conversationType))
                                    {
                                        QuestEditorSayEndQuestModel toAddTo = sayStop.Where(x => x.ConversationType == conversationType).FirstOrDefault();
                                        if (toAddTo == null)
                                        {
                                            toAddTo = new QuestEditorSayEndQuestModel()
                                            {
                                                ConversationType = conversationType,
                                            };
                                            sayStop.Add(toAddTo);
                                        }
                                        toAddTo.Responses.Add(new QuestEditorSayResponseModel() { Text = questStopPropItem.Value });
                                    } else
                                    {
                                        // ERROR
                                        string error = string.Format("[QuestEditor] Missing enum entry in QuestEditorStopConversationType. Name='{0}', QuestId={1}", questStopProp.Name, quest.Id);
                                        ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                                    }
                                }
                            }
                            else if (questStopProp.Name == "yes" || questStopProp.Name == "no") // an askYesNo conversation after stop, then more embedded 'stop'
                            {
                                // TODO
                                /**
                                 * <imgdir name="stop">
                                 * <imgdir name="yes">
                                 * <string name="0" value="I appreciated how you gave me an update on him last time, and now ... wow... thank you so much. With this, I should be good enough to go and tell his story to the queen."/>
                                 * </imgdir>
                                 * <imgdir name="stop">
                                 * <imgdir name="npc">
                                 * <string name="0" value="You haven&apos;t met my sister yet? Please get #b20 #t4000331#s#k for her, okay?"/>
                                 * </imgdir>
                                 * <imgdir name="item">
                                 * <string name="0" value="You&apos;re the one that came here last time to give me a word on #p2101001#. What brought you back here...? If you&apos;re here to meet the queen, please be careful."/>
                                 * </imgdir>
                                 * </imgdir>
                                 * </imgdir>*/
                            }
                            else if (questStopProp.Name == "stop")
                            {
                                // TODO
                                // there's also embedded stop
                                /*                
                                 *                <imgdir name="stop">
                                 *                <imgdir name="npc">
                                 *                <string name="0" value="You haven&apos;t met my sister yet? Please get #b20 #t4000331#s#k for her, okay?"/>
                                 *                </imgdir>
                                 *                <imgdir name="item">
                                 *                <string name="0" value="You&apos;re the one that came here last time to give me a word on #p2101001#. What brought you back here...? If you&apos;re here to meet the queen, please be careful."/>
                                 *                </imgdir>
                                 *                </imgdir>*/
                            }
                            else
                            {   
                                int intProp = -1;
                                if (int.TryParse(questStopProp.Name, out intProp))
                                {
                                    // 0 1 2 3 4 5
                                    /* 
                                     * <imgdir name="stop">
                                     * <imgdir name="0">
                                     * <int name="answer" value="1"/>
                                     * </imgdir>
                                     * <imgdir name="1">
                                     * <int name="answer" value="1"/>
                                     * </imgdir>
                                     * </imgdir>*/
                                }
                                else
                                {
                                    string error = string.Format("[QuestEditor] Unhandled quest stop type. Name='{0}', QuestId={1}", questStopProp.Name, quest.Id);
                                    ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                                }
                            }
                        }
                    }
                }

                // parse previous, set conversation type
                if (questEditorSayModel != null)
                {
                    bool bContainsAskConversation = false;
                    if (questEditorSayModel.NpcConversation.Contains("#L0#") || (questEditorSayModel.NpcConversation.Contains("#L1#") || questEditorSayModel.NpcConversation.Contains("#L2#") || questEditorSayModel.NpcConversation.Contains("#L3#"))
                        && questEditorSayModel.NpcConversation.Contains("#l"))
                    {
                        bContainsAskConversation = true; // flag
                    }

                    if (bContainsAskConversation)
                        questEditorSayModel.ConversationType = QuestEditorConversationType.Ask;
                    else if (questEditorSayModel.YesResponses.Count > 0 || questEditorSayModel.NoResponses.Count > 0)
                        questEditorSayModel.ConversationType = QuestEditorConversationType.YesNo;
                    else
                        questEditorSayModel.ConversationType = QuestEditorConversationType.NextPrev;
                }
            }
            return Tuple.Create(sayInfo, sayStop);
        }
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
        /// <summary>
        /// On click - button to generate the demand summary requirements from items in "Demand" tab
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_generateDemandSummary_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// On click - button to generate reward summary requirements from items in "Reward" tab
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_generateRewardSummary_Click(object sender, RoutedEventArgs e)
        {

        }
        #endregion

        #region Quest Say.img
        /// <summary>
        /// Add a new 'yes' conversation response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            var questModel = dataGridCell.DataContext as QuestEditorSayModel;

            if (questModel != null)
            {
                if (button.Name == "button_addResponse")
                {
                    questModel.YesResponses.Add(new QuestEditorSayResponseModel() { Text = "Add some text here." });
                }
                else if (button.Name == "button_addNoResponse")
                {
                    questModel.YesResponses.Add(new QuestEditorSayResponseModel() { Text = "Add some text here." });
                }
            }
        }

        /// <summary>
        /// Remove no conversation response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            var response = button.DataContext as QuestEditorSayResponseModel;
            var questModel = dataGridCell.DataContext as QuestEditorSayModel;

            if (response != null && questModel != null)
            {
                // find the listbox first
                // then get the ObservableCollection<QuestEditorSayResponseModel> it is binded to
                ListBox listboxParent = FindAncestor<ListBox>(button);
                ObservableCollection<QuestEditorSayResponseModel> responsesList = listboxParent.DataContext as ObservableCollection<QuestEditorSayResponseModel>;
                
                responsesList.Remove(response);
            }
        }

        /// <summary>
        /// Add a new text for 'stop' quest conversation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addResponse_stopQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            var questModel = dataGridCell.DataContext as QuestEditorSayEndQuestModel;

            if (questModel != null)
            {
                questModel.Responses.Add(new QuestEditorSayResponseModel() { Text = "You have not met the requirements yet. <ADD SOME TEXT>" });
            }
        }

        /// <summary>
        /// Remove no conversation response for 'stop' quest conversation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteResponse_stop_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            var response = button.DataContext as QuestEditorSayResponseModel;
            var questModel = dataGridCell.DataContext as QuestEditorSayEndQuestModel;

            if (response != null && questModel != null)
            {
                questModel.Responses.Remove(response);
            }
        }
        #endregion

        #region Quest Act.img
        /// <summary>
        /// On select item as reward
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorActInfoModel actInfo)
            {
                if (actInfo.ActType != QuestEditorActType.Item)
                    return;

                LoadItemSelector itemSelector = new LoadItemSelector();
                itemSelector.ShowDialog();
                int selectedItem = itemSelector.SelectedItemId;
                if (selectedItem != 0)
                {
                    actInfo.SelectedRewardItems.Add(selectedItem);
                }
            }
        }

        /// <summary>
        /// On delete reward item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_deleteItem_Click(object sender, RoutedEventArgs e)
        {
            Button btnSender = ((Button)sender);
            QuestEditorActInfoModel actInfo = FindAncestor<ListBox>(btnSender).DataContext as QuestEditorActInfoModel; // bz button is binded to <int>
            
            if (actInfo.ActType != QuestEditorActType.Item)
                return;

            if (btnSender.DataContext is int selectedDeleteItemId) 
            {
                actInfo.SelectedRewardItems.Remove(selectedDeleteItemId);
            }
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

            saveQuestSayConversation(quest, quest.SayInfoStartQuest, startQuestSubProperty); // start quest save
            saveQuestSayConversation(quest, quest.SayInfoEndQuest, endQuestSubProperty); // end quest save

            saveQuestStopSayConversation(quest.SayInfoStop_StartQuest, startQuestSubProperty);
            saveQuestStopSayConversation(quest.SayInfoStop_EndQuest, endQuestSubProperty);

            // remove previous quest say wzImage
            WzImage questSayParentImg = oldSayWzProp.Parent as WzImage; // this may be null, since not all quest contains Say.img sub property
            if (questSayParentImg == null)
                questSayParentImg = Program.InfoManager.QuestSays.FirstOrDefault().Value?.Parent as WzImage; // select any random "say" sub item and get its parent instead

            if (oldSayWzProp != null)
                oldSayWzProp.Remove();

            questSayParentImg.AddProperty(newSayWzProp); // add new to the parent

            // replace the old 
            Program.InfoManager.QuestSays[quest.Id.ToString()] = newSayWzProp;

            // flag unsaved changes bool
            _unsavedChanges = true;
            Program.WzManager.SetWzFileUpdated(questSayParentImg.GetTopMostWzDirectory().Name /* "map" */, questSayParentImg);

        }

        /// <summary>
        /// Saves this list of conversations to Say.img
        /// </summary>
        /// <param name="quest"></param>
        /// <param name="questSayItems"></param>
        /// <param name="questSaySubProperty"></param>
        private void saveQuestSayConversation(QuestEditorModel quest, ObservableCollection<QuestEditorSayModel> questSayItems, WzSubProperty questSaySubProperty)
        {
            bool bContainsAskConversation = false;

            int i = 0;
            foreach (QuestEditorSayModel sayModel in questSayItems)
            {
                // the main conversation
                questSaySubProperty.AddProperty(new WzStringProperty(i.ToString(), sayModel.NpcConversation));

                if (sayModel.IsYesNoConversation) // if there's nothing after a YesNo conversation, it is okay.. 
                {
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
                }

                if (sayModel.NpcConversation.Contains("#L0#") || (sayModel.NpcConversation.Contains("#L1#") || sayModel.NpcConversation.Contains("#L2#") || sayModel.NpcConversation.Contains("#L3#")) 
                    && sayModel.NpcConversation.Contains("#l"))
                {
                    bContainsAskConversation = true; // flag

                    if (sayModel.ConversationType != QuestEditorConversationType.Ask) 
                    {
                        // TODO warn the user about incorrect parameters entered
                    }
                }

                i++;
            }

            if (bContainsAskConversation /*quest.IsAskConversation*/) // dont rely on prior data, check with existing conversations
            {
                WzIntProperty wzAskBoolProperty = new WzIntProperty("ask", 1);
                questSaySubProperty.AddProperty(wzAskBoolProperty);

                // TODO: 
                // ask selections
            }
        }

        /// <summary>
        /// Saves the list of "stop" npc conversation to Say.img
        /// </summary>
        /// <param name="stopList"></param>
        /// <param name="questStartOrStopProperty"></param>
        private void saveQuestStopSayConversation(ObservableCollection<QuestEditorSayEndQuestModel> stopList, WzSubProperty questStartOrStopProperty)
        {
            WzSubProperty stopProperty = new WzSubProperty("stop");
            questStartOrStopProperty.AddProperty(stopProperty); // add to "0" or "1"

            foreach (QuestEditorSayEndQuestModel stopModel in stopList)
            {
                string convTypeName = stopModel.ConversationType.ToString().ToLower();

                WzSubProperty convTypeProperty;
                if (stopProperty[convTypeName] == null)
                {
                    convTypeProperty = new WzSubProperty(convTypeName); // create new if not exist
                    stopProperty.AddProperty(convTypeProperty); // add to "stop" property folder
                } else
                {
                    convTypeProperty = stopProperty[convTypeName] as WzSubProperty; // get from
                }

                for (int i = 0; i < stopModel.Responses.Count; i++) {
                    WzStringProperty npcResponseProperty = new WzStringProperty(i.ToString(), stopModel.Responses[i].Text);
                    convTypeProperty.AddProperty(npcResponseProperty);
                }
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

            // remove it off local collections
            Quests.Remove(_selectedQuest);
            FilteredQuests.Remove(_selectedQuest);


            //////////////////
            /// Remove from QuestInfo.img
            //////////////////
            WzSubProperty questWzSubProperty = Program.InfoManager.QuestInfos[quest.Id.ToString()];

            // remove it off WzDirectory in the WZ
            WzImage questInfoParentImg = questWzSubProperty.Parent as WzImage;
            questWzSubProperty.Remove();

            // flag unsaved changes bool
            _unsavedChanges = true;
            Program.WzManager.SetWzFileUpdated(questInfoParentImg.GetTopMostWzDirectory().Name /* "map" */, questInfoParentImg);

            //////////////////
            /// Remove from Say.img
            //////////////////
            WzSubProperty oldSayWzProp = Program.InfoManager.QuestSays.ContainsKey(quest.Id.ToString()) ? Program.InfoManager.QuestSays[quest.Id.ToString()] : null;
            if (oldSayWzProp != null)
            {
                Program.InfoManager.QuestSays.Remove(quest.Id.ToString());

                WzImage questSayParentImg = oldSayWzProp.Parent as WzImage; // TODO: this may be null, need to track reference of Say.img parent somewhere
                if (oldSayWzProp != null)
                    oldSayWzProp.Remove();

                Program.WzManager.SetWzFileUpdated(questSayParentImg.GetTopMostWzDirectory().Name /* "map" */, questSayParentImg);
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Helper method to find ancestor of a specific type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="current"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Helper method to find descendant of a specific type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static T FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                else
                {
                    T descendant = FindDescendant<T>(child);
                    if (descendant != null)
                        return descendant;
                }
            }
            return null;
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
