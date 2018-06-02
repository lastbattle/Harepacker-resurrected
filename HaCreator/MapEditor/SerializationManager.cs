/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

//#define SPEEDTEST

using HaCreator.Collections;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor
{
    public class SerializationManager
    {
        Board board;

        public const string HaClipboardData = "HaClipboardData";

        public SerializationManager(Board board)
        {
            this.board = board;
        }

        public static Dictionary<string, int> SerializePoint(XNA.Point p)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(2);
            result.Add("x", p.X);
            result.Add("y", p.Y);
            return result;
        }

        public static XNA.Point DeserializePoint(dynamic json)
        {
            return new XNA.Point((int)json.x, (int)json.y);
        }

        public string SerializeList(IEnumerable<ISerializableSelector> list)
        {
            // Get the list of all items to serialize, including dependencies and excluding non-serializable ISerializables
            List<ISerializable> items = new SerializableEnumerator(list).ToList();

            // Make reference IDs for every serialized object
            Dictionary<ISerializable, long> refDict = MakeSerializationRefDict(items);

            // Loop over all items, making their dynamic objects and adding them to the serialization queue
            List<dynamic> dynamicList = new List<dynamic>(items.Count);
            foreach (ISerializable item in items)
            {
                dynamic serData = new ExpandoObject();
                serData.type = item.GetType().FullName;
                object data = item.Serialize();
                serData.dataType = data.GetType().FullName;
                serData.data = JsonConvert.SerializeObject(data);
                serData.bindings = item.SerializeBindings(refDict);
                dynamicList.Add(serData);
            }

            // Serialize into JSON
            return JsonConvert.SerializeObject(dynamicList.ToArray());
        }

        public List<ISerializable> DeserializeList(string serialization)
        {
            dynamic[] dynamicArray = JsonConvert.DeserializeObject<dynamic[]>(serialization);
            List<ISerializable> items = new List<ISerializable>();
            List<IDictionary<string, object>> itemBindings = new List<IDictionary<string, object>>();
            foreach (dynamic serData in dynamicArray)
            {
                dynamic serData2 = Deserialize2(serData);
                string typeName = serData2.type;
                string data = serData2.data;
                string dataTypeName = serData2.dataType;
                Type dataType = Type.GetType(dataTypeName);
                object dataObject = JsonConvert.DeserializeObject(data, dataType);
                ISerializable item = (ISerializable)ConstructObject(typeName, new object[] { board, dataObject }, new[] { typeof(Board), dataType });
                items.Add(item);

                // Store the binding dict for later, since we cant deserialize binding data untill all objects have been constructed
                itemBindings.Add(serData2.bindings);
            }

            // Make binding references and deserialize them
            Dictionary<long, ISerializable> refDict = MakeDeserializationRefDict(items);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].DeserializeBindings(itemBindings[i], refDict);
            }
            return items;
        }

        public string SerializeBoard(bool userobjs)
        {
#if SPEEDTEST
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif
            dynamic serData = new ExpandoObject();
            lock (board.ParentControl)
            {
                // No need to also include FootholdLines beacuse they will be included through their anchors
                serData.items = SerializeList(board.BoardItems.Items);
                serData.info = JsonConvert.SerializeObject(board.MapInfo);
                serData.vr = JsonConvert.SerializeObject(board.VRRectangle == null ? null : board.VRRectangle.Serialize());
                serData.minimap = JsonConvert.SerializeObject(board.MinimapRectangle == null ? null : board.MinimapRectangle.Serialize());
                serData.center = SerializePoint(board.CenterPoint);
                serData.size = SerializePoint(board.MapSize);
            }
            serData.userobjs = userobjs ? board.ParentControl.UserObjects.SerializedForm : null;
            string result = JsonConvert.SerializeObject(serData);
#if SPEEDTEST
            System.Windows.Forms.MessageBox.Show(sw.ElapsedMilliseconds.ToString());
#endif
            return result;
        }

        public void DeserializeBoard(string data)
        {
            dynamic serData = JsonConvert.DeserializeObject(data);
            serData = Deserialize2(serData);
            if (serData.userobjs != null)
            {
                board.ParentControl.UserObjects.DeserializeObjects(serData.userobjs);
            }
            board.MapSize = DeserializePoint(serData.size);
            board.CenterPoint = DeserializePoint(serData.center);
            MapleEmptyRectangle.SerializationForm vrSer = JsonConvert.DeserializeObject<MapleEmptyRectangle.SerializationForm>(serData.vr);
            MapleEmptyRectangle.SerializationForm mmSer = JsonConvert.DeserializeObject<MapleEmptyRectangle.SerializationForm>(serData.minimap);
            board.VRRectangle = vrSer == null ? null : new VRRectangle(board, vrSer);
            board.MinimapRectangle = mmSer == null ? null : new MinimapRectangle(board, mmSer);
            board.MapInfo = JsonConvert.DeserializeObject<MapInfo>(serData.info);
            foreach (ISerializable item in DeserializeList(serData.items))
            {
                item.AddToBoard(null);
            }
            board.RegenerateMinimap();
            board.TabPage.Text = board.MapInfo.strMapName;
            foreach (Layer l in board.Layers)
            {
                l.RecheckTileSet();
                l.RecheckZM();
            }
            MapLoader.GenerateDefaultZms(board);
        }

        private Dictionary<ISerializable, long> MakeSerializationRefDict(List<ISerializable> items)
        {
            Dictionary<ISerializable, long> result = new Dictionary<ISerializable, long>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                result.Add(items[i], i);
            }
            return result;
        }

        private Dictionary<long, ISerializable> MakeDeserializationRefDict(List<ISerializable> items)
        {
            Dictionary<long, ISerializable> result = new Dictionary<long, ISerializable>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                result.Add(i, items[i]);
            }
            return result;
        }

        private object Deserialize2(JToken obj)
        {
            if (obj is JObject)
            {
                IDictionary<string, object> result = new ExpandoObject();
                foreach (KeyValuePair<string, JToken> pair in (JObject)obj)
                {
                    result.Add(pair.Key, Deserialize2(pair.Value));
                }
                return result;
            }
            else if (obj is JValue)
            {
                return ((JValue)obj).Value;
            }
            else if (obj is JArray)
            {
                JArray jarr = (JArray)obj;
                object[] arr = new object[jarr.Count];
                for (int i = 0; i < jarr.Count; i++)
                {
                    arr[i] = Deserialize2(jarr[i]);
                }
                return arr;
            }
            else
            {
                throw new Exception();
            }
        }

        private object ConstructObject(string typeName, object[] args, Type[] ctorTemplate)
        {
            Type type = Type.GetType(typeName);
            ConstructorInfo ctorInfo = type.GetConstructor(ctorTemplate);
            return ctorInfo.Invoke(args);
        }
    }
}
