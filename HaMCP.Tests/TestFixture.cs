using System.Drawing;
using System.Drawing.Imaging;
using HaMCP.Server;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tests;

/// <summary>
/// Shared test fixture that creates a minimal IMG filesystem for testing
/// </summary>
public class TestFixture : IDisposable
{
    public string TestDataPath { get; }
    public WzSessionManager Session { get; }

    public TestFixture()
    {
        // Create temp directory for test data
        TestDataPath = Path.Combine(Path.GetTempPath(), $"HaMCP_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDataPath);

        // Create a minimal IMG filesystem structure
        CreateTestData();

        // Initialize session
        Session = new WzSessionManager();
    }

    private void CreateTestData()
    {
        // Create manifest.json
        var manifestPath = Path.Combine(TestDataPath, "manifest.json");
        File.WriteAllText(manifestPath, @"{
            ""version"": ""999"",
            ""displayName"": ""Test Data v999"",
            ""sourceRegion"": ""Test"",
            ""isPreBB"": false,
            ""is64Bit"": false
        }");

        // Create Character category
        var characterPath = Path.Combine(TestDataPath, "Character");
        Directory.CreateDirectory(characterPath);

        // Create a test .img file using MapleLib
        CreateTestImage(characterPath, "Test.img");

        // Create Map category
        var mapPath = Path.Combine(TestDataPath, "Map");
        Directory.CreateDirectory(mapPath);

        // Create Map subcategory
        var map0Path = Path.Combine(mapPath, "Map0");
        Directory.CreateDirectory(map0Path);
        CreateTestImage(map0Path, "000000000.img");

        // Create Sound category
        var soundPath = Path.Combine(TestDataPath, "Sound");
        Directory.CreateDirectory(soundPath);
        CreateTestSoundImage(soundPath, "TestSound.img");
    }

    private void CreateTestImage(string directory, string fileName)
    {
        var img = new WzImage(fileName);
        img.Changed = true;

        // Add some test properties
        var stringProp = new WzStringProperty("testString", "Hello World");
        img.AddProperty(stringProp);

        var intProp = new WzIntProperty("testInt", 42);
        img.AddProperty(intProp);

        var floatProp = new WzFloatProperty("testFloat", 3.14f);
        img.AddProperty(floatProp);

        var vectorProp = new WzVectorProperty("testVector",
            new WzIntProperty("X", 100),
            new WzIntProperty("Y", 200));
        img.AddProperty(vectorProp);

        // Add a sub property with children
        var subProp = new WzSubProperty("info");
        subProp.AddProperty(new WzStringProperty("name", "Test Item"));
        subProp.AddProperty(new WzIntProperty("id", 12345));
        img.AddProperty(subProp);

        // Add a canvas property with a simple test image
        var canvas = new WzCanvasProperty("testCanvas");
        canvas.PngProperty = new WzPngProperty();
        using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Red);
            }
            canvas.PngProperty.PNG = bmp;
        }

        // Add origin to canvas
        var origin = new WzVectorProperty("origin",
            new WzIntProperty("X", 16),
            new WzIntProperty("Y", 16));
        canvas.AddProperty(origin);

        var delay = new WzIntProperty("delay", 100);
        canvas.AddProperty(delay);

        img.AddProperty(canvas);

        // Add animation frames
        var animation = new WzSubProperty("stand");
        for (int i = 0; i < 3; i++)
        {
            var frame = new WzCanvasProperty(i.ToString());
            frame.PngProperty = new WzPngProperty();
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(255, i * 80, i * 80, i * 80));
                }
                frame.PngProperty.PNG = bmp;
            }
            frame.AddProperty(new WzVectorProperty("origin",
                new WzIntProperty("X", 16),
                new WzIntProperty("Y", 16)));
            frame.AddProperty(new WzIntProperty("delay", 100));
            animation.AddProperty(frame);
        }
        img.AddProperty(animation);

        // Add a UOL property
        var uol = new WzUOLProperty("testUol", "info/name");
        img.AddProperty(uol);

        // Save the image
        var filePath = Path.Combine(directory, fileName);
        using (var fs = File.Create(filePath))
        using (var writer = new WzBinaryWriter(fs, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS)))
        {
            img.SaveImage(writer, true);
        }
    }

    private void CreateTestSoundImage(string directory, string fileName)
    {
        var img = new WzImage(fileName);
        img.Changed = true;

        // Add test string property
        var stringProp = new WzStringProperty("description", "Test Sound File");
        img.AddProperty(stringProp);

        // Note: Creating WzBinaryProperty for sound requires actual MP3 data
        // For testing, we'll just add metadata properties
        var soundInfo = new WzSubProperty("soundInfo");
        soundInfo.AddProperty(new WzIntProperty("length", 1000));
        soundInfo.AddProperty(new WzIntProperty("frequency", 44100));
        img.AddProperty(soundInfo);

        // Save the image
        var filePath = Path.Combine(directory, fileName);
        using (var fs = File.Create(filePath))
        using (var writer = new WzBinaryWriter(fs, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS)))
        {
            img.SaveImage(writer, true);
        }
    }

    public void InitializeDataSource()
    {
        Session.InitDataSource(TestDataPath);
    }

    public void Dispose()
    {
        Session.Dispose();

        // Clean up test data
        try
        {
            if (Directory.Exists(TestDataPath))
            {
                Directory.Delete(TestDataPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
