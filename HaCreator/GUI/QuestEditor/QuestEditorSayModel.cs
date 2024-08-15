using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HaCreator.GUI
{

    /// <summary>
    /// For every NPC conversation
    /// </summary>
    public class QuestEditorSayModel : INotifyPropertyChanged
    {
        private string _npcConversation;
        
        private ObservableCollection<QuestEditorSayResponseModel> _yesResponses = new ObservableCollection<QuestEditorSayResponseModel>();
        private ObservableCollection<QuestEditorSayResponseModel> _noResponses = new ObservableCollection<QuestEditorSayResponseModel>();

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
        /// If this is a Yes/No conversation as opposed to Next or NextPrev
        /// </summary>
        public bool IsYesNoConversation
        {
            get {
                return _yesResponses.Count > 0  || _noResponses.Count > 0;
            }
        }
        public Visibility IsYesNo => true ? Visibility.Visible : Visibility.Collapsed;


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