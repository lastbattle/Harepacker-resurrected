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
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorCheckInfoModel : INotifyPropertyChanged
    {

        /// <summary>
        /// The default constructor created by XAML
        /// </summary>
        public QuestEditorCheckInfoModel()
        {
            CheckType = QuestEditorCheckType.Null;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditorCheckInfoModel(QuestEditorCheckType checkType)
        {
            CheckType = checkType;
        }

        private QuestEditorCheckType _checkType = QuestEditorCheckType.Null;
        public QuestEditorCheckType CheckType
        {
            get => _checkType;
            set
            {
                if (_checkType != value)
                {
                    _checkType = value;
                    OnPropertyChanged(nameof(CheckType));
                }
            }
        }

        private long _amount;
        /// <summary>
        /// The amount, it may be EXP, fame, mesos, or NPC ID, infoNumber
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
                    if (_checkType == QuestEditorCheckType.CharismaMin || _checkType == QuestEditorCheckType.CharmMin || _checkType == QuestEditorCheckType.CraftMin ||
                        _checkType == QuestEditorCheckType.WillMin || _checkType == QuestEditorCheckType.InsightMin || _checkType == QuestEditorCheckType.SenseMin)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                    }
                    else if (_checkType == QuestEditorCheckType.LvMin || _checkType == QuestEditorCheckType.LvMax)
                    {
                        if (setAmount < 0)
                            setAmount = 0;
                        else if (setAmount > CharacterStats.MAX_LEVEL)
                            setAmount = CharacterStats.MAX_LEVEL;
                    }
                    else if (_checkType == QuestEditorCheckType.WorldMin || _checkType == QuestEditorCheckType.WorldMax)
                    {
                        if (setAmount < 0) // world id, TODO: better UX
                            setAmount = 0;
                        else if (setAmount > 200)
                            setAmount = 200;
                    }
                    else if (_checkType == QuestEditorCheckType.PetTamenessMin || _checkType == QuestEditorCheckType.PetTamenessMax)
                    {
                        if (setAmount < 0) // 
                            setAmount = 0;
                        else if (setAmount > 30000)
                            setAmount = 30000;
                    }
                    else if (_checkType == QuestEditorCheckType.TamingMobLevelMin)
                    {
                        if (setAmount < 0) // 
                            setAmount = 0;
                    }
                    else if (_checkType == QuestEditorCheckType.EndMeso)
                    {
                        if (setAmount < 0) // 
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

        private bool _boolean;
        /// <summary>
        /// The bool, it may be EXP, fame, mesos, or NPC ID
        /// dual-use
        /// </summary>
        public bool Boolean
        {
            get => _boolean;
            set
            {
                if (_boolean != value)
                {
                    _boolean = value;
                    OnPropertyChanged(nameof(Boolean));
                }
            }
        }

        private DateTime _date;
        /// <summary>
        /// DateTime for 'start_t' 'end_t'
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

        private ObservableCollection<int> _selectedNumbersItem = new();
        public ObservableCollection<int> SelectedNumbersItem
        {
            get { return _selectedNumbersItem; }
            set
            {
                this._selectedNumbersItem = value;
                OnPropertyChanged(nameof(SelectedNumbersItem));
            }
        }

        private ObservableCollection<QuestEditorCheckItemReqModel> _selectedReqItems = new();
        public ObservableCollection<QuestEditorCheckItemReqModel> SelectedReqItems
        {
            get { return _selectedReqItems; }
            set
            {
                this._selectedReqItems = value;
                OnPropertyChanged(nameof(SelectedReqItems));
            }
        }

        private ObservableCollection<QuestEditorCheckSkillModel> _skills = new();
        public ObservableCollection<QuestEditorCheckSkillModel> Skills
        {
            get { return _skills; }
            set
            {
                this._skills = value;
                OnPropertyChanged(nameof(Skills));
            }
        }

        private ObservableCollection<QuestEditorSkillModelJobIdWrapper> _jobs = new();
        public ObservableCollection<QuestEditorSkillModelJobIdWrapper> Jobs
        {
            get { return _jobs; }
            set
            {
                this._jobs = value;
                OnPropertyChanged(nameof(Jobs));
            }
        }


        private ObservableCollection<QuestEditorQuestReqModel> _questReqs = new();
        public ObservableCollection<QuestEditorQuestReqModel> QuestReqs
        {
            get { return _questReqs; }
            set
            {
                this._questReqs = value;
                OnPropertyChanged(nameof(QuestReqs));
            }
        }

        private ObservableCollection<QuestEditorCheckDayOfWeekModel> _dayOfWeek =
            [
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Monday),
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Tuesday),
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Wednesday),
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Thursday),
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Friday),
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Saturday),
                new QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType.Sunday)
            ];
        public ObservableCollection<QuestEditorCheckDayOfWeekModel> DayOfWeek
        {
            get { return _dayOfWeek; }
            set
            {
                this._dayOfWeek = value;
                OnPropertyChanged(nameof(DayOfWeek));
            }
        }

        private ObservableCollection<QuestEditorCheckMobModel> _mobReqs = new();
        public ObservableCollection<QuestEditorCheckMobModel> MobReqs
        {
            get { return _mobReqs; }
            set
            {
                this._mobReqs = value;
                OnPropertyChanged(nameof(MobReqs));
            }
        }

        private ObservableCollection<QuestEditorCheckQuestInfoModel> _questInfo = new();
        public ObservableCollection<QuestEditorCheckQuestInfoModel> QuestInfo
        {
            get { return _questInfo; }
            set
            {
                this._questInfo = value;
                OnPropertyChanged(nameof(QuestInfo));
            }
        }

        private ObservableCollection<QuestEditorCheckQuestInfoExModel> _questInfoEx = new();
        public ObservableCollection<QuestEditorCheckQuestInfoExModel> QuestInfoEx
        {
            get { return _questInfoEx; }
            set
            {
                this._questInfoEx = value;
                OnPropertyChanged(nameof(QuestInfoEx));
            }
        }


        #region Misc
        public bool IsPreBBDataWzFormat
        {
            get { return Program.WzManager.IsPreBBDataWzFormat; }
            private set { }
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