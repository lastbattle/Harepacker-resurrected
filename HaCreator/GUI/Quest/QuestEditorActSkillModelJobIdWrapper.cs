using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{ 
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
