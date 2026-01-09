using System.Linq;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaSharedLibrary;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using HaCreator.MapSimulator.Pools;

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
                        case "attack3":
                        case "skill1":
                        case "skill2":
                        case "skill3":
                        case "chase":
                        case "regen":
                            {
                                // Load frames for this specific action
                                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }

                                // Load hit effect frames for attack actions (attack1/info/hit, attack2/info/hit, etc.)
                                if (actionName.StartsWith("attack"))
                                {
                                    WzSubProperty infoNode = mobStateProperty["info"] as WzSubProperty;
                                    WzSubProperty hitNode = infoNode?["hit"] as WzSubProperty;
                                    if (hitNode != null)
                                    {
                                        List<IDXObject> hitFrames = MapSimulatorLoader.LoadFrames(texturePool, hitNode, 0, 0, device, ref usedProps);
                                        if (hitFrames.Count > 0)
                                        {
                                            animationSet.AddAttackHitEffect(actionName, hitFrames);
                                            System.Diagnostics.Debug.WriteLine($"[LifeLoader] Loaded {hitFrames.Count} hit effect frames for mob {mobInfo.ID} {actionName}");
                                        }
                                    }
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

            var mobItem = new MobItem(mobInstance, animationSet, nameTooltip);

            // Load mob-specific sounds from Sound.wz/Mob.img/{mobId}/
            LoadMobSounds(mobItem, mobInfo.ID);

            return mobItem;
        }

        /// <summary>
        /// Loads mob-specific sounds from Sound.wz/Mob.img/{mobId}/
        /// </summary>
        private static void LoadMobSounds(MobItem mobItem, string mobId)
        {
            if (string.IsNullOrEmpty(mobId))
            {
                System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: mobId is null or empty");
                return;
            }
            WzImage mobSoundImage = Program.FindImage("Sound", "Mob");
            if (mobSoundImage == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: Sound/Mob.img not found!");
                return;
            }

            // Look for the mob's sound directory (e.g., "0100100")
            WzSubProperty mobSounds = mobSoundImage[mobId.PadLeft(7, '0')] as WzSubProperty;
            if (mobSounds == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LifeLoader] LoadMobSounds: Mob '{mobId}' not found in Mob.img. Available: {string.Join(", ", mobSoundImage.WzProperties?.Take(10).Select(p => p.Name) ?? new string[0])}...");
                return;
            }

            // Load Damage sound
            WzSoundResourceStreamer damageSE = LoadSoundFromProperty(mobSounds["Damage"]);

            // Load Die sound
            WzSoundResourceStreamer dieSE = LoadSoundFromProperty(mobSounds["Die"]);

            // Load Attack1 sound
            WzSoundResourceStreamer attack1SE = LoadSoundFromProperty(mobSounds["Attack1"]);

            // Load Attack2 sound
            WzSoundResourceStreamer attack2SE = LoadSoundFromProperty(mobSounds["Attack2"]);

            // Load CharDam1 sound (character damage when mob hits player)
            WzSoundResourceStreamer charDam1SE = LoadSoundFromProperty(mobSounds["CharDam1"]);

            // Load CharDam2 sound
            WzSoundResourceStreamer charDam2SE = LoadSoundFromProperty(mobSounds["CharDam2"]);

            // Set sounds on mob item
            if (damageSE != null || dieSE != null)
            {
                mobItem.SetSounds(damageSE, dieSE);
            }

            if (attack1SE != null || attack2SE != null)
            {
                mobItem.SetAttackSounds(attack1SE, attack2SE);
            }

            if (charDam1SE != null || charDam2SE != null)
            {
                mobItem.SetCharDamSounds(charDam1SE, charDam2SE);
            }
        }

        /// <summary>
        /// Helper method to load a sound from a WZ property (handles UOL links)
        /// </summary>
        private static WzSoundResourceStreamer LoadSoundFromProperty(WzImageProperty prop)
        {
            if (prop == null)
                return null;

            WzBinaryProperty soundProp = prop as WzBinaryProperty
                ?? (prop as WzUOLProperty)?.LinkValue as WzBinaryProperty;

            if (soundProp != null)
            {
                return new WzSoundResourceStreamer(soundProp, false);
            }
            return null;
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
