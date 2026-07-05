using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace UnitTest_WzFile;

[TestClass]
[SupportedOSPlatform("windows")]
public class WzCoreOptimizationTests
{
    [TestMethod]
    public void DirectoryAndManagerLookups_AreCaseInsensitive()
    {
        using var file = new WzFile(95, WzMapleVersion.GMS) { Name = "Effect.wz" };
        file.WzDirectory.Name = "Effect.wz";
        var image = new WzImage("Sample.img");
        file.WzDirectory.AddImage(image);

        Assert.AreSame(image, file.WzDirectory["SAMPLE.IMG"]);
        Assert.AreSame(image, file.WzDirectory.GetImageByName("sample.IMG"));

        using var manager = new WzFileManager();
        manager.LoadWzFile(file.Name, file);
        Assert.IsTrue(manager.IsWzFileLoaded("EFFECT.WZ"));
        Assert.AreSame(file.WzDirectory, manager["effect"]);
        Assert.AreSame(file.WzDirectory, manager.GetMainDirectoryByName("Effect.WZ").MainDir);
    }

    [TestMethod]
    public void FullPathAndSpanPathLookup_PreserveHierarchy()
    {
        var root = new WzDirectory("Root");
        var child = new WzDirectory("Child");
        var image = new WzImage("Sample.img");
        var group = new WzSubProperty("Group");
        var value = new WzIntProperty("Value", 7);

        root.AddDirectory(child);
        child.AddImage(image);
        image.AddProperty(group);
        group.AddProperty(value);

        Assert.AreEqual(@"Root\Child\Sample.img\Group\Value", value.FullPath);
        Assert.AreSame(value, image.GetFromPath("/Group//Value/"));
        Assert.IsNull(image.GetFromPath("../Group/Value"));
    }

    [TestMethod]
    public void DirectoryDeepClone_DoesNotMutateSourceAndReparentsChildren()
    {
        var root = new WzDirectory("Root");
        var child = new WzDirectory("Child");
        var image = new WzImage("Sample.img");
        image.AddProperty(new WzIntProperty("Value", 42));
        root.AddDirectory(child);
        child.AddImage(image);

        WzDirectory clone = root.DeepClone();

        Assert.HasCount(1, root.WzDirectories);
        Assert.HasCount(1, clone.WzDirectories);
        Assert.AreNotSame(root.WzDirectories[0], clone.WzDirectories[0]);
        Assert.AreSame(clone, clone.WzDirectories[0].Parent);
        Assert.AreSame(clone.WzDirectories[0], clone.WzDirectories[0].WzImages[0].Parent);

        clone.ClearDirectories();
        Assert.HasCount(1, root.WzDirectories);
        Assert.IsEmpty(clone.WzDirectories);
    }

    [TestMethod]
    public void ListFileRoundTrip_DoesNotMutateInputOrHoldFileHandle()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wz-list-{Guid.NewGuid():N}.wz");
        var entries = new List<string> { "Effect/One.img", "Effect/Two.img" };
        string[] original = entries.ToArray();
        try
        {
            ListFileParser.SaveToDisk(path, WzMapleVersion.BMS, entries);
            CollectionAssert.AreEqual(original, entries);

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            CollectionAssert.AreEqual(original, ListFileParser.ParseListFile(path, WzMapleVersion.BMS));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void ScalarReaders_HandlePercentAndInvalidValuesWithoutExceptions()
    {
        var percent = new WzStringProperty("percent", "10%");
        var invalid = new WzStringProperty("invalid", "not-a-number");

        Assert.AreEqual(10, percent.ReadValue(-1));
        Assert.AreEqual(-1, invalid.ReadValue(-1));
        Assert.AreEqual(123L, invalid.ReadLong(123));
    }

    [TestMethod]
    public void LinkResolver_CopiesCompressedCanvasDataAndRemovesInlink()
    {
        var image = new WzImage("Linked.img");
        var source = new WzCanvasProperty("Source")
        {
            PngProperty = new WzPngProperty()
        };
        source.PngProperty.SetCompressedBytes([0x78, 0x9C, 0x03, 0x00], 1, 1, WzPngFormat.Format2);

        var destination = new WzCanvasProperty("Destination")
        {
            PngProperty = new WzPngProperty()
        };
        destination.PngProperty.SetCompressedBytes([0x78, 0x9C], 1, 1, WzPngFormat.Format2);
        destination.AddProperty(new WzStringProperty(WzCanvasProperty.InlinkPropertyName, "Source"));
        image.AddProperty(source);
        image.AddProperty(destination);

        Assert.IsTrue(WzLinkResolver.ResolveSingleCanvas(destination, inlinkOnly: true));
        Assert.IsFalse(destination.ContainsInlinkProperty());
        CollectionAssert.AreEqual(
            source.PngProperty.GetCompressedBytes(saveInMemory: true),
            destination.PngProperty.GetCompressedBytes(saveInMemory: true));
    }

    [TestMethod]
    public void Dispose_IsIdempotentForNewFileTree()
    {
        var file = new WzFile(95, WzMapleVersion.GMS);
        file.WzDirectory.AddImage(new WzImage("Sample.img"));

        file.Dispose();
        file.Dispose();

        Assert.IsTrue(file.IsUnloaded);
    }
}
