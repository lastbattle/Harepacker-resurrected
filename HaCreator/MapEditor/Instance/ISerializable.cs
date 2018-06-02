/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance
{
    public interface ISerializable : ISerializableSelector
    {
        // Serializes the object
        object Serialize();

        // Serialize the binding data of the object
        IDictionary<string, object> SerializeBindings(Dictionary<ISerializable, long> refDict);

        // Deserialize the binding data and bind the objects, returning a list of the bound objects
        void DeserializeBindings(IDictionary<string, object> bindSer, Dictionary<long, ISerializable> refDict);

        // Adds the object to the board's BoardItemManager
        void AddToBoard(List<UndoRedoAction> undoPipe);

        // Perform post deserialization actions, such as offsetting the object's position or selecting it
        void PostDeserializationActions(bool? selected, XNA.Point? offset);
    }
}
