using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Instance
{
    public interface ISerializableSelector
    {
        // Lets the object decide whether SelectSerialized should be called
        bool ShouldSelectSerialized { get; }

        // Lets the object add other objects to the serialization queue
        List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems);
    }
}
