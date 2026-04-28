using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator;

internal static class MobSkillLevelResolver
{
    public static WzSubProperty ResolveLevelNode(WzSubProperty levelNode, int level)
    {
        return FindLevelNode(levelNode, level, static _ => true);
    }

    public static WzSubProperty FindLevelNode(WzSubProperty levelNode, int level, Func<WzSubProperty, bool> predicate)
    {
        if (levelNode == null || predicate == null)
        {
            return null;
        }

        foreach (int candidateLevel in EnumerateCandidateLevels(levelNode, level))
        {
            if (levelNode[candidateLevel.ToString()] is not WzSubProperty candidateNode)
            {
                continue;
            }

            if (predicate(candidateNode))
            {
                return candidateNode;
            }
        }

        return null;
    }

    public static WzImageProperty FindInheritedProperty(WzSubProperty levelNode, int level, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        WzSubProperty candidateNode = FindLevelNode(
            levelNode,
            level,
            candidate => candidate[propertyName] != null);
        return candidateNode?[propertyName];
    }

    public static int ResolveInheritedInt(WzSubProperty levelNode, int level, string propertyName, int defaultValue = 0)
    {
        return TryResolveInheritedInt(levelNode, level, propertyName, out int value)
            ? value
            : defaultValue;
    }

    public static bool TryResolveInheritedInt(WzSubProperty levelNode, int level, string propertyName, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        WzImageProperty property = FindInheritedProperty(levelNode, level, propertyName);
        if (property == null)
        {
            return false;
        }

        value = MapleLib.WzLib.WzStructure.InfoTool.GetInt(property, 0);
        return true;
    }

    public static Point? ResolveInheritedVector(WzSubProperty levelNode, int level, string propertyName)
    {
        return TryResolveInheritedVector(levelNode, level, propertyName, out Point value)
            ? value
            : null;
    }

    public static bool TryResolveInheritedVector(WzSubProperty levelNode, int level, string propertyName, out Point value)
    {
        value = Point.Zero;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (FindInheritedProperty(levelNode, level, propertyName) is not WzVectorProperty vectorProperty)
        {
            return false;
        }

        value = new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
        return true;
    }

    public static WzSubProperty ResolveInheritedSubProperty(WzSubProperty levelNode, int level, string propertyName)
    {
        return FindInheritedProperty(levelNode, level, propertyName) as WzSubProperty;
    }

    public static int ResolveInheritedElementAttributeMask(WzSubProperty levelNode, int level)
    {
        WzImageProperty property = FindInheritedProperty(levelNode, level, "elemAttr");
        string token = property is WzStringProperty stringProperty
            ? stringProperty.Value
            : MapleLib.WzLib.WzStructure.InfoTool.GetOptionalString(property);
        return ResolveElementAttributeMask(token);
    }

    internal static int ResolveElementAttributeMask(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return 0;
        }

        int mask = 0;
        string[] parts = token.Split(
            new[] { ',', '|', '&', ' ', '/', '\\', ';', ':', '+', '(', ')', '[', ']', '{', '}', '-', '_' },
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            parts = new[] { token };
        }

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i]?.Trim();
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (TryResolveElementAttributeToken(part, out int partMask))
            {
                mask |= partMask;
                continue;
            }

            if (TryResolveCompactElementAttributeToken(part, out int compactMask))
            {
                mask |= compactMask;
            }
        }

        return mask & (1 | 2 | 4 | 8 | 16 | 32);
    }

    public static int ResolveInheritedAreaRange(WzSubProperty levelNode, int level)
    {
        Point? lt = ResolveInheritedVector(levelNode, level, "lt");
        Point? rb = ResolveInheritedVector(levelNode, level, "rb");
        if (!lt.HasValue || !rb.HasValue)
        {
            return 0;
        }

        int horizontalRange = Math.Max(Math.Abs(lt.Value.X), Math.Abs(rb.Value.X));
        int verticalRange = Math.Max(Math.Abs(lt.Value.Y), Math.Abs(rb.Value.Y));
        return Math.Max(horizontalRange, verticalRange);
    }

    private static bool TryResolveElementAttributeToken(string token, out int mask)
    {
        if (int.TryParse(token, out int numericMask) && numericMask >= 0)
        {
            mask = numericMask;
            return true;
        }

        mask = token?.Trim().ToLowerInvariant() switch
        {
            "f" or "fire" or "burn" or "blaze" => 1,
            "i" or "ice" or "cold" or "freeze" or "frost" or "frostbite" => 2,
            "l" or "lightning" or "thunder" => 4,
            "s" or "poison" or "venom" => 8,
            "h" or "holy" or "heal" or "light" => 16,
            "d" or "dark" or "darkness" or "shadow" => 32,
            _ => 0
        };

        return mask != 0;
    }

    private static bool TryResolveCompactElementAttributeToken(string token, out int mask)
    {
        mask = 0;
        string compact = token?.Trim();
        if (string.IsNullOrWhiteSpace(compact) || compact.Length > 6)
        {
            return false;
        }

        for (int i = 0; i < compact.Length; i++)
        {
            if (!TryResolveElementAttributeToken(compact[i].ToString(), out int charMask))
            {
                return false;
            }

            mask |= charMask;
        }

        return mask != 0;
    }

    private static IEnumerable<int> EnumerateCandidateLevels(WzSubProperty levelNode, int level)
    {
        var seenLevels = new HashSet<int>();

        if (level > 0 && seenLevels.Add(level))
        {
            yield return level;
        }

        var fallbackLevels = new List<int>();
        foreach (WzImageProperty child in levelNode.WzProperties)
        {
            if (int.TryParse(child.Name, out int candidateLevel) &&
                candidateLevel > 0 &&
                candidateLevel < level &&
                seenLevels.Add(candidateLevel))
            {
                fallbackLevels.Add(candidateLevel);
            }
        }

        fallbackLevels.Sort((left, right) => right.CompareTo(left));
        foreach (int candidateLevel in fallbackLevels)
        {
            yield return candidateLevel;
        }

        if (seenLevels.Add(1))
        {
            yield return 1;
        }
    }
}
