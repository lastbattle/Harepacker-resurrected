/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Info
{
    public class PortalInfo : MapleDrawableInfo
    {
        private PortalType type;

        public PortalInfo(PortalType type, Bitmap image, System.Drawing.Point origin, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            this.type = type;
        }

        public static PortalInfo Load(WzCanvasProperty parentObject)
        {
            PortalInfo portal = new PortalInfo(
                PortalTypeExtensions.FromCode(parentObject.Name), 
                parentObject.GetLinkedWzCanvasBitmap(), 
                WzInfoTools.PointFToSystemPoint(parentObject.GetCanvasOriginPosition()), parentObject);
            Program.InfoManager.Portals.Add(portal.type, portal);
            return portal;
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            switch (type)
            {
                case PortalType.StartPoint:
                    {
                        return new PortalInstance(this, board, x, y, "sp", type, "", MapConstants.MaxMap, null, null, null, null, null, null, null, null, null);
                    }
                case PortalType.Invisible:
                case PortalType.Visible:
                case PortalType.Collision:
                case PortalType.Changeable:
                case PortalType.ChangeableInvisible:
                    {
                        return new PortalInstance(this, board, x, y, "portal", type, "", MapConstants.MaxMap, null, null, null, null, null, null, null, null, null);
                    }
                case PortalType.TownPortalPoint:
                    {
                        return new PortalInstance(this, board, x, y, "tp", type, "", MapConstants.MaxMap, null, null, null, null, null, null, null, null, null);
                    }
                case PortalType.Script:
                case PortalType.ScriptInvisible:
                case PortalType.CollisionScript:
                    {
                        return new PortalInstance(this, board, x, y, "portal", type, "", MapConstants.MaxMap, "script", null, null, null, null, null, null, null, null);
                    }
                case PortalType.Hidden:
                    {
                        return new PortalInstance(this, board, x, y, "portal", type, "", MapConstants.MaxMap, null, null, null, null, null, null, "", null, null);
                    }
                case PortalType.ScriptHidden:
                    {
                        return new PortalInstance(this, board, x, y, "portal", type, "", MapConstants.MaxMap, "script", null, null, null, null, null, "", null, null);
                    }
                case PortalType.CollisionVerticalJump:
                case PortalType.CollisionCustomImpact:
                case PortalType.CollisionUnknownPcig:
                    {
                        return new PortalInstance(this, board, x, y, "portal", type, "", MapConstants.MaxMap, "script", null, null, null, null, null, "", null, null);
                    }
                case PortalType.ScriptHiddenUng: // TODO
                default:
                    throw new Exception("unknown pt @ CreateInstance, type: " + type);
            }
        }

        public PortalInstance CreateInstance(Board board, int x, int y, string pn, string tn, int tm, string script, int? delay, MapleBool hideTooltip, MapleBool onlyOnce, int? horizontalImpact, int? verticalImpact, string image, int? hRange, int? vRange)
        {
            return new PortalInstance(this, board, x, y, pn, type, tn, tm, script, delay, hideTooltip, onlyOnce, horizontalImpact, verticalImpact, image, hRange, vRange);
        }

        public PortalType Type
        {
            get { return type; }
        }

        public static PortalInfo GetPortalInfoByType(PortalType type)
        {
            return Program.InfoManager.Portals[type];
        }
    }
}
