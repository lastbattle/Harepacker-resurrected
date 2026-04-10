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
            Dictionary<int, List<IDXObject>> stateHitFrames = LoadReactorHitFrames(
                texturePool,
                linkedReactorImage,
                reactorInstance,
                device,
                usedProps);
            Dictionary<int, WzImageProperty> stateLayerProperties = GetReactorStateLayerProperties(linkedReactorImage);
            Dictionary<int, WzImageProperty> stateHitProperties = GetReactorStateHitProperties(linkedReactorImage);
            Dictionary<(int State, int ProperEventIndex), List<IDXObject>> stateIndexedHitFrames = LoadReactorIndexedHitFrames(
                texturePool,
                linkedReactorImage,
                reactorInstance,
                device,
                usedProps);
            Dictionary<(int State, int ProperEventIndex), WzImageProperty> stateIndexedHitProperties = GetReactorIndexedHitProperties(linkedReactorImage);
            WzImageProperty rootHitProperty = ResolveExactReactorSourceProperty(linkedReactorImage?["hit"]);
            List<IDXObject> rootHitFrames = LoadReactorFramesForProperty(
                texturePool,
                rootHitProperty,
                reactorInstance.X,
                reactorInstance.Y,
                device,
                usedProps);
            if (stateFrames.Count == 0 && stateLayerProperties.Count > 0)
            {
                int bootstrapState = stateLayerProperties.Keys.Min();
                List<IDXObject> bootstrapFrames = LoadReactorFramesForExactSourceProperty(
                    texturePool,
                    stateLayerProperties[bootstrapState],
                    reactorInstance.X,
                    reactorInstance.Y,
                    device,
                    usedProps);
                if (bootstrapFrames.Count > 0)
                {
                    stateFrames[bootstrapState] = bootstrapFrames;
                }
            }

            if (stateFrames.Count == 0)
                return null;

            List<IDXObject> LoadExactReactorFrames(WzImageProperty property)
            {
                return LoadReactorFramesForExactSourceProperty(
                    texturePool,
                    property,
                    reactorInstance.X,
                    reactorInstance.Y,
                    device,
                    usedProps);
            }

            return new ReactorItem(
                reactorInstance,
                stateFrames,
                stateHitFrames,
                stateIndexedHitFrames,
                rootHitFrames,
                stateLayerProperties,
                stateHitProperties,
                stateIndexedHitProperties,
                rootHitProperty,
                LoadExactReactorFrames);
        }

        internal static WzImageProperty ResolveReactorFrameSourceProperty(WzImageProperty property)
        {
            WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(property);
            if (resolvedProperty == null)
            {
                return null;
            }

            if (resolvedProperty is WzCanvasProperty || HasDirectNumericFrames(resolvedProperty))
            {
                return resolvedProperty;
            }

            WzImageProperty nestedDefaultFrames = WzInfoTools.GetRealProperty(resolvedProperty["0"]);
            if (nestedDefaultFrames is WzCanvasProperty || HasDirectNumericFrames(nestedDefaultFrames))
            {
                return resolvedProperty;
            }

            return null;
        }

        internal static WzImageProperty ResolveExactReactorSourceProperty(WzImageProperty property)
        {
            WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(property);
            return resolvedProperty is WzSubProperty or WzCanvasProperty
                ? resolvedProperty
                : null;
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

            return new List<IDXObject>();
        }

        private static Dictionary<int, List<IDXObject>> LoadReactorHitFrames(
            TexturePool texturePool,
            WzImage linkedReactorImage,
            ReactorInstance reactorInstance,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            Dictionary<int, List<IDXObject>> stateHitFrames = new Dictionary<int, List<IDXObject>>();
            if (linkedReactorImage == null)
            {
                return stateHitFrames;
            }

            IEnumerable<int> stateIds = linkedReactorImage.WzProperties
                .Select(prop => prop?.Name)
                .Where(name => int.TryParse(name, out _))
                .Select(int.Parse)
                .OrderBy(state => state);

            foreach (int state in stateIds)
            {
                WzImageProperty stateProperty = WzInfoTools.GetRealProperty(linkedReactorImage[state.ToString()]);
                List<IDXObject> frames = LoadReactorFramesForProperty(
                    texturePool,
                    WzInfoTools.GetRealProperty(stateProperty?["hit"]),
                    reactorInstance.X,
                    reactorInstance.Y,
                    device,
                    usedProps);
                if (frames.Count > 0)
                {
                    stateHitFrames[state] = frames;
                }
            }

            return stateHitFrames;
        }

        private static Dictionary<int, WzImageProperty> GetReactorStateLayerProperties(WzImage linkedReactorImage)
        {
            Dictionary<int, WzImageProperty> stateProperties = new Dictionary<int, WzImageProperty>();
            if (linkedReactorImage == null)
            {
                return stateProperties;
            }

            IEnumerable<int> stateIds = linkedReactorImage.WzProperties
                .Select(prop => prop?.Name)
                .Where(name => int.TryParse(name, out _))
                .Select(int.Parse)
                .OrderBy(state => state);

            foreach (int state in stateIds)
            {
                WzImageProperty stateProperty = ResolveExactReactorSourceProperty(linkedReactorImage[state.ToString()]);
                if (stateProperty != null)
                {
                    stateProperties[state] = stateProperty;
                }
            }

            return stateProperties;
        }

        private static Dictionary<int, WzImageProperty> GetReactorStateHitProperties(WzImage linkedReactorImage)
        {
            Dictionary<int, WzImageProperty> hitProperties = new Dictionary<int, WzImageProperty>();
            if (linkedReactorImage == null)
            {
                return hitProperties;
            }

            IEnumerable<int> stateIds = linkedReactorImage.WzProperties
                .Select(prop => prop?.Name)
                .Where(name => int.TryParse(name, out _))
                .Select(int.Parse)
                .OrderBy(state => state);

            foreach (int state in stateIds)
            {
                WzImageProperty hitProperty = ResolveExactReactorSourceProperty(
                    WzInfoTools.GetRealProperty(linkedReactorImage[state.ToString()])?["hit"]);
                if (hitProperty != null)
                {
                    hitProperties[state] = hitProperty;
                }
            }

            return hitProperties;
        }

        private static Dictionary<(int State, int ProperEventIndex), List<IDXObject>> LoadReactorIndexedHitFrames(
            TexturePool texturePool,
            WzImage linkedReactorImage,
            ReactorInstance reactorInstance,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            Dictionary<(int State, int ProperEventIndex), List<IDXObject>> indexedHitFrames = new Dictionary<(int State, int ProperEventIndex), List<IDXObject>>();
            if (linkedReactorImage == null)
            {
                return indexedHitFrames;
            }

            IEnumerable<int> stateIds = linkedReactorImage.WzProperties
                .Select(prop => prop?.Name)
                .Where(name => int.TryParse(name, out _))
                .Select(int.Parse)
                .OrderBy(state => state);

            foreach (int state in stateIds)
            {
                WzImageProperty stateProperty = WzInfoTools.GetRealProperty(linkedReactorImage[state.ToString()]);
                if (stateProperty?.WzProperties == null)
                {
                    continue;
                }

                foreach ((int properEventIndex, WzImageProperty eventProperty) in EnumerateReactorIndexedHitProperties(stateProperty))
                {
                    if (!IsReactorIndexedHitPropertyCandidate(eventProperty))
                    {
                        continue;
                    }

                    List<IDXObject> frames = LoadReactorFramesForExactSourceProperty(
                        texturePool,
                        WzInfoTools.GetRealProperty(eventProperty),
                        reactorInstance.X,
                        reactorInstance.Y,
                        device,
                        usedProps);
                    if (frames.Count > 0)
                    {
                        indexedHitFrames[(state, properEventIndex)] = frames;
                    }
                }
            }

            return indexedHitFrames;
        }

        internal static Dictionary<(int State, int ProperEventIndex), WzImageProperty> GetReactorIndexedHitProperties(WzImage linkedReactorImage)
        {
            Dictionary<(int State, int ProperEventIndex), WzImageProperty> indexedHitProperties = new Dictionary<(int State, int ProperEventIndex), WzImageProperty>();
            if (linkedReactorImage == null)
            {
                return indexedHitProperties;
            }

            IEnumerable<int> stateIds = linkedReactorImage.WzProperties
                .Select(prop => prop?.Name)
                .Where(name => int.TryParse(name, out _))
                .Select(int.Parse)
                .OrderBy(state => state);

            foreach (int state in stateIds)
            {
                WzImageProperty stateProperty = WzInfoTools.GetRealProperty(linkedReactorImage[state.ToString()]);
                if (stateProperty?.WzProperties == null)
                {
                    continue;
                }

                foreach ((int properEventIndex, WzImageProperty eventProperty) in EnumerateReactorIndexedHitProperties(stateProperty))
                {
                    WzImageProperty hitProperty = ResolveExactReactorSourceProperty(eventProperty);
                    if (hitProperty == null)
                    {
                        continue;
                    }

                    indexedHitProperties[(state, properEventIndex)] = hitProperty;
                }
            }

            return indexedHitProperties;
        }

        internal static bool IsReactorIndexedHitPropertyCandidate(WzImageProperty property)
        {
            WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(property);
            if (resolvedProperty is not WzSubProperty)
            {
                return false;
            }

            return ResolveReactorFrameSourceProperty(resolvedProperty) != null
                || ResolveReactorFrameSourceProperty(WzInfoTools.GetRealProperty(resolvedProperty["hit"])) != null;
        }

        private static IEnumerable<(int ProperEventIndex, WzImageProperty EventProperty)> EnumerateReactorIndexedHitProperties(WzImageProperty stateProperty)
        {
            WzImageProperty resolvedStateProperty = WzInfoTools.GetRealProperty(stateProperty);
            if (resolvedStateProperty?.WzProperties == null)
            {
                yield break;
            }

            HashSet<int> yieldedIndices = new HashSet<int>();
            if (WzInfoTools.GetRealProperty(resolvedStateProperty["event"]) is WzSubProperty eventProperty)
            {
                foreach (WzImageProperty child in eventProperty.WzProperties)
                {
                    if (int.TryParse(child?.Name, out int properEventIndex)
                        && yieldedIndices.Add(properEventIndex))
                    {
                        yield return (properEventIndex, child);
                    }
                }
            }

            foreach (WzImageProperty child in resolvedStateProperty.WzProperties)
            {
                if (int.TryParse(child?.Name, out int properEventIndex)
                    && WzInfoTools.GetRealProperty(child) is WzSubProperty
                    && yieldedIndices.Add(properEventIndex))
                {
                    yield return (properEventIndex, child);
                }
            }
        }

        private static List<IDXObject> LoadReactorFramesForProperty(
            TexturePool texturePool,
            WzImageProperty property,
            int x,
            int y,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(property);
            if (resolvedProperty == null)
            {
                return new List<IDXObject>();
            }

            if (resolvedProperty is WzCanvasProperty || HasDirectNumericFrames(resolvedProperty))
            {
                return MapSimulatorLoader.LoadFrames(texturePool, resolvedProperty, x, y, device, usedProps);
            }

            WzImageProperty nestedDefaultFrames = WzInfoTools.GetRealProperty(resolvedProperty["0"]);
            if (nestedDefaultFrames is WzCanvasProperty || HasDirectNumericFrames(nestedDefaultFrames))
            {
                return MapSimulatorLoader.LoadFrames(texturePool, nestedDefaultFrames, x, y, device, usedProps);
            }

            return new List<IDXObject>();
        }

        private static List<IDXObject> LoadReactorFramesForExactSourceProperty(
            TexturePool texturePool,
            WzImageProperty property,
            int x,
            int y,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            List<IDXObject> frames = LoadReactorFramesForProperty(
                texturePool,
                property,
                x,
                y,
                device,
                usedProps);
            if (frames.Count > 0)
            {
                return frames;
            }

            WzImageProperty resolvedProperty = WzInfoTools.GetRealProperty(property);
            WzImageProperty nestedHitProperty = WzInfoTools.GetRealProperty(resolvedProperty?["hit"]);
            if (nestedHitProperty == null)
            {
                return new List<IDXObject>();
            }

            return LoadReactorFramesForProperty(
                texturePool,
                nestedHitProperty,
                x,
                y,
                device,
                usedProps);
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
