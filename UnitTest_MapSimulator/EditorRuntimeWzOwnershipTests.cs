using System.IO;
using HaCreator.MapEditor.Simulation;
using HaCreator.MapSimulator.Contracts;
using HaCreator.Wz;
using MapleLib;
using MapleLib.Img;
using MapleLib.WzLib;

namespace UnitTest_MapSimulator;

public sealed class EditorRuntimeWzOwnershipTests
{
    [Fact]
    public void PreviewReopensWzSourceAndLeavesEditorManagerAlive()
    {
        string editorPath = CreateDirectory();
        WzFileManager previousGlobal = WzFileManager.fileManager;
        WzFileManager editorManager = new(editorPath, bIsStandAloneWzFile: false);
        WzFile editorFile = CreateImageFile("Map", "100000000.img");
        editorManager.LoadWzFile("Map", editorFile);
        WzFileDataSource editorSource = new(editorManager, ownsManager: false);
        EditorRuntimeAssets preview = null;
        try
        {
            preview = new EditorRuntimeAssets(
                editorSource,
                editorManager,
                new WzInformationManager(),
                Array.Empty<RuntimeMapDefinition>());

            Assert.Same(editorManager, WzFileManager.fileManager);
            preview.Dispose();
            preview = null;

            Assert.False(editorFile.IsUnloaded);
            Assert.Single(editorManager.WzFileList);
            Assert.Same(editorManager, WzFileManager.fileManager);
        }
        finally
        {
            preview?.Dispose();
            editorSource.Dispose();
            editorManager.Dispose();
            WzFileManager.fileManager = previousGlobal;
            DeleteDirectory(editorPath);
        }
    }

    private static WzFile CreateImageFile(string name, string imageName)
    {
        WzFile file = new(0, WzMapleVersion.BMS) { Name = name };
        file.WzDirectory.AddImage(new WzImage(imageName));
        return file;
    }

    private static string CreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "HarepackerEditorWz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
