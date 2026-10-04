using HaCreator.GUI.Skill;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace UnitTest_SkillEditor;

public sealed class SkillEditorPersistenceIntegrationTests
{
    [Fact]
    [Trait("Category", "OptInRealData")]
    public void OptionalRealImgCatalogEnumerationDoesNotLoadSkillImages()
    {
        string? root = Environment.GetEnvironmentVariable("HAREPACKER_SKILL_TEST_IMG_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;
        using var source = new ImgFileSystemDataSource(root, new HaCreatorConfig { ImgRootPath = root });
        var repository = new SkillEditorRepository(source, _ => { });
        int readsBefore = source.GetStats().DiskReadCount;

        IReadOnlyList<SkillBookDescriptor> books = repository.Catalog.EnumerateBooks();

        Assert.NotEmpty(books);
        Assert.Equal(readsBefore, source.GetStats().DiskReadCount);
        if ((source.VersionInfo?.SourceRegion ?? string.Empty).Contains("Global", StringComparison.OrdinalIgnoreCase) &&
            source.VersionInfo?.IsVUpdate == true && source.VersionInfo?.Is64Bit == true)
        {
            Assert.Equal("Jett", Assert.Single(books, book => book.BookId == "508").ClassName);
            Assert.Equal("Pathfinder", Assert.Single(books, book => book.BookId == "334").ClassName);
            Assert.Equal("Demon Avenger", Assert.Single(books, book => book.BookId == "3101").ClassName);
            Assert.Equal("Beast Tamer", Assert.Single(books, book => book.BookId == "11212").ClassName);
            Assert.Equal("Sia Astelle", Assert.Single(books, book => book.BookId == "18214").ClassName);
            Assert.Equal(SkillCatalogScope.Special, Assert.Single(books, book => book.BookId == "800129").Scope);
            Assert.DoesNotContain(books, book => !book.RelativePath.Contains('/') && book.Family == "Other / unknown");
            SkillBookDescriptor namedJett = Assert.Single(repository.ResolveBookNames(new[] { Assert.Single(books, book => book.BookId == "508") }));
            Assert.Equal("Jett's Crisis", namedJett.BookName);
            Assert.Contains("Jett's Crisis [508]", namedJett.DisplayName);
        }
        SkillBookDescriptor? redmoon = books.SingleOrDefault(book => string.Equals(book.RelativePath,
            "Roguelike/Skill/Redmoon/503.img", StringComparison.OrdinalIgnoreCase));
        if (redmoon != null)
        {
            SkillCatalogEntry anim = Assert.Single(repository.LoadEntries(redmoon), entry => entry.Id == "Anim");
            Assert.Equal(SkillActivityKind.Unknown, anim.Metadata.Activity);
            Assert.Contains("AvatarSlots", anim.Metadata.PropertyNames);
            Assert.Contains("Notifies", anim.Metadata.PropertyNames);
        }
    }

    [Fact]
    public void ImgFileSystemCatalogEnumerationRemainsNamesOnly()
    {
        string root = Path.Combine(Path.GetTempPath(), $"SkillEditorCatalog_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Skill"));
            Directory.CreateDirectory(Path.Combine(root, "String"));
            WriteManifest(root);
            WriteImages(root);
            using var source = new ImgFileSystemDataSource(root, new HaCreatorConfig { ImgRootPath = root });

            IReadOnlyList<SkillBookDescriptor> books = new SkillJobCatalog(source).EnumerateBooks();

            Assert.Contains(books, book => book.RelativePath == "100.img");
            Assert.Equal(0, source.GetStats().DiskReadCount);
            Assert.All(books, book => Assert.Equal(SkillBookPlaceholderStatus.Unknown, book.PlaceholderStatus));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ImgFileSystemSavePersistsSkillAndStringAcrossDataSourceReopen()
    {
        string root = Path.Combine(Path.GetTempPath(), $"SkillEditorImg_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Skill"));
            Directory.CreateDirectory(Path.Combine(root, "String"));
            WriteManifest(root);
            WriteImages(root);

            using (var source = new ImgFileSystemDataSource(root, new HaCreatorConfig { ImgRootPath = root }))
            {
                var repository = new SkillEditorRepository(source, _ => { });
                SkillBookDescriptor book = Assert.Single(repository.Catalog.EnumerateBooks(), candidate => candidate.BookId == "100");
                SkillCatalogEntry entry = Assert.Single(repository.LoadEntries(book));
                SkillDocument document = repository.OpenDocument(entry);
                document.EnableStringEditing();
                document.Edit("integration edit", () =>
                {
                    ((WzIntProperty)document.WorkingSkill["damage"]).Value = 27;
                    ((WzStringProperty)document.WorkingString["name"]).Value = "Persisted name";
                });

                SkillSaveResult result = repository.Save(document);

                Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
                Assert.False(document.IsDirty);
            }

            using (var reopened = new ImgFileSystemDataSource(root, new HaCreatorConfig { ImgRootPath = root }))
            {
                WzImage skill = reopened.GetImageByPath("Skill/100.img");
                WzImage text = reopened.GetImageByPath("String/Skill.img");
                Assert.NotNull(skill);
                Assert.NotNull(text);
                Assert.Equal(27, Assert.IsType<WzIntProperty>(skill["skill"]?["1001000"]?["damage"]).Value);
                Assert.Equal("Persisted name", Assert.IsType<WzStringProperty>(text["1001000"]?["name"]).Value);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void WriteImages(string root)
    {
        var skillImage = new WzImage("100.img");
        var skills = new WzSubProperty("skill");
        var skill = new WzSubProperty("1001000");
        skill.AddProperty(new WzIntProperty("damage", 1));
        skills.AddProperty(skill);
        skillImage.AddProperty(skills);

        var stringImage = new WzImage("Skill.img");
        var stringEntry = new WzSubProperty("1001000");
        stringEntry.AddProperty(new WzStringProperty("name", "Original name"));
        stringImage.AddProperty(stringEntry);

        WzImgSerializer serializer = WzImgSerializer.CreateForImgExtraction();
        serializer.SerializeImage(skillImage, Path.Combine(root, "Skill", "100.img"));
        serializer.SerializeImage(stringImage, Path.Combine(root, "String", "Skill.img"));
    }

    private static void WriteManifest(string root)
    {
        var manifest = new
        {
            version = "skill-editor-integration",
            displayName = "Skill Editor Integration",
            extractedDate = DateTime.UtcNow.ToString("O"),
            encryption = "BMS",
            is64Bit = false,
            isVUpdate = false,
            categories = new Dictionary<string, object>
            {
                ["Skill"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("O") },
                ["String"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("O") }
            }
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest));
    }
}
