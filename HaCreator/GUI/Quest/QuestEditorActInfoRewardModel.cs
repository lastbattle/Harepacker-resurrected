using MapleLib.WzLib.WzStructure.Data.ItemStructure;
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
        /// <summary>
        /// The itemId of the item to give
        /// </summary>
        public int ItemId { 
            get { return _itemId; }
            set { 
                this._itemId = value;
                OnPropertyChanged(nameof(ItemId));
                OnPropertyChanged(nameof(IsEquip));
            } 
        }

        private int _quantity;
        /// <summary>
        /// The quantity of the item to give. Negative is to take.
        /// </summary>
        public int Quantity
        {
            get { return _quantity; }
            set
            {
                this._quantity = value;
                OnPropertyChanged(nameof(Quantity));
            }
        }


        private string _potentialGrade;
        /// <summary>
        /// The potential grade of the item. "노멀"
        /// TODO
        /// </summary>
        public string PotentialGrade
        {
            get { return _potentialGrade; }
            set
            {
                this._potentialGrade = value;
                OnPropertyChanged(nameof(PotentialGrade));
            }
        }

        private string _expireDate;
        /// <summary>
        /// The expiry date of the item. "2009012300"
        /// TODO
        /// </summary>
        public string ExpireDate
        {
            get { return _expireDate; }
            set
            {
                this._expireDate = value;
                OnPropertyChanged(nameof(ExpireDate));
            }
        }

        private string _jobFor;
        /// <summary>
        /// The job group that the item is for <int name="job" value="32"/>
        /// TODO
        /// </summary>
        public string JobFor
        {
            get { return _jobFor; }
            set
            {
                this._jobFor = value;
                OnPropertyChanged(nameof(JobFor));
            }
        }


        /// <summary>
        /// The item name (only for user preview)
        /// </summary>
        public string ItemName
        {
            get {
                Tuple<string, string, string> nameCache = Program.InfoManager.ItemNameCache[ItemId]; // // itemid, <item category, item name, item desc>
                if (nameCache != null)
                {
                    return nameCache.Item2;
                }
                return "NO NAME"; 
            }
            private set
            {
            }
        }

        public bool IsEquip
        {
            get { return !ItemIdsCategory.IsEquipment(_itemId); }
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
