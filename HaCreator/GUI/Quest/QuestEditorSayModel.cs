using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace HaCreator.GUI.Quest
{

    /// <summary>
    /// For every NPC conversation
    /// </summary>
    public class QuestEditorSayModel : INotifyPropertyChanged
    {
        private string _npcConversation = string.Empty;

        public QuestEditorConversationType _questEditorConversationType;

        private ObservableCollection<QuestEditorSayResponseModel> _yesResponses = [];
        private ObservableCollection<QuestEditorSayResponseModel> _noResponses = [];
        private ObservableCollection<QuestEditorSayResponseModel> _askResponses = [];

        private QuestEditorConversationPreviewLine _selectedPreviewLine;

        public QuestEditorSayModel()
        {
            _yesResponses.CollectionChanged += Responses_CollectionChanged;
            _noResponses.CollectionChanged += Responses_CollectionChanged;
            RebuildPreviewLines();
        }

        /// <summary>
        /// The NPC conversation
        /// </summary>
        public string NpcConversation
        {
            get => _npcConversation;
            set
            {
                _npcConversation = value;
                OnPropertyChanged(nameof(NpcConversation));
            }
        }

        public QuestEditorConversationType ConversationType
        {
            get => _questEditorConversationType;
            set 
            {
                if (_questEditorConversationType != value)
                {
                    _questEditorConversationType = value;
                    OnPropertyChanged(nameof(ConversationType));

                    // call property change to other related items that is binded to
                    OnPropertyChanged(nameof(IsYesNoConversation));
                }
            }
        }

        /// <summary>
        /// Preserve nested Say.img conversation shape used by MapleStory China v113 (for example: 0/0/0, 0/yes/0).
        /// </summary>
        public bool PreserveNestedLayout { get; set; }

        /// <summary>
        /// If this is a Yes/No conversation as opposed to Next or NextPrev
        /// </summary>
        public bool IsYesNoConversation
        {
            get {
                return _questEditorConversationType == QuestEditorConversationType.YesNo;
            }
        }

        /// <summary>
        /// If the user selects yes, this defines what the NPC would say next
        /// </summary>
        public ObservableCollection<QuestEditorSayResponseModel> YesResponses
        {
            get => _yesResponses;
            set
            {
                _yesResponses.CollectionChanged -= Responses_CollectionChanged;
                _yesResponses = value ?? [];
                _yesResponses.CollectionChanged += Responses_CollectionChanged;
                OnPropertyChanged(nameof(YesResponses));
                RebuildPreviewLines();
            }
        }

        /// <summary>
        /// If the user selects no, this defines what the NPC would say next
        /// </summary>
        public ObservableCollection<QuestEditorSayResponseModel> NoResponses
        {
            get => _noResponses;
            set
            {
                _noResponses.CollectionChanged -= Responses_CollectionChanged;
                _noResponses = value ?? [];
                _noResponses.CollectionChanged += Responses_CollectionChanged;
                OnPropertyChanged(nameof(NoResponses));
                RebuildPreviewLines();
            }
        }

        /// <summary>
        /// Depending on options the user selects, this defines what the NPC would say next
        /// </summary>
        public ObservableCollection<QuestEditorSayResponseModel> AskResponses
        {
            get => _askResponses;
            set
            {
                _askResponses = value;
                OnPropertyChanged(nameof(AskResponses));
            }
        }

        public ObservableCollection<QuestEditorConversationPreviewLine> PreviewLines { get; } = [];

        public QuestEditorConversationPreviewLine SelectedPreviewLine
        {
            get => _selectedPreviewLine;
            set
            {
                if (_selectedPreviewLine != value)
                {
                    _selectedPreviewLine = value;
                    OnPropertyChanged(nameof(SelectedPreviewLine));
                }
            }
        }

        private void Responses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildPreviewLines();
        }

        private void RebuildPreviewLines()
        {
            object selectedConversation = _selectedPreviewLine?.Conversation;
            PreviewLines.Clear();
            PreviewLines.Add(new QuestEditorConversationPreviewLine("Main conversation", this));

            for (int i = 0; i < _yesResponses.Count; i++)
                PreviewLines.Add(new QuestEditorConversationPreviewLine($"Yes response {i + 1}", _yesResponses[i]));
            for (int i = 0; i < _noResponses.Count; i++)
                PreviewLines.Add(new QuestEditorConversationPreviewLine($"No response {i + 1}", _noResponses[i]));

            SelectedPreviewLine = PreviewLines.FirstOrDefault(line => line.Conversation == selectedConversation)
                ?? PreviewLines[0];
        }

        #region Property Changed Event
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    public sealed class QuestEditorConversationPreviewLine
    {
        public QuestEditorConversationPreviewLine(string label, object conversation)
        {
            Label = label;
            Conversation = conversation;
        }

        public string Label { get; }
        public object Conversation { get; }
    }

    public class QuestEditorSayResponseModel : INotifyPropertyChanged
    {
        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
