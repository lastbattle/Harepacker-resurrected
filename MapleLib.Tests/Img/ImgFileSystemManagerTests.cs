/*  MapleLib.Tests - Unit tests for MapleLib
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.IO;
using System.Text.Json;
using MapleLib.Img;
using Xunit;

namespace MapleLib.Tests.Img
{
    public class ImgFileSystemManagerTests : IDisposable
    {
        private readonly string _testVersionPath;
        private readonly HaCreatorConfig _config;

        public ImgFileSystemManagerTests()
        {
            // Create a temporary test directory with version structure
            _testVersionPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testVersionPath);

            _config = new HaCreatorConfig
            {
                ImgRootPath = _testVersionPath
            };

            // Create test version structure
            SetupTestVersionStructure();
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testVersionPath))
            {
                try
                {
                    Directory.Delete(_testVersionPath, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        private void SetupTestVersionStructure()
        {
            // Create category directories
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "String"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map", "Map"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map", "Map", "Map0"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Mob"));

            // Create manifest
            CreateTestManifest();
        }

        private void CreateTestManifest()
        {
            var manifest = new
            {
                version = "test",
                displayName = "Test Version",
                extractedDate = DateTime.UtcNow.ToString("o"),
                encryption = "GMS",
                is64Bit = false,
                isPreBB = false,
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 2, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Map"] = new { fileCount = 5, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Mob"] = new { fileCount = 3, lastModified = DateTime.UtcNow.ToString("o") }
                },
                features = new
                {
                    hasPets = true,
                    hasMount = true,
                    hasAndroid = false,
                    hasV5thJob = false
                }
            };

            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_testVersionPath, "manifest.json"), json);
        }

        [Fact]
        public void Constructor_ValidPath_InitializesSuccessfully()
        {
            // Act
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Assert
            Assert.True(manager.IsInitialized);
            Assert.NotNull(manager.VersionInfo);
        }

        [Fact]
        public void Constructor_InvalidPath_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() =>
            {
                using var manager = new ImgFileSystemManager("/nonexistent/path", _config);
                manager.Initialize();
            });
        }

        [Fact]
        public void GetCategories_ReturnsAvailableCategories()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var categories = manager.GetCategories().ToList();

            // Assert
            Assert.Contains("String", categories);
            Assert.Contains("Map", categories);
            Assert.Contains("Mob", categories);
        }

        [Fact]
        public void CategoryExists_ExistingCategory_ReturnsTrue()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.True(manager.CategoryExists("String"));
            Assert.True(manager.CategoryExists("Map"));
        }

        [Fact]
        public void CategoryExists_NonExistingCategory_ReturnsFalse()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.False(manager.CategoryExists("NonExistent"));
        }

        [Fact]
        public void CategoryExists_CaseInsensitive_ReturnsTrue()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.True(manager.CategoryExists("string"));
            Assert.True(manager.CategoryExists("STRING"));
            Assert.True(manager.CategoryExists("String"));
        }

        [Fact]
        public void GetSubdirectories_ReturnsSubdirectories()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var subdirs = manager.GetSubdirectories("Map").ToList();

            // Assert
            Assert.NotEmpty(subdirs);
            Assert.Contains(subdirs, s => s.Contains("Map"));
        }

        [Fact]
        public void GetDirectory_ExistingCategory_ReturnsVirtualDirectory()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var directory = manager.GetDirectory("String");

            // Assert
            Assert.NotNull(directory);
            Assert.IsType<VirtualWzDirectory>(directory);
        }

        [Fact]
        public void GetDirectory_NonExistingCategory_ReturnsNull()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var directory = manager.GetDirectory("NonExistent");

            // Assert
            Assert.Null(directory);
        }

        [Fact]
        public void LoadImage_NonExistingImage_ReturnsNull()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var image = manager.LoadImage("String", "NonExistent.img");

            // Assert
            Assert.Null(image);
        }

        [Fact]
        public void ImageExists_NonExistingImage_ReturnsFalse()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.False(manager.ImageExists("String", "NonExistent.img"));
        }

        [Fact]
        public void GetStats_ReturnsValidStats()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var stats = manager.GetStats();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.CategoryCount >= 3); // At least String, Map, Mob
        }

        [Fact]
        public void ClearCache_DoesNotThrow()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert (should not throw)
            manager.ClearCache();
        }

        [Fact]
        public void TrimCache_DoesNotThrow()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert (should not throw)
            manager.TrimCache(100);
        }

        [Fact]
        public void PreloadCategory_ValidCategory_DoesNotThrow()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert (should not throw)
            manager.PreloadCategory("String");
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert (should not throw)
            manager.Dispose();
            manager.Dispose();
        }
    }
}
