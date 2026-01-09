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
    public class VersionManagerTests : IDisposable
    {
        private readonly string _testRootPath;
        private readonly VersionManager _versionManager;

        public VersionManagerTests()
        {
            // Create a temporary test directory
            _testRootPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testRootPath);
            _versionManager = new VersionManager(_testRootPath);
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testRootPath))
            {
                try
                {
                    Directory.Delete(_testRootPath, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public void Constructor_CreatesRootDirectory()
        {
            // Arrange
            string newPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");

            try
            {
                // Act
                var manager = new VersionManager(newPath);

                // Assert
                Assert.True(Directory.Exists(newPath));
            }
            finally
            {
                if (Directory.Exists(newPath))
                    Directory.Delete(newPath, true);
            }
        }

        [Fact]
        public void ScanVersions_EmptyDirectory_ReturnsEmptyList()
        {
            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Empty(_versionManager.AvailableVersions);
            Assert.Equal(0, _versionManager.VersionCount);
        }

        [Fact]
        public void ScanVersions_WithValidVersion_FindsVersion()
        {
            // Arrange
            string versionPath = Path.Combine(_testRootPath, "v83");
            Directory.CreateDirectory(versionPath);
            CreateTestManifest(versionPath, "v83", "GMS v83");

            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Single(_versionManager.AvailableVersions);
            Assert.Equal("v83", _versionManager.AvailableVersions[0].Version);
            Assert.Equal("GMS v83", _versionManager.AvailableVersions[0].DisplayName);
        }

        [Fact]
        public void ScanVersions_WithMultipleVersions_FindsAll()
        {
            // Arrange
            CreateTestVersion("v55", "Old MapleStory");
            CreateTestVersion("v83", "GMS v83");
            CreateTestVersion("v176", "Modern MS");

            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Equal(3, _versionManager.VersionCount);
        }

        [Fact]
        public void ScanVersions_DirectoryWithoutManifest_CreatesBasicVersion()
        {
            // Arrange
            string versionPath = Path.Combine(_testRootPath, "noManifest");
            Directory.CreateDirectory(versionPath);
            Directory.CreateDirectory(Path.Combine(versionPath, "String"));

            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Single(_versionManager.AvailableVersions);
            Assert.Equal("noManifest", _versionManager.AvailableVersions[0].Version);
        }

        [Fact]
        public void GetVersion_ExistingVersion_ReturnsVersion()
        {
            // Arrange
            CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            // Act
            var version = _versionManager.GetVersion("v83");

            // Assert
            Assert.NotNull(version);
            Assert.Equal("v83", version.Version);
        }

        [Fact]
        public void GetVersion_NonExistingVersion_ReturnsNull()
        {
            // Arrange
            _versionManager.ScanVersions();

            // Act
            var version = _versionManager.GetVersion("nonexistent");

            // Assert
            Assert.Null(version);
        }

        [Fact]
        public void VersionExists_ExistingVersion_ReturnsTrue()
        {
            // Arrange
            CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            // Act & Assert
            Assert.True(_versionManager.VersionExists("v83"));
        }

        [Fact]
        public void VersionExists_NonExistingVersion_ReturnsFalse()
        {
            // Arrange
            _versionManager.ScanVersions();

            // Act & Assert
            Assert.False(_versionManager.VersionExists("nonexistent"));
        }

        [Fact]
        public void DeleteVersion_ExistingVersion_DeletesDirectory()
        {
            // Arrange
            string versionPath = CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            // Act
            bool result = _versionManager.DeleteVersion("v83");

            // Assert
            Assert.True(result);
            Assert.False(Directory.Exists(versionPath));
        }

        [Fact]
        public void DeleteVersion_NonExistingVersion_ReturnsFalse()
        {
            // Arrange
            _versionManager.ScanVersions();

            // Act
            bool result = _versionManager.DeleteVersion("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddExternalVersion_ValidPath_AddsToList()
        {
            // Arrange
            string externalPath = Path.Combine(Path.GetTempPath(), $"External_{Guid.NewGuid():N}");
            Directory.CreateDirectory(externalPath);
            CreateTestManifest(externalPath, "external", "External Version");

            try
            {
                // Act
                var version = _versionManager.AddExternalVersion(externalPath);

                // Assert
                Assert.NotNull(version);
                Assert.True(version.IsExternal);
                Assert.Equal(externalPath, version.DirectoryPath);
            }
            finally
            {
                if (Directory.Exists(externalPath))
                    Directory.Delete(externalPath, true);
            }
        }

        [Fact]
        public void Refresh_UpdatesVersionList()
        {
            // Arrange
            _versionManager.ScanVersions();
            Assert.Empty(_versionManager.AvailableVersions);

            // Add a version after initial scan
            CreateTestVersion("v83", "GMS v83");

            // Act
            _versionManager.Refresh();

            // Assert
            Assert.Single(_versionManager.AvailableVersions);
        }

        #region Helper Methods

        private string CreateTestVersion(string versionName, string displayName)
        {
            string versionPath = Path.Combine(_testRootPath, versionName);
            Directory.CreateDirectory(versionPath);
            CreateTestManifest(versionPath, versionName, displayName);
            return versionPath;
        }

        private void CreateTestManifest(string versionPath, string version, string displayName)
        {
            var manifest = new
            {
                version = version,
                displayName = displayName,
                extractedDate = DateTime.UtcNow.ToString("o"),
                encryption = "GMS",
                is64Bit = false,
                isPreBB = false,
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 8, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Map"] = new { fileCount = 100, lastModified = DateTime.UtcNow.ToString("o") }
                },
                features = new { }
            };

            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(versionPath, "manifest.json"), json);
        }

        #endregion
    }
}
