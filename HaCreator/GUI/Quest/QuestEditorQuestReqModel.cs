using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorQuestReqModel : INotifyPropertyChanged
    {
        private int _questId = 0;
        public int QuestId
        {
            get { return _questId; }
            set
            {
                this._questId = value;
                OnPropertyChanged(nameof(QuestId));
            }
        }

        private QuestStateType _questState = QuestStateType.Not_Started;
        public QuestStateType QuestState
        {
            get { return _questState; }
            set
            {
                this._questState = value;
                OnPropertyChanged(nameof(QuestState));
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
