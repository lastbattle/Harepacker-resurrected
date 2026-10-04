using System.Xml.Linq;
using System.IO;

namespace UnitTest_SkillEditor;

public sealed class LocalizationParityTests
{
    [Fact]
    public void EverySkillEditorLocaleHasTheNeutralKeySet()
    {
        string root = FindRepositoryRoot();
        string directory = Path.Combine(root, "HaCreator", "GUI", "Skill");
        string neutral = Path.Combine(directory, "SkillEditorText.resx");
        string[] expected = Keys(neutral);
        foreach (string culture in new[] { "zh-CHT", "zh-CHS", "ko", "ja" })
            Assert.Equal(expected, Keys(Path.Combine(directory, $"SkillEditorText.{culture}.resx")));
    }
    private static string[] Keys(string path) => XDocument.Load(path).Root!.Elements("data").Select(element => (string)element.Attribute("name")!).OrderBy(key => key, StringComparer.Ordinal).ToArray();
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "MapleHaSuite.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
