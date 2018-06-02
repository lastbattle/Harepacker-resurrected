/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
