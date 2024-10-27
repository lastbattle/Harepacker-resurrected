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


using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorModel : INotifyPropertyChanged
    {
        // wz based properties
        private int _id;
        private string _name = string.Empty;
        private string _parent = string.Empty;
        private string _summary = string.Empty, _questInfoDesc0 = string.Empty, _questInfoDesc1 = string.Empty, _questInfoDesc2 = string.Empty;
        private QuestAreaCodeType _area;
        private bool _blocked;
        private int _order;
        private bool _autoPreComplete, _autoComplete, _autoStart;
        private bool _selectedMob, _autoCancel, _oneShot;
        private bool _disableAtStartTab, _disableAtPerformTab, _disableAtCompleteTab;
        private string _demandSummary = string.Empty, _rewardSummary = string.Empty;

        private string _showLayerTag = string.Empty;

        // custom properties for UI
        public bool _isMedal;

        // Say
        private readonly ObservableCollection<QuestEditorSayModel> _sayInfoStartQuest = new ObservableCollection<QuestEditorSayModel>();
        private readonly ObservableCollection<QuestEditorSayModel> _sayInfoEndQuest = new ObservableCollection<QuestEditorSayModel>();

        private readonly ObservableCollection<QuestEditorSayEndQuestModel> _sayInfoStop_StartQuest = new ObservableCollection<QuestEditorSayEndQuestModel>();
        private readonly ObservableCollection<QuestEditorSayEndQuestModel> _sayInfoStop_EndQuest = new ObservableCollection<QuestEditorSayEndQuestModel>();
        private bool _isAskConversation;

        // Act
        private readonly ObservableCollection<QuestEditorActInfoModel> _actStartInfo = new ObservableCollection<QuestEditorActInfoModel>();
        private readonly ObservableCollection<QuestEditorActInfoModel> _actEndInfo = new ObservableCollection<QuestEditorActInfoModel>();

        // Check
        private readonly ObservableCollection<QuestEditorCheckInfoModel> _checkStartInfo = new ObservableCollection<QuestEditorCheckInfoModel>();
        private readonly ObservableCollection<QuestEditorCheckInfoModel> _checkEndInfo = new ObservableCollection<QuestEditorCheckInfoModel>();

        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditorModel()
        {
            _sayInfoStartQuest.CollectionChanged += ActConversation_StartStop_CollectionChanged;
            _sayInfoEndQuest.CollectionChanged += ActConversation_StartStop_CollectionChanged;
        }

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                OnPropertyChanged(nameof(Parent));
            }
        }

        public QuestAreaCodeType Area
        {
            get => _area;
            set
            {
                _area = value;
                OnPropertyChanged(nameof(Area));
            }
        }

        public bool Blocked
        {
            get => _blocked;
            set
            {
                _blocked = value;
                OnPropertyChanged(nameof(Blocked));
            }
        }

        public int Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged(nameof(Order));
            }
        }

        public bool IsMedal
        {
            get => _isMedal;
            set
            {
                _isMedal = value;
                OnPropertyChanged(nameof(IsMedal));
            }
        }

        public string ShowLayerTag
        {
            get => _showLayerTag;
            set
            {
                _showLayerTag = value;
                OnPropertyChanged(nameof(ShowLayerTag));
            }
        }


        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                _autoStart = value;
                OnPropertyChanged(nameof(AutoStart));
            }
        }

        public bool AutoPreComplete
        {
            get => _autoPreComplete;
            set
            {
                _autoPreComplete = value;
                OnPropertyChanged(nameof(AutoPreComplete));
            }
        }

        public bool AutoComplete
        {
            get => _autoComplete;
            set
            {
                _autoComplete = value;
                OnPropertyChanged(nameof(AutoComplete));
            }
        }

        public bool SelectedMob
        {
            get => _selectedMob;
            set
            {
                _selectedMob = value;
                OnPropertyChanged(nameof(SelectedMob));
            }
        }

        public bool AutoCancel
        {
            get => _autoCancel;
            set
            {
                _autoCancel = value;
                OnPropertyChanged(nameof(AutoCancel));
            }
        }

        public bool OneShot
        {
            get => _oneShot;
            set
            {
                _oneShot = value;
                OnPropertyChanged(nameof(OneShot));
            }
        }

        public bool DisableAtStartTab
        {
            get => _disableAtStartTab;
            set
            {
                _disableAtStartTab = value;
                OnPropertyChanged(nameof(DisableAtStartTab));
            }
        }
        public bool DisableAtPerformTab
        {
            get => _disableAtPerformTab;
            set
            {
                _disableAtPerformTab = value;
                OnPropertyChanged(nameof(DisableAtPerformTab));
            }
        }
        public bool DisableAtCompleteTab
        {
            get => _disableAtCompleteTab;
            set
            {
                _disableAtCompleteTab = value;
                OnPropertyChanged(nameof(DisableAtCompleteTab));
            }
        }

        public string DemandSummary
        {
            get => _demandSummary;
            set
            {
                _demandSummary = value;
                OnPropertyChanged(nameof(DemandSummary));
            }
        }

        public string RewardSummary
        {
            get => _rewardSummary;
            set
            {
                _rewardSummary = value;
                OnPropertyChanged(nameof(RewardSummary));
            }
        }

        public string Summary
        {
            get => _summary;
            set
            {
                _summary = value;
                OnPropertyChanged(nameof(Summary));
            }
        }

        /// <summary>
        /// The quest description
        /// </summary>
        public string QuestInfoDesc0
        {
            get => _questInfoDesc0;
            set
            {
                _questInfoDesc0 = value;
                OnPropertyChanged(nameof(RewardSummary));
            }
        }
        public string QuestInfoDesc1
        {
            get => _questInfoDesc1;
            set
            {
                _questInfoDesc1 = value;
                OnPropertyChanged(nameof(QuestInfoDesc1));
            }
        }
        public string QuestInfoDesc2
        {
            get => _questInfoDesc2;
            set
            {
                _questInfoDesc2 = value;
                OnPropertyChanged(nameof(QuestInfoDesc2));
            }
        }

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
        /// Say.img
        /// </summary>
        public ObservableCollection<QuestEditorSayModel> SayInfoStartQuest
        {
            get => _sayInfoStartQuest;
            private set
            {
            }
        }

        public ObservableCollection<QuestEditorSayModel> SayInfoEndQuest
        {
            get => _sayInfoEndQuest;
            private set
            {
            }
        }

        public ObservableCollection<QuestEditorSayEndQuestModel> SayInfoStop_StartQuest
        {
            get => _sayInfoStop_StartQuest;
            private set
            {
            }
        }

        public ObservableCollection<QuestEditorSayEndQuestModel> SayInfoStop_EndQuest
        {
            get => _sayInfoStop_EndQuest;
            private set
            {
            }
        }

        /// <summary>
        /// Determines if this conversation is 'ask' whereby the user selects from a list of options.
        /// </summary>
        public bool IsAskConversation
        {
            get => _isAskConversation;
            set
            {
                _isAskConversation = value;
                OnPropertyChanged(nameof(IsAskConversation));
            }
        }

        /// <summary>
        /// ActInfo.img
        /// </summary>
        public ObservableCollection<QuestEditorActInfoModel> ActStartInfo
        {
            get => _actStartInfo;
            private set
            {
            }
        }
        public ObservableCollection<QuestEditorActInfoModel> ActEndInfo
        {
            get => _actEndInfo;
            private set
            {
            }
        }

        /// <summary>
        /// Check.img start "0"
        /// </summary>
        public ObservableCollection<QuestEditorCheckInfoModel> CheckStartInfo
        {
            get => _checkStartInfo;
            private set
            {
            }
        }
        /// <summary>
        /// Check.img end "1"
        /// </summary>
        public ObservableCollection<QuestEditorCheckInfoModel> CheckEndInfo
        {
            get => _checkEndInfo;
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

                // a new item has been added at this point

                QuestEditorSayModel newItemAdded = e.NewItems[0] as QuestEditorSayModel;
                if (collection.Count > 1) // only if more than 1
                {
                    QuestEditorSayModel lastItem = collection[collection.Count - 2];

                    if (lastItem.IsYesNoConversation) // last time is YesNo, disallow more items to be added
                    {
                        // Remove the newly added item
                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                        {
                            collection.Remove(newItemAdded);

                            /*if (sender == _sayInfoStop_StartQuest)
                                OnPropertyChanged(nameof(SayInfoStop_StartQuest));
                            else if (sender == _sayInfoStop_EndQuest)
                                OnPropertyChanged(nameof(SayInfoStop_EndQuest));*/
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