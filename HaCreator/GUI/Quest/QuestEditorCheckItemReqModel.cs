/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public class QuestEditorCheckItemReqModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public QuestEditorCheckItemReqModel()
        {
        }

        private int _itemId;
        /// <summary>
        /// The itemId of the item to give
        /// </summary>
        public int ItemId
        {
            get { return _itemId; }
            set
            {
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

        #region Property Changed Event
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
