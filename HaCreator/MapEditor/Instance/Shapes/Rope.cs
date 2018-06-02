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

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class Rope : IContainsLayerInfo, ISerializable
    {
        private Board board;
        private RopeAnchor firstAnchor;
        private RopeAnchor secondAnchor;
        private RopeLine line;

        private int _page; // aka layer
        private bool _ladder;
        private bool _ladderSetByUser = false;
        private bool _uf; // Decides whether you can climb over the end of the rope (usually true)
                          // According to koolk it stands for "Upper Foothold"

        public Rope(Board board, int x, int y1, int y2, bool ladder, int page, bool uf)
        {
            this.board = board;
            this._page = page;
            this._ladder = ladder;
            this._uf = uf;
            this.firstAnchor = new RopeAnchor(board, x, y1, this);
            this.secondAnchor = new RopeAnchor(board, x, y2, this);
            this.line = new RopeLine(board, firstAnchor, secondAnchor);
            Create();
        }

        public void Remove(List<UndoRedoAction> undoPipe)
        {
            lock (board.ParentControl)
            {
                firstAnchor.Selected = false;
                secondAnchor.Selected = false;
                board.BoardItems.RopeAnchors.Remove(firstAnchor);
                board.BoardItems.RopeAnchors.Remove(secondAnchor);
                board.BoardItems.RopeLines.Remove(line);
                if (undoPipe != null)
                {
                    undoPipe.Add(UndoRedoManager.RopeRemoved(this));
                }
            }
        }

        public void Create()
        {
            lock (board.ParentControl)
            {
                board.BoardItems.RopeAnchors.Add(firstAnchor);
                board.BoardItems.RopeAnchors.Add(secondAnchor);
                board.BoardItems.RopeLines.Add(line);
            }
        }

        public void OnUserTouchedLadder()
        {
            _ladderSetByUser = true;
        }

        public int LayerNumber { get { return _page; } set { _page = value; } }
        public int PlatformNumber { get { return -1; } set { return; } }
        public bool ladder { get { return _ladder; } set { _ladder = value; } }
        public bool ladderSetByUser { get { return _ladderSetByUser; } }
        public bool uf { get { return _uf; } set { _uf = value; } }

        public RopeAnchor FirstAnchor { get { return firstAnchor; } }
        public RopeAnchor SecondAnchor { get { return secondAnchor; } }

        #region ISerializable Implementation
        public class SerializationForm
        {
            public int page;
            public bool ladder, uf, ladderuser;
            public int x, y1, y2;
        }

        public virtual bool ShouldSelectSerialized
        {
            get
            {
                return false;
            }
        }

        public List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems)
        {
            // Should never be called since ShouldSelectSerialized is false
            throw new NotImplementedException();
        }

        public virtual object Serialize()
        {
            SerializationForm result = new SerializationForm();
            result.page = _page;
            result.ladder = _ladder;
            result.uf = _uf;
            result.ladderuser = _ladderSetByUser;
            result.x = firstAnchor.X;
            result.y1 = firstAnchor.Y;
            result.y2 = secondAnchor.Y;
            return result;
        }

        public IDictionary<string, object> SerializeBindings(Dictionary<ISerializable, long> refDict)
        {
            return null;
        }

        public Rope(Board board, SerializationForm json)
        {
            this.board = board;
            _page = json.page;
            _ladder = json.ladder;
            _uf = json.uf;
            _ladderSetByUser = json.ladderuser;
            firstAnchor = new RopeAnchor(board, json.x, json.y1, this);
            secondAnchor = new RopeAnchor(board, json.x, json.y2, this);
            line = new RopeLine(board, firstAnchor, secondAnchor);
        }

        public void DeserializeBindings(IDictionary<string, object> bindSer, Dictionary<long, ISerializable> refDict)
        {
            return;
        }

        public void AddToBoard(List<UndoRedoAction> undoPipe)
        {
            Create();
            board.BoardItems.Ropes.Add(this);
            if (undoPipe != null)
            {
                _page = board.SelectedLayerIndex;
                undoPipe.Add(UndoRedoManager.RopeAdded(this));
            }
        }

        public void PostDeserializationActions(bool? selected, XNA.Point? offset)
        {
            if (selected.HasValue)
            {
                firstAnchor.Selected = secondAnchor.Selected = selected.Value;
            }
            if (offset.HasValue)
            {
                firstAnchor.MoveSilent(firstAnchor.X + offset.Value.X, firstAnchor.Y + offset.Value.Y);
                secondAnchor.MoveSilent(secondAnchor.X + offset.Value.X, secondAnchor.Y + offset.Value.Y);
            }
        }
        #endregion
    }
}
