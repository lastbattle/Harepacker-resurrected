/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.UndoRedo
{
    public class UndoRedoAction
    {
        private BoardItem item;
        private UndoRedoType type;
        private object ParamA;
        private object ParamB;
        private object ParamC;

        public UndoRedoAction(BoardItem item, UndoRedoType type, object ParamA, object ParamB)
        {
            this.item = item;
            this.type = type;
            this.ParamA = ParamA;
            this.ParamB = ParamB;
        }

        public UndoRedoAction(BoardItem item, UndoRedoType type, object ParamA, object ParamB, object ParamC)
            : this(item, type, ParamA, ParamB)
        {
            this.ParamC = ParamC;
        }

        public void UndoRedo(HashSet<int> layersToRecheck)
        {
            Board board;
            switch (type)
            {
                case UndoRedoType.ItemDeleted:
                    //item.Board.BoardItems.Add(item, true);
                    item.InsertItem();
                    break;
                case UndoRedoType.ItemAdded:
                    item.RemoveItem(null);
                    break;
                case UndoRedoType.ItemMoved:
                    XNA.Point oldPos = (XNA.Point)ParamA;
                    item.Move(oldPos.X, oldPos.Y);
                    break;
                case UndoRedoType.ItemFlipped:
                    ((IFlippable)item).Flip = !((IFlippable)item).Flip;
                    break;
                case UndoRedoType.LineRemoved:
                    board = ((MapleDot)ParamB).Board;
                    if (ParamC is FootholdLine)
                        board.BoardItems.FootholdLines.Add((FootholdLine)ParamC);
                    else if (ParamC is RopeLine)
                        board.BoardItems.RopeLines.Add((RopeLine)ParamC);
                    else throw new Exception("wrong type at undoredo, lineremoved");
                    ((MapleLine)ParamC).FirstDot = (MapleDot)ParamA;
                    ((MapleLine)ParamC).SecondDot = (MapleDot)ParamB;
                    ((MapleDot)ParamA).connectedLines.Add((MapleLine)ParamC);
                    ((MapleDot)ParamB).connectedLines.Add((MapleLine)ParamC);
                    break;
                case UndoRedoType.LineAdded:
                    board = ((MapleDot)ParamB).Board;
                    if (ParamC is FootholdLine)
                        board.BoardItems.FootholdLines.Remove((FootholdLine)ParamC);
                    else if (ParamC is RopeLine)
                        board.BoardItems.RopeLines.Remove((RopeLine)ParamC);
                    else
                        throw new Exception("wrong type at undoredo, lineadded");
                    ((MapleLine)ParamC).Remove(false, null);
                    break;
                case UndoRedoType.ToolTipLinked:
                    ((ToolTipInstance)item).CharacterToolTip = null;
                    ((ToolTipChar)ParamA).BoundTooltip = null;
                    break;
                case UndoRedoType.ToolTipUnlinked:
                    ((ToolTipChar)ParamA).BoundTooltip = (ToolTipInstance)item;
                    break;
                case UndoRedoType.BackgroundMoved:
                    ((BackgroundInstance)item).BaseX = ((XNA.Point)ParamA).X;
                    ((BackgroundInstance)item).BaseY = ((XNA.Point)ParamA).Y;
                    break;
                case UndoRedoType.ItemsLinked:
                    item.ReleaseItem((BoardItem)ParamA);
                    break;
                case UndoRedoType.ItemsUnlinked:
                    item.BindItem((BoardItem)ParamA, (Microsoft.Xna.Framework.Point)ParamB);
                    break;
                case UndoRedoType.ItemsLayerChanged:
                    InputHandler.ClearSelectedItems(((BoardItem)((List<IContainsLayerInfo>)ParamC)[0]).Board);
                    foreach (IContainsLayerInfo layerInfoItem in (List<IContainsLayerInfo>)ParamC)
                        layerInfoItem.LayerNumber = (int)ParamA;
                    ((BoardItem)((List<IContainsLayerInfo>)ParamC)[0]).Board.Layers[(int)ParamA].RecheckTileSet();
                    ((BoardItem)((List<IContainsLayerInfo>)ParamC)[0]).Board.Layers[(int)ParamB].RecheckTileSet();
                    break;
                case UndoRedoType.ItemLayerPlatChanged:
                    Tuple<int, int> oldLayerPlat = (Tuple<int, int>)ParamA;
                    Tuple<int, int> newLayerPlat = (Tuple<int, int>)ParamB;
                    IContainsLayerInfo li = (IContainsLayerInfo)ParamC;
                    li.LayerNumber = oldLayerPlat.Item1;
                    li.PlatformNumber = oldLayerPlat.Item2;
                    layersToRecheck.Add(oldLayerPlat.Item1);
                    layersToRecheck.Add(newLayerPlat.Item1);
                    break;
                case UndoRedoType.RopeAdded:
                    ((Rope)ParamA).Remove(null);
                    break;
                case UndoRedoType.RopeRemoved:
                    ((Rope)ParamA).Create();
                    break;
                case UndoRedoType.ItemZChanged:
                    item.Z = (int)ParamA;
                    item.Board.BoardItems.Sort();
                    break;
                case UndoRedoType.VRChanged:
                    //TODO
                    break;
                case UndoRedoType.MapCenterChanged:
                    //TODO
                    break;
                case UndoRedoType.LayerTSChanged:
                    string ts_old = (string)ParamA;
                    string ts_new = (string)ParamB;
                    Layer l = (Layer)ParamC;
                    l.ReplaceTS(ts_old);
                    break;
                case UndoRedoType.zMChanged:
                    int zm_old = (int)ParamA;
                    int zm_new = (int)ParamB;
                    IContainsLayerInfo target = (IContainsLayerInfo)ParamC;
                    target.PlatformNumber = zm_old;
                    break;
            }
        }

        public void SwitchAction()
        {
            switch (type)
            {
                case UndoRedoType.ItemAdded:
                    type = UndoRedoType.ItemDeleted;
                    break;
                case UndoRedoType.ItemDeleted:
                    type = UndoRedoType.ItemAdded;
                    break;
                case UndoRedoType.LineAdded:
                    type = UndoRedoType.LineRemoved;
                    break;
                case UndoRedoType.LineRemoved:
                    type = UndoRedoType.LineAdded;
                    break;
                case UndoRedoType.ToolTipLinked:
                    type = UndoRedoType.ToolTipUnlinked;
                    break;
                case UndoRedoType.ToolTipUnlinked:
                    type = UndoRedoType.ToolTipLinked;
                    break;
                case UndoRedoType.ItemsLinked:
                    type = UndoRedoType.ItemsUnlinked;
                    break;
                case UndoRedoType.ItemsUnlinked:
                    type = UndoRedoType.ItemsLinked;
                    break;
                case UndoRedoType.RopeAdded:
                    type = UndoRedoType.RopeRemoved;
                    break;
                case UndoRedoType.RopeRemoved:
                    type = UndoRedoType.RopeAdded;
                    break;
                case UndoRedoType.ItemsLayerChanged:
                case UndoRedoType.ItemLayerPlatChanged:
                case UndoRedoType.BackgroundMoved:
                case UndoRedoType.ItemMoved:
                case UndoRedoType.MapCenterChanged:
                case UndoRedoType.ItemZChanged:
                case UndoRedoType.VRChanged:
                case UndoRedoType.LayerTSChanged:
                case UndoRedoType.zMChanged:
                    object ParamBTemp = ParamB;
                    object ParamATemp = ParamA;
                    ParamA = ParamBTemp;
                    ParamB = ParamATemp;
                    break;
                case UndoRedoType.ItemFlipped:
                    break;
            }
        }
    }
}
