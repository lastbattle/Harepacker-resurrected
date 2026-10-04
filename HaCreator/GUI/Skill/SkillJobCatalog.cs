using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapleLib.Img;
using MapleLib.WzLib;

namespace HaCreator.GUI.Skill;

/// <summary>Data-derived Skill image catalog with conservative, versioned enrichment.</summary>
public sealed class SkillJobCatalog
{
    private static readonly HashSet<string> SpecialRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Attacktype", "BFSkill", "EliteMobSkill", "ExclusiveSlowStat", "FamiliarSkill", "FieldSkill",
        "HekatonFieldSkill", "ItemSkill", "MCGuardian", "MCSkill", "MExplorerSkill", "MobSkill",
        "RidingSkillInfo"
    };
    private static readonly HashSet<string> NonPlayerRoots = new(StringComparer.Ordinal)
    {
        "001", "7000", "7100", "7200", "800", "900", "910", "8000", "9000", "9100", "9500",
        "30000", "40000", "40001", "40002", "40003", "40004", "40005",
        "50000", "50006", "50007", "50008"
    };
    private readonly IDataSource _source;

    public SkillJobCatalog(IDataSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

    public IReadOnlyList<SkillBookDescriptor> EnumerateBooks()
    {
        var result = new List<SkillBookDescriptor>();
        IReadOnlyDictionary<string, SkillBookPlaceholderStatus> placeholders = DiscoverPlaceholderStatus();
        AddDirectory(result, string.Empty);
        foreach (string subdirectory in EnumerateSubdirectories()
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            AddDirectory(result, NormalizeDirectory(subdirectory));
        return result.Select(book => placeholders.TryGetValue(book.RelativePath, out SkillBookPlaceholderStatus status)
                ? book with { IsPlaceholder = status == SkillBookPlaceholderStatus.ConfirmedEmpty, PlaceholderStatus = status }
                : book)
            .GroupBy(book => book.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).OrderBy(book => book.Scope).ThenBy(book => book.Family)
            .ThenBy(book => book.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IEnumerable<string> EnumerateSubdirectories()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in _source.GetSubdirectories("Skill") ?? Enumerable.Empty<string>())
            AddDirectoryAndParents(paths, path);
        if (_source.VersionInfo?.Categories.TryGetValue("Skill", out CategoryInfo category) == true)
            foreach (string path in category.Subdirectories ?? Enumerable.Empty<string>()) AddDirectoryAndParents(paths, path);
        return paths;
    }

    private static void AddDirectoryAndParents(HashSet<string> paths, string path)
    {
        string normalized = NormalizeDirectory(path);
        if (normalized.Length == 0) return;
        string[] parts = normalized.Split('/');
        for (int count = 1; count <= parts.Length; count++) paths.Add(string.Join('/', parts.Take(count)));
    }

    private void AddDirectory(List<SkillBookDescriptor> result, string directory)
    {
        IEnumerable<string> names = _source.GetImageNamesInDirectory("Skill", directory) ?? Enumerable.Empty<string>();
        foreach (string rawName in names)
        {
            string name = NormalizeImageName(rawName);
            if (string.IsNullOrWhiteSpace(name)) continue;
            string relative = string.IsNullOrEmpty(directory) ? name : directory + "/" + name;
            string id = Path.GetFileNameWithoutExtension(name);
            result.Add(Classify(relative, id));
        }
    }

    public SkillBookDescriptor Classify(string relativePath, string bookId)
    {
        string normalized = NormalizeRelativePath(relativePath);
        string directory = normalized.Contains('/') ? normalized[..normalized.LastIndexOf('/')] : string.Empty;
        string bare = Path.GetFileNameWithoutExtension(bookId);
        bool placeholder = false;

        if (!string.IsNullOrEmpty(directory) || SpecialRootNames.Contains(bare) || bare.StartsWith("Recipe_", StringComparison.OrdinalIgnoreCase))
        {
            string specialFamily = directory.StartsWith("Dragon", StringComparison.OrdinalIgnoreCase) ? "Evan Dragon assets" :
                directory.Length > 0 ? directory.Replace('/', ' ') : SpecialFamily(bare);
            return new("Skill", normalized, Path.GetFileName(normalized), bare, specialFamily, SpecialAdvancement(bare),
                SkillCatalogScope.Special, placeholder, ClassName: SpecialClassName(directory, bare, specialFamily));
        }
        if (!bare.All(char.IsDigit) || NonPlayerRoots.Contains(bare) || IsSharedSystemRoot(bare) ||
            (int.TryParse(bare, out int specialId) && specialId is >= 9200 and <= 9204))
            return new("Skill", normalized, Path.GetFileName(normalized), bare, "System / shared", "System or shared skill data",
                SkillCatalogScope.Special, placeholder, ClassName: SystemClassName(bare));

        (string family, string className, string advancement) = PlayerClassification(bare);
        if (family == null)
            return new("Skill", normalized, Path.GetFileName(normalized), bare, "Other / unknown", "Unmatched numeric books",
                SkillCatalogScope.Special, placeholder, ClassName: "Unmatched numeric books");
        return new("Skill", normalized, Path.GetFileName(normalized), bare, family, advancement,
            SkillCatalogScope.Player, placeholder, ClassName: className);
    }

    private (string family, string className, string advancement) PlayerClassification(string id)
    {
        if (!int.TryParse(id, out int value)) return (null, null, null);
        bool modern = _source.VersionInfo?.IsVUpdate == true;
        int suffix = modern ? 14 : 12;

        string regionalPirate = IsSeaRegion() ? "ZEN" : "Jett";
        string animaSwordsman = IsGlobalRegion() ? "Ren" : "Len";
        if (value == 508) return Player("Explorers", regionalPirate, id, "1st advancement");
        if (BetweenAny(value, (570, 572))) return Player("Explorers", regionalPirate, id, OrdinalAdvancement(value - 568));
        if (value == 509) return Player("Explorers", "Pirate (shared)", id, "1st advancement");
        if (BetweenAny(value, (580, 582))) return Player("Explorers", "Buccaneer (legacy)", id, OrdinalAdvancement(value - 578));
        if (BetweenAny(value, (590, 592))) return Player("Explorers", "Corsair (legacy)", id, OrdinalAdvancement(value - 588));

        if (value == 0) return Player("Explorers", "Beginner", id, beginner: true);
        if (value == 100) return Player("Explorers", "Warrior", id);
        if (BetweenAny(value, (110, modern ? 114 : 112))) return Player("Explorers", "Hero", id);
        if (BetweenAny(value, (120, modern ? 124 : 122))) return Player("Explorers", "Paladin", id);
        if (BetweenAny(value, (130, modern ? 134 : 132))) return Player("Explorers", "Dark Knight", id);
        if (value == 200) return Player("Explorers", "Magician", id);
        if (BetweenAny(value, (210, modern ? 214 : 212))) return Player("Explorers", "Arch Mage (Fire, Poison)", id);
        if (BetweenAny(value, (220, modern ? 224 : 222))) return Player("Explorers", "Arch Mage (Ice, Lightning)", id);
        if (BetweenAny(value, (230, modern ? 234 : 232))) return Player("Explorers", "Bishop", id);
        if (value == 300) return Player("Explorers", "Bowman", id);
        if (BetweenAny(value, (310, modern ? 314 : 312))) return Player("Explorers", "Bowmaster", id);
        if (BetweenAny(value, (320, modern ? 324 : 322))) return Player("Explorers", "Marksman", id);
        if (modern && (value == 301 || BetweenAny(value, (330, 334)))) return Player("Explorers", "Pathfinder", id,
            value == 301 ? "1st advancement" : value == 330 ? "2nd advancement" : value == 331 ? "3rd advancement" : value == 332 ? "4th advancement" : "6th advancement");
        if (value == 400) return Player("Explorers", "Thief", id);
        if (BetweenAny(value, (410, modern ? 414 : 412))) return Player("Explorers", "Night Lord", id);
        if (BetweenAny(value, (420, modern ? 424 : 422))) return Player("Explorers", "Shadower", id);
        if (BetweenAny(value, (430, modern ? 436 : 434))) return Player("Explorers", "Dual Blade", id);
        if (value == 500) return Player("Explorers", "Pirate", id);
        if (value == 501 || BetweenAny(value, (530, modern ? 534 : 532))) return Player("Explorers", "Cannoneer", id, value == 501 ? "1st advancement" : null);
        if (BetweenAny(value, (510, modern ? 514 : 512))) return Player("Explorers", "Buccaneer", id);
        if (BetweenAny(value, (520, modern ? 524 : 522))) return Player("Explorers", "Corsair", id);

        if (value == 1000) return Player("Cygnus Knights", "Noblesse", id, beginner: true);
        if (Branch(value, suffix, 1100)) return Player("Cygnus Knights", "Dawn Warrior", id);
        if (Branch(value, suffix, 1200)) return Player("Cygnus Knights", "Blaze Wizard", id);
        if (Branch(value, suffix, 1300)) return Player("Cygnus Knights", "Wind Archer", id);
        if (Branch(value, suffix, 1400)) return Player("Cygnus Knights", "Night Walker", id);
        if (Branch(value, suffix, 1500)) return Player("Cygnus Knights", "Thunder Breaker", id);

        if (value == 2000) return Player("Heroes / Legends", "Aran", id, beginner: true);
        if (value == 2001) return Player("Heroes / Legends", "Evan", id, beginner: true);
        if (value == 2002) return Player("Heroes / Legends", "Mercedes", id, beginner: true);
        if (value == 2003) return Player("Heroes / Legends", "Phantom", id, beginner: true);
        if (value == 2004) return Player("Heroes / Legends", "Luminous", id, beginner: true);
        if (value == 2005) return Player("Heroes / Legends", "Shade", id, beginner: true);
        if (Branch(value, suffix, 2100)) return Player("Heroes / Legends", "Aran", id);
        if (value == 2200) return Player("Heroes / Legends", "Evan", id);
        if (BetweenAny(value, (2210, 2218))) return Player("Heroes / Legends", "Evan", id, OrdinalAdvancement(value - 2208));
        if (modern && value == 2220) return Player("Heroes / Legends", "Evan", id, "6th advancement (HEXA mastery)");
        if (Branch(value, suffix, 2300)) return Player("Heroes / Legends", "Mercedes", id);
        if (Branch(value, suffix, 2400)) return Player("Heroes / Legends", "Phantom", id);
        if (modern && Branch(value, 14, 2500)) return Player("Heroes / Legends", "Shade", id);
        if (modern && Branch(value, 14, 2700)) return Player("Heroes / Legends", "Luminous", id);

        if (value == 3000) return Player("Resistance", "Citizen", id, beginner: true);
        if (value == 3001) return Player("Resistance", "Demon", id, beginner: true);
        if (modern && value == 3002) return Player("Resistance", "Xenon", id, beginner: true);
        if (Branch(value, suffix, 3100)) return Player("Resistance", "Demon Slayer", id);
        if (modern && (value == 3101 || BetweenAny(value, (3120, 3124)))) return Player("Resistance", "Demon Avenger", id, value == 3101 ? "1st advancement" : null);
        if (Branch(value, suffix, 3200)) return Player("Resistance", "Battle Mage", id);
        if (Branch(value, suffix, 3300)) return Player("Resistance", "Wild Hunter", id);
        if (Branch(value, suffix, 3500)) return Player("Resistance", "Mechanic", id);
        if (modern && Branch(value, 14, 3600)) return Player("Resistance", "Xenon", id);
        if (modern && Branch(value, 14, 3700)) return Player("Resistance", "Blaster", id);

        if (value == 5000) return Player("Mihile", "Mihile", id, beginner: true);
        if (Branch(value, suffix, 5100)) return Player("Mihile", "Mihile", id);
        if (!modern) return (null, null, null);

        if (value == 4001) return Player("Sengoku", "Hayato", id, beginner: true);
        if (value == 4002) return Player("Sengoku", "Kanna", id, beginner: true);
        if (Branch(value, 14, 4100)) return Player("Sengoku", "Hayato", id);
        if (Branch(value, 14, 4200)) return Player("Sengoku", "Kanna", id);
        if (value == 6000) return Player("Nova", "Kaiser", id, beginner: true);
        if (value == 6001) return Player("Nova", "Angelic Buster", id, beginner: true);
        if (value == 6002) return Player("Nova", "Cadena", id, beginner: true);
        if (value == 6003) return Player("Nova", "Kain", id, beginner: true);
        if (Branch(value, 14, 6100)) return Player("Nova", "Kaiser", id);
        if (Branch(value, 14, 6300)) return Player("Nova", "Kain", id);
        if (Branch(value, 14, 6400)) return Player("Nova", "Cadena", id);
        if (Branch(value, 14, 6500)) return Player("Nova", "Angelic Buster", id);
        if (value == 10000) return Player("Child of God", "Zero", id, beginner: true);
        if (Branch(value, 14, 10100)) return Player("Child of God", "Zero", id);
        if (value == 11000) return Player("Overseas / legacy", "Beast Tamer", id, beginner: true);
        if (Branch(value, 14, 11200)) return Player("Overseas / legacy", "Beast Tamer", id);
        if (value == 14000) return Player("Kinesis", "Kinesis", id, beginner: true);
        if (Branch(value, 14, 14200)) return Player("Kinesis", "Kinesis", id);
        if (value == 15000) return Player("Flora", "Illium", id, beginner: true);
        if (value == 15001) return Player("Flora", "Ark", id, beginner: true);
        if (value == 15002) return Player("Flora", "Adele", id, beginner: true);
        if (value == 15003) return Player("Flora", "Khali", id, beginner: true);
        if (Branch(value, 14, 15100)) return Player("Flora", "Adele", id);
        if (Branch(value, 14, 15200)) return Player("Flora", "Illium", id);
        if (Branch(value, 14, 15400)) return Player("Flora", "Khali", id);
        if (Branch(value, 14, 15500)) return Player("Flora", "Ark", id);
        if (value == 16000) return Player("Anima", "Hoyoung", id, beginner: true);
        if (value == 16001) return Player("Anima", "Lara", id, beginner: true);
        if (value == 16002) return Player("Anima", animaSwordsman, id, beginner: true);
        if (Branch(value, 14, 16100)) return Player("Anima", animaSwordsman, id);
        if (Branch(value, 14, 16200)) return Player("Anima", "Lara", id);
        if (Branch(value, 14, 16400)) return Player("Anima", "Hoyoung", id);
        if (value == 17000) return Player("Overseas / regional", "Mo Xuan", id, beginner: true);
        if (value == 17001) return Player("Overseas / regional", "Lynn", id, beginner: true);
        if (Branch(value, 14, 17200)) return Player("Overseas / regional", "Lynn", id);
        if (Branch(value, 14, 17500)) return Player("Overseas / regional", "Mo Xuan", id);
        if (value == 18000) return Player("Overseas / regional", "Sia Astelle", id, beginner: true);
        if (value == 18001) return Player("Overseas / regional", "Erel Light", id, beginner: true);
        if (Branch(value, 14, 18100)) return Player("Overseas / regional", "Erel Light", id);
        if (Branch(value, 14, 18200)) return Player("Overseas / regional", "Sia Astelle", id);
        if (value is 13000 or 13100) return Player("Event / crossover", "Pink Bean", id, value == 13000 ? "Beginner / shared advancement" : "Event advancement");
        if (value is 13001 or 13500) return Player("Event / crossover", "Yeti", id, value == 13001 ? "Beginner / shared advancement" : "Event advancement");
        if (value is 12005 or 12006 or 12100 or 12200) return Player("Event / crossover", "Event class", id, "Event advancement");
        return (null, null, null);
    }

    private static bool Branch(int value, int finalSuffix, params int[] roots) =>
        roots.Any(root => value == root || value >= root + 10 && value <= root + finalSuffix);
    private static bool BetweenAny(int value, params (int Min, int Max)[] ranges) => ranges.Any(range => value >= range.Min && value <= range.Max);
    private static (string family, string className, string advancement) Player(string family, string className, string id,
        string advancement = null, bool beginner = false) =>
        (family, className, advancement ?? Advancement(id, beginner));
    private static string Advancement(string id, bool beginner = false) => beginner ? "Beginner / shared advancement" : id switch
    {
        _ when id.EndsWith("14", StringComparison.Ordinal) => "6th advancement",
        _ when id.EndsWith("12", StringComparison.Ordinal) => "4th advancement",
        _ when id.EndsWith("11", StringComparison.Ordinal) => "3rd advancement",
        _ when id.EndsWith("10", StringComparison.Ordinal) => "2nd advancement",
        _ => "1st / shared advancement"
    };
    private static string OrdinalAdvancement(int stage) => stage switch
    {
        1 => "1st advancement", 2 => "2nd advancement", 3 => "3rd advancement",
        4 => "4th advancement", 5 => "5th advancement", 6 => "6th advancement",
        7 => "7th advancement", 8 => "8th advancement", 9 => "9th advancement",
        10 => "10th advancement", _ => "Shared advancement"
    };
    private bool IsSeaRegion()
    {
        string region = _source.VersionInfo?.SourceRegion ?? string.Empty;
        return region.Contains("MSEA", StringComparison.OrdinalIgnoreCase) ||
            region.Contains("MapleStorySEA", StringComparison.OrdinalIgnoreCase) ||
            region.Equals("SEA", StringComparison.OrdinalIgnoreCase) ||
            (_source.VersionInfo?.Version ?? string.Empty).Contains("msea", StringComparison.OrdinalIgnoreCase);
    }
    private bool IsGlobalRegion() => (_source.VersionInfo?.SourceRegion ?? string.Empty).Contains("Global", StringComparison.OrdinalIgnoreCase) ||
        (_source.VersionInfo?.Version ?? string.Empty).StartsWith("global", StringComparison.OrdinalIgnoreCase) ||
        (_source.VersionInfo?.Version ?? string.Empty).StartsWith("gms", StringComparison.OrdinalIgnoreCase);
    private static bool IsSharedSystemRoot(string id) => id.StartsWith("800", StringComparison.Ordinal) && id.Length >= 6;
    private static string SpecialFamily(string id) => id switch
    {
        "MobSkill" or "EliteMobSkill" => "Mob skills", "ItemSkill" => "Item skills", "Attacktype" => "Attack types",
        "FamiliarSkill" => "Familiar skills", "FieldSkill" or "HekatonFieldSkill" => "Field skills",
        "MExplorerSkill" => "Monster Life skills", "RidingSkillInfo" => "Riding skills",
        "ExclusiveSlowStat" => "Status definitions",
        "BFSkill" or "MCGuardian" or "MCSkill" => "Battlefield / minigame",
        _ when id.StartsWith("Recipe_", StringComparison.OrdinalIgnoreCase) => "Professions / recipes",
        _ => "Special data"
    };
    private static string SpecialAdvancement(string id) => id.StartsWith("Recipe_", StringComparison.OrdinalIgnoreCase) ? "Recipe table" : "Generic and specialized editor";
    private static string SpecialClassName(string directory, string id, string family) =>
        !string.IsNullOrEmpty(directory) ? directory.Replace('/', ' ') : id.Length > 0 ? id : family;
    private static string SystemClassName(string id) => id switch
    {
        "800" => "Manager", "900" => "GM", "910" => "Super GM", "8000" => "Riding skills",
        "9000" or "9100" => "Additional skills", "7200" => "Monster Life shared skills",
        "9500" => "Copied / shared class skills", _ when id.StartsWith("4000", StringComparison.Ordinal) => "V and HEXA shared skills",
        _ when id.StartsWith("5000", StringComparison.Ordinal) => "Shared skill systems",
        _ when id.StartsWith("800", StringComparison.Ordinal) => "Shared skill groups", _ => "System data"
    };
    public static string NormalizeRelativePath(string path)
    {
        string normalized = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        if (!normalized.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) normalized += ".img";
        return normalized;
    }
    private static string NormalizeDirectory(string path) => (path ?? string.Empty).Replace('\\', '/').Trim('/');
    private static string NormalizeImageName(string name)
    {
        string file = (name ?? string.Empty).Replace('\\', '/').Split('/').LastOrDefault() ?? string.Empty;
        return file.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? file : file + ".img";
    }

    private IReadOnlyDictionary<string, SkillBookPlaceholderStatus> DiscoverPlaceholderStatus()
    {
        var result = new Dictionary<string, SkillBookPlaceholderStatus>(StringComparer.OrdinalIgnoreCase);
        WzDirectory root = _source.GetDirectory("Skill");
        // VirtualWzDirectory.WzImages materializes every IMG in the directory. Catalog
        // construction must remain names-only, especially for multi-gigabyte modern Skill data.
        // IMG filesystem placeholder status stays Unknown until a selected image is opened.
        if (root is VirtualWzDirectory)
            return result;
        if (root != null) AddPlaceholderStatus(result, root, string.Empty, categoryRoot: true);
        return result;
    }

    private static void AddPlaceholderStatus(Dictionary<string, SkillBookPlaceholderStatus> result, WzDirectory directory, string prefix, bool categoryRoot)
    {
        string current = categoryRoot || string.IsNullOrWhiteSpace(directory.Name) ? prefix :
            string.IsNullOrEmpty(prefix) ? directory.Name : prefix + "/" + directory.Name;
        foreach (WzImage image in directory.WzImages)
        {
            string path = NormalizeRelativePath(string.IsNullOrEmpty(current) ? image.Name : current + "/" + image.Name);
            result[path] = image.BlockSize <= 0 && !image.Parsed && !image.Changed
                ? SkillBookPlaceholderStatus.ConfirmedEmpty : SkillBookPlaceholderStatus.ConfirmedNonEmpty;
        }
        foreach (WzDirectory child in directory.WzDirectories) AddPlaceholderStatus(result, child, current, false);
    }
}
