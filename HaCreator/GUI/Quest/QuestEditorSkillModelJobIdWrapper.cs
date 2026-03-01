using System.ComponentModel;

namespace HaCreator.GUI.Quest
{ 
    /// <summary>
    /// Dual-use class model between Act and Check
    /// </summary>
    public class QuestEditorSkillModelJobIdWrapper : INotifyPropertyChanged
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
