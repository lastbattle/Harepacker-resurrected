using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorActSkillModel : INotifyPropertyChanged
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

        private int _skillLevel = 0;
        public int SkillLevel
        {
            get { return _skillLevel; }
            set
            {
                if (_masterLevel != value)
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

        private int _masterLevel = 0;
        public int MasterLevel
        {
            get { return _masterLevel; }
            set
            {
                if (_masterLevel != value)
                {
                    int setAmount = value;

                    // input validation
                    if (setAmount < 0)
                        setAmount = 0;
                    else if (setAmount > 50)
                        setAmount = 50;

                    this._masterLevel = setAmount;
                    OnPropertyChanged(nameof(MasterLevel));
                }
            }
        }

        private bool _onlyMasterLevel = false;
        public bool OnlyMasterLevel
        {
            get { return _onlyMasterLevel; }
            set
            {
                this._onlyMasterLevel = value;
                OnPropertyChanged(nameof(OnlyMasterLevel));
            }
        }

        private short _acquire = 0;
        /// <summary>
        /// Acquire (kinda 'remove skill', badly named in the wz files)  <short name="acquire" value="65535"/> == -1
        /// </summary>
        public short Acquire // <imgdir name="6034">
        {
            get { return _acquire; }
            set
            {
                this._acquire = value;
                OnPropertyChanged(nameof(Acquire));
            }
        }

        private ObservableCollection<QuestEditorActSkillModelJobIdWrapper> _jobIds = new ObservableCollection<QuestEditorActSkillModelJobIdWrapper>();
        public ObservableCollection<QuestEditorActSkillModelJobIdWrapper> JobIds
        {
            get { return _jobIds; }
            set
            {
                this._jobIds = value;
                OnPropertyChanged(nameof(JobIds));
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

    public class QuestEditorActSkillModelJobIdWrapper : INotifyPropertyChanged
    {
        private int _jobId;
        public int JobId
        {
            get => _jobId;
            set
            {
                if (_jobId != value)
                {
                    _jobId = value;
                    OnPropertyChanged(nameof(JobId));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}