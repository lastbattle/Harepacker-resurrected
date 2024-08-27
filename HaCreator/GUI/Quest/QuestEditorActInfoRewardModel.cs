using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorActInfoRewardModel : INotifyPropertyChanged
    {

        private int _itemId;
        public int ItemId { 
            get { return _itemId; }
            set { 
                this._itemId = value;
                OnPropertyChanged(nameof(ItemId));
                OnPropertyChanged(nameof(IsEquip));
            } 
        }

        private int _quantity;
        public int Quantity
        {
            get { return _quantity; }
            set
            {
                this._quantity = value;
                OnPropertyChanged(nameof(Quantity));
            }
        }

        private string _itemName;
        /// <summary>
        /// The item name (only for user preview)
        /// </summary>
        public string ItemName
        {
            get { return _itemName; }
            set
            {
                this._itemName = value;
                OnPropertyChanged(nameof(ItemName));
            }
        }

        public bool IsEquip
        {
            get { return _itemId / 1000000 == 1; }
            private set { }
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
