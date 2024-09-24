using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
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

        private int _period;
        /// <summary>
        /// The expiration period (in minutes) from the time that the item is received.
        /// </summary>
        public int Period
        {
            get { return _period; }
            set
            {
                int newValue = value;
                
                newValue = Math.Max(0, Math.Min(int.MaxValue, newValue));

                this._period = newValue;
                OnPropertyChanged(nameof(Period));
            }
        }


        private QuestEditorActInfoRewardPropTypeModel _prob;
        /// <summary>
        /// If prop > 0: The item has a chance to be randomly selected.Higher values increase the likelihood.
        /// If prop == 0: The item is always given (no randomness involved).
        /// If prop == -1: The item is part of an external selection process(possibly player choice).
        /// </summary>
        public QuestEditorActInfoRewardPropTypeModel Prop
        {
            get { return _prob; }
            set
            {
                QuestEditorActInfoRewardPropTypeModel newValue = value;

                this._prob = newValue;
                OnPropertyChanged(nameof(Prop));
            }
        }


        private QuestEditorActInfoPotentialType _potentialGrade;
        /// <summary>
        /// The potential grade of the item. "노멀" = normal, "레어" = rare, Epic 에픽, Unique 유니크, Legendary 레전드리
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

        private CharacterGenderType _gender;
        /// <summary>
        /// The character gender used in WZ and client.
        /// 0 = Male, 1 = Female, 2 = both [default = 2 for extraction if unavailable]
        /// </summary>
        public CharacterGenderType Gender
        {
            get { return _gender; }
            set
            {
                this._gender = value;
                OnPropertyChanged(nameof(Gender));
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

        private int _job;
        /// <summary>
        /// The job group that the item is for <int name="job" value="32"/>
        /// TODO
        /// </summary>
        public int Job
        {
            get { return _job; }
            set
            {
                this._job = value;
                OnPropertyChanged(nameof(Job));
            }
        }

        private int _jobEx;
        /// <summary>
        /// The job group that the item is for <int name="job" value="32"/>
        /// </summary>
        public int JobEx
        {
            get { return _jobEx; }
            set
            {
                this._jobEx = value;
                OnPropertyChanged(nameof(JobEx));
            }
        }

        public bool IsEquip
        {
            get { return ItemIdsCategory.IsEquipment(_itemId); }
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
