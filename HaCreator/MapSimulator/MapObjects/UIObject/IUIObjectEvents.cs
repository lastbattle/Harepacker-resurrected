using HaCreator.MapSimulator.Objects.UIObject;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.MapObjects.UIObject
{
    public interface IUIObjectEvents
    {
        bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor);
    }
}
