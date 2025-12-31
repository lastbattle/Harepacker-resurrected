using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.MapObjects.FieldObject;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Handles loading of life objects (Mobs, NPCs) for MapSimulator.
    /// Extracted from MapSimulatorLoader for better code organization.
    /// </summary>
    public static class LifeLoader
    {
        #region Mob
        /// <summary>
        /// Creates a MobItem with separate animations for each action (stand, move, fly, etc.)
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="mobInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(
            TexturePool texturePool, MobInstance mobInstance, float UserScreenScaleFactor,
            GraphicsDevice device, ref List<WzObject> usedProps)
        {
            MobInfo mobInfo = (MobInfo)mobInstance.BaseInfo;
            WzImage source = mobInfo.LinkedWzImage;

            // Create animation set to store frames per action
            MobAnimationSet animationSet = new MobAnimationSet();

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                if (childProperty is WzSubProperty mobStateProperty) // issue with 867119250, Eluna map mobs
                {
                    string actionName = mobStateProperty.Name.ToLower();

                    switch (actionName)
                    {
                        case "info": // info/speak/0 WzStringProperty - skip info node
                            break;

                        case "stand":
                        case "move":
                        case "walk":
                        case "fly":
                        case "jump":
                        case "hit1":
                        case "die1":
                        case "die2":
                        case "attack1":
                        case "attack2":
                        case "skill1":
                        case "skill2":
                        case "chase":
                        case "regen":
                            {
                                // Load frames for this specific action
                                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }
                                break;
                            }

                        default:
                            {
                                // For unknown actions, still load them in case they're needed
                                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }
                                break;
                            }
                    }
                }
            }

            System.Drawing.Color color_foreGround = System.Drawing.Color.White; // mob foreground color
            NameTooltipItem nameTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                mobInstance.MobInfo.Name, mobInstance.X, mobInstance.Y, color_foreGround,
                texturePool, UserScreenScaleFactor, device);

            return new MobItem(mobInstance, animationSet, nameTooltip);
        }
        #endregion

        #region NPC
        /// <summary>
        /// NPC
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="npcInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(
            TexturePool texturePool, NpcInstance npcInstance, float UserScreenScaleFactor,
            GraphicsDevice device, ref List<WzObject> usedProps)
        {
            NpcInfo npcInfo = (NpcInfo)npcInstance.BaseInfo;
            WzImage source = npcInfo.LinkedWzImage;

            // Create animation set to store frames by action (stand, speak, blink, etc.)
            NpcAnimationSet animationSet = new NpcAnimationSet();

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                WzSubProperty npcStateProperty = (WzSubProperty)childProperty;
                switch (npcStateProperty.Name)
                {
                    case "info": // info/speak/0 WzStringProperty
                        {
                            break;
                        }
                    default:
                        {
                            // Load frames for this action and store by action name
                            List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, npcStateProperty, npcInstance.X, npcInstance.Y, device, ref usedProps);
                            if (actionFrames.Count > 0)
                            {
                                animationSet.AddAnimation(npcStateProperty.Name, actionFrames);
                            }
                            break;
                        }
                }
            }
            if (animationSet.ActionCount == 0) // fix japan ms v186, (9000021.img「ガガ」) なぜだ？;(
                return null;

            System.Drawing.Color color_foreGround = System.Drawing.Color.FromArgb(255, 255, 255, 0); // gold npc foreground color

            NameTooltipItem nameTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                npcInstance.NpcInfo.StringName, npcInstance.X, npcInstance.Y, color_foreGround,
                texturePool, UserScreenScaleFactor, device);

            const int NPC_FUNC_Y_POS = 17;

            NameTooltipItem npcDescTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                npcInstance.NpcInfo.StringFunc, npcInstance.X, npcInstance.Y + NPC_FUNC_Y_POS, color_foreGround,
                texturePool, UserScreenScaleFactor, device);

            return new NpcItem(npcInstance, animationSet, nameTooltip, npcDescTooltip);
        }
        #endregion
    }
}
