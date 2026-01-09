using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Loads character parts from Character.wz
    /// </summary>
    public class CharacterLoader
    {
        private readonly WzFile _characterWz;
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;

        // Cache for loaded parts
        private readonly Dictionary<int, BodyPart> _bodyCache = new();
        private readonly Dictionary<int, FacePart> _faceCache = new();
        private readonly Dictionary<int, HairPart> _hairCache = new();
        private readonly Dictionary<int, CharacterPart> _equipCache = new();

        // Standard actions to load
        private static readonly string[] StandardActions = new[]
        {
            "stand1", "stand2", "walk1", "walk2", "jump", "sit", "prone",
            "ladder", "rope", "alert", "heal"
        };

        // Attack actions
        private static readonly string[] AttackActions = new[]
        {
            "stabO1", "stabO2", "stabOF", "stabT1", "stabT2", "stabTF",
            "swingO1", "swingO2", "swingO3", "swingOF", "swingT1", "swingT2", "swingT3", "swingTF",
            "swingP1", "swingP2", "swingPF", "shoot1", "shoot2", "shootF", "proneStab"
        };

        public CharacterLoader(WzFile characterWz, GraphicsDevice device, TexturePool texturePool)
        {
            _characterWz = characterWz; // Can be null - will use Program.FindImage fallback
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _texturePool = texturePool;
        }

        /// <summary>
        /// Get a character image by name, using WzFile if available or Program.FindImage fallback
        /// </summary>
        private WzImage GetCharacterImage(string imgName)
        {
            // Try WzFile first if available
            if (_characterWz?.WzDirectory != null)
            {
                var img = _characterWz.WzDirectory[imgName] as WzImage;
                if (img != null)
                    return img;
            }

            // Fall back to Program.FindImage (works with .img file loading)
            return Program.FindImage("Character", imgName);
        }

        #region Body/Head Loading

        /// <summary>
        /// Load body part (00002xxx.img)
        /// </summary>
        public BodyPart LoadBody(SkinColor skin)
        {
            int bodyId = 2000 + (int)skin;
            if (_bodyCache.TryGetValue(bodyId, out var cached))
                return cached;

            string imgName = bodyId.ToString("D8") + ".img";
            var imgNode = GetCharacterImage(imgName);

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadBody skin={skin}, imgName={imgName}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var body = new BodyPart
            {
                ItemId = bodyId,
                Name = $"Body_{skin}",
                Type = CharacterPartType.Body,
                SkinColor = skin,
                IsHead = false
            };

            LoadPartAnimations(body, imgNode as WzImage);
            _bodyCache[bodyId] = body;
            return body;
        }

        /// <summary>
        /// Load head part (00012xxx.img)
        /// </summary>
        public BodyPart LoadHead(SkinColor skin)
        {
            int headId = 12000 + (int)skin;
            if (_bodyCache.TryGetValue(headId, out var cached))
                return cached;

            string imgName = headId.ToString("D8") + ".img";
            var imgNode = GetCharacterImage(imgName);

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadHead skin={skin}, imgName={imgName}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var head = new BodyPart
            {
                ItemId = headId,
                Name = $"Head_{skin}",
                Type = CharacterPartType.Head,
                SkinColor = skin,
                IsHead = true
            };

            LoadPartAnimations(head, imgNode as WzImage);
            _bodyCache[headId] = head;
            return head;
        }

        #endregion

        #region Face Loading

        /// <summary>
        /// Load face from Face/xxxxx.img
        /// </summary>
        public FacePart LoadFace(int faceId)
        {
            if (_faceCache.TryGetValue(faceId, out var cached))
                return cached;

            string imgName = faceId.ToString("D8") + ".img";
            WzImage imgNode = null;

            // Try WzFile first
            var faceDir = _characterWz?.WzDirectory?["Face"];
            if (faceDir != null)
            {
                imgNode = faceDir[imgName] as WzImage;
            }

            // Fall back to getting Face directory via Program, then find image in it
            if (imgNode == null)
            {
                // First get the Character directory
                var charDirObj = Program.FindWzObject("Character", "");
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Character dir lookup: {charDirObj?.GetType().Name ?? "NULL"}");

                // Then get Face subdirectory from it
                if (charDirObj is MapleLib.Img.VirtualWzDirectory virtualCharDir)
                {
                    // Get Face subdirectory
                    var faceSubDir = virtualCharDir["Face"];
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face subdir: {faceSubDir?.GetType().Name ?? "NULL"}");

                    if (faceSubDir is MapleLib.Img.VirtualWzDirectory virtualFaceDir)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face VirtualDir path: {virtualFaceDir.FilesystemPath}");
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face VirtualDir has {virtualFaceDir.WzImages.Count} images:");
                        foreach (var img in virtualFaceDir.WzImages.Take(5))
                        {
                            System.Diagnostics.Debug.WriteLine($"  - {img.Name}");
                        }

                        imgNode = virtualFaceDir[imgName] as WzImage;
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] VirtualFaceDir[{imgName}] = {imgNode?.Name ?? "NULL"}");
                    }
                }
                else if (charDirObj is WzDirectory charWzDir)
                {
                    var faceWzDir = charWzDir["Face"] as WzDirectory;
                    if (faceWzDir != null)
                    {
                        imgNode = faceWzDir[imgName] as WzImage;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadFace id={faceId}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var face = new FacePart
            {
                ItemId = faceId,
                Name = GetItemName(imgNode as WzImage) ?? $"Face_{faceId}",
                Type = CharacterPartType.Face,
                Slot = EquipSlot.None
            };

            // Load face expressions
            LoadFaceExpressions(face, imgNode as WzImage);

            _faceCache[faceId] = face;
            return face;
        }

        private void LoadFaceExpressions(FacePart face, WzImage img)
        {
            if (img == null) return;

            img.ParseImage();

            // Debug: show top-level nodes in face image
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face image nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
                // Show subnodes for first expression
                if (prop is WzSubProperty subProp && prop.Name == "default")
                {
                    foreach (var child in subProp.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {child.Name} ({child.GetType().Name})");
                        // Show one more level
                        if (child is WzSubProperty childSub)
                        {
                            foreach (var grandchild in childSub.WzProperties)
                            {
                                System.Diagnostics.Debug.WriteLine($"      - {grandchild.Name} ({grandchild.GetType().Name})");
                            }
                        }
                    }
                }
            }

            // Standard expressions
            string[] expressions = { "default", "blink", "hit", "smile", "troubled", "cry", "angry", "bewildered", "stunned", "oops" };

            foreach (var expr in expressions)
            {
                var exprNode = img[expr];
                if (exprNode != null)
                {
                    var anim = LoadFaceAnimation(exprNode);
                    if (anim != null && anim.Frames.Count > 0)
                    {
                        anim.ActionName = expr;
                        face.Expressions[expr] = anim;
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face expression '{expr}' loaded with {anim.Frames.Count} frames");
                    }
                }
            }

            // Also check for numbered face actions
            foreach (WzImageProperty prop in img.WzProperties)
            {
                if (!expressions.Contains(prop.Name) && prop.Name != "info")
                {
                    var anim = LoadFaceAnimation(prop);
                    if (anim != null && anim.Frames.Count > 0)
                    {
                        anim.ActionName = prop.Name;
                        face.Expressions[prop.Name] = anim;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Face loaded with {face.Expressions.Count} expressions");
        }

        /// <summary>
        /// Load face animation - face structure is expression/face/0 or expression/0
        /// </summary>
        private CharacterAnimation LoadFaceAnimation(WzImageProperty node)
        {
            if (node == null) return null;

            var anim = new CharacterAnimation();

            if (node is WzSubProperty subProp)
            {
                // Try direct numbered frames first (expression/0, expression/1)
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = subProp[i.ToString()];
                    if (frameNode == null) break;

                    if (frameNode is WzCanvasProperty frameCanvas)
                    {
                        var frame = LoadFrame(frameCanvas, i.ToString());
                        if (frame != null) anim.Frames.Add(frame);
                    }
                }

                // If no direct frames, check for "face" subnode (expression/face/0)
                if (anim.Frames.Count == 0)
                {
                    var faceNode = subProp["face"];
                    if (faceNode is WzSubProperty faceSub)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            var frameNode = faceSub[i.ToString()];
                            if (frameNode == null) break;

                            if (frameNode is WzCanvasProperty frameCanvas)
                            {
                                var frame = LoadFrame(frameCanvas, i.ToString());
                                if (frame != null) anim.Frames.Add(frame);
                            }
                        }
                    }
                    // Also check if face is directly a canvas (expression/face is a single frame)
                    else if (faceNode is WzCanvasProperty faceCanvas)
                    {
                        var frame = LoadFrame(faceCanvas, "0");
                        if (frame != null) anim.Frames.Add(frame);
                    }
                }
            }
            else if (node is WzCanvasProperty canvas)
            {
                var frame = LoadFrame(canvas, "0");
                if (frame != null) anim.Frames.Add(frame);
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        #endregion

        #region Hair Loading

        /// <summary>
        /// Load hair from Hair/xxxxx.img
        /// </summary>
        public HairPart LoadHair(int hairId)
        {
            if (_hairCache.TryGetValue(hairId, out var cached))
                return cached;

            string imgName = hairId.ToString("D8") + ".img";
            WzImage imgNode = null;

            // Try WzFile first
            var hairDir = _characterWz?.WzDirectory?["Hair"];
            if (hairDir != null)
            {
                imgNode = hairDir[imgName] as WzImage;
            }

            // Fall back - try to load hair image directly
            if (imgNode == null)
            {
                // Try direct image lookup via Program.FindImage
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Trying direct Hair lookup: Hair/{imgName}");
                imgNode = Program.FindImage("Character", $"Hair/{imgName}");

                if (imgNode == null)
                {
                    // Try ImgFileSystemManager directly if available
                    var dataSource = Program.DataSource;
                    if (dataSource != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Trying DataSource for Hair...");
                        imgNode = dataSource.GetImage("Character", $"Hair/{imgName}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair lookup result: {imgNode?.Name ?? "NULL"}");
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadHair id={hairId}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            var hair = new HairPart
            {
                ItemId = hairId,
                Name = GetItemName(imgNode as WzImage) ?? $"Hair_{hairId}",
                Type = CharacterPartType.Hair,
                Slot = EquipSlot.None
            };

            LoadHairAnimations(hair, imgNode as WzImage);

            _hairCache[hairId] = hair;
            return hair;
        }

        private void LoadHairAnimations(HairPart hair, WzImage img)
        {
            if (img == null) return;

            img.ParseImage();

            // Debug: show top-level nodes in hair image
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair image nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
                // Show subnodes for stand1
                if (prop is WzSubProperty subProp && prop.Name == "stand1")
                {
                    foreach (var child in subProp.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {child.Name} ({child.GetType().Name})");
                        // Show one more level for hair subnode
                        if (child is WzSubProperty childSub && child.Name == "hair")
                        {
                            foreach (var grandchild in childSub.WzProperties)
                            {
                                System.Diagnostics.Debug.WriteLine($"      - {grandchild.Name} ({grandchild.GetType().Name})");
                            }
                        }
                    }
                }
            }

            // Load regular hair animations - include attack animations for proper character posing
            var allActions = StandardActions.Concat(AttackActions);
            foreach (var action in allActions)
            {
                var actionNode = img[action];
                if (actionNode != null)
                {
                    // Check for "hair" subnode - structure is action/hair/0, action/hair/1, etc.
                    var hairNode = actionNode["hair"];
                    if (hairNode != null)
                    {
                        var anim = LoadHairSubAnimation(hairNode);
                        if (anim != null && anim.Frames.Count > 0)
                        {
                            anim.ActionName = action;
                            anim.Action = CharacterPart.ParseActionString(action);
                            hair.Animations[action] = anim;
                            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair action '{action}' loaded with {anim.Frames.Count} frames");
                        }
                    }

                    // Check for "hairOverHead"
                    var overHeadNode = actionNode["hairOverHead"];
                    if (overHeadNode != null)
                    {
                        var anim = LoadHairSubAnimation(overHeadNode);
                        if (anim != null && anim.Frames.Count > 0)
                        {
                            anim.ActionName = action + "_overHead";
                            // Store separately or merge - for now just log
                            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair overHead '{action}' loaded with {anim.Frames.Count} frames");
                        }
                    }

                    // Check for "backHair" or "hairBelowBody"
                    var backNode = actionNode["hairBelowBody"] ?? actionNode["backHair"];
                    if (backNode != null)
                    {
                        hair.HasBackHair = true;
                        var anim = LoadHairSubAnimation(backNode);
                        if (anim != null && anim.Frames.Count > 0)
                        {
                            anim.ActionName = action;
                            hair.BackHairAnimations[action] = anim;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Hair loaded with {hair.Animations.Count} animations");
        }

        /// <summary>
        /// Load hair sub-animation - structure is hair/0, hair/1, etc. or direct canvas
        /// </summary>
        private CharacterAnimation LoadHairSubAnimation(WzImageProperty node)
        {
            if (node == null) return null;

            var anim = new CharacterAnimation();

            if (node is WzSubProperty subProp)
            {
                // Try numbered frames (hair/0, hair/1)
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = subProp[i.ToString()];
                    if (frameNode == null) break;

                    if (frameNode is WzCanvasProperty frameCanvas)
                    {
                        var frame = LoadFrame(frameCanvas, i.ToString());
                        if (frame != null) anim.Frames.Add(frame);
                    }
                }
            }
            else if (node is WzCanvasProperty canvas)
            {
                // Single frame
                var frame = LoadFrame(canvas, "0");
                if (frame != null) anim.Frames.Add(frame);
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        #endregion

        #region Equipment Loading

        /// <summary>
        /// Load equipment from appropriate folder
        /// </summary>
        public CharacterPart LoadEquipment(int itemId)
        {
            if (_equipCache.TryGetValue(itemId, out var cached))
                return cached;

            // Determine equipment folder based on ID range
            string folder = GetEquipmentFolder(itemId);
            if (folder == null)
                return null;

            string imgName = itemId.ToString("D8") + ".img";
            WzImage imgNode = null;

            // Try WzFile first
            var equipDir = _characterWz?.WzDirectory?[folder];
            if (equipDir != null)
            {
                imgNode = equipDir[imgName] as WzImage;
            }

            // Fall back to Program.FindImage
            if (imgNode == null)
            {
                imgNode = Program.FindImage("Character", $"{folder}/{imgName}");
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadEquipment id={itemId}, folder={folder}, found={imgNode != null}");

            if (imgNode == null)
                return null;

            CharacterPart part;
            EquipSlot slot = GetEquipSlot(itemId);

            // Create appropriate part type
            if (folder == "Weapon")
            {
                part = LoadWeapon(imgNode as WzImage, itemId);
            }
            else
            {
                part = new CharacterPart
                {
                    ItemId = itemId,
                    Name = GetItemName(imgNode as WzImage) ?? $"Equip_{itemId}",
                    Type = GetPartType(folder),
                    Slot = slot
                };

                LoadPartAnimations(part, imgNode as WzImage);
            }

            if (part != null)
            {
                // Load info
                LoadEquipInfo(part, imgNode as WzImage);
                _equipCache[itemId] = part;
            }

            return part;
        }

        private WeaponPart LoadWeapon(WzImage img, int itemId)
        {
            if (img == null) return null;

            var weapon = new WeaponPart
            {
                ItemId = itemId,
                Name = GetItemName(img) ?? $"Weapon_{itemId}",
                Type = CharacterPartType.Weapon,
                Slot = EquipSlot.Weapon
            };

            img.ParseImage();

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadWeapon id={itemId}, img nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
            }

            // Load weapon info
            var info = img["info"];
            if (info != null)
            {
                weapon.AttackSpeed = GetIntValue(info["attackSpeed"]) ?? 6;
                weapon.Attack = GetIntValue(info["incPAD"]) ?? 0;
                weapon.IsTwoHanded = GetIntValue(info["twoHanded"]) == 1;
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon info: attackSpeed={weapon.AttackSpeed}, PAD={weapon.Attack}, twoHanded={weapon.IsTwoHanded}");
            }

            // Load animations - weapon structure is action/frame/weapon (e.g., stand1/0/weapon)
            LoadWeaponAnimations(weapon, img);

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon loaded with {weapon.Animations.Count} animations");

            return weapon;
        }

        /// <summary>
        /// Load weapon animations - handles weapon-specific structure
        /// Weapon structure: action/frame/weapon (e.g., stand1/0/weapon)
        /// </summary>
        private void LoadWeaponAnimations(WeaponPart weapon, WzImage img)
        {
            if (img == null) return;

            // Combine standard and attack actions
            var allActions = StandardActions.Concat(AttackActions);

            foreach (var action in allActions)
            {
                var actionNode = img[action];
                if (actionNode == null) continue;

                var anim = LoadWeaponAnimation(actionNode, action);
                if (anim != null && anim.Frames.Count > 0)
                {
                    anim.ActionName = action;
                    anim.Action = CharacterPart.ParseActionString(action);
                    weapon.Animations[action] = anim;
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon action '{action}' loaded with {anim.Frames.Count} frames");
                }
            }
        }

        /// <summary>
        /// Load weapon animation - structure is action/frame/weapon or action/weapon/frame
        /// </summary>
        private CharacterAnimation LoadWeaponAnimation(WzImageProperty actionNode, string actionName)
        {
            if (actionNode == null) return null;

            var anim = new CharacterAnimation();

            if (actionNode is WzSubProperty actionSub)
            {
                // Debug first frame structure
                var frame0 = actionSub["0"];
                if (frame0 is WzSubProperty frame0Sub)
                {
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon {actionName}/0 nodes:");
                    foreach (var child in frame0Sub.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {child.Name} ({child.GetType().Name})");
                    }
                }

                // Try numbered frames (action/0/weapon, action/1/weapon, etc.)
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = actionSub[i.ToString()];
                    if (frameNode == null) break;

                    if (frameNode is WzSubProperty frameSub)
                    {
                        // Look for "weapon" subnode
                        var weaponNode = frameSub["weapon"];
                        if (weaponNode is WzCanvasProperty weaponCanvas)
                        {
                            var frame = LoadWeaponFrame(weaponCanvas, frameSub, i.ToString(), actionName);
                            if (frame != null)
                            {
                                anim.Frames.Add(frame);
                            }
                        }
                        else if (weaponNode is WzUOLProperty weaponUol)
                        {
                            // Resolve UOL link
                            var resolved = weaponUol.LinkValue;
                            if (resolved is WzCanvasProperty resolvedCanvas)
                            {
                                var frame = LoadWeaponFrame(resolvedCanvas, frameSub, i.ToString(), actionName);
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                        // Also check for direct canvas (some weapons have this structure)
                        else
                        {
                            foreach (WzImageProperty child in frameSub.WzProperties)
                            {
                                if (child is WzCanvasProperty childCanvas)
                                {
                                    var frame = LoadWeaponFrame(childCanvas, frameSub, child.Name, actionName);
                                    if (frame != null)
                                    {
                                        anim.Frames.Add(frame);
                                        break; // Take first canvas
                                    }
                                }
                            }
                        }
                    }
                    else if (frameNode is WzCanvasProperty directCanvas)
                    {
                        // Direct canvas at frame level
                        var frame = LoadWeaponFrame(directCanvas, null, i.ToString(), actionName);
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                }

                // If no frames found, try alternate structure (action/weapon/0, action/weapon/1)
                if (anim.Frames.Count == 0)
                {
                    var weaponNode = actionSub["weapon"];
                    if (weaponNode is WzSubProperty weaponSub)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            var frameNode = weaponSub[i.ToString()];
                            if (frameNode == null) break;

                            if (frameNode is WzCanvasProperty frameCanvas)
                            {
                                var frame = LoadWeaponFrame(frameCanvas, null, i.ToString(), actionName);
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                    }
                    else if (weaponNode is WzCanvasProperty singleCanvas)
                    {
                        // Single weapon frame
                        var frame = LoadWeaponFrame(singleCanvas, null, "0", actionName);
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                }
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        /// <summary>
        /// Load a weapon frame with proper z-layer handling.
        /// Weapon z-layer may come from the canvas or parent frame node.
        /// </summary>
        private CharacterFrame LoadWeaponFrame(WzCanvasProperty canvas, WzSubProperty parentFrame, string frameName, string actionName = null)
        {
            if (canvas == null) return null;

            var frame = LoadFrame(canvas, frameName);
            if (frame == null) return null;

            // Get z-layer from canvas first, then parent frame
            string zLayer = GetStringValue(canvas["z"]);

            if (string.IsNullOrEmpty(zLayer) || !ZMapReference.HasZLayer(zLayer))
            {
                // Try parent frame's z property
                if (parentFrame != null)
                {
                    zLayer = GetStringValue(parentFrame["z"]);
                }
            }

            // If still no valid z-layer, determine based on action type
            if (string.IsNullOrEmpty(zLayer) || !ZMapReference.HasZLayer(zLayer))
            {
                // Use context-aware z-layer based on animation type
                zLayer = GetDefaultWeaponZLayer(actionName);
            }

            frame.Z = zLayer;
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Weapon frame {frameName} (action={actionName}) z-layer: {zLayer}");

            return frame;
        }

        /// <summary>
        /// Get the default z-layer for a weapon based on the action type.
        /// Standing poses: weapon behind hand (so hand appears to grip it)
        /// Attack poses: weapon in front (so swing is visible)
        /// </summary>
        private static string GetDefaultWeaponZLayer(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return "weaponBelowHand";

            string actionLower = actionName.ToLowerInvariant();

            // Attack animations - weapon should be more visible (in front)
            if (actionLower.StartsWith("swing") ||
                actionLower.StartsWith("stab") ||
                actionLower.StartsWith("shoot") ||
                actionLower.StartsWith("pronestab") ||
                actionLower.StartsWith("attack"))
            {
                return "weapon";
            }

            // Standing/idle poses - weapon behind hand (hand grips weapon)
            if (actionLower.StartsWith("stand") ||
                actionLower.StartsWith("walk") ||
                actionLower.StartsWith("jump") ||
                actionLower.StartsWith("sit") ||
                actionLower.StartsWith("prone") ||
                actionLower.StartsWith("ladder") ||
                actionLower.StartsWith("rope") ||
                actionLower.StartsWith("alert"))
            {
                return "weaponBelowHand";
            }

            // Default to behind hand for unknown actions
            return "weaponBelowHand";
        }

        private void LoadEquipInfo(CharacterPart part, WzImage img)
        {
            if (img == null) return;

            var info = img["info"];
            if (info == null) return;

            part.VSlot = GetStringValue(info["vslot"]);
            part.IsCash = GetIntValue(info["cash"]) == 1;

            // Load icon
            var iconNode = info["icon"];
            if (iconNode is WzCanvasProperty canvas)
            {
                part.Icon = LoadTexture(canvas);
            }

            var iconRawNode = info["iconRaw"];
            if (iconRawNode is WzCanvasProperty canvasRaw)
            {
                part.IconRaw = LoadTexture(canvasRaw);
            }
        }

        private string GetEquipmentFolder(int itemId)
        {
            // Equipment ID ranges
            int category = itemId / 10000;
            return category switch
            {
                100 => "Cap",
                101 => "Accessory",  // Face accessory
                102 => "Accessory",  // Eye accessory
                103 => "Earrings",
                104 => "Coat",
                105 => "Longcoat",
                106 => "Pants",
                107 => "Shoes",
                108 => "Glove",
                109 => "Shield",
                110 => "Cape",
                111 => "Ring",
                >= 130 and < 170 => "Weapon",
                180 => "TamingMob",
                _ => null
            };
        }

        private EquipSlot GetEquipSlot(int itemId)
        {
            int category = itemId / 10000;
            return category switch
            {
                100 => EquipSlot.Cap,
                101 => EquipSlot.FaceAccessory,
                102 => EquipSlot.EyeAccessory,
                103 => EquipSlot.Earrings,
                104 => EquipSlot.Coat,
                105 => EquipSlot.Longcoat,
                106 => EquipSlot.Pants,
                107 => EquipSlot.Shoes,
                108 => EquipSlot.Glove,
                109 => EquipSlot.Shield,
                110 => EquipSlot.Cape,
                >= 130 and < 170 => EquipSlot.Weapon,
                180 => EquipSlot.TamingMob,
                _ => EquipSlot.None
            };
        }

        private CharacterPartType GetPartType(string folder)
        {
            return folder switch
            {
                "Cap" => CharacterPartType.Cap,
                "Accessory" => CharacterPartType.Accessory,
                "Earrings" => CharacterPartType.Earrings,
                "Coat" => CharacterPartType.Coat,
                "Longcoat" => CharacterPartType.Longcoat,
                "Pants" => CharacterPartType.Pants,
                "Shoes" => CharacterPartType.Shoes,
                "Glove" => CharacterPartType.Glove,
                "Shield" => CharacterPartType.Shield,
                "Cape" => CharacterPartType.Cape,
                "Weapon" => CharacterPartType.Weapon,
                "TamingMob" => CharacterPartType.TamingMob,
                _ => CharacterPartType.Body
            };
        }

        #endregion

        #region Animation Loading

        private void LoadPartAnimations(CharacterPart part, WzImage img)
        {
            if (img == null) return;

            img.ParseImage();

            // Debug: list top-level nodes in the image
            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] LoadPartAnimations for {part.Name}, nodes:");
            foreach (var prop in img.WzProperties)
            {
                System.Diagnostics.Debug.WriteLine($"  - {prop.Name} ({prop.GetType().Name})");
            }

            // Debug head structure specifically
            bool isHead = part.Name.Contains("Head");
            if (isHead)
            {
                var stand1Node = img["stand1"];
                if (stand1Node is WzSubProperty stand1Sub)
                {
                    System.Diagnostics.Debug.WriteLine($"[CharacterLoader] HEAD stand1 contents:");
                    foreach (var child in stand1Sub.WzProperties)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {child.Name} ({child.GetType().Name})");
                    }
                }
            }

            // Determine which actions to load - body parts need attack animations too
            bool isBodyPart = part.Type == CharacterPartType.Body || part.Type == CharacterPartType.Head;
            var actionsToLoad = isBodyPart ? StandardActions.Concat(AttackActions) : StandardActions;

            foreach (var action in actionsToLoad)
            {
                var actionNode = img[action];
                if (actionNode != null)
                {
                    // Pass debug context for head to see structure
                    var anim = LoadAnimation(actionNode, isHead && action == "stand1" ? $"{part.Name}/{action}" : null);
                    if (anim != null && anim.Frames.Count > 0)
                    {
                        anim.ActionName = action;
                        anim.Action = CharacterPart.ParseActionString(action);
                        part.Animations[action] = anim;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharacterLoader] {part.Name} has {part.Animations.Count} animations");
        }

        private CharacterAnimation LoadAnimation(WzImageProperty node, string debugContext = null)
        {
            if (node == null) return null;

            var anim = new CharacterAnimation();

            // Debug: show what's inside the action node
            if (debugContext != null && node is WzSubProperty debugSub)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadAnimation] {debugContext} contents:");
                foreach (var child in debugSub.WzProperties)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {child.Name} ({child.GetType().Name})");
                }
            }

            // Check if node is a direct canvas (single frame)
            if (node is WzCanvasProperty canvas)
            {
                var frame = LoadFrame(canvas, "0");
                if (frame != null)
                {
                    anim.Frames.Add(frame);
                }
            }
            // Check for numbered frame children
            else if (node is WzSubProperty subProp)
            {
                // First, try direct numbered frames (0, 1, 2, ...)
                bool foundDirectFrames = false;
                for (int i = 0; i < 100; i++)
                {
                    var frameNode = subProp[i.ToString()];
                    if (frameNode == null)
                        break;

                    foundDirectFrames = true;
                    if (frameNode is WzCanvasProperty frameCanvas)
                    {
                        var frame = LoadFrame(frameCanvas, i.ToString());
                        if (frame != null)
                        {
                            anim.Frames.Add(frame);
                        }
                    }
                    else if (frameNode is WzSubProperty frameSub)
                    {
                        // Body images contain multiple parts: body, arm, lHand, rHand, head, etc.
                        // We need to load ALL parts as sub-parts for correct rendering
                        CharacterFrame bodyFrame = LoadBodyFrameWithSubParts(frameSub, i.ToString());
                        if (bodyFrame != null)
                        {
                            anim.Frames.Add(bodyFrame);
                        }
                    }
                }

                // If direct numbered frames found but no canvases added, check for head canvas inside each frame
                // Structure: stand1/0/head, stand1/1/head (for head images)
                if (foundDirectFrames && anim.Frames.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadAnimation] Found frames but no canvas, checking for 'head' inside each frame");
                    for (int i = 0; i < 100; i++)
                    {
                        var frameNode = subProp[i.ToString()];
                        if (frameNode == null)
                            break;

                        if (frameNode is WzSubProperty frameSub)
                        {
                            // Debug: show what's inside frame 0
                            if (i == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoadAnimation] Frame 0 contents:");
                                foreach (var fc in frameSub.WzProperties)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  - {fc.Name} ({fc.GetType().Name})");
                                }
                            }

                            // Look for "head" property inside the frame
                            var headProp = frameSub["head"];
                            WzCanvasProperty headCanvas = null;

                            // Resolve UOL (User Object Link) to get actual canvas
                            if (headProp is WzUOLProperty uol)
                            {
                                var resolved = uol.LinkValue;
                                System.Diagnostics.Debug.WriteLine($"[LoadAnimation] Resolved UOL to: {resolved?.GetType().Name ?? "NULL"}");
                                if (resolved is WzCanvasProperty resolvedCanvas)
                                {
                                    headCanvas = resolvedCanvas;
                                }
                            }
                            else if (headProp is WzCanvasProperty directCanvas)
                            {
                                headCanvas = directCanvas;
                            }

                            if (headCanvas != null)
                            {
                                var frame = LoadFrame(headCanvas, i.ToString());
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                            // Also try looking for first canvas or UOL with any name
                            else
                            {
                                foreach (WzImageProperty child in frameSub.WzProperties)
                                {
                                    WzCanvasProperty foundCanvas = null;
                                    if (child is WzCanvasProperty childCanvas2)
                                    {
                                        foundCanvas = childCanvas2;
                                    }
                                    else if (child is WzUOLProperty childUol2)
                                    {
                                        if (childUol2.LinkValue is WzCanvasProperty resolvedCanvas2)
                                        {
                                            foundCanvas = resolvedCanvas2;
                                        }
                                    }

                                    if (foundCanvas != null)
                                    {
                                        var frame = LoadFrame(foundCanvas, child.Name);
                                        if (frame != null)
                                        {
                                            anim.Frames.Add(frame);
                                            break; // Take first canvas
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // If no direct numbered frames, check for "head" subnode (alternate structure)
                if (!foundDirectFrames && anim.Frames.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadAnimation] No direct frames, checking for 'head' subnode");
                    var headNode = subProp["head"];
                    System.Diagnostics.Debug.WriteLine($"[LoadAnimation] headNode: {headNode?.GetType().Name ?? "NULL"}");

                    if (headNode is WzSubProperty headSub)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            var frameNode = headSub[i.ToString()];
                            if (frameNode == null)
                                break;

                            if (frameNode is WzCanvasProperty frameCanvas)
                            {
                                var frame = LoadFrame(frameCanvas, i.ToString());
                                if (frame != null)
                                {
                                    anim.Frames.Add(frame);
                                }
                            }
                        }
                    }
                }
            }

            anim.CalculateTotalDuration();
            return anim;
        }

        private CharacterFrame LoadFrame(WzCanvasProperty canvas, string frameName)
        {
            if (canvas == null) return null;

            var texture = LoadTexture(canvas);
            if (texture == null) return null;

            // Default to 200ms for character animations (MapleStory typically uses 200-300ms per frame)
            // The delay may be overridden by the parent frame node in LoadBodyFrameWithSubParts
            var frame = new CharacterFrame
            {
                Texture = texture,
                Delay = GetIntValue(canvas["delay"]) ?? 200
            };

            // Load origin
            var origin = canvas["origin"];
            if (origin is WzVectorProperty originVec)
            {
                frame.Origin = new Point(originVec.X.Value, originVec.Y.Value);
            }

            // Load z-layer
            frame.Z = GetStringValue(canvas["z"]) ?? frameName;

            // Load map points
            var mapNode = canvas["map"];
            if (mapNode is WzSubProperty mapSub)
            {
                foreach (WzImageProperty mapPoint in mapSub.WzProperties)
                {
                    if (mapPoint is WzVectorProperty vec)
                    {
                        frame.Map[mapPoint.Name] = new Point(vec.X.Value, vec.Y.Value);
                    }
                }
            }

            // Calculate bounds
            frame.Bounds = new Rectangle(
                -frame.Origin.X,
                -frame.Origin.Y,
                canvas.PngProperty?.Width ?? 0,
                canvas.PngProperty?.Height ?? 0);

            return frame;
        }

        /// <summary>
        /// Load a body frame with all sub-parts (body, arm, lHand, rHand)
        /// Each sub-part is positioned relative to the body's navel
        /// </summary>
        private CharacterFrame LoadBodyFrameWithSubParts(WzSubProperty frameSub, string frameName)
        {
            if (frameSub == null) return null;

            // List of body sub-parts to load, in z-order (back to front)
            // These are the standard parts in a body animation frame
            string[] subPartNames = { "body", "arm", "lHand", "rHand" };

            CharacterFrame bodyFrame = null;
            Point bodyNavel = Point.Zero;

            // Get the frame delay from the parent frame node (stand1/0/delay), not from sub-parts
            // This is where MapleStory stores the animation timing
            int frameDelay = GetIntValue(frameSub["delay"]) ?? 200; // Default to 200ms for character animations

            // First pass: load the main "body" canvas to get navel reference point
            var bodyCanvas = frameSub["body"] as WzCanvasProperty;
            if (bodyCanvas != null)
            {
                bodyFrame = LoadFrame(bodyCanvas, "body");
                if (bodyFrame != null)
                {
                    bodyNavel = bodyFrame.Map.ContainsKey("navel") ? bodyFrame.Map["navel"] : Point.Zero;
                    // Override with frame-level delay
                    bodyFrame.Delay = frameDelay;
                }
            }

            // Fallback: if no "body" canvas, take first canvas
            if (bodyFrame == null)
            {
                foreach (WzImageProperty child in frameSub.WzProperties)
                {
                    if (child is WzCanvasProperty childCanvas)
                    {
                        bodyFrame = LoadFrame(childCanvas, child.Name);
                        if (bodyFrame != null)
                        {
                            bodyNavel = bodyFrame.Map.ContainsKey("navel") ? bodyFrame.Map["navel"] : Point.Zero;
                            // Override with frame-level delay
                            bodyFrame.Delay = frameDelay;
                            break;
                        }
                    }
                }
            }

            if (bodyFrame == null) return null;

            // Second pass: load all sub-parts
            foreach (var partName in subPartNames)
            {
                var partCanvas = frameSub[partName] as WzCanvasProperty;
                if (partCanvas == null) continue;

                var subPart = LoadSubPart(partCanvas, partName, bodyNavel);
                if (subPart != null)
                {
                    bodyFrame.SubParts.Add(subPart);
                }
            }

            // Extract "hand" map point from "arm" canvas for weapon positioning
            // The arm is positioned relative to body via navel, so:
            // hand_in_body_coords = body.navel - arm.navel + arm.hand
            var armCanvas = frameSub["arm"] as WzCanvasProperty;
            if (armCanvas != null)
            {
                var armMap = armCanvas["map"];
                if (armMap is WzSubProperty armMapSub)
                {
                    Point armNavel = Point.Zero;
                    Point armHand = Point.Zero;
                    bool hasArmHand = false;

                    foreach (WzImageProperty mapPoint in armMapSub.WzProperties)
                    {
                        if (mapPoint is WzVectorProperty vec)
                        {
                            if (mapPoint.Name == "navel")
                                armNavel = new Point(vec.X.Value, vec.Y.Value);
                            else if (mapPoint.Name == "hand")
                            {
                                armHand = new Point(vec.X.Value, vec.Y.Value);
                                hasArmHand = true;
                            }
                        }
                    }

                    // Calculate hand position in body's coordinate system
                    if (hasArmHand)
                    {
                        bodyFrame.Map["hand"] = new Point(
                            bodyNavel.X - armNavel.X + armHand.X,
                            bodyNavel.Y - armNavel.Y + armHand.Y);
                    }
                }
            }

            // Also check for "lHand" (left hand) which may have handMove for weapon motion
            var lHandCanvas = frameSub["lHand"] as WzCanvasProperty;
            if (lHandCanvas != null)
            {
                var lHandMap = lHandCanvas["map"];
                if (lHandMap is WzSubProperty lHandMapSub)
                {
                    Point lHandNavel = Point.Zero;
                    Point lHandMove = Point.Zero;
                    bool hasHandMove = false;

                    foreach (WzImageProperty mapPoint in lHandMapSub.WzProperties)
                    {
                        if (mapPoint is WzVectorProperty vec)
                        {
                            if (mapPoint.Name == "navel")
                                lHandNavel = new Point(vec.X.Value, vec.Y.Value);
                            else if (mapPoint.Name == "handMove")
                            {
                                lHandMove = new Point(vec.X.Value, vec.Y.Value);
                                hasHandMove = true;
                            }
                        }
                    }

                    if (hasHandMove)
                    {
                        bodyFrame.Map["handMove"] = new Point(
                            bodyNavel.X - lHandNavel.X + lHandMove.X,
                            bodyNavel.Y - lHandNavel.Y + lHandMove.Y);
                    }
                }
            }

            return bodyFrame;
        }

        /// <summary>
        /// Load a single sub-part (body, arm, lHand, rHand) with its position relative to body navel
        /// </summary>
        private CharacterSubPart LoadSubPart(WzCanvasProperty canvas, string partName, Point bodyNavel)
        {
            if (canvas == null) return null;

            var texture = LoadTexture(canvas);
            if (texture == null) return null;

            var subPart = new CharacterSubPart
            {
                Name = partName,
                Texture = texture
            };

            // Load origin
            var origin = canvas["origin"];
            if (origin is WzVectorProperty originVec)
            {
                subPart.Origin = new Point(originVec.X.Value, originVec.Y.Value);
            }

            // Load z-layer
            subPart.Z = GetStringValue(canvas["z"]) ?? partName;

            // Load map points
            var mapNode = canvas["map"];
            if (mapNode is WzSubProperty mapSub)
            {
                foreach (WzImageProperty mapPoint in mapSub.WzProperties)
                {
                    if (mapPoint is WzVectorProperty vec)
                    {
                        subPart.Map[mapPoint.Name] = new Point(vec.X.Value, vec.Y.Value);
                    }
                }
            }

            // Calculate offset from body navel
            // Each sub-part has its own navel point that should align with body's navel
            Point partNavel = subPart.Map.ContainsKey("navel") ? subPart.Map["navel"] : Point.Zero;
            subPart.NavelOffset = new Point(
                bodyNavel.X - partNavel.X,
                bodyNavel.Y - partNavel.Y);

            return subPart;
        }

        private IDXObject LoadTexture(WzCanvasProperty canvas)
        {
            if (canvas?.PngProperty == null) return null;

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null) return null;

                var texture = bitmap.ToTexture2D(_device);
                if (texture == null) return null;

                var origin = canvas["origin"] as WzVectorProperty;
                int originX = origin?.X.Value ?? 0;
                int originY = origin?.Y.Value ?? 0;
                int delay = GetIntValue(canvas["delay"]) ?? 100;

                return new DXObject(0, 0, texture, delay)
                {
                    Tag = canvas
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Utility

        private string GetItemName(WzImage img)
        {
            if (img == null) return null;

            img.ParseImage();
            var info = img["info"];
            if (info == null) return null;

            // Try to get name from String.wz or embedded
            return GetStringValue(info["name"]);
        }

        private static int? GetIntValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzIntProperty intProp => intProp.Value,
                WzShortProperty shortProp => shortProp.Value,
                WzLongProperty longProp => (int)longProp.Value,
                WzStringProperty strProp => int.TryParse(strProp.Value, out int v) ? v : null,
                _ => null
            };
        }

        private static string GetStringValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzStringProperty strProp => strProp.Value,
                WzIntProperty intProp => intProp.Value.ToString(),
                _ => null
            };
        }

        #endregion

        #region Presets

        /// <summary>
        /// Load a default male character
        /// </summary>
        public CharacterBuild LoadDefaultMale()
        {
            var build = new CharacterBuild
            {
                Gender = CharacterGender.Male,
                Skin = SkinColor.Light,
                Body = LoadBody(SkinColor.Light),
                Head = LoadHead(SkinColor.Light),
                Face = LoadFace(20000),   // Default male face
                Hair = LoadHair(30000),   // Default male hair
                Name = "Default Male"
            };

            // Load and equip beginner sword (One-Handed Sword)
            var weapon = LoadEquipment(1302000);
            if (weapon != null)
            {
                build.Equip(weapon);
                System.Diagnostics.Debug.WriteLine($"[CharacterLoader] Equipped weapon: {weapon.Name}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CharacterLoader] Failed to load weapon 1302000");
            }

            return build;
        }

        /// <summary>
        /// Load a default female character
        /// </summary>
        public CharacterBuild LoadDefaultFemale()
        {
            var build = new CharacterBuild
            {
                Gender = CharacterGender.Female,
                Skin = SkinColor.Light,
                Body = LoadBody(SkinColor.Light),
                Head = LoadHead(SkinColor.Light),
                Face = LoadFace(21000),   // Default female face
                Hair = LoadHair(31000),   // Default female hair
                Name = "Default Female"
            };

            // Load and equip beginner sword (One-Handed Sword)
            var weapon = LoadEquipment(1302000);
            if (weapon != null)
            {
                build.Equip(weapon);
            }

            return build;
        }

        /// <summary>
        /// Load a random character build
        /// </summary>
        public CharacterBuild LoadRandom()
        {
            var random = new Random();
            var gender = random.Next(2) == 0 ? CharacterGender.Male : CharacterGender.Female;
            var skin = (SkinColor)random.Next(5);

            return new CharacterBuild
            {
                Gender = gender,
                Skin = skin,
                Body = LoadBody(skin),
                Head = LoadHead(skin),
                Face = LoadFace(gender == CharacterGender.Male ? 20000 + random.Next(30) : 21000 + random.Next(30)),
                Hair = LoadHair(gender == CharacterGender.Male ? 30000 + random.Next(50) : 31000 + random.Next(50)),
                Name = "Random"
            };
        }

        #endregion

        #region Cache Management

        public void ClearCache()
        {
            _bodyCache.Clear();
            _faceCache.Clear();
            _hairCache.Clear();
            _equipCache.Clear();
        }

        public int GetCacheCount()
        {
            return _bodyCache.Count + _faceCache.Count + _hairCache.Count + _equipCache.Count;
        }

        #endregion
    }
}
