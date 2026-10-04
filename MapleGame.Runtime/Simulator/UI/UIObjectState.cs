using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Click-able XNA button state
    /// </summary>
    public enum UIObjectState
    {
        Null = -1,
        Normal = 0x0,
        Disabled = 0x1,
        Pressed = 0x2,
        MouseOver = 0x3,
    }
}
