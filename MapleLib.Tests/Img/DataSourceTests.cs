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
    public class DataSourceFactoryTests
    {
        [Fact]
        public void Create_ImgFileSystemMode_ReturnsImgFileSystemDataSource()
        {
            // Arrange
            string testPath = CreateTestVersionDirectory();

            try
            {
                // Act
                using var dataSource = DataSourceFactory.Create(
                    DataSourceMode.ImgFileSystem,
                    testPath,
                    new HaCreatorConfig());

                // Assert
                Assert.IsType<ImgFileSystemDataSource>(dataSource);
            }
            finally
            {
                CleanupTestDirectory(testPath);
            }
        }

        [Fact]
        public void Create_InvalidMode_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                DataSourceFactory.Create((DataSourceMode)999, "/path", new HaCreatorConfig());
            });
        }

        [Fact]
        public void Create_WithNullConfig_UsesDefaultConfig()
        {
            // Arrange
            string testPath = CreateTestVersionDirectory();

            try
            {
                // Act - should not throw
                using var dataSource = DataSourceFactory.Create(
                    DataSourceMode.ImgFileSystem,
                    testPath,
                    null);

                // Assert
                Assert.NotNull(dataSource);
            }
            finally
            {
                CleanupTestDirectory(testPath);
            }
        }

        #region Helper Methods

        private string CreateTestVersionDirectory()
        {
            string testPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testPath);
            Directory.CreateDirectory(Path.Combine(testPath, "String"));

            // Create manifest
            var manifest = new
            {
                version = "test",
                displayName = "Test Version",
                extractedDate = DateTime.UtcNow.ToString("o"),
                encryption = "GMS",
                is64Bit = false,
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("o") }
                }
            };

            string json = JsonSerializer.Serialize(manifest);
            File.WriteAllText(Path.Combine(testPath, "manifest.json"), json);

            return testPath;
        }

        private void CleanupTestDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch { }
            }
        }

        #endregion
    }

    public class ImgFileSystemDataSourceTests : IDisposable
    {
        private readonly string _testPath;
        private readonly HaCreatorConfig _config;

        public ImgFileSystemDataSourceTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testPath);
            Directory.CreateDirectory(Path.Combine(_testPath, "String"));
            Directory.CreateDirectory(Path.Combine(_testPath, "Map"));
            Directory.CreateDirectory(Path.Combine(_testPath, "Mob"));

            // Create mock .img files (required for category detection)
            CreateMockImgFile(Path.Combine(_testPath, "String", "Test.img"));
            CreateMockImgFile(Path.Combine(_testPath, "Map", "Test.img"));
            CreateMockImgFile(Path.Combine(_testPath, "Mob", "Test.img"));

            CreateTestManifest();

            _config = new HaCreatorConfig { ImgRootPath = _testPath };
        }

        private void CreateMockImgFile(string path)
        {
            File.WriteAllBytes(path, Array.Empty<byte>());
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                try
                {
                    Directory.Delete(_testPath, true);
                }
                catch { }
            }
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
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Map"] = new { fileCount = 10, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Mob"] = new { fileCount = 5, lastModified = DateTime.UtcNow.ToString("o") }
                }
            };

            string json = JsonSerializer.Serialize(manifest);
            File.WriteAllText(Path.Combine(_testPath, "manifest.json"), json);
        }

        [Fact]
        public void Constructor_ValidPath_InitializesSuccessfully()
        {
            // Act
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Assert
            Assert.True(dataSource.IsInitialized);
            Assert.NotNull(dataSource.Name);
        }

        [Fact]
        public void Name_ReturnsDisplayName()
        {
            // Act
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Assert
            Assert.Equal("Test Version", dataSource.Name);
        }

        [Fact]
        public void VersionInfo_ReturnsVersionInfo()
        {
            // Act
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Assert
            Assert.NotNull(dataSource.VersionInfo);
            Assert.Equal("test", dataSource.VersionInfo.Version);
        }

        [Fact]
        public void GetCategories_ReturnsCategories()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act
            var categories = dataSource.GetCategories().ToList();

            // Assert - categories are stored in lowercase
            Assert.Contains(categories, c => c.Equals("string", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(categories, c => c.Equals("map", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(categories, c => c.Equals("mob", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CategoryExists_ExistingCategory_ReturnsTrue()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act & Assert
            Assert.True(dataSource.CategoryExists("String"));
        }

        [Fact]
        public void CategoryExists_NonExistingCategory_ReturnsFalse()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act & Assert
            Assert.False(dataSource.CategoryExists("NonExistent"));
        }

        [Fact]
        public void GetImage_NonExistingImage_ReturnsNull()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act
            var image = dataSource.GetImage("String", "NonExistent.img");

            // Assert
            Assert.Null(image);
        }

        [Fact]
        public void ImageExists_NonExistingImage_ReturnsFalse()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act & Assert
            Assert.False(dataSource.ImageExists("String", "NonExistent.img"));
        }

        [Fact]
        public void GetDirectory_ExistingCategory_ReturnsDirectory()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act
            var directory = dataSource.GetDirectory("String");

            // Assert
            Assert.NotNull(directory);
        }

        [Fact]
        public void GetDirectory_NonExistingCategory_ReturnsNull()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act
            var directory = dataSource.GetDirectory("NonExistent");

            // Assert
            Assert.Null(directory);
        }

        [Fact]
        public void GetStats_ReturnsStats()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act
            var stats = dataSource.GetStats();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.CategoryCount >= 3);
        }

        [Fact]
        public void ClearCache_DoesNotThrow()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act & Assert
            dataSource.ClearCache();
        }

        [Fact]
        public void PreloadCategory_ValidCategory_DoesNotThrow()
        {
            // Arrange
            using var dataSource = new ImgFileSystemDataSource(_testPath, _config);

            // Act & Assert
            dataSource.PreloadCategory("String");
        }
    }

    public class HybridDataSourceTests : IDisposable
    {
        private readonly string _testPath;
        private readonly HaCreatorConfig _config;

        public HybridDataSourceTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testPath);
            Directory.CreateDirectory(Path.Combine(_testPath, "String"));

            // Create mock .img file (required for category detection)
            File.WriteAllBytes(Path.Combine(_testPath, "String", "Test.img"), Array.Empty<byte>());

            CreateTestManifest();

            _config = new HaCreatorConfig { ImgRootPath = _testPath };
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                try
                {
                    Directory.Delete(_testPath, true);
                }
                catch { }
            }
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
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("o") }
                }
            };

            string json = JsonSerializer.Serialize(manifest);
            File.WriteAllText(Path.Combine(_testPath, "manifest.json"), json);
        }

        [Fact]
        public void Constructor_WithImgPath_InitializesImgSource()
        {
            // Act
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Assert
            Assert.True(dataSource.IsInitialized);
        }

        [Fact]
        public void Name_ReturnsName()
        {
            // Act
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Assert
            Assert.NotNull(dataSource.Name);
        }

        [Fact]
        public void GetCategories_ReturnsCategories()
        {
            // Arrange
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Act
            var categories = dataSource.GetCategories().ToList();

            // Assert - categories are stored in lowercase
            Assert.Contains(categories, c => c.Equals("string", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CategoryExists_ExistingCategory_ReturnsTrue()
        {
            // Arrange
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Act & Assert
            Assert.True(dataSource.CategoryExists("String"));
        }

        [Fact]
        public void GetStats_ReturnsStats()
        {
            // Arrange
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Act
            var stats = dataSource.GetStats();

            // Assert
            Assert.NotNull(stats);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var dataSource = new HybridDataSource(_testPath, _config);

            // Act & Assert
            dataSource.Dispose();
            dataSource.Dispose();
        }
    }
}
