/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Info
{
    public abstract class MapleDrawableInfo
    {
        private Bitmap image;
        private Texture2D texture;
        private Point origin;
        private WzObject parentObject;
        int width;
        int height;

        public MapleDrawableInfo(Bitmap image, Point origin, WzObject parentObject)
        {
            this.Image = image;
            this.origin = origin;
            this.parentObject = parentObject;
        }

        public void CreateTexture(GraphicsDevice device)
        {
            texture = BoardItem.TextureFromBitmap(device, image);
        }

        public abstract BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip);

        public virtual Texture2D GetTexture(SpriteBatch sprite)
        {
            if (texture == null) CreateTexture(sprite.GraphicsDevice);
            return texture;
        }

        public virtual WzObject ParentObject
        {
            get
            {
                return parentObject;
            }
            set
            {
                parentObject = value;
            }
        }

        public virtual Bitmap Image
        {
            get
            {
                return image;
            }
            set
            {
                image = value;
                texture = null;

                if (image != null)
                {
                    width = image.Width;
                    height = image.Height;
                }
            }
        }

        public virtual int Width
        {
            get
            {
                return width;
            }
        }

        public virtual int Height
        {
            get
            {
                return height;
            }
        }

        public virtual Point Origin
        {
            get
            {
                return origin;
            }
            set
            {
                origin = value;
            }
        }
    }
}
