/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib.WzLib.WzStructure.Data;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;

namespace HaCreator.Collections
{
    public class BoardItemsManager
    {
        public MapleList<BackgroundInstance> BackBackgrounds = new MapleList<BackgroundInstance>(ItemTypes.Backgrounds, true);
        public MapleList<LayeredItem> TileObjs = new MapleList<LayeredItem>(ItemTypes.None, true);
        public MapleList<MobInstance> Mobs = new MapleList<MobInstance>(ItemTypes.Mobs, true);
        public MapleList<NpcInstance> NPCs = new MapleList<NpcInstance>(ItemTypes.NPCs, true);
        public MapleList<ReactorInstance> Reactors = new MapleList<ReactorInstance>(ItemTypes.Reactors, true);
        public MapleList<PortalInstance> Portals = new MapleList<PortalInstance>(ItemTypes.Portals, true);
        public MapleList<BackgroundInstance> FrontBackgrounds = new MapleList<BackgroundInstance>(ItemTypes.Backgrounds, true);
        public MapleList<FootholdLine> FootholdLines = new MapleList<FootholdLine>(ItemTypes.Footholds, false);
        public MapleList<RopeLine> RopeLines = new MapleList<RopeLine>(ItemTypes.Ropes, false);
        public MapleList<FootholdAnchor> FHAnchors = new MapleList<FootholdAnchor>(ItemTypes.Footholds, true);
        public MapleList<RopeAnchor> RopeAnchors = new MapleList<RopeAnchor>(ItemTypes.Ropes, true);
        public MapleList<Chair> Chairs = new MapleList<Chair>(ItemTypes.Chairs, true);
        public MapleList<ToolTipChar> CharacterToolTips = new MapleList<ToolTipChar>(ItemTypes.ToolTips, true);
        public MapleList<ToolTipInstance> ToolTips = new MapleList<ToolTipInstance>(ItemTypes.ToolTips, true);
        public MapleList<ToolTipDot> ToolTipDots = new MapleList<ToolTipDot>(ItemTypes.ToolTips, true);
        public MapleList<BoardItem> MiscItems = new MapleList<BoardItem>(ItemTypes.Misc, true);
        public MapleList<MapleDot> SpecialDots = new MapleList<MapleDot>(ItemTypes.Misc, true);

        public List<Rope> Ropes = new List<Rope>();
        public IMapleList[] AllItemLists;
        public BoardItemsCollection Items;
        public MapleLinesCollection Lines;

        private Board board;

        public BoardItemsManager(Board board)
        {
            AllItemLists = new IMapleList[] { BackBackgrounds, TileObjs, Mobs, NPCs, Reactors, Portals, FrontBackgrounds, FootholdLines, RopeLines, FHAnchors, RopeAnchors, Chairs, CharacterToolTips, ToolTips, ToolTipDots, MiscItems, SpecialDots };
            this.board = board;
            Items = new BoardItemsCollection(this, true);
            Lines = new MapleLinesCollection(this, false);
        }

        public void Clear()
        {
            foreach (IMapleList itemList in AllItemLists)
            {
                itemList.Clear();
            }
        }

        public void Remove(BoardItem item)
        {
            lock (board.ParentControl)
            {
                if (item is TileInstance || item is ObjectInstance)
                    TileObjs.Remove((LayeredItem)item);
                else if (item is BackgroundInstance)
                {
                    if (((BackgroundInstance)item).front)
                    {
                        FrontBackgrounds.Remove((BackgroundInstance)item);
                    }
                    else
                    {
                        BackBackgrounds.Remove((BackgroundInstance)item);
                    }
                }
                else if (item.Type == ItemTypes.Misc)
                {
                    if (item is VRDot || item is MinimapDot)
                        SpecialDots.Remove((MapleDot)item);
                    else
                        MiscItems.Remove(item);
                }
                else
                {
                    Type itemType = item.GetType();
                    foreach (IMapleList itemList in AllItemLists)
                    {
                        Type listType = itemList.GetType().GetGenericArguments()[0];
                        if (listType.FullName == itemType.FullName)
                        {
                            itemList.Remove(item);
                            return;
                        }
                    }
                    throw new Exception("unknown type at boarditems.remove");
                }
            }
        }

