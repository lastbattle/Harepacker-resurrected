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
                int newValue = value;

                if (newValue == 0) // dont allow 0
                    newValue = 1;

                if (ItemIdsCategory.IsEquipment(_itemId))  // Restrict to -1 or 1 if IsEquip is true
                    newValue = Math.Sign(newValue);
                else // Clamp between -9999 and 9999 if IsEquip is false
                    newValue = Math.Max(-9999, Math.Min(9999, newValue));

                this._quantity = newValue;
                OnPropertyChanged(nameof(Quantity));
            }
        }


        private QuestEditorActInfoPotentialType _potentialGrade;
        /// <summary>
        /// The potential grade of the item. "노멀" = normal, "레어" = rare, Epic 에픽, Unique 유니크, Legendary 레전드리
        /// TODO
        /// </summary>
        public QuestEditorActInfoPotentialType PotentialGrade
        {
            get { return _potentialGrade; }
            set
            {
                this._potentialGrade = value;
                OnPropertyChanged(nameof(PotentialGrade));
            }
        }

        private DateTime _expireDate;
        /// <summary>
        /// The expiry date of the item. "2009012300"
        /// TODO
        /// </summary>
        public DateTime ExpireDate
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

        public string ItemName
        {
            get
            {
                Tuple<string, string, string> nameCache = Program.InfoManager.ItemNameCache[ItemId]; // // itemid, <item category, item name, item desc>
                if (nameCache != null)
                {
                    return nameCache.Item2;
                }
                return "NO NAME";
            }
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
