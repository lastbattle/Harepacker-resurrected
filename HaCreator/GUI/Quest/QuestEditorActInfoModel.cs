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

using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorActInfoModel : INotifyPropertyChanged
    {
        private bool _bLoadingFromFile = false;
        /// <summary>
        /// Prevent input validation from working while loading from files. 
        /// </summary>
        public bool IsLoadingFromFile
        {
            get { return _bLoadingFromFile; }
            set { this._bLoadingFromFile = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditorActInfoModel()
        {
            _actConversationStart.CollectionChanged += ActConversation_StartStop_CollectionChanged;
            _actConversationStop.CollectionChanged += ActConversation_StartStop_CollectionChanged;
        }

        private ObservableCollection<QuestEditorActInfoRewardModel> _selectedRewardItems = new();
        public ObservableCollection<QuestEditorActInfoRewardModel> SelectedRewardItems
        {
            get { return _selectedRewardItems; }
            set { 
                this._selectedRewardItems = value;
                OnPropertyChanged(nameof(SelectedRewardItems));
            }
        }

        private ObservableCollection<int> _selectedNumbersItem = new ObservableCollection<int>();
        public ObservableCollection<int> SelectedNumbersItem
        {
            get { return _selectedNumbersItem; }
            set
            {
                this._selectedNumbersItem = value;
                OnPropertyChanged(nameof(SelectedNumbersItem));
            }
        }

        private QuestEditorActType _actType = QuestEditorActType.Exp;
        public QuestEditorActType ActType
        {
            get => _actType;
            set
            {
                if (_actType != value)
                {
                    _actType = value;
                    OnPropertyChanged(nameof(ActType));
                }
            }
        }

        private long _amount;
        /// <summary>
        /// The amount, it may be EXP, fame, mesos, or NPC ID
        /// dual-use
        /// </summary>
        public long Amount
        {
            get => _amount;
            set
            {
                if (_amount != value)
                {
                    long setAmount = value;

                    // input validation
                    if (ActType == QuestEditorActType.LvMin)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                        if (setAmount >= CharacterStats.MAX_LEVEL)
                            setAmount = CharacterStats.MAX_LEVEL;
                    }
                    else if (ActType == QuestEditorActType.LvMax)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                        if (setAmount >= CharacterStats.MAX_LEVEL)
                            setAmount = CharacterStats.MAX_LEVEL;
                    }
                    else if (ActType == QuestEditorActType.NextQuest)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                    }
                    else if (ActType == QuestEditorActType.PetSpeed || ActType == QuestEditorActType.PetTameness)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                    }
                    else if (ActType == QuestEditorActType.Interval)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                    }
                    if (setAmount != _amount)
                    {
                        _amount = setAmount;
                        OnPropertyChanged(nameof(Amount));
                    }
                }
            }
        }

        private string _text;
        /// <summary>
        /// The Text, it may be EXP, fame, mesos, or NPC ID
        /// dual-use
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        private DateTime _date;
        /// <summary>
        /// DateTime for 'start' 'end'
        /// </summary>
        public DateTime Date
        {
            get => _date;
            set
            {
                if (_date != value)
                {
                    _date = value;
                    OnPropertyChanged(nameof(Date));
                }
            }
        }

        private ObservableCollection<QuestEditorActSpModel> _sp = new ObservableCollection<QuestEditorActSpModel>();
        public ObservableCollection<QuestEditorActSpModel> SP
        {
            get { return _sp; }
            set
            {
                this._sp = value;
                OnPropertyChanged(nameof(SP));
            }
        }

        private ObservableCollection<QuestEditorActSkillModel> _skillsAcquire = new ObservableCollection<QuestEditorActSkillModel>();
        public ObservableCollection<QuestEditorActSkillModel> SkillsAcquire
        {
            get { return _skillsAcquire; }
            set
            {
                this._skillsAcquire = value;
                OnPropertyChanged(nameof(SkillsAcquire));
            }
        }

        private ObservableCollection<QuestEditorSkillModelJobIdWrapper> _jobsReqs = new ObservableCollection<QuestEditorSkillModelJobIdWrapper>();
        public ObservableCollection<QuestEditorSkillModelJobIdWrapper> JobsReqs
        {
            get { return _jobsReqs; }
            set
            {
                this._jobsReqs = value;
                OnPropertyChanged(nameof(JobsReqs));
            }
        }

        private ObservableCollection<QuestEditorQuestReqModel> _questReqs = new ObservableCollection<QuestEditorQuestReqModel>();
        public ObservableCollection<QuestEditorQuestReqModel> QuestReqs
        {
            get { return _questReqs; }
            set
            {
                this._questReqs = value;
                OnPropertyChanged(nameof(QuestReqs));
            }
        }

        // Say
        private readonly ObservableCollection<QuestEditorSayModel> _actConversationStart = new ObservableCollection<QuestEditorSayModel>();
        /// <summary>
        /// Conversation
        /// </summary>
        public ObservableCollection<QuestEditorSayModel> ActConversationStart
        {
            get => _actConversationStart;
            private set
            {
            }
        }
        private readonly ObservableCollection<QuestEditorSayEndQuestModel> _actConversationStop = new ObservableCollection<QuestEditorSayEndQuestModel>();
        /// <summary>
        /// Conversation
        /// </summary>
        public ObservableCollection<QuestEditorSayEndQuestModel> ActConversationStop
        {
            get => _actConversationStop;
            private set
            {
            }
        }

        #region Events
        /// <summary>
        /// On act conversation collection changed
        /// for input validation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ActConversation_StartStop_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_bLoadingFromFile)
                return;

            var collection = sender as ObservableCollection<QuestEditorSayModel>;
            if (collection == null)
                return;
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems.Count == 0)
                    return;

                QuestEditorSayModel newItemAdded = e.NewItems[0] as QuestEditorSayModel;
                if (collection.Count > 0) // only if more than 1
                {
                    QuestEditorSayModel lastItem = collection[collection.Count - 2];

                    if (lastItem.IsYesNoConversation) // last time is YesNo, disallow more items to be added
                    {
                        // Remove the newly added item
                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                        {
                            collection.Remove(newItemAdded);

                            /*if (sender == _actConversationStart)
                                OnPropertyChanged(nameof(ActConversationStart));
                            else if (sender == _actConversationStop)
                                OnPropertyChanged(nameof(ActConversationStop));*/
                        }));
                    }
                }
            }
        }
        #endregion

        #region Property Changed Event
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
