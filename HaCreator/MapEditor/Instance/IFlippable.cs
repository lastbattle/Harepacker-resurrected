using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Instance
{
    public interface IFlippable
    {
        bool Flip { get; set; }

        int UnflippedX { get; }
    }
}
