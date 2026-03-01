using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using HaCreator.Wz;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using HaCreator.MapSimulator.Pools;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Handles loading of effect objects (Portals, Reactors) for MapSimulator.
    /// Extracted from MapSimulatorLoader for better code organization.
    /// </summary>
    public static class EffectLoader
    {
        #region Reactor
        /// <summary>
        /// Create reactor item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="reactorInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static ReactorItem CreateReactorFromProperty(
            TexturePool texturePool, ReactorInstance reactorInstance,
            GraphicsDevice device, ref List<WzObject> usedProps)
        {
            ReactorInfo reactorInfo = (ReactorInfo)reactorInstance.BaseInfo;

            List<IDXObject> frames = new List<IDXObject>();

            WzImage linkedReactorImage = reactorInfo.LinkedWzImage;
            if (linkedReactorImage != null)
            {
                WzImageProperty framesImage = (WzImageProperty)linkedReactorImage?["0"]?["0"];
                if (framesImage != null)
                {
                    frames = MapSimulatorLoader.LoadFrames(texturePool, framesImage, reactorInstance.X, reactorInstance.Y, device, ref usedProps);
                }
            }
            if (frames.Count == 0)
            {
                //string error = string.Format("[MapSimulatorLoader] 0 frames loaded for reactor from src: '{0}'",  reactorInfo.ID);

                //ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }
            return new ReactorItem(reactorInstance, frames);
        }
        #endregion

        #region Portal
        /// <summary>
        /// Create portal item from Map.wz/MapHelper.img/portal/game
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="gameParent"></param>
        /// <param name="portalInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static PortalItem CreatePortalFromProperty(
            TexturePool texturePool, WzSubProperty gameParent, PortalInstance portalInstance,
            GraphicsDevice device, ref List<WzObject> usedProps)
        {
            PortalInfo portalInfo = (PortalInfo)portalInstance.BaseInfo;

            if (portalInstance.pt == PortalType.StartPoint ||
                portalInstance.pt == PortalType.Invisible ||
                //portalInstance.pt == PortalType.PORTALTYPE_CHANGABLE_INVISIBLE ||
                portalInstance.pt == PortalType.ScriptInvisible ||
                portalInstance.pt == PortalType.Script ||
                portalInstance.pt == PortalType.Collision ||
                portalInstance.pt == PortalType.CollisionScript ||
                portalInstance.pt == PortalType.CollisionCustomImpact || // springs in Mechanical grave 350040240
                portalInstance.pt == PortalType.CollisionVerticalJump) // vertical spring actually
                return null;

            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            //string portalType = portalInstance.pt;
            //int portalId = Program.InfoManager.PortalIdByType[portalInstance.pt];

            WzSubProperty portalTypeProperty = (WzSubProperty)gameParent[portalInstance.pt.ToCode()];
            if (portalTypeProperty == null)
            {
                portalTypeProperty = (WzSubProperty)gameParent["pv"];
            }
            else
            {
                // Support for older versions of MapleStory where 'pv' is a subproperty for the image frame than a collection of subproperty of frames
                if (portalTypeProperty["0"] is WzCanvasProperty)
                {
                    frames.AddRange(MapSimulatorLoader.LoadFrames(texturePool, portalTypeProperty, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    portalTypeProperty = null;
                }
            }

            if (portalTypeProperty != null)
            {
                WzSubProperty portalImageProperty = (WzSubProperty)portalTypeProperty[portalInstance.image == null ? "default" : portalInstance.image];

                if (portalImageProperty != null)
                {
                    WzSubProperty framesPropertyParent;
                    if (portalImageProperty["portalContinue"] != null)
                        framesPropertyParent = (WzSubProperty)portalImageProperty["portalContinue"];
                    else
                        framesPropertyParent = (WzSubProperty)portalImageProperty;

                    if (framesPropertyParent != null)
                    {
                        frames.AddRange(MapSimulatorLoader.LoadFrames(texturePool, framesPropertyParent, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    }
                }
            }
            if (frames.Count == 0)
                return null;
            return new PortalItem(portalInstance, frames);
        }
        #endregion
    }
}
