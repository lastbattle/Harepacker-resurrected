/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.Exceptions;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.TilesDesign;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance
{
    public class TileInstance : LayeredItem, ISnappable
    {
        private TileInfo baseInfo;

        public TileInstance(TileInfo baseInfo, Layer layer, Board board, int x, int y, int z, int zM)
            : base(board, layer, zM, x, y, z)
        {
            this.baseInfo = baseInfo;
            AttachToLayer(layer);
        }

        public void AttachToLayer(Layer layer)
        {
            lock (board.ParentControl)
            {
                if (layer.tS != null && layer.tS != baseInfo.tS)
                {
                    Board.BoardItems.TileObjs.Remove(this);
                    layer.Items.Remove(this);
                    throw new Exception("tile added to a layer with different tS");
                }
                else layer.tS = baseInfo.tS;
            }
        }

        public List<Tuple<double, TileInstance, MapTileDesignPotential>> FindSnappableTiles(float threshold, Predicate<TileInstance> pred = null)
        {
            List<Tuple<double, TileInstance, MapTileDesignPotential>> result = new List<Tuple<double, TileInstance, MapTileDesignPotential>>();
            MapTileDesign tilegroup = (MapTileDesign)TileSnap.tileCats[baseInfo.u];
            int mag = baseInfo.mag;
            float first_threshold = MultiBoard.FirstSnapVerification * mag;
            foreach (BoardItem item in Board.BoardItems.Items)
            {
                if (item is TileInstance)
                {
                    // Trying to snap to other selected items can mess up some of the mouse bindings
                    if (item.Selected || item.Equals(this))
                        continue;
                    TileInstance tile = (TileInstance)item;
                    if (tile.LayerNumber != this.LayerNumber)
                        continue;
                    int dx = tile.X - this.X, dy = tile.Y - this.Y;
                    // first verification to save time
                    // Note that we are first checking dx and dy alone; although this is already covered by the following distance calculation,
                    // it is significantly faster and will likely weed out most of the candidates before calculating their actual distance.
                    if (dx > first_threshold || dy > first_threshold || InputHandler.Distance(dx, dy) > first_threshold)
                        continue;
                    if (pred != null && !pred.Invoke(tile))
                        continue;
                    foreach (MapTileDesignPotential snapInfo in tilegroup.potentials)
                    {
                        if (snapInfo.type != tile.baseInfo.u) continue;
                        double distance = InputHandler.Distance(this.X - tile.X + snapInfo.x * mag, this.Y - tile.Y + snapInfo.y * mag);
                        if (distance > threshold) continue;
                        result.Add(new Tuple<double, TileInstance, MapTileDesignPotential>(distance, tile, snapInfo));
                    }
                }
            }
            return result;
        }

        public void DoSnap()
        {
            // Get candidates
            var candidates = FindSnappableTiles(UserSettings.SnapDistance);
            if (candidates.Count == 0)
            {
                return;
            }

            // Get closest candidate
            int best = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Item1 < candidates[best].Item1)
                {
                    best = i;
                }
            }

            // Move all selected items to snap
            int mag = baseInfo.mag;
            TileInstance closestTile = candidates[best].Item2;
            MapTileDesignPotential closestInfo = candidates[best].Item3;
            SnapMoveAllMouseBoundItems(new XNA.Point(closestTile.X - closestInfo.x * mag, closestTile.Y - closestInfo.y * mag));
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Tiles; }
        }

        public override MapleDrawableInfo BaseInfo
        {
            get { return baseInfo; }
        }

        public override void RemoveItem(List<UndoRedoAction> undoPipe)
        {
            lock (board.ParentControl)
            {
                Layer thisLayer = Layer;
                base.RemoveItem(undoPipe);
                thisLayer.RecheckTileSet();
            }
        }

        public override void InsertItem()
        {
            lock (board.ParentControl)
            {
                base.InsertItem();
                AttachToLayer(Layer);
            }
        }

        public override void Draw(SpriteBatch sprite, XNA.Color color, int xShift, int yShift)
        {
            XNA.Rectangle destinationRectangle = new XNA.Rectangle((int)X + xShift - Origin.X, (int)Y + yShift - Origin.Y, Width, Height);
            sprite.Draw(baseInfo.GetTexture(sprite), destinationRectangle, null, color, 0f, new XNA.Vector2(0f, 0f), /*Flip ? SpriteEffects.FlipHorizontally : */SpriteEffects.None, 0 /*Layer.LayerNumber / 10f + Z / 1000f*/);
            base.Draw(sprite, color, xShift, yShift);
        }

        // Only to be used by layer TS changing, do not use this for ANYTHING else.
        public void SetBaseInfo(TileInfo newInfo)
        {
            this.baseInfo = newInfo;
        }

        public override System.Drawing.Bitmap Image
        {
            get
            {
                return baseInfo.Image;
            }
        }

        public override int Width
        {
            get { return baseInfo.Width; }
        }

        public override int Height
        {
            get { return baseInfo.Height; }
        }

        public override System.Drawing.Point Origin
        {
            get
            {
                return baseInfo.Origin;
            }
        }

        public new class SerializationForm : LayeredItem.SerializationForm
        {
            public string ts, u, no;
        }

        public override object Serialize()
        {
            SerializationForm result = new SerializationForm();
            UpdateSerializedForm(result);
            return result;
        }

        protected void UpdateSerializedForm(SerializationForm result)
        {
            base.UpdateSerializedForm(result);
            result.ts = baseInfo.tS;
            result.u = baseInfo.u;
            result.no = baseInfo.no;
        }

        public TileInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            baseInfo = TileInfo.Get(json.ts, json.u, json.no);
        }
    }
}
