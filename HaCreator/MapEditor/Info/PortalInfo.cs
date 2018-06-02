/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
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
        private string type;

        public PortalInfo(string type, Bitmap image, System.Drawing.Point origin, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            this.type = type;
        }

        public static PortalInfo Load(WzCanvasProperty parentObject)
        {
            PortalInfo portal = new PortalInfo(parentObject.Name, parentObject.PngProperty.GetPNG(false), WzInfoTools.VectorToSystemPoint((WzVectorProperty)parentObject["origin"]), parentObject);
            Program.InfoManager.Portals.Add(portal.type, portal);
            return portal;
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            switch (type)
            {
                case PortalType.PORTALTYPE_STARTPOINT:
                    return new PortalInstance(this, board, x, y, "sp", type, "", 999999999, null, null, null, null, null, null, null, null, null);
                case PortalType.PORTALTYPE_INVISIBLE:
                case PortalType.PORTALTYPE_VISIBLE:
                case PortalType.PORTALTYPE_COLLISION:
                case PortalType.PORTALTYPE_CHANGABLE:
                case PortalType.PORTALTYPE_CHANGABLE_INVISIBLE:
                    return new PortalInstance(this, board, x, y, "portal", type, "", 999999999, null, null, null, null, null, null, null, null, null);
                case PortalType.PORTALTYPE_TOWNPORTAL_POINT:
                    return new PortalInstance(this, board, x, y, "tp", type, "", 999999999, null, null, null, null, null, null, null, null, null);
                case PortalType.PORTALTYPE_SCRIPT:
                case PortalType.PORTALTYPE_SCRIPT_INVISIBLE:
                case PortalType.PORTALTYPE_COLLISION_SCRIPT:
                    return new PortalInstance(this, board, x, y, "portal", type, "", 999999999, "script", null, null, null, null, null, null, null, null);
                case PortalType.PORTALTYPE_HIDDEN:
                    return new PortalInstance(this, board, x, y, "portal", type, "", 999999999, null, null, null, null, null, null, "", null, null);
                case PortalType.PORTALTYPE_SCRIPT_HIDDEN:
                    return new PortalInstance(this, board, x, y, "portal", type, "", 999999999, "script", null, null, null, null, null, "", null, null);
                case PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP:
                case PortalType.PORTALTYPE_COLLISION_CUSTOM_IMPACT:
                case PortalType.PORTALTYPE_COLLISION_UNKNOWN_PCIG:
                    return new PortalInstance(this, board, x, y, "portal", type, "", 999999999, "script", null, null, null, null, null, "", null, null);
                default:
                    throw new Exception("unknown pt @ CreateInstance");
            }
        }

        public PortalInstance CreateInstance(Board board, int x, int y, string pn, string tn, int tm, string script, int? delay, MapleBool hideTooltip, MapleBool onlyOnce, int? horizontalImpact, int? verticalImpact, string image, int? hRange, int? vRange)
        {
            return new PortalInstance(this, board, x, y, pn, type, tn, tm, script, delay, hideTooltip, onlyOnce, horizontalImpact, verticalImpact, image, hRange, vRange);
        }

        public string Type
        {
            get { return type; }
        }

        public static PortalInfo GetPortalInfoByType(string type)
        {
            return Program.InfoManager.Portals[type];
        }
    }
}
