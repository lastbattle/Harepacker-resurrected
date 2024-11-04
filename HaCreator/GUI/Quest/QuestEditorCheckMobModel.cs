using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorCheckMobModel : INotifyPropertyChanged
    {

        private int _id = 0;
        /// <summary>
        /// The monster Id
        /// </summary>
        public int Id
        {
            get { return _id; }
            set
            {
                this._id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        private int _count = 0;
        /// <summary>
        /// The amount of monsters to kill
        /// </summary>
        public int Count
        {
            get { return _count; }
            set
            {
                if (_count != value)
                {
                    int setAmount = value;
                    if (setAmount < 0)
                        setAmount = 0;
                    else if (setAmount > 9999)
                        setAmount = 9999;

                    this._count = setAmount;
                    OnPropertyChanged(nameof(Count));
                }
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
