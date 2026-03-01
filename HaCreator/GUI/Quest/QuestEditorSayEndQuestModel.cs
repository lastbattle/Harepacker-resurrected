using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorSayEndQuestModel : INotifyPropertyChanged
    {
        public QuestEditorStopConversationType _questEditorConversationType;

        private ObservableCollection<QuestEditorSayResponseModel> _responses = new ObservableCollection<QuestEditorSayResponseModel>();

        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditorSayEndQuestModel()
        {

        }

        public QuestEditorStopConversationType ConversationType
        {
            get => _questEditorConversationType;
            set
            {
                if (_questEditorConversationType != value)
                {
                    _questEditorConversationType = value;
                    OnPropertyChanged(nameof(ConversationType));
                }
            }
        }

        /// <summary>
        /// If the user selects yes, this defines what the NPC would say next
        /// </summary>
        public ObservableCollection<QuestEditorSayResponseModel> Responses
        {
            get => _responses;
            set
            {
                _responses = value;
                OnPropertyChanged(nameof(Responses));
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
