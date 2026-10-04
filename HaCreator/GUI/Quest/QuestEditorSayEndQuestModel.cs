using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        private QuestEditorSayResponseModel _selectedResponse;

        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditorSayEndQuestModel()
        {
            _responses.CollectionChanged += Responses_CollectionChanged;
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
                if (_responses != null)
                    _responses.CollectionChanged -= Responses_CollectionChanged;

                _responses = value ?? new ObservableCollection<QuestEditorSayResponseModel>();
                _responses.CollectionChanged += Responses_CollectionChanged;
                OnPropertyChanged(nameof(Responses));
                EnsureSelectedResponse();
            }
        }

        /// <summary>
        /// The stop response currently selected for editing and client preview.
        /// </summary>
        public QuestEditorSayResponseModel SelectedResponse
        {
            get => _selectedResponse;
            set
            {
                if (_selectedResponse != value)
                {
                    _selectedResponse = value;
                    OnPropertyChanged(nameof(SelectedResponse));
                }
            }
        }

        private void Responses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            EnsureSelectedResponse();
        }

        private void EnsureSelectedResponse()
        {
            if (_selectedResponse == null || !_responses.Contains(_selectedResponse))
                SelectedResponse = _responses.FirstOrDefault();
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
