using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public sealed class TransportationFieldDefinition
    {
        public TransportationFieldDefinition(int dockX, int dockY, int awayX, int flip, int moveDurationSeconds, int shipKind, string shipObjectPath)
        {
            DockX = dockX;
            DockY = dockY;
            AwayX = awayX;
            Flip = flip;
            MoveDurationSeconds = moveDurationSeconds;
            ShipKind = shipKind;
            ShipObjectPath = shipObjectPath ?? string.Empty;
        }

        public int DockX { get; }
        public int DockY { get; }
        public int AwayX { get; }
        public int Flip { get; }
        public int MoveDurationSeconds { get; }
        public int ShipKind { get; }
        public string ShipObjectPath { get; }
    }

    public static class TransportationFieldDefinitionLoader
    {
        public static bool TryCreate(MapInfo mapInfo, out TransportationFieldDefinition definition)
        {
            definition = null;
            if (mapInfo?.additionalNonInfoProps == null)
            {
                return false;
            }

            WzSubProperty shipObject = mapInfo.additionalNonInfoProps
                .OfType<WzSubProperty>()
                .FirstOrDefault(prop => string.Equals(prop.Name, "shipObj", StringComparison.OrdinalIgnoreCase));
            if (shipObject == null)
            {
                return false;
            }

            WzImageProperty xProperty = shipObject["x"];
            WzImageProperty yProperty = shipObject["y"];
            WzImageProperty shipPathProperty = shipObject["shipObj"];
            if (xProperty == null || yProperty == null || shipPathProperty == null)
            {
                return false;
            }

            int dockX = InfoTool.GetInt(xProperty);
            int dockY = InfoTool.GetInt(yProperty);
            string shipPath = InfoTool.GetString(shipPathProperty);
            int shipKind = GetInt(shipObject["shipKind"], 0);
            int flip = GetInt(shipObject["f"], 0);
            int moveDurationSeconds = GetInt(shipObject["tMove"], shipKind == 1 ? 2 : 10);
            int awayX = GetInt(shipObject["x0"], dockX - 800);

            definition = new TransportationFieldDefinition(
                dockX,
                dockY,
                awayX,
                flip,
                moveDurationSeconds,
                shipKind,
                shipPath);
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
    }
}
