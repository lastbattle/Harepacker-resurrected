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

using HaCreator.GUI.Input;
using HaCreator.GUI.InstanceEditor;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

// developer debug
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
// end developer debug

                // Quest name
                string questName = (questProp["name"] as WzStringProperty)?.Value;

                QuestEditorModel quest = new()
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
                quest.Area = QuestAreaCodeTypeExt.ToEnum( (questProp["area"] as WzIntProperty)?.Value ?? 0);
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

                    WzSubProperty questSayStart0Prop = null;
                    if (questSayProp["0"] is WzSubProperty)
                        questSayStart0Prop = (WzSubProperty)questSayProp["0"]; // GMS shit 
                    else
                    {
                        //FullPath = "Quest.wz\\Say.img\\10670\\0"
                        // {Hi there! Did you hear that MapleStory is celebrating its 6th Anniversary? Time flies, doesn't it? To mark the occasion, I made a bunch of Jigsaw Puzzles. Do you want to try them?}
                    }
                    WzSubProperty questSayEnd0Prop = (WzSubProperty)questSayProp["1"];

                    if (questSayStart0Prop != null)
                    {
                        var loadedModels = parseQuestSayConversations(questSayStart0Prop, quest);

                        quest.IsLoadingFromFile = true;
                        try
                        {
                            foreach (QuestEditorSayModel sayModel in loadedModels.Item1)
                            {
                                quest.SayInfoStartQuest.Add(sayModel);
                            }
                            foreach (QuestEditorSayEndQuestModel sayStopModel in loadedModels.Item2)
                            {
                                quest.SayInfoStop_StartQuest.Add(sayStopModel);
                            }
                        } finally
                        {
                            quest.IsLoadingFromFile = false;
                        }
                    }
                    if (questSayEnd0Prop != null)
                    {
                        var loadedModels = parseQuestSayConversations(questSayEnd0Prop, quest);

                        quest.IsLoadingFromFile = true;
                        try
                        {
                            foreach (QuestEditorSayModel sayModel in loadedModels.Item1)
                            {
                                quest.SayInfoEndQuest.Add(sayModel);
                            }
                            foreach (QuestEditorSayEndQuestModel sayStopModel in loadedModels.Item2)
                            {
                                quest.SayInfoStop_EndQuest.Add(sayStopModel);
                            }
                        } finally
                        {
                            quest.IsLoadingFromFile = false;
                        }
                    }
                }
                else
                {
                    // add empty placeholders
                }

                // Parse Act.img
                if (Program.InfoManager.QuestActs.ContainsKey(key)) // sometimes it does not exist in the Quest.wz/Say.img
                {
                    WzSubProperty questActProp = Program.InfoManager.QuestActs[key];

                    WzSubProperty questActStart0Prop = (WzSubProperty)questActProp["0"];
                    WzSubProperty questActEnd1Prop = (WzSubProperty)questActProp["1"];

                    if (questActStart0Prop != null)
                        parseQuestAct(questActStart0Prop, quest.ActStartInfo, quest); // start
                    if (questActEnd1Prop != null)
                        parseQuestAct(questActEnd1Prop, quest.ActEndInfo, quest); // end quest
                }

                // Parse Check.img
                if (Program.InfoManager.QuestChecks.ContainsKey(key)) // sometimes it does not exist in the Quest.wz/Say.img
                {
                    WzSubProperty questCheckProp = Program.InfoManager.QuestChecks[key];

                    WzSubProperty questCheckStart0Prop = (WzSubProperty)questCheckProp["0"];
                    WzSubProperty questCheckEnd1Prop = (WzSubProperty)questCheckProp["1"];

                    if (questCheckStart0Prop != null)
                        parseQuestCheck(questCheckStart0Prop, quest.CheckStartInfo, quest); // start
                    if (questCheckEnd1Prop != null)
                        parseQuestCheck(questCheckEnd1Prop, quest.CheckEndInfo, quest); // end quest
                }
                
                // add
                Quests.Add(quest);
            }
            FilteredQuests = Quests;

            if (Quests.Count > 0)
            {
                SelectedQuest = Quests[0];
            }
        }

        /// <summary>
        /// Parses "Quest.wz/Check.img/0", "Quest.wz/Check.img/1"
        /// </summary>
        /// <param name="questCheckProp"></param>
        /// <param name="quest"></param>
        private static void parseQuestCheck(WzSubProperty questCheckProp, ObservableCollection<QuestEditorCheckInfoModel> questChecks, QuestEditorModel quest)
        {
            foreach (WzImageProperty checkTypeProp in questCheckProp.WzProperties)
            {
                QuestEditorCheckType checkType = checkTypeProp.Name.ToQuestEditorCheckType();
                switch (checkType)
                {
                    case QuestEditorCheckType.Npc:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0;  
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Job:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty jobProps in checkTypeProp.WzProperties) // job Ids "0", "1", "2"
                            {
                                int jobId = (jobProps as WzIntProperty)?.GetInt() ?? 0;

                                QuestEditorSkillModelJobIdWrapper jobModel = new QuestEditorSkillModelJobIdWrapper()
                                {
                                    JobId = jobId
                                };
                                firstCheck.Jobs.Add(jobModel);
                            }
                            break;
                        }
                    case QuestEditorCheckType.Quest:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty questProp in checkTypeProp.WzProperties) // "0", "1", "2",
                            {
                                int questId = (questProp["id"] as WzIntProperty)?.GetInt() ?? 0;
                                int stateInt = (questProp["state"] as WzIntProperty)?.GetInt() ?? 0;

                                if (Enum.IsDefined(typeof(QuestStateType), stateInt))
                                {
                                    QuestStateType state = (QuestStateType)stateInt;

                                    QuestEditorQuestReqModel req = new QuestEditorQuestReqModel()
                                    {
                                        QuestId = questId,
                                        QuestState = state,
                                    };
                                    firstCheck.QuestReqs.Add(req);
                                }
                                else
                                {
                                    string error = string.Format("[QuestEditor] Incorrect quest state for QuestId={0}. IntValue={1}", questId, stateInt);
                                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.Item:
                        {
                            foreach (WzImageProperty itemProp in checkTypeProp.WzProperties)
                            {
                                // for future versions, dev debug
// developer debug
                                foreach (WzImageProperty itemSubProperties in itemProp.WzProperties)
                                {
                                    switch (itemSubProperties.Name)
                                    {
                                        case "id":
                                        case "count":
                                            break;
                                        default:
                                            {
                                                string error = string.Format("[QuestEditor] Unhandled quest Check.img item property. Name='{0}', QuestId={1}", itemSubProperties.Name, quest.Id);
                                                ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                                                break;
                                            }
                                    }
                                }
// end developer debug

                                int itemId = (itemProp["id"] as WzIntProperty)?.GetInt() ?? 0;
                                short count = (itemProp["count"] as WzIntProperty)?.GetShort() ?? 0;
                                
                                if (itemId != 0)
                                {
                                    var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                    QuestEditorCheckItemReqModel actReward = new QuestEditorCheckItemReqModel()
                                    {
                                        ItemId = itemId,
                                        Quantity = count,
                                    };
                                    firstCheck.SelectedReqItems.Add(actReward);
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.Info:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty infoProp in checkTypeProp.WzProperties) // "0", "1", "2"
                            {
                                string info = (infoProp as WzStringProperty)?.ToString() ?? string.Empty;

                                if (info != string.Empty)
                                {
                                    firstCheck.QuestInfo.Add(new QuestEditorCheckQuestInfoModel()
                                    {
                                        Text = info
                                    });
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.InfoNumber:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            int questId = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0; // "infoNumber"

                            firstCheck.Amount = questId;
                            break;
                        }
                    case QuestEditorCheckType.InfoEx:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty infoProp in checkTypeProp.WzProperties) // "0", "1", "2"
                            {
                                string infoValue = (infoProp["value"] as WzStringProperty)?.ToString() ?? string.Empty;
                                int cond = (infoProp["cond"] as WzIntProperty)?.GetInt() ?? 0; // 0, 1, 2. probably equal, above or below?

                                // infoEx
                                // [29004] The One Who Stood on Top - Value = "5", Condition = "1" (Stand on 5 top areas)
                                // [29005] Beginner Explorer - Value = "20", Condition = "1" (20 areas)
                                // [29006] El Nath Explorer - Value = "10", Condition = "1" (10 areas)
                                // [29012] Ossyria Explorer - Value = "1", Condition = "1" (1 areas)
                                // [53172] Gaga's Crayons -  Value = "254", Condition = "2" 
                                // [11120] Ribbit Ribbit Spring Picnic - Value = "4", Condition = "2" 
                                // [11360] [Artifact Hunt Contest] Announcement! - Value = "6", Condition = "2" 
                                // [53228] Rainbow Week: Yellow Magic -  Value = "2", Condition = "2" 
                                // [53230] Rainbow Week: Yellow Magic -  Value = "2", Condition = "2" 

                                if (infoValue != string.Empty)
                                {
                                    firstCheck.QuestInfoEx.Add(new QuestEditorCheckQuestInfoExModel()
                                    {
                                        Value = infoValue,
                                        Condition = cond
                                    });
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.DayByDay:
                        {
                            //  <int name="dayByDay" value="1"/> bool
                            bool set = ((checkTypeProp as WzIntProperty)?.GetInt() ?? 0) > 0;
                            if (set)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Boolean = set;
                            }
                            break;
                        }
                    case QuestEditorCheckType.DayOfWeek:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty dayOfWeekProp in checkTypeProp.WzProperties)
                            {
                                string dayOfWeekStr = (dayOfWeekProp as WzStringProperty)?.ToString() ?? string.Empty;

                                if (dayOfWeekStr != string.Empty)
                                {
                                    /*if (dayOfWeekStr == "1") // [8248] - Maple 7th Day Market opens tomorrow!
                                    {

                                    }*/
                                    QuestEditorCheckDayOfWeekType dayOfWeekType = QuestEditorCheckDayOfWeekTypeExt.FromWzString(dayOfWeekStr);

                                    QuestEditorCheckDayOfWeekModel modelSet = firstCheck.DayOfWeek.Where(x => x.DayOfWeek == dayOfWeekType).FirstOrDefault();
                                    modelSet.IsSelected = true;
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.FieldEnter:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty fieldProp in checkTypeProp.WzProperties)
                            {
                                int mapId = (fieldProp as WzIntProperty)?.GetInt() ?? 0;

                                firstCheck.SelectedNumbersItem.Add(mapId);
                            }
                            break;
                        }
                    case QuestEditorCheckType.SubJobFlags:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            int subJobFlag = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0;

// developer debug
                            CharacterSubJobFlagType flag = CharacterSubJobFlagTypeExt.ToEnum(subJobFlag);
                            if (subJobFlag != 0 && flag == CharacterSubJobFlagType.Any)
                            {
                                string error = string.Format("[QuestEditor] Unhandled quest Check.img 'subJobFlag' property. Flag='{0}', QuestId={1}", subJobFlag, quest.Id);
                                ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                            }
// end developer debug

                            firstCheck.Amount = subJobFlag;
                            break;
                        }
                    case QuestEditorCheckType.Premium:
                        {
                            //  <int name="premium" value="1"/> bool
                            bool set = ((checkTypeProp as WzIntProperty)?.GetInt() ?? 0) > 0;
                            if (set)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Boolean = set;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Pop:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0; // fame 
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Skill:
                        {
                            ObservableCollection<QuestEditorCheckSkillModel> skillsAcquire = new();

                            foreach (WzImageProperty skillItemProp in checkTypeProp.WzProperties) // "0", "1", "2", "3"
                            {
                                // for future versions, dev debug
// developer debug
                                foreach (WzImageProperty itemSubProperties in skillItemProp.WzProperties)
                                {
                                    switch (itemSubProperties.Name)
                                    {
                                        case "id":
                                        case "level":
                                        case "acquire":
                                        case "levelCondition":
                                            break;
                                        default:
                                            {
                                                string error = string.Format("[QuestEditor] Unhandled quest Check.img skill property. Name='{0}', QuestId={1}", itemSubProperties.Name, quest.Id);
                                                ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                                                break;
                                            }
                                    }
                                }
// end developer debug
                                QuestEditorCheckSkillModel skillModel = new()
                                {
                                    Id = (skillItemProp["id"] as WzIntProperty)?.GetInt() ?? 0,
                                    SkillLevel = (skillItemProp["level"] as WzIntProperty)?.GetInt() ?? 0, // for Act.img its "skillLevel"
                                    Acquire = ((skillItemProp["acquire"] as WzIntProperty)?.GetInt() ?? 0) > 0 
                                };

                                string conditionTypeStr = (skillItemProp["levelCondition"] as WzStringProperty)?.GetString() ?? "none";
                                if (conditionTypeStr != null) // its ok if is null
                                {
                                    skillModel.ConditionType = QuestEditorCheckSkillCondTypeExt.FromWzString(conditionTypeStr);
                                }

                                if (skillModel.Id != 0)
                                {
                                    skillsAcquire.Add(skillModel);
                                }
                            }

                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);
                            firstCheck.Skills = skillsAcquire;
                            break;
                        }
                    case QuestEditorCheckType.Mob:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty mobProp in checkTypeProp.WzProperties) // "0", "1", "2", "3"
                            {
                                int mobId = (mobProp["id"] as WzIntProperty)?.GetInt() ?? 0;
                                int mobCount = (mobProp["count"] as WzIntProperty)?.GetInt() ?? 0;

                                QuestEditorCheckMobModel mobModel = new()
                                {
                                    Id = mobId,
                                    Count = mobCount,
                                };
                                firstCheck.MobReqs.Add(mobModel);
                            }
                            break;
                        }
                    case QuestEditorCheckType.EndMeso:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0; //  
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Pet:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty petProp in checkTypeProp.WzProperties) // "0" "1" "2" pets
                            {
                                int petId = (petProp["id"] as WzIntProperty)?.GetInt() ?? 0;

                                firstCheck.SelectedNumbersItem.Add(petId);
                            }
                            break;
                        }
                    case QuestEditorCheckType.PetTamenessMin:
                    case QuestEditorCheckType.PetTamenessMax:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0; //  
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.PetRecallLimit:
                    case QuestEditorCheckType.PetAutoSpeakingLimit:
                        {
                            //  <int name="petRecallLimit" value="1"/> bool
                            bool set = ((checkTypeProp as WzIntProperty)?.GetInt() ?? 0) > 0;
                            if (set)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Boolean = set;
                            }
                            break;
                        }
                    case QuestEditorCheckType.TamingMobLevelMin:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.WeeklyRepeat:
                        {
                            //  <int name="weeklyRepeat" value="1"/> bool
                            bool set = ((checkTypeProp as WzIntProperty)?.GetInt() ?? 0) > 0;
                            if (set)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Boolean = set;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Married:
                        {
                            //  <int name="marriaged" value="1"/> bool
                            bool set = ((checkTypeProp as WzIntProperty)?.GetInt() ?? 0) > 0;
                            if (set)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Boolean = set;
                            }
                            break;
                        }
                    case QuestEditorCheckType.CharmMin:
                    case QuestEditorCheckType.CharismaMin:
                    case QuestEditorCheckType.InsightMin:
                    case QuestEditorCheckType.WillMin:
                    case QuestEditorCheckType.CraftMin:
                    case QuestEditorCheckType.SenseMin:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0; // sense 
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.ExceptBuff:
                        {
                            // <string name="exceptbuff" value="2022631"/> // its a string somehow, omg nexon!
                            int buffId = (checkTypeProp as WzStringProperty)?.GetInt() ?? 0;
                            if (buffId != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = buffId;
                            }
                            break;
                        }
                    case QuestEditorCheckType.EquipAllNeed:
                    case QuestEditorCheckType.EquipSelectNeed:
                        {
                            var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                            foreach (WzImageProperty eqpProp in checkTypeProp.WzProperties)
                            {
                                int itemId = (eqpProp as WzIntProperty)?.GetInt() ?? 0;
                                if (itemId != 0)
                                {
                                    firstCheck.SelectedNumbersItem.Add(itemId);
                                }
                            }

                            break;
                        }
                    case QuestEditorCheckType.WorldMin:
                        {
                            //  <string name="worldmin" value="23"/>
                            // oddly enough, worldmin and worldmax uses a WzStringProperty
                            int amount = (checkTypeProp as WzStringProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.WorldMax:
                        {
                            int amount = (checkTypeProp as WzStringProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.LvMin:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.LvMax:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.NormalAutoStart:
                        {
                            //  <int name="normalAutoStart" value="1"/> bool
                            bool set = ((checkTypeProp as WzIntProperty)?.GetInt() ?? 0) > 0;
                            if (set)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Boolean = set;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Interval:
                        {
                            int amount = (checkTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorCheckType.Start:
                    case QuestEditorCheckType.End:
                    case QuestEditorCheckType.Start_t:
                    case QuestEditorCheckType.End_t:
                        {
                            //<string name="start" value="2006072000"/>
                            //<string name="end" value="2006100100" />
                            //<string name="start_t" value="200809050000"/>
                            //<string name="end_t" value="200809260000"/>
                            WzStringProperty dateStr = (checkTypeProp as WzStringProperty);
                            if (dateStr != null)
                            {
                                DateTime? date = dateStr.GetDateTime();

                                if (date != null)
                                {
                                    var firstExpAct = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                    firstExpAct.Date = date.Value;
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.Startscript:
                    case QuestEditorCheckType.Endscript:
                        {
                            string text = (checkTypeProp as WzStringProperty)?.GetString() ?? null;
                            if (text != null)
                            {
                                var firstCheck = AddCheckItemIfNoneAndGet(checkType, questChecks);

                                firstCheck.Text = text;
                            }
                            break;
                        }
                    default:
                        {
                            string error = string.Format("[QuestEditor] Unhandled quest check type. Name='{0}', QuestId={1}", checkTypeProp.Name, quest.Id);
                            ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                            break;
                        }
                }
            }
        }
        private static QuestEditorCheckInfoModel AddCheckItemIfNoneAndGet(QuestEditorCheckType checkTypeEnum, ObservableCollection<QuestEditorCheckInfoModel> questChecks)
        {
            bool containsItemCheckType = questChecks.Any(act => act.CheckType == checkTypeEnum);
            if (!containsItemCheckType)
            {
                questChecks.Add(new QuestEditorCheckInfoModel(checkTypeEnum));
            }
            var firstCheck = questChecks.FirstOrDefault(act => act.CheckType == checkTypeEnum);
            return firstCheck;
        }

        /// <summary>
        /// Parses "Quest.wz/Act.img/0", "Quest.wz/Act.img/1"
        /// </summary>
        /// <param name="questActProp"></param>
        /// <param name="quest"></param>
        private void parseQuestAct(WzSubProperty questActProp, ObservableCollection<QuestEditorActInfoModel> questActs, QuestEditorModel quest)
        {
            bool bContainsConversation = false;

            foreach (WzImageProperty actTypeProp in questActProp.WzProperties)
            {
                QuestEditorActType actType = actTypeProp.Name.ToQuestEditorActType();
                switch (actType)
                {
                    case QuestEditorActType.Item:
                        {
                            foreach (WzImageProperty itemProp in actTypeProp.WzProperties)
                            {
                                // for future versions, dev debug
// developer debug
                                foreach (WzImageProperty itemSubProperties in itemProp.WzProperties)
                                {
                                    switch (itemSubProperties.Name)
                                    {
                                        case "id":
                                        case "count":
                                        case "dateExpire":
                                        case "potentialGrade":
                                        case "job":
                                        case "jobEx":
                                        case "period":
                                        case "prop":
                                        case "gender":
                                        case "var":
                                        case "resignRemove":
                                        case "name":
                                        case "potentialCount": // only quest '11396' seems to use this.
                                            break;
                                        default:
                                            {
                                                string error = string.Format("[QuestEditor] Unhandled quest Act.img item property. Name='{0}', QuestId={1}", itemSubProperties.Name, quest.Id);
                                                ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                                                break;
                                            }
                                    }
                                }
// end developer debug

                                int itemId = (itemProp["id"] as WzIntProperty)?.GetInt() ?? 0;
                                short count = (itemProp["count"] as WzIntProperty)?.GetShort() ?? 0;
                                WzStringProperty dateExpireProp = (itemProp["dateExpire"] as WzStringProperty);
                                string potentialGrade = (itemProp["potentialGrade"] as WzStringProperty)?.GetString() ?? null;
                                int job = (itemProp["job"] as WzIntProperty)?.GetInt() ?? 0;
                                int jobEx = (itemProp["jobEx"] as WzIntProperty)?.GetInt() ?? 0; // TODO
                                int period = (itemProp["period"] as WzIntProperty)?.GetInt() ?? 0; // The expiration period (in minutes) from the time that the item is received.
                                int prop = (itemProp["prop"] as WzIntProperty)?.GetInt() ?? 0;
                                CharacterGenderType gender = (CharacterGenderType)((itemProp["gender"] as WzIntProperty)?.GetInt() ?? 2); // 0 = Male, 1 = Female, 2 = both [default = 2 for extraction if unavailable]
                                //int unk_var = (itemProp["var"] as WzIntProperty)?.GetInt() ?? 0;

                                if (itemId != 0)
                                {
                                    var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Item, questActs);

                                    // potential
                                    QuestEditorActInfoPotentialType potentialType = QuestEditorActInfoPotentialType.Normal;
                                    if (potentialGrade != null) // its ok if potentialGrade is null
                                    {
                                        // Normal 노멀
                                        // Rare 레어
                                        // Epic 에픽
                                        // Unique 유니크
                                        // Legendary 레전드리
                                        potentialType = QuestEditorActInfoPotentialTypeExt.FromWzString(potentialGrade);
                                    }

                                    if (job != 0)
                                    {
                                        //MapleJobTypeExtensions.GetMatchingJobs(jobEx);
                                    }

                                    QuestEditorActInfoRewardPropTypeModel propType = QuestEditorActInfoRewardPropTypeModel.AlwaysGiven;
                                    if (prop == -1 || prop == 0 || prop == 1)
                                    {
                                        //If prop > 0: The item has a chance to be randomly selected.Higher values increase the likelihood.
                                        //If prop == 0: The item is always given (no randomness involved).
                                        //If prop == -1: The item is part of an external selection process(possibly player choice).
                                        propType = (QuestEditorActInfoRewardPropTypeModel)prop;
                                    }

                                    QuestEditorActInfoRewardModel actReward = new QuestEditorActInfoRewardModel()
                                    {
                                        ItemId = itemId,
                                        Quantity = count,
                                        PotentialGrade = potentialType,
                                        Job = job,
                                        JobEx = jobEx,
                                        Period = period,
                                        Prop = propType,
                                        Gender = gender,
                                    };
                                    if (dateExpireProp != null)
                                    {
                                        DateTime? date = dateExpireProp.GetDateTime();

                                        if (date == null)
                                        {
                                            string error = string.Format("[QuestEditor] Unknown 'dateExpire' format for items. Data={0}", dateExpireProp.GetString());
                                            ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                                        }
                                        else
                                        {
                                            actReward.ExpireDate = date.Value;
                                        }
                                    }
                                    firstAct.SelectedRewardItems.Add(actReward);
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.Quest:
                        {
                            var firstExpAct = AddActItemIfNoneAndGet(QuestEditorActType.Quest, questActs);

                            foreach (WzImageProperty questProp in actTypeProp.WzProperties) // "0", "1", "2",
                            {
                                int questId = (questProp["id"] as WzIntProperty)?.GetInt() ?? 0;
                                int stateInt = (questProp["state"] as WzIntProperty)?.GetInt() ?? 0;

                                if (Enum.IsDefined(typeof(QuestStateType), stateInt))
                                {
                                    QuestStateType state = (QuestStateType)stateInt;
 
                                    QuestEditorQuestReqModel req = new QuestEditorQuestReqModel()
                                    {
                                        QuestId = questId,
                                        QuestState = state,
                                    };
                                    firstExpAct.QuestReqs.Add(req);
                                }
                                else
                                {
                                    string error = string.Format("[QuestEditor] Incorrect quest state for QuestId={0}. IntValue={1}", questId, stateInt);
                                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.NextQuest:
                        {
                            int nextQuestId = (actTypeProp as WzIntProperty)?.GetInt() ?? 0; // for 
                            if (nextQuestId != 0)
                            {
                                var firstExpAct = AddActItemIfNoneAndGet(QuestEditorActType.NextQuest, questActs);

                                firstExpAct.Amount = nextQuestId;
                            }
                            break;
                        }
                    case QuestEditorActType.Npc:
                        {
                            int npcId = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (npcId != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Npc, questActs);

                                firstAct.Amount = npcId;
                            }
                            break;
                        }
                    case QuestEditorActType.NpcAct:
                        {
                            string npcAct = (actTypeProp as WzStringProperty)?.Value;

                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.NpcAct, questActs);

                            firstAct.Text = npcAct;
                            break;
                        }
                    case QuestEditorActType.LvMin:
                        {
                            int amount = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.LvMin, questActs);

                                firstAct.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorActType.LvMax:
                        {
                            int amount = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.LvMax, questActs);

                                firstAct.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorActType.Interval:
                        {
                            int amount = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (amount != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Interval, questActs);

                                firstAct.Amount = amount;
                            }
                            break;
                        }
                    case QuestEditorActType.Start:
                    case QuestEditorActType.End:
                        {
                            //<string name="start" value="2006072000"/>
                            //<string name="end" value="2006100100" />
                            WzStringProperty dateStr = (actTypeProp as WzStringProperty);
                            if (dateStr != null)
                            {
                                DateTime? date = dateStr.GetDateTime();

                                if (date != null)
                                {
                                    QuestEditorActType actEnum = (QuestEditorActType)Enum.Parse(typeof(QuestEditorActType), StringUtility.CapitalizeFirstCharacter(actTypeProp.Name));
                                    var firstExpAct = AddActItemIfNoneAndGet(actEnum, questActs);

                                    firstExpAct.Date = date.Value;
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.Exp:
                        {
                            long expAmount = (actTypeProp as WzIntProperty)?.GetLong() ?? 0; // for 
                            if (expAmount != 0)
                            {
                                var firstExpAct = AddActItemIfNoneAndGet(QuestEditorActType.Exp, questActs);

                                firstExpAct.Amount = expAmount;
                            }
                            break;
                        }
                    case QuestEditorActType.Money:
                        {
                            long mesosAmount = (actTypeProp as WzIntProperty)?.GetLong() ?? 0; // for 
                            if (mesosAmount != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Money, questActs);

                                firstAct.Amount = mesosAmount;
                            }
                            break;
                        }
                    case QuestEditorActType.Info: // infoEx string
                        {
                            string info = (actTypeProp as WzStringProperty)?.Value;

                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Info, questActs);

                            firstAct.Text = info;
                            break;
                        }
                    case QuestEditorActType.Pop: // fame
                        {
                            int fameAmount = (actTypeProp as WzIntProperty)?.GetInt() ?? 0; // for 
                            if (fameAmount != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Pop, questActs);

                                firstAct.Amount = fameAmount;
                            }
                            break;
                        }
                    case QuestEditorActType.FieldEnter: // is only used by questid 9866
                        {
                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.FieldEnter, questActs);

                            foreach (WzImageProperty fieldProp in actTypeProp.WzProperties)
                            {
                                int mapId = (fieldProp as WzIntProperty)?.GetInt() ?? 0;

                                firstAct.SelectedNumbersItem.Add(mapId);
                            }
                            break;
                        }
                    case QuestEditorActType.PetTameness:
                        {
                            int tame = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;

                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.PetTameness, questActs);
                            firstAct.Amount = tame;
                            break;
                        }
                    case QuestEditorActType.PetSpeed:
                        {
                            int speed = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;

                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.PetSpeed, questActs);
                            firstAct.Amount = speed;
                            break;
                        }
                    case QuestEditorActType.PetSkill: // only used by quest 4660 4661
                        {
                            int skillVal = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;

                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.PetSkill, questActs);
                            firstAct.Amount = skillVal;
                            break;
                        }
                    case QuestEditorActType.Sp: // mostly for Evan
                        {
                            /*
                             * <imgdir name="sp">
                             * <imgdir name="0">
                             * <int name="sp_value" value="1"/>
                             * <imgdir name="job">
                             * <int name="0" value="2210"/>
                             * </imgdir>
                             * </imgdir>
                             * </imgdir>
                             */
                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Sp, questActs);

                            foreach (WzImageProperty spItem in actTypeProp.WzProperties)
                            {
                                int sp_value = (spItem["sp_value"] as WzIntProperty)?.GetInt() ?? 0;

                                if (sp_value == 0)
                                    continue;

                                QuestEditorActSpModel spModel = new QuestEditorActSpModel()
                                {
                                    SPValue = sp_value,
                                };
                                foreach (WzImageProperty jobProp in spItem["job"].WzProperties)
                                {
                                    int jobId = (jobProp as WzIntProperty)?.GetInt() ?? 0;

                                    QuestEditorSkillModelJobIdWrapper jobModel = new QuestEditorSkillModelJobIdWrapper()
                                    {
                                        JobId = jobId
                                    };
                                    spModel.Jobs.Add(jobModel);
                                }
                                firstAct.SP.Add(spModel);
                            }
                            break;
                        }
                    case QuestEditorActType.Job:
                        {
                            var firstExpAct = AddActItemIfNoneAndGet(QuestEditorActType.Job, questActs);

                            foreach (WzImageProperty jobProp in actTypeProp.WzProperties) // job Ids "0", "1", "2"
                            {
                                int jobId = (jobProp as WzIntProperty)?.GetInt() ?? 0;

                                QuestEditorSkillModelJobIdWrapper jobModel = new QuestEditorSkillModelJobIdWrapper()
                                {
                                    JobId = jobId
                                };
                                firstExpAct.JobsReqs.Add(jobModel);
                            }
                            break;
                        }
                    case QuestEditorActType.Skill:
                        {
                            ObservableCollection<QuestEditorActSkillModel> skillsAcquire = new ObservableCollection<QuestEditorActSkillModel>();

                            foreach (WzImageProperty jobItemProp in actTypeProp.WzProperties) // "0", "1", "2", "3"
                            {
                                QuestEditorActSkillModel skillModel = new QuestEditorActSkillModel();
                                skillModel.Id = (jobItemProp["id"] as WzIntProperty)?.GetInt() ?? 0;
                                skillModel.SkillLevel = (jobItemProp["skillLevel"] as WzIntProperty)?.GetInt() ?? 0;
                                skillModel.MasterLevel = (jobItemProp["masterLevel"] as WzIntProperty)?.GetInt() ?? 0;
                                skillModel.OnlyMasterLevel = ((jobItemProp["onlyMasterLevel"] as WzIntProperty)?.GetInt() ?? 0) > 0;
                                skillModel.Acquire = (jobItemProp["acquire"] as WzShortProperty)?.GetShort() ?? 0; // if (short) -1 it means "remove this skill".. wtf nexon, why not bool

                                if (jobItemProp["job"] != null)
                                {
                                    foreach (WzImageProperty jobProp in jobItemProp["job"].WzProperties)
                                    {
                                        int jobId = (jobProp as WzIntProperty)?.GetInt() ?? 0;
                                        skillModel.JobIds.Add(new QuestEditorSkillModelJobIdWrapper()
                                        {
                                            JobId = jobId
                                        });
                                    }
                                }
                                if (skillModel.Id != 0)
                                {
                                    skillsAcquire.Add(skillModel);
                                }
                            }

                            var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Skill, questActs);
                            firstAct.SkillsAcquire = skillsAcquire;
                            break;
                        }
                    case QuestEditorActType.SenseEXP: // traits
                    case QuestEditorActType.WillEXP:
                    case QuestEditorActType.InsightEXP:
                    case QuestEditorActType.CharismaEXP:
                    case QuestEditorActType.CharmEXP:
                    case QuestEditorActType.CraftEXP:
                        {
                            QuestEditorActType actEnum = (QuestEditorActType) Enum.Parse(typeof(QuestEditorActType), StringUtility.CapitalizeFirstCharacter(actTypeProp.Name));

                            int exp = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;

                            var firstAct = AddActItemIfNoneAndGet(actEnum, questActs);
                            firstAct.Amount = exp;
                            break;
                        }
                    case QuestEditorActType.Message_Map:
                        {
                            if (actTypeProp.Name.Equals("map", StringComparison.OrdinalIgnoreCase))
                            {
                                /*
                                 * <int name="buffItemID" value="2022109"/>
                                 * <string name="message" value="나인스피릿 아기용의 힘찬 울음소리를 듣자 신비로운 힘이 솟아오른다."/>
                                 * <imgdir name="map">
                                 * <int name="0" value="240000000"/>
                                 * <int name="1" value="240040611"/>
                                 * </imgdir>*/
                                ObservableCollection<int> maps = new ObservableCollection<int>();
                                int i = 0;
                                WzImageProperty img0Prop = null;
                                while ((img0Prop = (actTypeProp as WzSubProperty)[i.ToString()]) != null)
                                {
                                    int mapid = (img0Prop as WzIntProperty)?.Value ?? 0;
                                    if (mapid != 0)
                                        maps.Add(mapid);
                                    i++;
                                }
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Message_Map, questActs);
                                foreach (int map in maps)
                                {
                                    firstAct.SelectedNumbersItem.Add(map);
                                }
                            }
                            else if (actTypeProp.Name.Equals("message", StringComparison.OrdinalIgnoreCase))
                            {
                                /*
                                 * <int name="buffItemID" value="2022109"/>
                                 * <string name="message" value="나인스피릿 아기용의 힘찬 울음소리를 듣자 신비로운 힘이 솟아오른다."/>
                                 * <imgdir name="map">
                                 * <int name="0" value="240000000"/>
                                 * <int name="1" value="240040611"/>
                                 * </imgdir>*/
                                string message = (actTypeProp as WzStringProperty)?.Value ?? string.Empty;

                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Message_Map, questActs);
                                if (message != string.Empty)
                                {
                                    firstAct.Text = message;
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.BuffItemId:
                        {
                            int buffItemID = (actTypeProp as WzIntProperty)?.GetInt() ?? 0;
                            if (buffItemID != 0)
                            {
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.BuffItemId, questActs);

                                firstAct.Amount = buffItemID;
                            }
                            break;
                        }
                    case QuestEditorActType.Null:
                    default:
                        {
                            // parse 1~10
                            int actNum = -1;
                            if (int.TryParse(actTypeProp.Name, out actNum) && actNum < 20 && actNum > 0 
                                || (actTypeProp.Name == "yes") 
                                || (actTypeProp.Name == "no") 
                                || (actTypeProp.Name == "ask") 
                                || (actTypeProp.Name == "stop")) // is conversation property "0" "1" "2" "3"
                            {
                                bContainsConversation = true; // flags this
                                // and parse it later in order.
                                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Conversation0123, questActs);
                            }
                            else
                            {
                                string error = string.Format("[QuestEditor] Unhandled quest act type. Name='{0}', QuestId={1}", actTypeProp.Name, quest.Id);
                                ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                            }
                            break;
                        }
                }
            }

            if (bContainsConversation) // conversation
            {
                var firstAct = AddActItemIfNoneAndGet(QuestEditorActType.Conversation0123, questActs);

                firstAct.IsLoadingFromFile = true;
                try
                {
                    Tuple<ObservableCollection<QuestEditorSayModel>, ObservableCollection<QuestEditorSayEndQuestModel>> ret = parseQuestSayConversations(questActProp, quest);
                    foreach (QuestEditorSayModel sayModel in ret.Item1)
                    {
                        firstAct.ActConversationStart.Add(sayModel);
                    }
                    foreach (QuestEditorSayEndQuestModel sayModel in ret.Item2)
                    {
                        firstAct.ActConversationStop.Add(sayModel);
                    }
                } finally
                {
                    firstAct.IsLoadingFromFile = false;
                }
            }

        }
        private static QuestEditorActInfoModel AddActItemIfNoneAndGet(QuestEditorActType actTypeEnum, ObservableCollection<QuestEditorActInfoModel> questActs)
        {
            bool containsItemActType = questActs.Any(act => act.ActType == actTypeEnum);
            if (!containsItemActType)
            {
                questActs.Add(new QuestEditorActInfoModel()
                {
                    ActType = actTypeEnum,
                });
            }
            var firstAct = questActs.FirstOrDefault(act => act.ActType == actTypeEnum);
            return firstAct;
        }

        /// <summary>
        /// Parses quest say, and say stop conversations into a list.
        /// </summary>
        /// <param name="questSayStart0Prop"></param>
        /// <param name="quest"></param>
        /// <returns></returns>
        private static Tuple<
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
                        // TODO
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
            using (var inputForm = new NameValueInput((questName, questId) =>
            {
                bool existingQuestId = Program.InfoManager.QuestInfos.ContainsKey(questId.ToString());
                if (existingQuestId)
                    return string.Format("This quest ID [{0}] was already being used.", questId);

                if (questName.Length == 0 || questName.Length > 100)
                    return "Quest name is too long.";

                return string.Empty;
            }))
            {
                inputForm.SetWindowInfo("Quest Name", "Quest Id", "Add a new quest:");

                if (inputForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string name = inputForm.SelectedName;
                    int questId = inputForm.SelectedValue;

                    // Add quest to the list
                    WzSubProperty questWzSubProp = new WzSubProperty(questId.ToString());

                    {
                        questWzSubProp.AddProperty(new WzStringProperty("name", name));

                        // select any questInfo from the list, to get the CheckInfo parent directory
                        WzImage anyQuestInfoParentImg = Program.InfoManager.QuestInfos.FirstOrDefault().Value.Parent as WzImage;

                        // replace the old 
                        Program.InfoManager.QuestInfos[questId.ToString()] = questWzSubProp;

                        // add back the newly created image
                        anyQuestInfoParentImg.AddProperty(questWzSubProp);

                        // flag unsaved changes bool
                        _unsavedChanges = true;
                        Program.WzManager.SetWzFileUpdated(anyQuestInfoParentImg.GetTopMostWzDirectory().Name /* "map" */, anyQuestInfoParentImg);


                        // Navigate to this quest on the scrollviewer
                        QuestEditorModel quest = new()
                        {
                            Id = questId,
                            Name = name,
                        };

                        // add
                        Quests.Add(quest);
                        SelectedQuest = quest;
                        listbox_Quest.SelectedItem = SelectedQuest;
                        listbox_Quest.ScrollIntoView(SelectedQuest); // Add this line
                    }
                }
            }
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
        /// On click - select a quest name from the list of quests
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_selectParentFromList_Click(object sender, RoutedEventArgs e)
        {
            LoadQuestSelector questSelector = new();
            questSelector.ShowDialog();

            if (questSelector.SelectedQuestId == string.Empty)
                return;

            string selectedQId = questSelector.SelectedQuestId;

            if (Program.InfoManager.QuestInfos.ContainsKey(selectedQId)) 
            {
                WzSubProperty subQuestProp = Program.InfoManager.QuestInfos[selectedQId];

                string questName = (subQuestProp["name"] as WzStringProperty)?.Value ?? "NO NAME";

                SelectedQuest.Parent = questName;
            }
        }

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
            /*
* "#Wbasic#
 * #Wprob#
 * #Wselect#
 * 
 * #i2040826:# #t2040826:# x 1
 * #i2040845:# #t2040845:# x 1
 * Select 1 of the above
 * 
 * New Party Quest Challenge 3 now available.
 * 
 * Can proceed to the &apos;Moonlight Sonata Music Box&apos; quest"
 * 
 * #f<Image Path># = Show image path in Wz (Example : #fUI/UIWindow.img/QuestIcon/4/0#)
 * >> #fUI/UIWindow.img/QuestIcon/0/0# = Quest Available
 * >> #fUI/UIWindow.img/QuestIcon/1/0# = Quest Started
 * >> #fUI/UIWindow.img/QuestIcon/10/0# = Evan SP
 * >> #fUI/UIWindow.img/QuestIcon/2/0# = Quest completed
 * >> #fUI/UIWindow.img/QuestIcon/3/0# = Select item
 * ?> #fUI/UIWindow.img/QuestIcon/4/0# = Reward item
 * >> #fUI/UIWindow.img/QuestIcon/5/0# = Unknown Item
 * >> #fUI/UIWindow.img/QuestIcon/6/0# = Fame
 * >> #fUI/UIWindow.img/QuestIcon/7/0# = Meso
 * >> #fUI/UIWindow.img/QuestIcon/8/0# = EXP
 * >> #fUI/UIWindow.img/QuestIcon/9/0# = Closeness
 * >> #fMob/0100100.img/stand/0# = Mob image
 */
            var button = (Button)sender;
            if (SelectedQuest == null)
                return;

            StringBuilder sb = new StringBuilder();

            foreach (QuestEditorActInfoModel act in SelectedQuest.ActEndInfo)
            {
                switch (act.ActType)
                {
                    case QuestEditorActType.Item:
                        {
                            sb.Append("#Wbasic#").Append("\r\n");

                            foreach (QuestEditorActInfoRewardModel reward in act.SelectedRewardItems)
                            {
                                if (reward.Quantity < 0)
                                    continue; // dont put it as a reward for requirements.

                                sb.Append(string.Format("#i{0}:# #t{0}:# x {1}", reward.ItemId, reward.Quantity.ToString("#,##0")));
                                sb.Append("\r\n");

                                // TODO: time limited item
                                // #i1012270:# #t1012270:# (5 days) x 1
                            }
                            break;
                        }
                    case QuestEditorActType.Npc:
                        {
                            // nothing for user preview for NPC
                            break;
                        }
                    case QuestEditorActType.Money:
                        {
                            if (act.Amount > 0)
                            {
                                sb.Append(string.Format("{0} Mesos", act.Amount.ToString("#,##0"))); // amount
                                sb.Append("\r\n");
                            }
                            break;
                        }
                    case QuestEditorActType.Exp:
                        {
                            if (act.Amount > 0)
                            {
                                sb.Append(string.Format("{0} EXP", act.Amount.ToString("#,##0"))); // amount
                                sb.Append("\r\n");
                            }
                            // EXP #b(depends on level)#k
                            break;
                        }
                    case QuestEditorActType.Pop:
                        {
                            if (act.Amount > 0)
                            {
                                sb.Append(string.Format("{0} Fame", act.Amount.ToString("#,##0"))); // amount
                                sb.Append("\r\n");
                            }
                            break;
                        }
                    default:
                        throw new Exception("Unhandled QuestEditorActType" + act.ActType.ToString());
                }
            }
            sb.Append("\r\n");
            SelectedQuest.RewardSummary = sb.ToString();
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
                    questModel.NoResponses.Add(new QuestEditorSayResponseModel() { Text = "Add some text here." });
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

                LoadItemSelector itemSelector = new LoadItemSelector(0);
                itemSelector.ShowDialog();
                int selectedItem = itemSelector.SelectedItemId;
                if (selectedItem != 0)
                {
                    actInfo.SelectedRewardItems.Add(
                        new QuestEditorActInfoRewardModel() {
                            ItemId = selectedItem,
                            Quantity = 1,
                        });
                }
            }
        }

        /// <summary>
        /// On item expiry date selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /*private void datePicker_itemExpiry_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            DatePicker picker = sender as DatePicker;
            if (picker.SelectedDate.HasValue)
            {
                DateTime selectedDate = picker.SelectedDate.Value;

                QuestEditorActInfoRewardModel reward = picker.DataContext as QuestEditorActInfoRewardModel;
                reward.ExpireDate = selectedDate;
                //reward.ExpireDate = selectedDate.Year.ToString().PadLeft(4, '0') + selectedDate.Month.ToString().PadLeft(2, '0') + selectedDate.Day.ToString().PadLeft(2, '0') + "00"; // 2010100700
            }
        }*/

        /// <summary>
        /// On select buff as reward
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectBuff_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorActInfoModel actInfo)
            {
                if (actInfo.ActType != QuestEditorActType.BuffItemId)
                    return;

                LoadItemSelector itemSelector = new LoadItemSelector(ItemIdsCategory.BUFF_CATEGORY, InventoryType.USE);
                itemSelector.ShowDialog();
                int selectedItem = itemSelector.SelectedItemId;
                if (selectedItem != 0)
                {
                    actInfo.Amount = selectedItem;
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

            if (btnSender.DataContext is QuestEditorActInfoRewardModel selectedItem) 
            {
                actInfo.SelectedRewardItems.Remove(selectedItem);
            }
        }


        /// <summary>
        /// On select class types
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_selectClasses_Click(object sender, RoutedEventArgs e)
        {
            Button button = ((Button)sender);
            QuestEditorActInfoRewardModel rewardModel = button.DataContext as QuestEditorActInfoRewardModel;

            if (rewardModel != null)
            {
                LoadClassListSelector classListSelector = new LoadClassListSelector(rewardModel.Job);
                classListSelector.ShowDialog();

                long classesSelected = classListSelector.SelectedClassCategoryBitfield;
                if (classesSelected != 0)
                {
                    rewardModel.Job = (int)classesSelected;
                }
            }
        }

        /// <summary>
        /// Select map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectMaps_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorActInfoModel actInfo)
            {
                if (actInfo.ActType != QuestEditorActType.Message_Map && actInfo.ActType != QuestEditorActType.FieldEnter)
                    return;

                LoadMapSelector mapSelector = new LoadMapSelector();
                mapSelector.ShowDialog();

                string selectedItem = mapSelector.SelectedMap;
                if (selectedItem != string.Empty)
                {
                    actInfo.SelectedNumbersItem.Add( int.Parse(selectedItem));
                }
            }
        }


        /// <summary>
        /// Removes the map from the list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteMapResponse_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            var response = (int) button.DataContext;
            var questModel = dataGridCell.DataContext as QuestEditorActInfoModel;

            if (questModel != null)
            {
                // find the listbox first
                // then get the ObservableCollection<QuestEditorSayResponseModel> it is binded to
                ListBox listboxParent = FindAncestor<ListBox>(button);

                questModel.SelectedNumbersItem.Remove(response);
            }
        }

        /// <summary>
        /// Select NPC
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_selectNPC_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorActInfoModel actInfo)
            {
                if (actInfo.ActType != QuestEditorActType.Npc)
                    return;

                LoadNpcSelector npcSelector = new LoadNpcSelector();
                npcSelector.ShowDialog();

                string selectedItem = npcSelector.SelectedNpcId;
                if (selectedItem != string.Empty)
                {
                    actInfo.Amount = int.Parse(selectedItem);
                }
            }
        }

        /// <summary>
        /// Select the next quest id
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectQuest_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorActInfoModel actInfo)
            {
                if (actInfo.ActType != QuestEditorActType.NextQuest)
                    return;

                LoadQuestSelector questSelector = new LoadQuestSelector();
                questSelector.ShowDialog();

                string selectedItem = questSelector.SelectedQuestId;
                if (selectedItem != string.Empty)
                {
                    actInfo.Amount = int.Parse(selectedItem);
                }
            }
        }

        /// <summary>
        /// Pet skill combobox changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_petSkill_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || _isLoading)
                return;

            ComboBox comboBox = sender as ComboBox;
            PetSkillFlag comboBoxSelectedItem = (PetSkillFlag) comboBox.SelectedItem;
            StackPanel parentSp = FindAncestor<StackPanel>(comboBox);
            QuestEditorActInfoModel actInfoModel = parentSp.DataContext as QuestEditorActInfoModel;

            if (actInfoModel != null) 
            {
                // actInfoModel.Amount = comboBoxSelectedItem.GetValue(); // no need via binding
            }
        }

        /// <summary>
        /// Add new skill button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectAddSkill_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorActInfoModel actInfo)
            {
                if (actInfo.ActType != QuestEditorActType.Skill)
                    return;

                LoadSkillSelector skillSelector = new LoadSkillSelector(0);
                skillSelector.ShowDialog();
                int selectedSkillId = skillSelector.SelectedSkillId;
                if (selectedSkillId != 0)
                {
                    QuestEditorActSkillModel skillModel = new QuestEditorActSkillModel()
                    {
                        Id = selectedSkillId,
                    };
                    actInfo.SkillsAcquire.Add(skillModel);
                }

            }
        }

        /// <summary>
        /// Remove job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeSkillJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            ListBox listboxSkillParent = FindAncestor<ListBox>(listboxJobParent);
            var questSkillModel = listboxSkillParent.DataContext as QuestEditorActSkillModel;

            if (questSkillModel != null)
            {
                questSkillModel.JobIds.Remove((QuestEditorSkillModelJobIdWrapper)button.DataContext);
            }
        }

        /// <summary>
        /// Remove job from a skill
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questSkillModel = button.DataContext as QuestEditorActSkillModel;

            if (questSkillModel != null)
            {
                LoadJobSelector skillSelector = new LoadJobSelector();
                skillSelector.ShowDialog();
                CharacterJob selectedJob = skillSelector.SelectedJob;
                if (selectedJob != CharacterJob.None)
                {
                    questSkillModel.JobIds.Add(new QuestEditorSkillModelJobIdWrapper()
                    {
                        JobId = (int)selectedJob
                    });
                }
            }
        }

        /// <summary>
        /// Remove skill
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeSkill_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questSkillModel = button.DataContext as QuestEditorActSkillModel;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorActInfoModel;

            if (questSkillModel != null && questModel != null)
            {
                questModel.SkillsAcquire.Remove(questSkillModel);
            }
        }

        /// <summary>
        /// Add quest for action 'quest'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectAddQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            QuestEditorActInfoModel questModel = button.DataContext as QuestEditorActInfoModel;

            if (questModel.ActType != QuestEditorActType.Quest)
                return;

            LoadQuestSelector questSelector = new LoadQuestSelector();
            questSelector.ShowDialog();

            string selectedItem = questSelector.SelectedQuestId;
            if (selectedItem != string.Empty)
            {
                questModel.QuestReqs.Add(new QuestEditorQuestReqModel()
                {
                    QuestId = int.Parse(selectedItem),
                    QuestState = QuestStateType.Completed, // default is 2 for 'act' typically
                });
            }
        }

        /// <summary>
        /// Remove quest for action 'quest'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questActModel = button.DataContext as QuestEditorQuestReqModel;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorActInfoModel;

            if (questActModel != null && questModel != null)
            {
                questModel.QuestReqs.Remove(questActModel);
            }
        }

        /// <summary>
        /// Remove job for action 'job'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questActJobModel = button.DataContext as QuestEditorSkillModelJobIdWrapper;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorActInfoModel;

            if (questActJobModel != null && questModel != null)
            {
                questModel.JobsReqs.Remove(questActJobModel);
            }
        }

        /// <summary>
        /// Add job for action 'job'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectAddJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questModel = button.DataContext as QuestEditorActInfoModel;

            if (questModel != null)
            {
                LoadJobSelector skillSelector = new LoadJobSelector();
                skillSelector.ShowDialog();
                CharacterJob selectedJob = skillSelector.SelectedJob;
                if (selectedJob != CharacterJob.None)
                {
                    questModel.JobsReqs.Add(new QuestEditorSkillModelJobIdWrapper()
                    {
                        JobId = (int)selectedJob
                    });
                }
            }
        }

        /// <summary>
        /// Add sp for 'sp'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_selectAddSP_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questModel = button.DataContext as QuestEditorActInfoModel;

            if (questModel != null)
            {
                questModel.SP.Add(new QuestEditorActSpModel()
                {
                    SPValue = 1
                });
            }
        }

        /// <summary>
        /// Remove sp for 'sp'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeSP_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorActInfoModel;
            QuestEditorActSpModel spModel = button.DataContext as QuestEditorActSpModel;

            if (questModel.SP.Contains(spModel))
            {
                questModel.SP.Remove(spModel);
            }
        }

        /// <summary>
        /// Add sp job for 'sp'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addSPJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            QuestEditorActSpModel spModel = button.DataContext as QuestEditorActSpModel;

            LoadJobSelector skillSelector = new LoadJobSelector();
            skillSelector.ShowDialog();
            CharacterJob selectedJob = skillSelector.SelectedJob;
            if (selectedJob != CharacterJob.None)
            {
                spModel.Jobs.Add(new QuestEditorSkillModelJobIdWrapper()
                {
                    JobId = (int)selectedJob
                });
            }
        }

        /// <summary>
        /// Remove sp job for 'sp'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_removeSPJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var jobModel = button.DataContext as QuestEditorSkillModelJobIdWrapper;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorActSpModel;

            if (questModel.Jobs.Contains(jobModel))
            {
                questModel.Jobs.Remove(jobModel);
            }
        }
        #endregion

        #region Quest Check.img
        /// <summary>
        /// Select NPC for Check.img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_checkSelectNPC_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel actInfo)
            {
                if (actInfo.CheckType != QuestEditorCheckType.Npc)
                    return;

                LoadNpcSelector npcSelector = new LoadNpcSelector();
                npcSelector.ShowDialog();

                string selectedItem = npcSelector.SelectedNpcId;
                if (selectedItem != string.Empty)
                {
                    actInfo.Amount = int.Parse(selectedItem);
                }
            }
        }

        /// <summary>
        /// Select except buff for Check.img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_Check_selectBuff_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel actInfo)
            {
                if (actInfo.CheckType != QuestEditorCheckType.ExceptBuff)
                    return;

                LoadItemSelector itemSelector = new(ItemIdsCategory.BUFF_CATEGORY);
                itemSelector.ShowDialog();
                int selectedItem = itemSelector.SelectedItemId;
                if (selectedItem != 0)
                {
                    actInfo.Amount = selectedItem;
                }
            }
        }

        /// <summary>
        /// Check.img -- Delete an item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_deleteItem_Click(object sender, RoutedEventArgs e)
        {
            Button btnSender = ((Button)sender);
            QuestEditorCheckInfoModel checkType = FindAncestor<ListBox>(btnSender).DataContext as QuestEditorCheckInfoModel; // bz button is binded to <int>

            if (checkType.CheckType != QuestEditorCheckType.Item)
                return;

            if (btnSender.DataContext is QuestEditorCheckItemReqModel selectedItem)
            {
                checkType.SelectedReqItems.Remove(selectedItem);
            }
        }

        /// <summary>
        /// Check.img select an item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel checkInfo)
            {
                if (checkInfo.CheckType != QuestEditorCheckType.Item)
                    return;

                LoadItemSelector itemSelector = new(0);
                itemSelector.ShowDialog();
                int selectedItem = itemSelector.SelectedItemId;
                if (selectedItem != 0)
                {
                    checkInfo.SelectedReqItems.Add(
                        new QuestEditorCheckItemReqModel()
                        {
                            ItemId = selectedItem,
                            Quantity = 1,
                        });
                }
            }
        }

        /// <summary>
        /// Check.img "mob" delete mob
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_deleteMob_Click(object sender, RoutedEventArgs e)
        {
            Button btnSender = ((Button)sender);
            QuestEditorCheckInfoModel checkType = FindAncestor<ListBox>(btnSender).DataContext as QuestEditorCheckInfoModel; // bz button is binded to <int>

            if (checkType.CheckType != QuestEditorCheckType.Mob)
                return;

            if (btnSender.DataContext is QuestEditorCheckMobModel selectedMob)
            {
                checkType.MobReqs.Remove(selectedMob);
            }
        }

        /// <summary>
        /// Check.img "mob" add mob
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_addMob_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel checkInfo)
            {
                if (checkInfo.CheckType != QuestEditorCheckType.Mob)
                    return;

                LoadMobSelector mobSelector = new();
                mobSelector.ShowDialog();
                int selectedMobId = mobSelector.SelectedMonsterId;
                if (selectedMobId != 0)
                {
                    checkInfo.MobReqs.Add(
                        new QuestEditorCheckMobModel()
                        {
                            Id = selectedMobId,
                            Count = 1,
                        });
                }
            }
        }

        /// <summary>
        /// Check.img "pet" add item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectPetItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel checkInfo)
            {
                if (checkInfo.CheckType != QuestEditorCheckType.Pet)
                    return;

                LoadItemSelector itemSelector = new(ItemIdsCategory.PET_CATEGORY, InventoryType.CASH);
                itemSelector.ShowDialog();
                int selectedPetId = itemSelector.SelectedItemId;
                if (selectedPetId != 0)
                {
                    checkInfo.SelectedNumbersItem.Add(selectedPetId);
                }
            }
        }

        /// <summary>
        /// Check.img "pet" remove item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_deletePetItem_Click(object sender, RoutedEventArgs e)
        {
            Button btnSender = ((Button)sender);
            QuestEditorCheckInfoModel checkType = FindAncestor<ListBox>(btnSender).DataContext as QuestEditorCheckInfoModel; // bz button is binded to <int>

            if (checkType.CheckType != QuestEditorCheckType.Pet)
                return;

            if (btnSender.DataContext is int selectedPetId)
            {
                checkType.SelectedNumbersItem.Remove(selectedPetId);
            }
        }

        /// <summary>
        /// Check.img select equipment to add
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectEquip_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel checkInfo)
            {
                if ((checkInfo.CheckType != QuestEditorCheckType.EquipAllNeed) && (checkInfo.CheckType != QuestEditorCheckType.EquipSelectNeed))
                    return;

                LoadItemSelector itemSelector = new(0, InventoryType.EQUIP);
                itemSelector.ShowDialog();
                int selectedItem = itemSelector.SelectedItemId;
                if (selectedItem != 0)
                {
                    checkInfo.SelectedNumbersItem.Add(selectedItem);
                }
            }
        }

        /// <summary>
        /// Check.img delete equipment
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_deleteEquip_Click(object sender, RoutedEventArgs e)
        {
            Button btnSender = ((Button)sender);
            QuestEditorCheckInfoModel checkType = FindAncestor<ListBox>(btnSender).DataContext as QuestEditorCheckInfoModel; // bz button is binded to <int>
            
            if ((checkType.CheckType != QuestEditorCheckType.EquipAllNeed) && (checkType.CheckType != QuestEditorCheckType.EquipSelectNeed))
                return;

            if (btnSender.DataContext is int selectedItem)
            {
                checkType.SelectedNumbersItem.Remove(selectedItem);
            }
        }

        /// <summary>
        /// Check.img remove skill
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_removeSkill_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questSkillModel = button.DataContext as QuestEditorCheckSkillModel;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorCheckInfoModel;

            if (questSkillModel != null && questModel != null)
            {
                questModel.Skills.Remove(questSkillModel);
            }
        }

        /// <summary>
        /// Check.img add skill
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectAddSkill_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel checkModel)
            {
                if (checkModel.CheckType != QuestEditorCheckType.Skill)
                    return;

                LoadSkillSelector skillSelector = new(0);
                skillSelector.ShowDialog();
                int selectedSkillId = skillSelector.SelectedSkillId;
                if (selectedSkillId != 0)
                {
                    QuestEditorCheckSkillModel skillModel = new()
                    {
                        Id = selectedSkillId,
                    };
                    checkModel.Skills.Add(skillModel);
                }
            }
        }

        /// <summary>
        /// Check.img remove job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_removeJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questActJobModel = button.DataContext as QuestEditorSkillModelJobIdWrapper;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorCheckInfoModel;

            if (questActJobModel != null && questModel != null)
            {
                questModel.Jobs.Remove(questActJobModel);
            }

        }

        /// <summary>
        /// Check.img add job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectAddJob_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questModel = button.DataContext as QuestEditorCheckInfoModel;

            if (questModel != null)
            {
                LoadJobSelector skillSelector = new();
                skillSelector.ShowDialog();
                CharacterJob selectedJob = skillSelector.SelectedJob;
                if (selectedJob != CharacterJob.None)
                {
                    questModel.Jobs.Add(new QuestEditorSkillModelJobIdWrapper()
                    {
                        JobId = (int)selectedJob
                    });
                }
            }
        }

        /// <summary>
        /// Check.img add quest
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectAddQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            QuestEditorCheckInfoModel questModel = button.DataContext as QuestEditorCheckInfoModel;

            if (questModel.CheckType != QuestEditorCheckType.Quest)
                return;

            LoadQuestSelector questSelector = new();
            questSelector.ShowDialog();

            string questId = questSelector.SelectedQuestId;
            if (questId != string.Empty)
            {
                questModel.QuestReqs.Add(new QuestEditorQuestReqModel()
                {
                    QuestId = int.Parse(questId),
                    QuestState = QuestStateType.Completed, // default is 2 for 'act' typically
                });
            }
        }

        /// <summary>
        /// Check.img "infoNumber" select quest
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_selectQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            QuestEditorCheckInfoModel questModel = button.DataContext as QuestEditorCheckInfoModel;

            if (questModel.CheckType != QuestEditorCheckType.InfoNumber)
                return;

            LoadQuestSelector questSelector = new();
            questSelector.ShowDialog();

            string questId = questSelector.SelectedQuestId;
            if (questId != string.Empty)
            {
                questModel.Amount = long.Parse(questId);
            }
        }

        /// <summary>
        /// Check.img "info" add info
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_info_addQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            StackPanel stackPanelParent = FindAncestor<StackPanel>(button);
            var questModel = stackPanelParent.DataContext as QuestEditorCheckInfoModel;

            if (questModel != null)
            {
                questModel.QuestInfo.Add(new QuestEditorCheckQuestInfoModel()
                {
                     Text = "0",
                });
            }
        }

        /// <summary>
        /// Check.img "info" remove info
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_info_remove_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var infoModel = button.DataContext as QuestEditorCheckQuestInfoModel;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorCheckInfoModel;

            if (infoModel != null && questModel != null)
            {
                questModel.QuestInfo.Remove(infoModel);
            }
        }

        /// <summary>
        /// Check.img "infoEx" add info
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_infoEx_addQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            StackPanel stackPanelParent = FindAncestor<StackPanel>(button);
            var questModel = stackPanelParent.DataContext as QuestEditorCheckInfoModel;

            if (questModel != null)
            {
                questModel.QuestInfoEx.Add(new QuestEditorCheckQuestInfoExModel()
                {
                     Value = "1",
                     Condition = 0,
                });
            }
        }

        /// <summary>
        /// Check.img "infoEx" remove info
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_infoEx_remove_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var infoExModel = button.DataContext as QuestEditorCheckQuestInfoExModel;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorCheckInfoModel;

            if (infoExModel != null && questModel != null)
            {
                questModel.QuestInfoEx.Remove(infoExModel);
            }
        }

        /// <summary>
        /// Check.img remove quest
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_removeQuest_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var questActModel = button.DataContext as QuestEditorQuestReqModel;
            ListBox listboxJobParent = FindAncestor<ListBox>(button);
            var questModel = listboxJobParent.DataContext as QuestEditorCheckInfoModel;

            if (questActModel != null && questModel != null)
            {
                questModel.QuestReqs.Remove(questActModel);
            }
        }

        /// <summary>
        /// Check.img fieldEnter select maps
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void botton_check_selectMaps_Click(object sender, RoutedEventArgs e)
        {
            // Get the DataContext of the button
            if (((Button)sender).DataContext is QuestEditorCheckInfoModel checkInfo)
            {
                if (checkInfo.CheckType != QuestEditorCheckType.FieldEnter)
                    return;

                LoadMapSelector mapSelector = new LoadMapSelector();
                mapSelector.ShowDialog();

                string selectedItem = mapSelector.SelectedMap;
                if (selectedItem != string.Empty)
                {
                    checkInfo.SelectedNumbersItem.Add(int.Parse(selectedItem));
                }
            }
        }

        /// <summary>
        /// Check.img fieldEnter delete maps
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_check_fieldEnter_delete_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dataGridRow = FindAncestor<DataGridRow>(button);
            var dataGridCell = FindAncestor<DataGridCell>(button);
            var response = (int)button.DataContext;
            var questModel = dataGridCell.DataContext as QuestEditorCheckInfoModel;

            if (questModel != null)
            {
                // find the listbox first
                // then get the ObservableCollection<QuestEditorSayResponseModel> it is binded to
                ListBox listboxParent = FindAncestor<ListBox>(button);

                questModel.SelectedNumbersItem.Remove(response);
            }
        }
        #endregion

        #region Save and Delete quest
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
                    questWzSubProp.AddProperty(new WzIntProperty("area", (int) quest.Area));
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
            {
                WzSubProperty newSayWzProp = new WzSubProperty(quest.Id.ToString());
                WzSubProperty oldSayWzProp = Program.InfoManager.QuestSays.ContainsKey(quest.Id.ToString()) ? Program.InfoManager.QuestSays[quest.Id.ToString()] : null;

                // start quest
                WzSubProperty startQuestSubProperty = new("0");
                WzSubProperty endQuestSubProperty = new("1");

                newSayWzProp.AddProperty(startQuestSubProperty);
                newSayWzProp.AddProperty(endQuestSubProperty);

                saveQuestSayConversation(quest.SayInfoStartQuest, startQuestSubProperty); // start quest save
                saveQuestSayConversation(quest.SayInfoEndQuest, endQuestSubProperty); // end quest save

                saveQuestStopSayConversation(quest.SayInfoStop_StartQuest, startQuestSubProperty);
                saveQuestStopSayConversation(quest.SayInfoStop_EndQuest, endQuestSubProperty);

                // select any parent node
                WzImage questSayParentImg = oldSayWzProp?.Parent as WzImage; // this may be null, since not all quest contains Say.img sub property
                if (questSayParentImg == null)
                    questSayParentImg = Program.InfoManager.QuestSays.FirstOrDefault().Value?.Parent as WzImage; // select any random "say" sub item and get its parent instead

                if (oldSayWzProp != null)
                    oldSayWzProp.Remove();

                questSayParentImg.AddProperty(newSayWzProp); // add new to the parent

                // replace the old 
                Program.InfoManager.QuestSays[quest.Id.ToString()] = newSayWzProp;

                // flag wz file unsaved
                Program.WzManager.SetWzFileUpdated(questSayParentImg.GetTopMostWzDirectory().Name /* "map" */, questSayParentImg);
            }

            ///////////////////
            ////// Save Act.img
            ///////////////////
            {
                WzSubProperty questAct_SubPropOriginal = Program.InfoManager.QuestActs.ContainsKey(quest.Id.ToString()) ? Program.InfoManager.QuestActs[quest.Id.ToString()] : null; // old quest "Act" to reference
                WzSubProperty questAct_New = new(quest.Id.ToString()); // Create a new one based on the models  <imgdir name="28483">

                WzSubProperty act_startSubProperty = new("0");
                WzSubProperty act_endSubProperty = new("1");
                questAct_New.AddProperty(act_startSubProperty);
                questAct_New.AddProperty(act_endSubProperty);

                SaveActInfo(quest.ActStartInfo, act_startSubProperty, quest);
                SaveActInfo(quest.ActEndInfo, act_endSubProperty, quest);

                // select any parent node
                WzImage questActParentImg;
                if (questAct_SubPropOriginal != null)
                    questActParentImg = questAct_SubPropOriginal?.Parent as WzImage;
                else
                    questActParentImg = Program.InfoManager.QuestActs.FirstOrDefault().Value?.Parent as WzImage; // select any random "act" sub item and get its parent instead

                questActParentImg.AddProperty(questAct_New); // add new to the parent

                // replace the old 
                Program.InfoManager.QuestActs[quest.Id.ToString()] = questAct_New;

                // flag wz file unsaved
                Program.WzManager.SetWzFileUpdated(questActParentImg.GetTopMostWzDirectory().Name /* "map" */, questActParentImg);
            }

            ///////////////////
            ////// Save Check.img
            ///////////////////
            {
                WzSubProperty questCheck_SubPropOriginal = Program.InfoManager.QuestChecks.ContainsKey(quest.Id.ToString()) ? Program.InfoManager.QuestChecks[quest.Id.ToString()] : null; // old quest "Check" to reference
                WzSubProperty questCheck_New = new(quest.Id.ToString()); // Create a new one based on the models  <imgdir name="28483">

                WzSubProperty check_startSubProperty = new("0");
                WzSubProperty check_endSubProperty = new("1");
                questCheck_New.AddProperty(check_startSubProperty);
                questCheck_New.AddProperty(check_endSubProperty);

                SaveCheck(quest.CheckStartInfo, check_startSubProperty, quest);
                SaveCheck(quest.CheckEndInfo, check_endSubProperty, quest);

                // select any parent node
                WzImage questCheckParentImg;
                if (questCheck_SubPropOriginal != null)
                    questCheckParentImg = questCheck_SubPropOriginal?.Parent as WzImage;
                else
                    questCheckParentImg = Program.InfoManager.QuestChecks.FirstOrDefault().Value?.Parent as WzImage; // select any random "act" sub item and get its parent instead

                questCheckParentImg.AddProperty(questCheck_New); // add new to the parent

                // replace the old 
                Program.InfoManager.QuestChecks[quest.Id.ToString()] = questCheck_New;

                // flag wz file unsaved
                Program.WzManager.SetWzFileUpdated(questCheckParentImg.GetTopMostWzDirectory().Name /* "map" */, questCheckParentImg);
            }

            // flag unsaved changes bool
            _unsavedChanges = true;

        }

        private void SaveCheck(ObservableCollection<QuestEditorCheckInfoModel> checkModels, WzSubProperty act01Property, QuestEditorModel quest)
        {
            foreach (QuestEditorCheckInfoModel check in checkModels)
            {
                string originalCheckTypeName = QuestEditorCheckTypeExtensions.ToOriginalString(check.CheckType);

                switch (check.CheckType)
                {
                    case QuestEditorCheckType.Npc:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.Job:
                        {
                            WzSubProperty jobSubProperty = new WzSubProperty(originalCheckTypeName);
                            act01Property.AddProperty(jobSubProperty);

                            for (int i = 0; i < check.Jobs.Count; i++)
                            {
                                jobSubProperty.AddProperty(new WzIntProperty(i.ToString(), check.Jobs[i].JobId));
                            }
                            break;
                        }
                    case QuestEditorCheckType.Quest:
                        {
                            WzSubProperty questSubProperty = new WzSubProperty(originalCheckTypeName);
                            act01Property.AddProperty(questSubProperty);

                            for (int i = 0; i < check.QuestReqs.Count; i++)
                            {
                                var req = check.QuestReqs[i];
                                WzSubProperty reqSubProperty = new WzSubProperty(i.ToString());
                                questSubProperty.AddProperty(reqSubProperty);

                                reqSubProperty.AddProperty(new WzIntProperty("id", req.QuestId));
                                reqSubProperty.AddProperty(new WzIntProperty("state", (int)req.QuestState));
                            }
                            break;
                        }
                    case QuestEditorCheckType.Item:
                        {
                            if (check.SelectedReqItems.Count > 0)
                            {
                                WzSubProperty itemSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(itemSubProperty);

                                for (int i = 0; i < check.SelectedReqItems.Count; i++)
                                {
                                    var reqItem = check.SelectedReqItems[i];
                                    WzSubProperty itemReqProperty = new WzSubProperty(i.ToString());
                                    itemSubProperty.AddProperty(itemReqProperty);

                                    itemReqProperty.AddProperty(new WzIntProperty("id", reqItem.ItemId));
                                    itemReqProperty.AddProperty(new WzIntProperty("count", reqItem.Quantity));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.Info:
                        {
                            WzSubProperty infoSubProperty = new WzSubProperty(originalCheckTypeName);
                            act01Property.AddProperty(infoSubProperty);

                            for (int i = 0; i < check.QuestInfo.Count; i++)
                            {
                                infoSubProperty.AddProperty(new WzStringProperty(i.ToString(), check.QuestInfo[i].Text));
                            }
                            break;
                        }
                    case QuestEditorCheckType.InfoNumber:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.InfoEx:
                        {
                            if (check.QuestInfoEx.Count > 0)
                            {
                                WzSubProperty infoExSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(infoExSubProperty);

                                for (int i = 0; i < check.QuestInfoEx.Count; i++)
                                {
                                    var infoEx = check.QuestInfoEx[i];
                                    WzSubProperty infoExItemProperty = new WzSubProperty(i.ToString());
                                    infoExSubProperty.AddProperty(infoExItemProperty);

                                    infoExItemProperty.AddProperty(new WzStringProperty("value", infoEx.Value));
                                    if (infoEx.Condition != 0)
                                        infoExItemProperty.AddProperty(new WzIntProperty("cond", infoEx.Condition));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.DayByDay:
                        {
                            if (check.Boolean)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, check.Boolean ? 1 : 0));
                            break;
                        }
                    case QuestEditorCheckType.DayOfWeek:
                        {
                            if (check.DayOfWeek != null && check.DayOfWeek.Any(x => x.IsSelected))
                            {
                                WzSubProperty dayOfWeekSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(dayOfWeekSubProperty);

                                int i = 0;
                                foreach (var day in check.DayOfWeek.Where(x => x.IsSelected))
                                {
                                    dayOfWeekSubProperty.AddProperty(new WzStringProperty(i.ToString(), day.DayOfWeek.ToWzString()));
                                    i++;
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.FieldEnter:
                        {
                            if (check.SelectedNumbersItem.Count > 0)
                            {
                                WzSubProperty fieldEnterSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(fieldEnterSubProperty);

                                for (int i = 0; i < check.SelectedNumbersItem.Count; i++)
                                {
                                    fieldEnterSubProperty.AddProperty(new WzIntProperty(i.ToString(), check.SelectedNumbersItem[i]));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.SubJobFlags:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.Premium:
                        {
                            if (check.Boolean)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, check.Boolean ? 1 : 0));
                            break;
                        }
                    case QuestEditorCheckType.Pop:
                    case QuestEditorCheckType.CharmMin:
                    case QuestEditorCheckType.CharismaMin:
                    case QuestEditorCheckType.InsightMin:
                    case QuestEditorCheckType.WillMin:
                    case QuestEditorCheckType.CraftMin:
                    case QuestEditorCheckType.SenseMin:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.Skill:
                        {
                            if (check.Skills.Count > 0)
                            {
                                WzSubProperty skillSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(skillSubProperty);

                                for (int i = 0; i < check.Skills.Count; i++)
                                {
                                    var skill = check.Skills[i];
                                    WzSubProperty skillItemProperty = new WzSubProperty(i.ToString());
                                    skillSubProperty.AddProperty(skillItemProperty);

                                    skillItemProperty.AddProperty(new WzIntProperty("id", skill.Id));
                                    if (skill.SkillLevel != 0)
                                        skillItemProperty.AddProperty(new WzIntProperty("level", skill.SkillLevel));
                                    if (skill.Acquire)
                                        skillItemProperty.AddProperty(new WzIntProperty("acquire", skill.Acquire ? 1 : 0));
                                    if (skill.ConditionType != QuestEditorCheckSkillCondType.None) // custom placeholder
                                        skillItemProperty.AddProperty(new WzStringProperty("levelCondition", skill.ConditionType.ToWzString()));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.Mob:
                        {
                            if (check.MobReqs.Count > 0)
                            {
                                WzSubProperty mobSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(mobSubProperty);

                                for (int i = 0; i < check.MobReqs.Count; i++)
                                {
                                    var mob = check.MobReqs[i];
                                    WzSubProperty mobItemProperty = new WzSubProperty(i.ToString());
                                    mobSubProperty.AddProperty(mobItemProperty);

                                    mobItemProperty.AddProperty(new WzIntProperty("id", mob.Id));
                                    mobItemProperty.AddProperty(new WzIntProperty("count", mob.Count));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.EndMeso:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.Pet:
                        {
                            if (check.SelectedNumbersItem.Count > 0)
                            {
                                WzSubProperty petSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(petSubProperty);

                                for (int i = 0; i < check.SelectedNumbersItem.Count; i++)
                                {
                                    WzSubProperty petItemProperty = new WzSubProperty(i.ToString());
                                    petSubProperty.AddProperty(petItemProperty);
                                    petItemProperty.AddProperty(new WzIntProperty("id", check.SelectedNumbersItem[i]));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.PetTamenessMin:
                    case QuestEditorCheckType.PetTamenessMax:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.PetRecallLimit:
                    case QuestEditorCheckType.PetAutoSpeakingLimit:
                        {
                            if (check.Boolean)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, check.Boolean ? 1 : 0));
                            break;
                        }
                    case QuestEditorCheckType.TamingMobLevelMin:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.WeeklyRepeat:
                        {
                            if (check.Boolean)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, check.Boolean ? 1 : 0));
                            break;
                        }
                    case QuestEditorCheckType.Married:
                        {
                            if (check.Boolean)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, check.Boolean ? 1 : 0));
                            break;
                        }
                    case QuestEditorCheckType.ExceptBuff:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzStringProperty(originalCheckTypeName, check.Amount.ToString()));
                            break;
                        }
                    case QuestEditorCheckType.EquipAllNeed:
                    case QuestEditorCheckType.EquipSelectNeed:
                        {
                            if (check.SelectedNumbersItem.Count > 0)
                            {
                                WzSubProperty equipSubProperty = new WzSubProperty(originalCheckTypeName);
                                act01Property.AddProperty(equipSubProperty);

                                for (int i = 0; i < check.SelectedNumbersItem.Count; i++)
                                {
                                    equipSubProperty.AddProperty(new WzIntProperty(i.ToString(), check.SelectedNumbersItem[i]));
                                }
                            }
                            break;
                        }
                    case QuestEditorCheckType.WorldMin:
                    case QuestEditorCheckType.WorldMax:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzStringProperty(originalCheckTypeName, check.Amount.ToString()));
                            break;
                        }
                    case QuestEditorCheckType.LvMin:
                    case QuestEditorCheckType.LvMax:
                    case QuestEditorCheckType.Interval:
                        {
                            if (check.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, (int)check.Amount));
                            break;
                        }
                    case QuestEditorCheckType.NormalAutoStart:
                        {
                            if (check.Boolean)
                                act01Property.AddProperty(new WzIntProperty(originalCheckTypeName, check.Boolean ? 1 : 0));
                            break;
                        }
                    case QuestEditorCheckType.Start:
                    case QuestEditorCheckType.End:
                    case QuestEditorCheckType.Start_t:
                    case QuestEditorCheckType.End_t:
                        {
                            if (check.Date != DateTime.MinValue)
                            {
                                WzStringProperty dateProp = new WzStringProperty(originalCheckTypeName, "0");
                                dateProp.SetDateValue(check.Date);
                                act01Property.AddProperty(dateProp);
                            }
                            break;
                        }
                    case QuestEditorCheckType.Startscript:
                    case QuestEditorCheckType.Endscript:
                        {
                            if (!string.IsNullOrEmpty(check.Text))
                                act01Property.AddProperty(new WzStringProperty(originalCheckTypeName, check.Text));
                            break;
                        }
                    default:
                        {
                            string error = string.Format("[QuestEditor] SaveCheck() Unhandled CheckType save. Name='{0}', QuestId={1}", originalCheckTypeName, quest.Id);
                            ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                            break;
                        }
                }
            }
        }

        private void SaveActInfo(ObservableCollection<QuestEditorActInfoModel> actInfo, WzSubProperty act01Property, QuestEditorModel quest)
        {
            foreach (QuestEditorActInfoModel act in actInfo)
            {
                switch (act.ActType)
                {
                    case QuestEditorActType.Item:
                        {
                            WzSubProperty actSubItemProperty = new WzSubProperty("item");
                            act01Property.AddProperty(actSubItemProperty);

                            int i = 0;
                            foreach (QuestEditorActInfoRewardModel reward in act.SelectedRewardItems)
                            {
                                WzSubProperty actSubItemRewardProperty = new WzSubProperty(i.ToString()); // "0", "1", "2", "3"
                                actSubItemProperty.AddProperty(actSubItemRewardProperty);

                                // item properties
                                actSubItemRewardProperty.AddProperty(new WzIntProperty("id", reward.ItemId)); // id
                                actSubItemRewardProperty.AddProperty(new WzIntProperty("count", reward.Quantity)); // count

                                if (reward.ExpireDate != DateTime.MinValue) // date expire none = {1/1/0001 12:00:00 AM}
                                {
                                    WzStringProperty strDateProp = new WzStringProperty("dateExpire", "0");
                                    strDateProp.SetDateValue(reward.ExpireDate);
                                    actSubItemRewardProperty.AddProperty(strDateProp);
                                }

                                if (reward.PotentialGrade != QuestEditorActInfoPotentialType.Normal) // potential
                                {
                                    WzStringProperty strPotProp = new WzStringProperty("potentialGrade", reward.PotentialGrade.ToWzString());
                                    actSubItemRewardProperty.AddProperty(strPotProp);
                                }

                                if (reward.Job != 0) // job bitfield
                                {
                                    actSubItemRewardProperty.AddProperty(new WzIntProperty("job", reward.Job));
                                }

                                if (reward.JobEx != 0) // jobEx
                                {
                                    actSubItemRewardProperty.AddProperty(new WzIntProperty("JobEx", reward.JobEx));
                                }

                                actSubItemRewardProperty.AddProperty(new WzIntProperty("period", reward.Period));

                                if (reward.Prop != QuestEditorActInfoRewardPropTypeModel.AlwaysGiven) // 0
                                {
                                    actSubItemRewardProperty.AddProperty(new WzIntProperty("prop", (int)reward.Prop));
                                }

                                if (reward.Gender != CharacterGenderType.Both)
                                {
                                    actSubItemRewardProperty.AddProperty(new WzIntProperty("gender", (int)reward.Gender));
                                }
                                i++;
                            }
                            break;
                        }
                    case QuestEditorActType.Quest:
                        {
                            WzSubProperty questSubProperty = new WzSubProperty("quest");
                            act01Property.AddProperty(questSubProperty);

                            for (int i = 0; i < act.QuestReqs.Count; i++)
                            {
                                var req = act.QuestReqs[i];
                                WzSubProperty reqSubProperty = new WzSubProperty(i.ToString());
                                questSubProperty.AddProperty(reqSubProperty);

                                reqSubProperty.AddProperty(new WzIntProperty("id", req.QuestId));
                                reqSubProperty.AddProperty(new WzIntProperty("state", (int)req.QuestState));
                            }
                            break;
                        }
                    case QuestEditorActType.NextQuest:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty("nextQuest", (int) act.Amount));
                            break;
                        }
                    case QuestEditorActType.Npc:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty("npc", (int) act.Amount));
                            break;
                        }
                    case QuestEditorActType.NpcAct:
                        {
                            act01Property.AddProperty(new WzStringProperty("npcAct", act.Text));
                            break;
                        }
                    case QuestEditorActType.LvMin:
                        {
                            act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int) act.Amount));
                            break;
                        }
                    case QuestEditorActType.LvMax:
                        {
                            act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.Interval:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.Start:
                        {
                            WzStringProperty dateProp = new WzStringProperty(act.ActType.ToOriginalString(), "0");
                            dateProp.SetDateValue(act.Date);
                            act01Property.AddProperty(dateProp);
                            break;
                        }
                    case QuestEditorActType.End:
                        {
                            WzStringProperty dateProp = new WzStringProperty(act.ActType.ToOriginalString(), "0");
                            dateProp.SetDateValue(act.Date);
                            act01Property.AddProperty(dateProp);
                            break;
                        }
                    case QuestEditorActType.Exp:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int) act.Amount));
                            break;
                        }
                    case QuestEditorActType.Money:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.Info:
                        {
                            act01Property.AddProperty(new WzStringProperty(act.ActType.ToOriginalString(), act.Text));
                            break;
                        }
                    case QuestEditorActType.Pop:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.FieldEnter:
                        {
                            WzSubProperty fieldEnterSubProperty = new WzSubProperty(act.ActType.ToOriginalString());
                            act01Property.AddProperty(fieldEnterSubProperty);

                            for (int i = 0; i < act.SelectedNumbersItem.Count; i++)
                            {
                                fieldEnterSubProperty.AddProperty(new WzIntProperty(i.ToString(), act.SelectedNumbersItem[i]));
                            }
                            break;
                        }
                    case QuestEditorActType.PetTameness:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int) act.Amount));
                            break;
                        }
                    case QuestEditorActType.PetSpeed:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.PetSkill:
                        {
                            act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.Sp:
                        {
                            WzSubProperty spSubProperty = new WzSubProperty(act.ActType.ToOriginalString());
                            act01Property.AddProperty(spSubProperty);

                            for (int i = 0; i < act.SP.Count; i++)
                            {
                                WzSubProperty spItemSubProperty = new WzSubProperty(i.ToString());
                                spSubProperty.AddProperty(spItemSubProperty);

                                var spModel = act.SP[i];
                                spItemSubProperty.AddProperty(new WzIntProperty("sp_value", spModel.SPValue));

                                WzSubProperty jobSubProperty = new WzSubProperty("job");
                                spItemSubProperty.AddProperty(jobSubProperty);

                                for (int j = 0; j < spModel.Jobs.Count; j++)
                                {
                                    jobSubProperty.AddProperty(new WzIntProperty(j.ToString(), spModel.Jobs[j].JobId));
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.Job:
                        {
                            WzSubProperty jobSubProperty = new WzSubProperty(act.ActType.ToOriginalString());
                            act01Property.AddProperty(jobSubProperty);

                            for (int i = 0; i < act.JobsReqs.Count; i++)
                            {
                                jobSubProperty.AddProperty(new WzIntProperty(i.ToString(), act.JobsReqs[i].JobId));
                            }
                            break;
                        }
                    case QuestEditorActType.Skill:
                        {
                            WzSubProperty skillSubProperty = new WzSubProperty(act.ActType.ToOriginalString());
                            act01Property.AddProperty(skillSubProperty);

                            for (int i = 0; i < act.SkillsAcquire.Count; i++)
                            {
                                WzSubProperty skillItemSubProperty = new WzSubProperty(i.ToString());
                                skillSubProperty.AddProperty(skillItemSubProperty);

                                var skillModel = act.SkillsAcquire[i];
                                skillItemSubProperty.AddProperty(new WzIntProperty("id", skillModel.Id));
                                if (skillModel.SkillLevel != 0)
                                    skillItemSubProperty.AddProperty(new WzIntProperty("skillLevel", skillModel.SkillLevel));
                                if (skillModel.MasterLevel != 0)
                                    skillItemSubProperty.AddProperty(new WzIntProperty("masterLevel", skillModel.MasterLevel));
                                if (skillModel.OnlyMasterLevel)
                                    skillItemSubProperty.AddProperty(new WzIntProperty("onlyMasterLevel", skillModel.OnlyMasterLevel ? 1 : 0));
                                if (skillModel.Acquire == -1) // only save 'acquire' if value is -1
                                    skillItemSubProperty.AddProperty(new WzShortProperty("acquire", skillModel.Acquire));

                                if (skillModel.JobIds.Count > 0)
                                {
                                    WzSubProperty jobSubProperty = new WzSubProperty("job");
                                    skillItemSubProperty.AddProperty(jobSubProperty);

                                    for (int j = 0; j < skillModel.JobIds.Count; j++)
                                    {
                                        jobSubProperty.AddProperty(new WzIntProperty(j.ToString(), skillModel.JobIds[j].JobId));
                                    }
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.CraftEXP:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int) act.Amount));
                            break;
                        }
                    case QuestEditorActType.CharmEXP:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.CharismaEXP:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.InsightEXP:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.WillEXP:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.SenseEXP:
                        {
                            if (act.Amount != 0)
                                act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.Message_Map:
                        {
                            if (!string.IsNullOrEmpty(act.Text))
                            {
                                act01Property.AddProperty(new WzStringProperty("message", act.Text));
                            }

                            if (act.SelectedNumbersItem.Count > 0)
                            {
                                WzSubProperty mapSubProperty = new WzSubProperty("map");
                                act01Property.AddProperty(mapSubProperty);

                                for (int i = 0; i < act.SelectedNumbersItem.Count; i++)
                                {
                                    mapSubProperty.AddProperty(new WzIntProperty(i.ToString(), act.SelectedNumbersItem[i]));
                                }
                            }
                            break;
                        }
                    case QuestEditorActType.BuffItemId:
                        {
                            act01Property.AddProperty(new WzIntProperty(act.ActType.ToOriginalString(), (int)act.Amount));
                            break;
                        }
                    case QuestEditorActType.Conversation0123:
                        {

                            saveQuestSayConversation(act.ActConversationStart, act01Property); // start quest save
                            saveQuestStopSayConversation(act.ActConversationStop, act01Property);
                           
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Saves this list of conversations to Say.img
        /// </summary>
        /// <param name="questSayItems"></param>
        /// <param name="questSaySubProperty"></param>
        private void saveQuestSayConversation(ObservableCollection<QuestEditorSayModel> questSayItems, WzSubProperty questSaySubProperty)
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