        public void Add(BoardItem item, bool sort)
        {
            lock (board.ParentControl)
            {
                if (item is TileInstance || item is ObjectInstance)
                {
                    TileObjs.Add((LayeredItem)item);
                    if (sort) 
                        Sort();
                }
                else if (item is BackgroundInstance)
                {
                    if (((BackgroundInstance)item).front)
                    {
                        FrontBackgrounds.Add((BackgroundInstance)item);
                    }
                    else
                    {
                        BackBackgrounds.Add((BackgroundInstance)item);
                    }
                    if (sort) 
                        Sort();
                }
                else if (item.Type == ItemTypes.Misc)
                {
                    if (item is VRDot || item is MinimapDot)
                        SpecialDots.Add((MapleDot)item);
                    else
                        MiscItems.Add(item);
                }
                else
                {
                    Type itemType = item.GetType();
                    foreach (IMapleList itemList in AllItemLists)
                    {
                        Type listType = itemList.GetType().GetGenericArguments()[0];
                        if (listType.FullName == itemType.FullName)
                        {
                            itemList.Add(item);
                            return;
                        }
                    }
                    throw new Exception("unknown type at boarditems.add");
                }
            }
        }

        public void Sort()
        {
            SortLayers();
            SortBackBackgrounds();
            SortFrontBackgrounds();
        }

        private void SortLayers()
        {
            lock (board.ParentControl)
            {
                for (int i = 0; i < 2; i++)
                {
                    TileObjs.Sort(
                        delegate(LayeredItem a, LayeredItem b)
                        {
                            if (a.Layer.LayerNumber > b.Layer.LayerNumber)
                                return 1;
                            else if (a.Layer.LayerNumber < b.Layer.LayerNumber)
                                return -1;
                            else
                            {
                                if (a is TileInstance && b is TileInstance)
                                {
                                    TileInfo ai = (TileInfo)a.BaseInfo;
                                    TileInfo bi = (TileInfo)b.BaseInfo;
                                    if (ai.z > bi.z)
                                        return 1;
                                    else if (ai.z < bi.z)
                                        return -1;
                                    else
                                    {
                                        if (a.Z > b.Z)
                                            return 1;
                                        else if (a.Z < b.Z)
                                            return -1;
                                        else
                                            return 0;
                                    }
                                }
                                if (a is ObjectInstance && b is ObjectInstance)
                                {
                                    if (a.Z > b.Z)
                                        return 1;
                                    else if (a.Z < b.Z)
                                        return -1;
                                    else return 0;
                                }
                                else if (a is TileInstance && b is ObjectInstance)
                                    return 1;
                                else
                                    return -1;
                            }
                        }
                    );
                }
            }
        }

        public int Count
        {
            get 
            { 
                int total = 0;
                foreach (IList itemList in AllItemLists) total += itemList.Count;
                return total;
            }
        }

        public BoardItem this[int index]
        {
            get
            {
                if (index < 0) throw new Exception("invalid index");
                foreach (IList list in AllItemLists)
                {
                    if (index < list.Count) return (BoardItem)list[index];
                    index -= list.Count;
                }
                throw new Exception("invalid index");
            }
        }

        private void SortBackBackgrounds()
        {
            lock (board.ParentControl)
            {
                BackBackgrounds.Sort(
                    delegate(BackgroundInstance a, BackgroundInstance b)
                    {

                        if (a.Z > b.Z) return 1;
                        else if (a.Z < b.Z) return -1;
                        else return 0;
                    }
                );
            }
        }

        private void SortFrontBackgrounds()
        {
            lock (board.ParentControl)
            {
                FrontBackgrounds.Sort(
                    delegate(BackgroundInstance a, BackgroundInstance b)
                    {

                        if (a.Z > b.Z) return 1;
                        else if (a.Z < b.Z) return -1;
                        else return 0;
                    }
                );
            }
        }
    }
}
