using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorCheckSkillModel : INotifyPropertyChanged
    {
        private int _id = 0;
        public int Id
        {
            get { return _id; }
            set
            {
                this._id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        /// <summary>
        /// <int name="level" value="5"/>
        /// </summary>
        private int _skillLevel = 0;
        public int SkillLevel
        {
            get { return _skillLevel; }
            set
            {
                if (_skillLevel != value)
                {
                    int setAmount = value;

                    // input validation
                    if (setAmount < 0)
                        setAmount = 0;
                    else if (setAmount > 50)
                        setAmount = 50;

                    this._skillLevel = value;
                    OnPropertyChanged(nameof(SkillLevel));
                }
            }
        }

        private bool _acquire = false;
        /// <summary>
        /// Acquire 
        /// This is different than 'acquire' from Act.img. 
        /// </summary>
        public bool Acquire
        {
            get { return _acquire; }
            set
            {
                this._acquire = value;
                OnPropertyChanged(nameof(Acquire));
            }
        }

        private QuestEditorCheckSkillCondType _conditionType = QuestEditorCheckSkillCondType.None;
        /// <summary>
        /// <string name="levelCondition" value="이상"/>
        /// </summary>
        public QuestEditorCheckSkillCondType ConditionType
        {
            get { return _conditionType; }
            set
            {
                this._conditionType = value;
                OnPropertyChanged(nameof(ConditionType));
            }
        }

        #region Property Changed Event
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}