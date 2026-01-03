using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor
{
    public struct SelectionInfo
    {
        public SelectionInfo(int selectedLayer, int selectedPlatform, ItemTypes visibleTypes, ItemTypes editedTypes)
        {
            this.selectedLayer = selectedLayer;
            this.selectedPlatform = selectedPlatform;
            this.visibleTypes = visibleTypes;
            this.editedTypes = editedTypes;
        }

        public int selectedLayer;
        public int selectedPlatform;
        public ItemTypes visibleTypes;
        public ItemTypes editedTypes;
    }
}
