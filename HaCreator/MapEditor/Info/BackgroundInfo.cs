/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using HaCreator.Properties;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Drawing;
using System.Linq;

namespace HaCreator.MapEditor.Info
{
    public class BackgroundInfo : MapleDrawableInfo
    {
        private string _bS;
        private string _no;
        private BackgroundInfoType _type;
        private readonly WzImageProperty imageProperty;

        private WzSpineAnimationItem wzSpineAnimationItem; // only applicable if its a spine item, otherwise null.

        /// <summary>
        /// Constructor
        /// Only to be initialized in Get
        /// </summary>
        /// <param name="image"></param>
        /// <param name="origin"></param>
        /// <param name="bS"></param>
        /// <param name="_type"></param>
        /// <param name="no"></param>
        /// <param name="parentObject"></param>
        /// <param name="wzSpineAnimationItem"></param>
        private BackgroundInfo(WzImageProperty imageProperty, Bitmap image, System.Drawing.Point origin, string bS, BackgroundInfoType _type, string no, WzObject parentObject,
            WzSpineAnimationItem wzSpineAnimationItem)
            : base(image, origin, parentObject)
        {
            this.imageProperty = imageProperty;
            this._bS = bS;
            this._type = _type;
            this._no = no;
            this.wzSpineAnimationItem = wzSpineAnimationItem;
        }

        /// <summary>
        /// Get background by name
        /// </summary>
        /// <param name="graphicsDevice">The graphics device that the backgroundInfo is to be rendered on (loading spine)</param>
        /// <param name="bS"></param>
        /// <param name="type">Select type</param>
        /// <param name="no"></param>
        /// <returns></returns>
        public static BackgroundInfo Get(GraphicsDevice graphicsDevice, string bS, BackgroundInfoType type, string no)
        {
            if (!Program.InfoManager.BackgroundSets.ContainsKey(bS))
                return null;

            WzImage bsImg = Program.InfoManager.BackgroundSets[bS];
            WzImageProperty bgInfoProp = bsImg[type == BackgroundInfoType.Animation ? "ani" : type == BackgroundInfoType.Spine ? "spine" : "back"][no];

            if (type == BackgroundInfoType.Spine)
            {
                if (bgInfoProp.HCTagSpine == null)
                {
                    bgInfoProp.HCTagSpine = Load(graphicsDevice, bgInfoProp, bS, type, no);
                }

                return (BackgroundInfo)bgInfoProp.HCTagSpine;
            }
            else
            {
                if (bgInfoProp.HCTag == null)
                {
                    bgInfoProp.HCTag = Load(graphicsDevice, bgInfoProp, bS, type, no);
                }
                return (BackgroundInfo)bgInfoProp.HCTag;
            }
        }

        /// <summary>
        /// Load background from WzImageProperty
        /// </summary>
        /// <param name="graphicsDevice">The graphics device that the backgroundInfo is to be rendered on (loading spine)</param>
        /// <param name="parentObject"></param>
        /// <param name="spineParentObject"></param>
        /// <param name="bS"></param>
        /// <param name="type"></param>
        /// <param name="no"></param>
        /// <returns></returns>
        private static BackgroundInfo Load(GraphicsDevice graphicsDevice, WzImageProperty parentObject, string bS, BackgroundInfoType type, string no)
        {
            WzCanvasProperty frame0;
            if (type == BackgroundInfoType.Animation)
            {
                frame0 = (WzCanvasProperty)WzInfoTools.GetRealProperty(parentObject["0"]);
            }
            else if (type == BackgroundInfoType.Spine)
            {
                // TODO: make a preview of the spine image ffs
                WzCanvasProperty spineCanvas = (WzCanvasProperty)parentObject["0"];
                if (spineCanvas != null)
                {
                    // Load spine
                    WzSpineAnimationItem wzSpineAnimationItem = null;
                    if (graphicsDevice != null) // graphicsdevice needed to work.. assuming that it is loaded by now before BackgroundPanel
                    {
                        WzImageProperty spineAtlasProp = ((WzSubProperty)parentObject).WzProperties.FirstOrDefault(
                            wzprop => wzprop is WzStringProperty && ((WzStringProperty)wzprop).IsSpineAtlasResources);
                        if (spineAtlasProp != null)
                        {
                            WzStringProperty stringObj = (WzStringProperty)spineAtlasProp;
                            wzSpineAnimationItem = new WzSpineAnimationItem(stringObj);

                            wzSpineAnimationItem.LoadResources(graphicsDevice);
                        }
                    }

                    // Preview Image
                    Bitmap bitmap = spineCanvas.GetLinkedWzCanvasBitmap();

                    // Origin
                    PointF origin__ = spineCanvas.GetCanvasOriginPosition();

                    return new BackgroundInfo(parentObject, bitmap, WzInfoTools.PointFToSystemPoint(origin__), bS, type, no, parentObject, wzSpineAnimationItem);
                }
                else
                {
                    PointF origin_ = new PointF();
                    return new BackgroundInfo(parentObject, Properties.Resources.placeholder, WzInfoTools.PointFToSystemPoint(origin_), bS, type, no, parentObject, null);
                }
            }
            else
                frame0 = (WzCanvasProperty)WzInfoTools.GetRealProperty(parentObject);

            PointF origin = frame0.GetCanvasOriginPosition();
            return new BackgroundInfo(frame0, frame0.GetLinkedWzCanvasBitmap(), WzInfoTools.PointFToSystemPoint(origin), bS, type, no, parentObject, null);
        }

        /// <summary>
        /// Creates an instance of BoardItem from editor panels
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="board"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            return CreateInstance(board, x, y, z, -100, -100, 0, 0, 0, 255, false, flip, 0, null, false);
        }

        /// <summary>
        /// Creates an instance of BoardItem from file
        /// </summary>
        /// <param name="board"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="rx"></param>
        /// <param name="ry"></param>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="type"></param>
        /// <param name="a"></param>
        /// <param name="front"></param>
        /// <param name="flip"></param>
        /// <param name="screenMode">The screen resolution to display this background object. (0 = all res)</param>
        /// <param name="spineAni"></param>
        /// <param name="spineRandomStart"></param>
        /// <returns></returns>
        public BoardItem CreateInstance(Board board, int x, int y, int z, int rx, int ry, int cx, int cy, BackgroundType type, int a, bool front, bool flip, int screenMode, 
            string spineAni, bool spineRandomStart)
        {
            if (spineAni == null) // if one isnt set already, via pre-existing object in map. It probably means its created via BackgroundPanel
            { 
                // attempt to get one
                if (wzSpineAnimationItem != null && wzSpineAnimationItem.SkeletonData.Animations.Count > 0)
                {
                    spineAni = wzSpineAnimationItem.SkeletonData.Animations[0].Name; // actually we should allow the user to select, but nexon only places 1 animation for now
                }
            }
            return new BackgroundInstance(this, board, x, y, z, rx, ry, cx, cy, type, a, front, flip, screenMode, 
                spineAni, spineRandomStart);
        }


        #region Members
        public string bS
        {
            get { return _bS; }
            set { this._bS = value; }
        }

        /// <summary>
        /// The background information type (animation, spine, background)
        /// </summary>
        public BackgroundInfoType Type
        {
            get { return _type; }
            set { this._type = value; }
        }

        public string no
        {
            get { return _no; }
            set { this._no = value; }
        }

        /// <summary>
        /// The WzImageProperty where the BackgroundInfo is loaded from
        /// </summary>
        public WzImageProperty WzImageProperty
        {
            get { return imageProperty; }
            private set {  }
        }
        #endregion
    }
}
