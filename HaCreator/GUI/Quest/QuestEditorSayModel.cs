using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HaCreator.GUI.Quest
{

    /// <summary>
    /// For every NPC conversation
    /// </summary>
    public class QuestEditorSayModel : INotifyPropertyChanged
    {
        private string _npcConversation;

        private bool _isAskConversation = false;

        public QuestEditorConversationType _questEditorConversationType;

        private ObservableCollection<QuestEditorSayResponseModel> _yesResponses = new ObservableCollection<QuestEditorSayResponseModel>();
        private ObservableCollection<QuestEditorSayResponseModel> _noResponses = new ObservableCollection<QuestEditorSayResponseModel>();
        private ObservableCollection<QuestEditorSayResponseModel> _askResponses = new ObservableCollection<QuestEditorSayResponseModel>();

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

        /// <summary>
        /// Determines if this conversation is 'ask' whereby the user selects from a list of options.
        /// </summary>
        public bool IsAskConversation
        {
            get => _isAskConversation;
            set
            {
                _isAskConversation = value;
                OnPropertyChanged(nameof(IsAskConversation));
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
        /// If this is a Yes/No conversation as opposed to Next or NextPrev
        /// </summary>
        public bool IsYesNoConversation
        {
            get {
                return _yesResponses.Count > 0  || _noResponses.Count > 0 || _questEditorConversationType == QuestEditorConversationType.YesNo;
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
                _yesResponses = value;
                OnPropertyChanged(nameof(YesResponses));
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
                _noResponses = value;
                OnPropertyChanged(nameof(NoResponses));
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

        #region Property Changed Event
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
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