/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    class SerializableEnumerator : IEnumerable<ISerializable>, IEnumerator<ISerializable>
    {
        HashSet<ISerializableSelector> visited;
        Queue<ISerializableSelector> queue;
        ISerializableSelector current = null;

        public SerializableEnumerator(IEnumerable<ISerializableSelector> startList)
        {
            visited = new HashSet<ISerializableSelector>(startList);
            queue = new Queue<ISerializableSelector>(visited);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ISerializable> GetEnumerator()
        {
            return this;
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public ISerializable Current
        {
            get { return (ISerializable)current; }
        }

        public bool MoveNext()
        {
            // This method iterates the binding tree in order to find all ISerializables that need to be serialized
            // (these are all the selected ISerializableSelectors that are also ISerializable, and all their children who are as such)
            //
            // SelectSerialized might return the same item on different objects (I can't think of a sitauation in the current
            // implementation that will cause it, but we will assume that anyway to future-proof this class). Therefore, we
            // hold a HashSet of all the items we already added to the Queue, and only enqueue those that are unique.
            //
            // Additionally, we need to make sure that FootholdAnchors are called last, because they decide whether or not
            // to include their Footholds by checking if both ends of the line are selected. This is fine because if one
            // or more of the ends is not selected in the initial list, and is included later on, it will be pushed to
            // the end of the queue and will be called after all tiles/objects have already been processed.

            do
            {
                if (queue.Count == 0)
                    return false;
                current = queue.Dequeue();
                if (current.ShouldSelectSerialized)
                {
                    List<ISerializableSelector> currList = current.SelectSerialized(visited);
                    foreach (ISerializableSelector item in currList)
                    {
                        if (visited.Add(item))
                            queue.Enqueue(item);
                    }
                }
            }
            while (!(current is ISerializable));
            return true;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }
    }
}
