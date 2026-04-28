using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public sealed class TransportationFieldDefinition
    {
        public TransportationFieldDefinition(int dockX, int dockY, int awayX, int flip, int moveDurationSeconds, int shipKind, int routeLayerZ, string shipObjectPath, string sourceDescription = null)
        {
            DockX = dockX;
            DockY = dockY;
            AwayX = awayX;
            Flip = flip;
            MoveDurationSeconds = moveDurationSeconds;
            ShipKind = shipKind;
            RouteLayerZ = routeLayerZ;
            ShipObjectPath = shipObjectPath ?? string.Empty;
            SourceDescription = string.IsNullOrWhiteSpace(sourceDescription)
                ? "WZ shipObj"
                : sourceDescription.Trim();
        }

        public TransportationFieldDefinition(int dockX, int dockY, int awayX, int flip, int moveDurationSeconds, int shipKind, int routeLayerZ)
            : this(dockX, dockY, awayX, flip, moveDurationSeconds, shipKind, routeLayerZ, string.Empty)
        {
        }

        public TransportationFieldDefinition(int dockX, int dockY, int awayX, int flip, int moveDurationSeconds, int shipKind, string shipObjectPath)
            : this(dockX, dockY, awayX, flip, moveDurationSeconds, shipKind, 0, shipObjectPath)
        {
        }

        public int DockX { get; }
        public int DockY { get; }
        public int AwayX { get; }
        public int Flip { get; }
        public int MoveDurationSeconds { get; }
        public int ShipKind { get; }
        public int RouteLayerZ { get; }
        public string ShipObjectPath { get; }
        public string SourceDescription { get; }
    }

    public static class TransportationFieldDefinitionLoader
    {
        public static bool TryCreate(MapInfo mapInfo, out TransportationFieldDefinition definition)
        {
            definition = null;
            if (mapInfo == null)
            {
                return false;
            }

            if (!TryResolveShipObject(mapInfo, out WzSubProperty shipObject, out string sourceDescription))
            {
                return false;
            }

            WzImageProperty xProperty = shipObject["x"];
            WzImageProperty yProperty = shipObject["y"];
            WzImageProperty shipPathProperty = shipObject["shipObj"];
            if (xProperty == null || yProperty == null)
            {
                return false;
            }

            int dockX = InfoTool.GetInt(xProperty);
            int dockY = InfoTool.GetInt(yProperty);
            // Client CShip::Init falls back to an empty ship path when this member is missing.
            string shipPath = shipPathProperty == null
                ? string.Empty
                : InfoTool.GetString(shipPathProperty);
            int shipKind = GetInt(shipObject["shipKind"], 0);
            int flip = GetInt(shipObject["f"], 0);
            // CShip::Init uses default 0 for missing shipObj numeric members.
            int moveDurationSeconds = GetInt(shipObject["tMove"], 0);
            int awayX = GetInt(shipObject["x0"], 0);
            int routeLayerZ = shipKind == 0 ? GetInt(shipObject["z"], 0) : 0;

            definition = new TransportationFieldDefinition(
                dockX,
                dockY,
                awayX,
                flip,
                moveDurationSeconds,
                shipKind,
                routeLayerZ,
                shipPath,
                sourceDescription);
            return true;
        }

        public static bool TryResolveObjectSetPath(string shipObjectPath, out string objectSetName, out string propertyPath)
        {
            objectSetName = null;
            propertyPath = null;
            if (string.IsNullOrWhiteSpace(shipObjectPath))
            {
                return false;
            }

            string normalizedPath = shipObjectPath
                .Replace('\\', '/')
                .Trim();

            const string mapObjectPrefix = "Map/Obj/";
            if (!normalizedPath.StartsWith(mapObjectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relativePath = normalizedPath.Substring(mapObjectPrefix.Length);
            string[] segments = relativePath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            objectSetName = segments[0];
            if (objectSetName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                objectSetName = objectSetName[..^4];
            }

            propertyPath = string.Join("/", segments.Skip(1));
            return !string.IsNullOrWhiteSpace(objectSetName) && !string.IsNullOrWhiteSpace(propertyPath);
        }

        private static int GetInt(WzImageProperty property, int defaultValue)
        {
            return property == null ? defaultValue : InfoTool.GetInt(property);
        }

        private static bool TryResolveShipObject(MapInfo mapInfo, out WzSubProperty shipObject, out string sourceDescription)
        {
            if (TryResolveShipObjectFromAdditionalNonInfoProps(mapInfo, out shipObject))
            {
                sourceDescription = "MapInfo.additionalNonInfoProps/shipObj";
                return true;
            }

            if (TryResolveShipObjectFromMapImage(mapInfo?.Image, out shipObject))
            {
                sourceDescription = "MapInfo.Image/shipObj";
                return true;
            }

            if (TryResolveShipObjectFromLinkedMapImage(mapInfo, out shipObject, out int linkedMapId))
            {
                sourceDescription = $"linked map {linkedMapId:D9}/shipObj via info/link";
                return true;
            }

            sourceDescription = string.Empty;
            return shipObject != null;
        }

        private static bool TryResolveShipObjectFromAdditionalNonInfoProps(MapInfo mapInfo, out WzSubProperty shipObject)
        {
            shipObject = mapInfo?.additionalNonInfoProps?
                .OfType<WzSubProperty>()
                .FirstOrDefault(prop => string.Equals(prop.Name, "shipObj", StringComparison.OrdinalIgnoreCase));
            return shipObject != null;
        }

        private static bool TryResolveShipObjectFromMapImage(WzImage mapImage, out WzSubProperty shipObject)
        {
            shipObject = null;
            if (mapImage == null)
            {
                return false;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            shipObject = mapImage.WzProperties
                .OfType<WzSubProperty>()
                .FirstOrDefault(prop => string.Equals(prop.Name, "shipObj", StringComparison.OrdinalIgnoreCase));
            return shipObject != null;
        }

        private static bool TryResolveShipObjectFromLinkedMapImage(MapInfo mapInfo, out WzSubProperty shipObject, out int linkedMapId)
        {
            shipObject = null;
            linkedMapId = TryResolveLinkedMapId(mapInfo?.Image);
            if (linkedMapId <= 0)
            {
                return false;
            }

            WzImage linkedMapImage = TryResolveLinkedMapImage(mapInfo, linkedMapId);
            return TryResolveShipObjectFromMapImage(linkedMapImage, out shipObject);
        }

        private static int TryResolveLinkedMapId(WzImage mapImage)
        {
            if (mapImage == null)
            {
                return 0;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            WzImageProperty linkProperty = mapImage["info"]?["link"];
            if (linkProperty == null)
            {
                return 0;
            }

            if (linkProperty is WzIntProperty intProperty)
            {
                int linkedMapId = intProperty.GetInt();
                return linkedMapId > 0 ? linkedMapId : 0;
            }

            string linkedMapToken = InfoTool.GetString(linkProperty);
            return int.TryParse(linkedMapToken, out int parsedLinkedMapId) && parsedLinkedMapId > 0
                ? parsedLinkedMapId
                : 0;
        }

        private static WzImage TryResolveLinkedMapImage(MapInfo mapInfo, int linkedMapId)
        {
            if (mapInfo?.Image?.Parent is WzDirectory parentDirectory)
            {
                string linkedImageName = linkedMapId.ToString("D9", CultureInfo.InvariantCulture) + ".img";
                if (parentDirectory[linkedImageName] is WzImage siblingMapImage)
                {
                    return siblingMapImage;
                }
            }

            if (global::HaCreator.Program.WzManager == null)
            {
                return null;
            }

            return WzInfoTools.FindMapImage(linkedMapId.ToString(CultureInfo.InvariantCulture), global::HaCreator.Program.WzManager);
        }
    }
}
