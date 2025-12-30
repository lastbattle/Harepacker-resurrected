using MapleLib.WzLib.WzStructure.Data;
using System.Collections;

namespace HaCreator.Collections
{
    public interface IMapleList : IList
    {
        bool IsItem { get; }
        ItemTypes ListType { get; }
    }
}
