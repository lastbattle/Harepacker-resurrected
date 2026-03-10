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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Wz;

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
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps)
        {
            ReactorInfo reactorInfo = (ReactorInfo)reactorInstance.BaseInfo;
            WzImage linkedReactorImage = reactorInfo.LinkedWzImage;
            Dictionary<int, List<IDXObject>> stateFrames = LoadReactorStateFrames(
                texturePool,
                linkedReactorImage,
                reactorInstance,
                device,
                usedProps);
            if (stateFrames.Count == 0)
                return null;

            return new ReactorItem(reactorInstance, stateFrames);
        }

        private static Dictionary<int, List<IDXObject>> LoadReactorStateFrames(
            TexturePool texturePool,
            WzImage linkedReactorImage,
            ReactorInstance reactorInstance,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            Dictionary<int, List<IDXObject>> stateFrames = new Dictionary<int, List<IDXObject>>();
            if (linkedReactorImage == null)
                return stateFrames;

            IEnumerable<int> stateIds = linkedReactorImage.WzProperties
                .Select(prop => prop?.Name)
                .Where(name => int.TryParse(name, out _))
                .Select(int.Parse)
                .OrderBy(state => state);

            foreach (int state in stateIds)
            {
                WzImageProperty stateProperty = WzInfoTools.GetRealProperty(linkedReactorImage[state.ToString()]);
                if (stateProperty == null)
                    continue;

                List<IDXObject> frames = LoadReactorFramesForState(
                    texturePool,
                    stateProperty,
                    reactorInstance.X,
                    reactorInstance.Y,
                    device,
                    usedProps);
                if (frames.Count > 0)
                {
                    stateFrames[state] = frames;
                }
            }

            return stateFrames;
        }

        private static List<IDXObject> LoadReactorFramesForState(
            TexturePool texturePool,
            WzImageProperty stateProperty,
            int x,
            int y,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            WzImageProperty resolvedStateProperty = WzInfoTools.GetRealProperty(stateProperty);
            if (resolvedStateProperty == null)
                return new List<IDXObject>();

            if (HasDirectNumericFrames(resolvedStateProperty))
            {
                return MapSimulatorLoader.LoadFrames(texturePool, resolvedStateProperty, x, y, device, usedProps);
            }

            WzImageProperty nestedDefaultFrames = WzInfoTools.GetRealProperty(resolvedStateProperty["0"]);
            if (nestedDefaultFrames != null)
            {
                return MapSimulatorLoader.LoadFrames(texturePool, nestedDefaultFrames, x, y, device, usedProps);
            }

            WzImageProperty hitFrames = WzInfoTools.GetRealProperty(resolvedStateProperty["hit"]);
            if (hitFrames != null)
            {
                return MapSimulatorLoader.LoadFrames(texturePool, hitFrames, x, y, device, usedProps);
            }

            return new List<IDXObject>();
        }

        private static bool HasDirectNumericFrames(WzImageProperty stateProperty)
        {
            if (stateProperty is not WzSubProperty subProperty)
                return false;

            return subProperty.WzProperties.Any(prop => int.TryParse(prop.Name, out _));
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
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps)
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
                    frames.AddRange(MapSimulatorLoader.LoadFrames(texturePool, portalTypeProperty, portalInstance.X, portalInstance.Y, device, usedProps));
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
                        frames.AddRange(MapSimulatorLoader.LoadFrames(texturePool, framesPropertyParent, portalInstance.X, portalInstance.Y, device, usedProps));
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
