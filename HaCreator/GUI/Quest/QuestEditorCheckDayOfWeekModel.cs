
using System.ComponentModel;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorCheckDayOfWeekModel : INotifyPropertyChanged
    {
        private QuestEditorCheckDayOfWeekType _dayOfWeek;
        private bool _isSelected;

        public QuestEditorCheckDayOfWeekModel(QuestEditorCheckDayOfWeekType dayOfWeek, bool isSelected = false)
        {
            _dayOfWeek = dayOfWeek;
            _isSelected = isSelected;
        }

        public QuestEditorCheckDayOfWeekType DayOfWeek
        {
            get => _dayOfWeek;
            set
            {
                _dayOfWeek = value;
                OnPropertyChanged(nameof(DayOfWeek));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
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
