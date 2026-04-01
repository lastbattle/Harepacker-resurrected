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
