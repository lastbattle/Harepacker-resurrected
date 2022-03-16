/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Drawing;

namespace HaCreator.MapEditor.Info
{
    public class ReactorInfo : MapleExtractableInfo
    {
        private readonly string id;

        private WzImage _LinkedWzImage;

        public ReactorInfo(Bitmap image, System.Drawing.Point origin, string id, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            this.id = id;
        }

        private void ExtractPNGFromImage(WzImage image)
        {
            WzCanvasProperty reactorImage = WzInfoTools.GetReactorImage(image);
            if (reactorImage != null)
            {
                Image = reactorImage.GetLinkedWzCanvasBitmap();
                Origin = WzInfoTools.PointFToSystemPoint(reactorImage.GetCanvasOriginPosition());
            }
            else
            {
                Image = new Bitmap(1, 1);
                Origin = new System.Drawing.Point();
            }
        }

        public override void ParseImage()
        {
            if (LinkedWzImage != null) // load from here too
                ExtractPNGFromImage(_LinkedWzImage);
            else
                ExtractPNGFromImage((WzImage)ParentObject);
        }

        public static ReactorInfo Get(string id)
        {
            ReactorInfo result = Program.InfoManager.Reactors[id];
            result.ParseImageIfNeeded();
            return result;
        }

        public static ReactorInfo Load(WzImage parentObject)
        {
            return new ReactorInfo(null, new System.Drawing.Point(), WzInfoTools.RemoveExtension(parentObject.Name), parentObject);
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            if (Image == null) ParseImage();
            return new ReactorInstance(this, board, x, y, UserSettings.defaultReactorTime, "", flip);
        }

        public BoardItem CreateInstance(Board board, int x, int y, int reactorTime, string name, bool flip)
        {
            if (Image == null) ParseImage();
            return new ReactorInstance(this, board, x, y, reactorTime, name, flip);
        }

        public string ID
        {
            get { return id; }
            private set { }
        }

        /// <summary>
        /// The source WzImage of the reactor
        /// </summary>
        public WzImage LinkedWzImage
        {
            get {
                WzStringProperty link = (WzStringProperty)((WzSubProperty)((WzImage)ParentObject)["info"])["link"];
                if (link != null)
                    _LinkedWzImage = (WzImage)Program.WzManager["reactor_000"][link.Value + ".img"];
                else
                    _LinkedWzImage = (WzImage)Program.WzManager["reactor_000"][id + ".img"];
                return _LinkedWzImage; 
            }
            set { this._LinkedWzImage = value; }
        }
    }
}
