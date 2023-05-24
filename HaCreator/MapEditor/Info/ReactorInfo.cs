/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.GUI;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
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
                if (_LinkedWzImage == null) {
                    WzStringProperty link = (WzStringProperty)((WzSubProperty)((WzImage)ParentObject)["info"])["link"];

                    string imgName = WzInfoTools.AddLeadingZeros(id, 7) + ".img";

                    WzObject reactorObject = Program.WzManager.FindWzImageByName("reactor", imgName);

                    if (link != null) {
                        string linkImgName = WzInfoTools.AddLeadingZeros(link.Value, 7) + ".img";
                        WzImage findLinkedImg = (WzImage)Program.WzManager.FindWzImageByName("reactor", linkImgName);

                        _LinkedWzImage = findLinkedImg ?? (WzImage) reactorObject; // fallback if link is null
                    }
                    else
                        _LinkedWzImage = (WzImage)reactorObject;
                }
                return _LinkedWzImage;
            }
            set { this._LinkedWzImage = value; }
        }
    }
}
