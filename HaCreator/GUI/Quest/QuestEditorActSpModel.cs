using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorActSpModel : INotifyPropertyChanged
    {
        private int _SPValue = 0;
        public int SPValue
        {
            get { return _SPValue; }
            set
            {
                int setValue = value;

                if (setValue != value)
                {
                    if (setValue < 0)
                        setValue = 0;
                    else if (setValue > 500)
                        setValue = 500;

                    this._SPValue = value;
                    OnPropertyChanged(nameof(SPValue));
                }
            }
        }

        private ObservableCollection<QuestEditorSkillModelJobIdWrapper> _jobs = new ObservableCollection<QuestEditorSkillModelJobIdWrapper>();
        public ObservableCollection<QuestEditorSkillModelJobIdWrapper> Jobs
        {
            get { return _jobs; }
            set
            {
                this._jobs = value;
                OnPropertyChanged(nameof(Jobs));
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
