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
    public class QuestEditorSayModel : INotifyPropertyChanged
    {
        private string _type;
        private ObservableCollection<string> _messages;
        private string _yes;
        private string _no;
        private string _stop;

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(IsYesNo));
            }
        }

        public ObservableCollection<string> Messages
        {
            get => _messages;
            set
            {
                _messages = value;
                OnPropertyChanged(nameof(Messages));
            }
        }

        public string Yes
        {
            get => _yes;
            set
            {
                _yes = value;
                OnPropertyChanged(nameof(Yes));
            }
        }

        public string No
        {
            get => _no;
            set
            {
                _no = value;
                OnPropertyChanged(nameof(No));
            }
        }

        public string Stop
        {
            get => _stop;
            set
            {
                _stop = value;
                OnPropertyChanged(nameof(Stop));
            }
        }

        public Visibility IsYesNo => Type == "YesNo" ? Visibility.Visible : Visibility.Collapsed;

        public QuestEditorSayModel()
        {
            Messages = new ObservableCollection<string>();
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