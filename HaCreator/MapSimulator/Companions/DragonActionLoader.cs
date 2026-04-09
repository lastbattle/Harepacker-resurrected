using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Companions
{
    internal sealed class DragonActionLoader
    {
        private const int FirstClientDragonActionCode = 147;
        // IDA: CDragon::PrepareActionLayer allocates 0x1D action slots, which
        // resolves to stand + move + raw-action codes 147..173.
        private const int LastClientDragonActionCode = 173;

        private static readonly Dictionary<string, string> ClientActionAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["stand1"] = "stand",
            ["stand2"] = "stand",
            ["alert"] = "stand",
            ["walk1"] = "move",
            ["walk2"] = "move",
            ["jump"] = "move",
            ["fly"] = "move",
            ["ladder"] = "move",
            ["rope"] = "move"
        };
        private static readonly string[] ClientActionTable = BuildClientActionTable();
        private static readonly HashSet<string> ClientActionNames = new(ClientActionTable, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ClientHeldActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "breathe_prepare",
            "icebreathe_prepare"
        };

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, Dictionary<string, SkillAnimation>> _actionCache = new();

        public DragonActionLoader(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public string ResolveClientActionName(string requestedActionName)
        {
            if (string.IsNullOrWhiteSpace(requestedActionName))
            {
                return null;
            }

            string resolvedActionName = ClientActionAliases.TryGetValue(requestedActionName, out string mappedActionName)
                ? mappedActionName
                : requestedActionName;

            return ClientActionNames.Contains(resolvedActionName)
                ? resolvedActionName
                : requestedActionName;
        }

        public SkillAnimation GetOrLoadAnimation(int dragonJob, string requestedActionName)
        {
            string resolvedActionName = ResolveClientActionName(requestedActionName);
            if (string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return null;
            }

            if (!_actionCache.TryGetValue(dragonJob, out Dictionary<string, SkillAnimation> cachedActions))
            {
                cachedActions = new Dictionary<string, SkillAnimation>(StringComparer.OrdinalIgnoreCase);
                _actionCache[dragonJob] = cachedActions;
            }

            if (cachedActions.TryGetValue(resolvedActionName, out SkillAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            SkillAnimation loadedAnimation = LoadAnimation(dragonJob, resolvedActionName);
            if (loadedAnimation != null)
            {
                cachedActions[resolvedActionName] = loadedAnimation;
            }

            return loadedAnimation;
        }

        public IEnumerable<string> EnumerateKnownActionNames(int dragonJob)
        {
            WzImage image = FindDragonImage(dragonJob);
            if (image == null)
            {
                return ClientActionTable;
            }

            return ClientActionTable
                .Concat(EnumerateImageActionNames(image))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static IEnumerable<string> EnumerateClientActionNames()
        {
            return ClientActionTable;
        }

        internal static bool IsClientHeldActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && ClientHeldActionNames.Contains(actionName);
        }

        internal static bool TryGetClientActionNameFromRawActionCode(int rawActionCode, out string actionName)
        {
            if (rawActionCode == 0)
            {
                actionName = "stand";
                return true;
            }

            if (rawActionCode == 1)
            {
                actionName = "move";
                return true;
            }

            if (rawActionCode >= FirstClientDragonActionCode
                && rawActionCode <= LastClientDragonActionCode
                && CharacterPart.TryGetActionStringFromCode(rawActionCode, out actionName))
            {
                return true;
            }

            actionName = null;
            return false;
        }

        private SkillAnimation LoadAnimation(int dragonJob, string resolvedActionName)
        {
            WzImage image = FindDragonImage(dragonJob);
            WzSubProperty actionNode = FindActionNode(image, resolvedActionName);
            if (actionNode == null)
            {
                return null;
            }

            List<SkillFrame> frames = LoadFrames(actionNode);
            if (frames.Count == 0)
            {
                return null;
            }

            var animation = new SkillAnimation
            {
                Name = resolvedActionName,
                Loop = IsLoopingAction(resolvedActionName),
                PositionCode = GetIntValue(actionNode["pos"])
            };

            animation.Frames.AddRange(frames);
            if (GetIntValue(actionNode["repeat"]) > 0)
            {
                for (int i = frames.Count - 1; i >= 0; i--)
                {
                    animation.Frames.Add(CloneFrame(frames[i]));
                }
            }

            animation.CalculateDuration();
            return animation;
        }

        private static WzImage FindDragonImage(int dragonJob)
        {
            return global::HaCreator.Program.FindImage("Skill", $"Dragon/{dragonJob}.img")
                   ?? global::HaCreator.Program.FindImage("Skill", $"{dragonJob}.img");
        }

        internal static IEnumerable<string> EnumerateImageActionNames(WzImage image)
        {
            if (image == null)
            {
                yield break;
            }

            foreach (string actionName in image.WzProperties
                         .OfType<WzSubProperty>()
                         .Where(property => !string.Equals(property.Name, "info", StringComparison.OrdinalIgnoreCase)
                                            && !string.Equals(property.Name, "skill", StringComparison.OrdinalIgnoreCase))
                         .Select(property => property.Name))
            {
                yield return actionName;
            }

            if (image["skill"] is not WzSubProperty skillRoot)
            {
                yield break;
            }

            foreach (WzSubProperty skillNode in skillRoot.WzProperties.OfType<WzSubProperty>())
            {
                foreach (string actionName in EnumerateSkillActionNames(skillNode))
                {
                    yield return actionName;
                }
            }
        }

        internal static WzSubProperty FindActionNode(WzImage image, string actionName)
        {
            if (image == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            if (image[actionName] is WzSubProperty directActionNode)
            {
                return directActionNode;
            }

            if (image["skill"] is not WzSubProperty skillRoot)
            {
                return null;
            }

            foreach (WzSubProperty skillNode in skillRoot.WzProperties.OfType<WzSubProperty>())
            {
                if (skillNode["mob"] is not WzSubProperty mobNode)
                {
                    continue;
                }

                if (EnumerateSkillActionNames(skillNode).Any(candidate =>
                        string.Equals(candidate, actionName, StringComparison.OrdinalIgnoreCase)))
                {
                    return mobNode;
                }
            }

            return null;
        }

        private List<SkillFrame> LoadFrames(WzSubProperty actionNode)
        {
            List<SkillFrame> frames = new();

            IEnumerable<WzCanvasProperty> orderedFrames = actionNode.WzProperties
                .OfType<WzCanvasProperty>()
                .OrderBy(frame => ParseFrameIndex(frame.Name));

            foreach (WzCanvasProperty canvas in orderedFrames)
            {
                WzCanvasProperty metadataCanvas = ResolveMetadataCanvas(canvas);
                IDXObject texture = LoadTexture(metadataCanvas);
                if (texture == null)
                {
                    continue;
                }

                WzVectorProperty origin = metadataCanvas["origin"] as WzVectorProperty;
                frames.Add(new SkillFrame
                {
                    Texture = texture,
                    Origin = new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                    Delay = Math.Max(1, GetIntValue(metadataCanvas["delay"]) ?? 100),
                    Bounds = ResolveFrameBounds(metadataCanvas, texture),
                    AlphaStart = Math.Clamp(GetIntValue(metadataCanvas["a0"]) ?? 255, 0, 255),
                    AlphaEnd = Math.Clamp(GetIntValue(metadataCanvas["a1"]) ?? 255, 0, 255)
                });
            }

            return frames;
        }

        private static IEnumerable<string> EnumerateSkillActionNames(WzSubProperty skillNode)
        {
            if (skillNode?["action"] is not WzSubProperty actionNode)
            {
                yield break;
            }

            foreach (WzImageProperty property in actionNode.WzProperties)
            {
                if (TryGetStringValue(property, out string actionName)
                    && !string.IsNullOrWhiteSpace(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private IDXObject LoadTexture(WzCanvasProperty canvas)
        {
            if (canvas?.PngProperty == null)
            {
                return null;
            }

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return null;
                }

                Texture2D texture = bitmap.ToTexture2DAndDispose(_device);
                return texture == null
                    ? null
                    : new DXObject(0, 0, texture, Math.Max(1, GetIntValue(canvas["delay"]) ?? 100))
                    {
                        Tag = canvas
                    };
            }
            catch
            {
                return null;
            }
        }

        private static SkillFrame CloneFrame(SkillFrame frame)
        {
            return new SkillFrame
            {
                Texture = frame.Texture,
                Origin = frame.Origin,
                Delay = frame.Delay,
                Bounds = frame.Bounds,
                Flip = frame.Flip,
                Z = frame.Z,
                AlphaStart = frame.AlphaStart,
                AlphaEnd = frame.AlphaEnd
            };
        }

        private static bool IsLoopingAction(string actionName)
        {
            return string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] BuildClientActionTable()
        {
            var actions = new List<string>
            {
                "stand",
                "move"
            };

            for (int rawActionCode = FirstClientDragonActionCode; rawActionCode <= LastClientDragonActionCode; rawActionCode++)
            {
                if (CharacterPart.TryGetActionStringFromCode(rawActionCode, out string actionName)
                    && !string.IsNullOrWhiteSpace(actionName))
                {
                    actions.Add(actionName);
                }
            }

            return actions
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int ParseFrameIndex(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : int.MaxValue;
        }

        private static int? GetIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                _ => null
            };
        }

        private static bool TryGetStringValue(WzImageProperty property, out string value)
        {
            if (property is WzStringProperty stringProperty)
            {
                value = stringProperty.Value;
                return true;
            }

            value = null;
            return false;
        }

        private static WzCanvasProperty ResolveMetadataCanvas(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            try
            {
                return canvas.GetLinkedWzImageProperty() as WzCanvasProperty ?? canvas;
            }
            catch
            {
                return canvas;
            }
        }

        private static Rectangle ResolveFrameBounds(WzCanvasProperty canvas, IDXObject texture)
        {
            WzVectorProperty lt = canvas["lt"] as WzVectorProperty;
            WzVectorProperty rb = canvas["rb"] as WzVectorProperty;
            if (lt != null && rb != null)
            {
                int left = lt.X.Value;
                int top = lt.Y.Value;
                int width = Math.Max(1, rb.X.Value - left);
                int height = Math.Max(1, rb.Y.Value - top);
                return new Rectangle(left, top, width, height);
            }

            WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
            int originX = origin?.X.Value ?? 0;
            int originY = origin?.Y.Value ?? 0;
            return new Rectangle(-originX, -originY, texture.Width, texture.Height);
        }
    }
}
