using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Instance
{
    // The difference between LayeredItem and this is that LayeredItems are actually 
    // ordered according to their layer (tiles\objs) in the editor. IContainsLayerInfo only
    // contains info about layers, and is not necessarily drawn according to it.
    public interface IContainsLayerInfo
    {
        int LayerNumber { get; set; }
        int PlatformNumber { get; set; }
    }
}
