using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// A WZ canvas positioned in character-local coordinates. The source canvas is
    /// intentionally retained so callers can choose their own renderer (XNA, WPF,
    /// export, or another bitmap backend).
    /// </summary>
    public sealed class CharacterWzLayer
    {
        public CharacterWzLayer(WzCanvasProperty canvas, int x, int y, int zIndex)
        {
            Canvas = canvas;
            X = x;
            Y = y;
            ZIndex = zIndex;
        }

        public WzCanvasProperty Canvas { get; }
        public int X { get; }
        public int Y { get; }
        public int ZIndex { get; }
    }

    public sealed record CharacterWzActionFrame(string Key, int Delay, bool Flip, IReadOnlyList<CharacterWzLayer> Layers);

    /// <summary>
    /// Shared character layout rules used by MapSimulator and WZ-backed previews.
    /// This class does not allocate textures or depend on a graphics device.
    /// </summary>
    public static class CharacterCompositionMath
    {
        private const string MapNavel = "navel";
        private const string MapHand = "hand";
        private const string MapHandMove = "handMove";

        public static Point CalculateEquipmentOffset(
            CharacterPartType type,
            IReadOnlyDictionary<string, Point> bodyMap,
            Point bodyOrigin,
            IReadOnlyDictionary<string, Point> equipmentMap,
            Point equipmentOrigin,
            Point baseOffset,
            IReadOnlyDictionary<string, Point> headMap = null,
            Point? headOffset = null,
            Point headOrigin = default)
        {
            if (headMap != null && headOffset.HasValue && UsesHeadAnchor(type, equipmentMap))
            {
                string headPoint = equipmentMap.ContainsKey("brow") ? "brow" : "ear";
                Point headAnchor = Add(headOffset.Value, GetMapPoint(headMap, headPoint, headOrigin));
                Point headEquipmentAnchor = GetMapPoint(equipmentMap, headPoint, equipmentOrigin);
                return new Point(
                    headAnchor.X - headEquipmentAnchor.X,
                    headAnchor.Y - headEquipmentAnchor.Y);
            }

            string mapPoint = type switch
            {
                CharacterPartType.Weapon or CharacterPartType.WeaponOverGlove or CharacterPartType.WeaponOverHand => MapHand,
                CharacterPartType.Glove => MapHand,
                CharacterPartType.Coat or CharacterPartType.Longcoat or CharacterPartType.Pants or CharacterPartType.Shoes => MapNavel,
                CharacterPartType.Cape => MapNavel,
                _ => MapNavel
            };

            Point bodyAnchor;
            Point equipmentAnchor;
            bool usesHandFallback = type is CharacterPartType.Weapon
                or CharacterPartType.WeaponOverGlove
                or CharacterPartType.WeaponOverHand;

            if (usesHandFallback)
            {
                if (bodyMap.TryGetValue(MapHand, out Point hand))
                    bodyAnchor = hand;
                else if (bodyMap.TryGetValue(MapHandMove, out Point handMove))
                    bodyAnchor = handMove;
                else if (bodyMap.TryGetValue(MapNavel, out Point navel))
                    bodyAnchor = new Point(navel.X + 10, navel.Y - 15);
                else
                    bodyAnchor = bodyOrigin;

                if (!equipmentMap.TryGetValue(MapHand, out equipmentAnchor)
                    && !equipmentMap.TryGetValue(MapHandMove, out equipmentAnchor))
                    equipmentAnchor = equipmentOrigin;
            }
            else
            {
                bodyAnchor = GetMapPoint(bodyMap, mapPoint, bodyOrigin);
                equipmentAnchor = GetMapPoint(equipmentMap, mapPoint, equipmentOrigin);
            }

            return new Point(
                baseOffset.X + bodyAnchor.X - equipmentAnchor.X,
                baseOffset.Y + bodyAnchor.Y - equipmentAnchor.Y);
        }

        private static bool UsesHeadAnchor(CharacterPartType type, IReadOnlyDictionary<string, Point> equipmentMap)
        {
            return (type is CharacterPartType.Cap
                or CharacterPartType.Accessory
                or CharacterPartType.Face_Accessory
                or CharacterPartType.Eye_Accessory
                or CharacterPartType.Earrings)
                && (equipmentMap.ContainsKey("brow") || equipmentMap.ContainsKey("ear"));
        }

        private static Point GetMapPoint(IReadOnlyDictionary<string, Point> map, string name, Point fallback)
        {
            return map != null && map.TryGetValue(name, out Point point) ? point : fallback;
        }

        private static Point Add(Point left, Point right) => new(left.X + right.X, left.Y + right.Y);
    }

    /// <summary>
    /// Composes the standing character pose directly from WZ canvases. It mirrors
    /// CharacterLoader/CharacterAssembler's body, head, face, hair, equipment,
    /// anchor, and z-layer conventions while remaining renderer-neutral.
    /// </summary>
    public static class CharacterWzComposition
    {
        private const string MapNavel = "navel";
        private const string MapNeck = "neck";
        private const string MapBrow = "brow";

        public static IReadOnlyList<CharacterWzLayer> ComposeStanding(
            WzImage bodyImage,
            WzImage headImage,
            WzImage faceImage,
            WzImage hairImage,
            IEnumerable<(int ItemId, WzImage Image)> equipmentImages)
        {
            return ComposeAction(bodyImage, headImage, faceImage, hairImage, equipmentImages, "stand1", "0");
        }

        /// <summary>Composes an ordinary, unmounted character action without requiring a graphics device.</summary>
        public static IReadOnlyList<CharacterWzLayer> ComposeAction(
            WzImage bodyImage,
            WzImage headImage,
            WzImage faceImage,
            WzImage hairImage,
            IEnumerable<(int ItemId, WzImage Image)> equipmentImages,
            string action,
            string frameKey)
        {
            List<CharacterWzLayer> layers = new();
            if (bodyImage == null || headImage == null)
                return layers;

            Parse(bodyImage);
            Parse(headImage);
            Parse(faceImage);
            Parse(hairImage);

            action = string.IsNullOrWhiteSpace(action) ? "stand1" : action;
            frameKey = string.IsNullOrWhiteSpace(frameKey) ? "0" : frameKey;
            WzImageProperty bodyFrame = bodyImage.GetFromPath($"{action}/{frameKey}") ?? bodyImage.GetFromPath("stand1/0");
            WzCanvasProperty body = GetCanvas(bodyFrame?["body"]) ?? GetCanvas(bodyFrame);
            WzCanvasProperty head = GetCanvas(headImage.GetFromPath("front/head"))
                ?? GetCanvas(headImage.GetFromPath($"{action}/{frameKey}/head"))
                ?? GetCanvas(headImage.GetFromPath("stand1/0/head"));
            if (body == null || head == null)
                return layers;

            Point bodyNavel = GetMapPoint(body, MapNavel);
            Point baseOffset = new(-bodyNavel.X, -bodyNavel.Y);
            Dictionary<string, Point> bodyMap = ReadMap(body);
            bodyMap[MapNavel] = bodyNavel;

            AddLayer(layers, body, baseOffset, "body");
            AddBodySubParts(layers, bodyFrame, bodyNavel, baseOffset, bodyMap);

            Point bodyNeck = GetMapPoint(body, MapNeck);
            Point headNeck = GetMapPoint(head, MapNeck);
            Point headOffset = Add(baseOffset, Subtract(bodyNeck, headNeck));
            Dictionary<string, Point> headMap = ReadMap(head);
            AddLayer(layers, head, headOffset, "head");

            AddFaceAndHair(layers, head, headOffset, faceImage, hairImage);

            foreach ((int itemId, WzImage image) in equipmentImages ?? Enumerable.Empty<(int, WzImage)>())
                AddEquipment(layers, itemId, image, bodyMap, GetOrigin(body), baseOffset, headMap, headOffset, GetOrigin(head), action, frameKey);

            return layers;
        }

        public static IReadOnlyList<CharacterWzActionFrame> ComposeActionFrames(
            WzImage bodyImage, WzImage headImage, WzImage faceImage, WzImage hairImage,
            IEnumerable<(int ItemId, WzImage Image)> equipmentImages, string action)
        {
            if (bodyImage == null) return Array.Empty<CharacterWzActionFrame>();
            Parse(bodyImage);
            WzImageProperty actionNode = bodyImage[action];
            if (actionNode?.WzProperties == null) return Array.Empty<CharacterWzActionFrame>();
            var result = new List<CharacterWzActionFrame>();
            foreach (WzImageProperty frame in actionNode.WzProperties.Where(property => int.TryParse(property.Name, out _)))
            {
                int delay = (frame["delay"] as WzIntProperty)?.Value ?? 100;
                bool flip = (frame["flip"] as WzIntProperty)?.Value != 0;
                result.Add(new(frame.Name, delay, flip, ComposeAction(bodyImage, headImage, faceImage, hairImage, equipmentImages, action, frame.Name)));
            }
            return result;
        }

        public static string GetEquipmentFolder(int itemId)
        {
            return (itemId / 10000) switch
            {
                100 => "Cap",
                101 or 102 or 112 or 113 or 114 => "Accessory",
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

        public static EquipSlot GetEquipSlot(int itemId)
        {
            return (itemId / 10000) switch
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

        public static CharacterPartType GetPartType(string folder)
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

        public static WzCanvasProperty GetCanvas(WzImageProperty property)
        {
            if (property == null)
                return null;

            try
            {
                return property.GetLinkedWzImageProperty() as WzCanvasProperty ?? property as WzCanvasProperty;
            }
            catch
            {
                return property as WzCanvasProperty;
            }
        }

        public static Point GetMapPoint(WzCanvasProperty canvas, string name)
        {
            return canvas?["map"] is WzSubProperty map && map[name] is WzVectorProperty vector
                ? vector.Pos
                : GetOrigin(canvas);
        }

        public static Point GetOrigin(WzCanvasProperty canvas)
        {
            return canvas?["origin"] is WzVectorProperty vector ? vector.Pos : Point.Empty;
        }

        public static Dictionary<string, Point> ReadMap(WzCanvasProperty canvas)
        {
            Dictionary<string, Point> result = new();
            if (canvas?["map"] is not WzSubProperty map)
                return result;

            foreach (WzImageProperty property in map.WzProperties)
            {
                if (property is WzVectorProperty vector)
                    result[property.Name] = vector.Pos;
            }
            return result;
        }

        public static string GetZLayer(WzCanvasProperty canvas, string fallback)
        {
            return canvas?["z"]?.WzValue?.ToString() ?? fallback;
        }

        private static void AddBodySubParts(
            List<CharacterWzLayer> layers,
            WzImageProperty bodyFrame,
            Point bodyNavel,
            Point baseOffset,
            Dictionary<string, Point> bodyMap)
        {
            foreach (string partName in new[] { "arm", "lHand", "rHand" })
            {
                WzCanvasProperty part = GetCanvas(bodyFrame?[partName]);
                if (part == null)
                    continue;

                Point partNavel = GetMapPoint(part, MapNavel);
                Point partOffset = Add(baseOffset, Subtract(bodyNavel, partNavel));
                AddLayer(layers, part, partOffset, GetZLayer(part, partName));

                if (partName == "arm")
                    AddRelativeMapPoint(bodyMap, part, partNavel, "hand");
                else if (partName == "lHand")
                    AddRelativeMapPoint(bodyMap, part, partNavel, "handMove");
            }
        }

        private static void AddRelativeMapPoint(
            Dictionary<string, Point> bodyMap,
            WzCanvasProperty part,
            Point partNavel,
            string pointName)
        {
            if (part?["map"] is not WzSubProperty map || map[pointName] is not WzVectorProperty point)
                return;

            bodyMap[pointName] = Add(Subtract(bodyMap[MapNavel], partNavel), point.Pos);
        }

        private static void AddFaceAndHair(
            List<CharacterWzLayer> layers,
            WzCanvasProperty head,
            Point headOffset,
            WzImage faceImage,
            WzImage hairImage)
        {
            Point headBrow = Add(headOffset, GetMapPoint(head, MapBrow));

            WzCanvasProperty face = GetCanvas(faceImage?.GetFromPath("default/face"));
            if (face != null)
                AddAnchoredLayer(layers, face, headBrow, MapBrow, "face");

            WzCanvasProperty hair = GetCanvas(hairImage?.GetFromPath("default/hair"));
            if (hair != null)
                AddAnchoredLayer(layers, hair, headBrow, MapBrow, "hair");

            WzCanvasProperty hairOverHead = GetCanvas(hairImage?.GetFromPath("default/hairOverHead"));
            if (hairOverHead != null)
                AddAnchoredLayer(layers, hairOverHead, headBrow, MapBrow, "hairOverHead");
        }

        private static void AddEquipment(
            List<CharacterWzLayer> layers,
            int itemId,
            WzImage image,
            IReadOnlyDictionary<string, Point> bodyMap,
            Point bodyOrigin,
            Point baseOffset,
            IReadOnlyDictionary<string, Point> headMap,
            Point headOffset,
            Point headOrigin,
            string action,
            string frameKey)
        {
            string folder = GetEquipmentFolder(itemId);
            if (image == null || folder == null)
                return;

            Parse(image);
            WzImageProperty frame = image.GetFromPath($"{action}/{frameKey}")
                ?? image.GetFromPath($"{action}/0")
                ?? image.GetFromPath("stand1/0")
                ?? image.GetFromPath("default");
            if (frame == null)
                return;

            CharacterPartType type = GetPartType(folder);
            WzCanvasProperty directCanvas = GetCanvas(frame);
            if (directCanvas != null)
            {
                AddEquipmentLayer(layers, directCanvas, frame.Name, type, bodyMap, bodyOrigin, baseOffset, headMap, headOffset, headOrigin);
                return;
            }

            foreach (WzImageProperty child in frame.WzProperties)
            {
                WzCanvasProperty canvas = GetCanvas(child);
                if (canvas != null)
                    AddEquipmentLayer(layers, canvas, child.Name, type, bodyMap, bodyOrigin, baseOffset, headMap, headOffset, headOrigin);
            }
        }

        private static void AddEquipmentLayer(
            List<CharacterWzLayer> layers,
            WzCanvasProperty canvas,
            string name,
            CharacterPartType type,
            IReadOnlyDictionary<string, Point> bodyMap,
            Point bodyOrigin,
            Point baseOffset,
            IReadOnlyDictionary<string, Point> headMap,
            Point headOffset,
            Point headOrigin)
        {
            Dictionary<string, Point> equipmentMap = ReadMap(canvas);
            Point offset = CharacterCompositionMath.CalculateEquipmentOffset(
                type,
                bodyMap,
                bodyOrigin,
                equipmentMap,
                GetOrigin(canvas),
                baseOffset,
                headMap,
                headOffset,
                headOrigin);
            AddLayer(layers, canvas, offset, GetZLayer(canvas, name));
        }

        private static void AddAnchoredLayer(
            List<CharacterWzLayer> layers,
            WzCanvasProperty canvas,
            Point target,
            string mapPoint,
            string fallbackZ)
        {
            Point offset = Subtract(target, GetMapPoint(canvas, mapPoint));
            AddLayer(layers, canvas, offset, GetZLayer(canvas, fallbackZ));
        }

        private static void AddLayer(List<CharacterWzLayer> layers, WzCanvasProperty canvas, Point offset, string fallbackZ)
        {
            if (canvas == null)
                return;

            Point origin = GetOrigin(canvas);
            layers.Add(new CharacterWzLayer(
                canvas,
                offset.X - origin.X,
                offset.Y - origin.Y,
                ZMapReference.GetZIndex(GetZLayer(canvas, fallbackZ))));
        }

        private static void Parse(WzImage image)
        {
            image?.ParseImage();
        }

        private static Point Add(Point left, Point right) => new(left.X + right.X, left.Y + right.Y);
        private static Point Subtract(Point left, Point right) => new(left.X - right.X, left.Y - right.Y);
    }
}
