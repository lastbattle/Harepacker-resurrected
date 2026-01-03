using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.UndoRedo
{
    public enum UndoRedoType
    {
        ItemDeleted,
        ItemAdded,
        ItemMoved,
        ItemFlipped,
        LineRemoved,
        LineAdded,
        ToolTipLinked,
        ToolTipUnlinked,
        BackgroundMoved,
        ItemsUnlinked,
        ItemsLinked,
        ItemsLayerChanged,
        ItemLayerPlatChanged,
        RopeRemoved,
        RopeAdded,
        ItemZChanged,
        VRChanged,
        MapCenterChanged,
        LayerTSChanged,
        zMChanged,
        BackgroundPropertiesChanged // New type for full background property changes via BackgroundInstnanceEditor exclusively
    }
}
