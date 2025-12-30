/*  MapleLib.Tests - Unit tests for MapleLib
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.IO;
using MapleLib.Img;
using Xunit;

namespace MapleLib.Tests.Img
{
    public class HaCreatorConfigTests : IDisposable
    {
        private readonly string _testConfigPath;

        public HaCreatorConfigTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}", "config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_testConfigPath)!);
        }

        public void Dispose()
        {
            string? dir = Path.GetDirectoryName(_testConfigPath);
            if (dir != null && Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch { }
            }
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Act
            var config = new HaCreatorConfig();

            // Assert
            Assert.Equal(DataSourceMode.ImgFileSystem, config.DataSourceMode);
            Assert.NotNull(config.Cache);
            Assert.NotNull(config.Legacy);
            Assert.NotNull(config.Extraction);
            Assert.NotNull(config.AdditionalVersionPaths);
        }

        [Fact]
        public void CacheConfig_HasDefaultValues()
        {
            // Act
            var config = new HaCreatorConfig();

            // Assert
            Assert.Equal(512, config.Cache.MaxMemoryCacheMB);
            Assert.Equal(1000, config.Cache.MaxCachedImages);
            Assert.True(config.Cache.EnableMemoryMappedFiles);
            Assert.NotNull(config.Cache.PreloadCategories);
        }

        [Fact]
        public void LegacyConfig_HasDefaultValues()
        {
            // Act
            var config = new HaCreatorConfig();

            // Assert
            Assert.Null(config.Legacy.WzFilePath);
            Assert.False(config.Legacy.AllowWzFallback);
            Assert.False(config.Legacy.AutoConvertOnLoad);
        }

        [Fact]
        public void ExtractionConfig_HasDefaultValues()
        {
            // Act
            var config = new HaCreatorConfig();

            // Assert
            Assert.Equal(4, config.Extraction.ParallelThreads);
            Assert.True(config.Extraction.GenerateIndex);
            Assert.True(config.Extraction.ValidateAfterExtract);
        }

        [Fact]
        public void Save_CreatesFile()
        {
            // Arrange
            var config = new HaCreatorConfig();
            config.DataSourceMode = DataSourceMode.WzFiles;

            // Act
            config.Save(_testConfigPath);

            // Assert
            Assert.True(File.Exists(_testConfigPath));
        }

        [Fact]
        public void Load_ExistingFile_ReturnsConfig()
        {
            // Arrange
            var originalConfig = new HaCreatorConfig();
            originalConfig.DataSourceMode = DataSourceMode.Hybrid;
            originalConfig.LastUsedVersion = "v83";
            originalConfig.Save(_testConfigPath);

            // Act
            var loadedConfig = HaCreatorConfig.Load(_testConfigPath);

            // Assert
            Assert.Equal(DataSourceMode.Hybrid, loadedConfig.DataSourceMode);
            Assert.Equal("v83", loadedConfig.LastUsedVersion);
        }

        [Fact]
        public void Load_NonExistingFile_ReturnsDefaultConfig()
        {
            // Act
            var config = HaCreatorConfig.Load("/nonexistent/path/config.json");

            // Assert
            Assert.NotNull(config);
            Assert.Equal(DataSourceMode.ImgFileSystem, config.DataSourceMode);
        }

        [Fact]
        public void EnsureDirectoriesExist_CreatesDirectories()
        {
            // Arrange
            string testRoot = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            var config = new HaCreatorConfig
            {
                ImgRootPath = testRoot
            };

            try
            {
                // Act
                config.EnsureDirectoriesExist();

                // Assert
                Assert.True(Directory.Exists(testRoot));
                Assert.True(Directory.Exists(config.VersionsPath));
                Assert.True(Directory.Exists(config.CustomPath));
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, true);
            }
        }

        [Fact]
        public void VersionsPath_ReturnsCorrectPath()
        {
            // Arrange
            var config = new HaCreatorConfig
            {
                ImgRootPath = @"C:\Test\Data"
            };

            // Act
            string versionsPath = config.VersionsPath;

            // Assert
            Assert.Equal(Path.Combine(@"C:\Test\Data", "versions"), versionsPath);
        }

        [Fact]
        public void CustomPath_ReturnsCorrectPath()
        {
            // Arrange
            var config = new HaCreatorConfig
            {
                ImgRootPath = @"C:\Test\Data"
            };

            // Act
            string customPath = config.CustomPath;

            // Assert
            Assert.Equal(Path.Combine(@"C:\Test\Data", "custom"), customPath);
        }

        [Fact]
        public void AdditionalVersionPaths_CanBeModified()
        {
            // Arrange
            var config = new HaCreatorConfig();

            // Act
            config.AdditionalVersionPaths.Add(@"C:\External\Version1");
            config.AdditionalVersionPaths.Add(@"D:\Another\Version2");

            // Assert
            Assert.Equal(2, config.AdditionalVersionPaths.Count);
            Assert.Contains(@"C:\External\Version1", config.AdditionalVersionPaths);
        }

        [Fact]
        public void SaveAndLoad_PreservesAllSettings()
        {
            // Arrange
            var config = new HaCreatorConfig
            {
                DataSourceMode = DataSourceMode.Hybrid,
                LastUsedVersion = "gms_v230",
                ImgRootPath = @"C:\CustomPath"
            };
            config.Cache.MaxMemoryCacheMB = 1024;
            config.Cache.MaxCachedImages = 2000;
            config.Legacy.WzFilePath = @"D:\MapleStory";
            config.Legacy.AllowWzFallback = true;
            config.Extraction.ParallelThreads = 8;
            config.AdditionalVersionPaths.Add(@"E:\External");

            // Act
            config.Save(_testConfigPath);
            var loaded = HaCreatorConfig.Load(_testConfigPath);

            // Assert
            Assert.Equal(DataSourceMode.Hybrid, loaded.DataSourceMode);
            Assert.Equal("gms_v230", loaded.LastUsedVersion);
            Assert.Equal(1024, loaded.Cache.MaxMemoryCacheMB);
            Assert.Equal(2000, loaded.Cache.MaxCachedImages);
            Assert.Equal(@"D:\MapleStory", loaded.Legacy.WzFilePath);
            Assert.True(loaded.Legacy.AllowWzFallback);
            Assert.Equal(8, loaded.Extraction.ParallelThreads);
            Assert.Contains(@"E:\External", loaded.AdditionalVersionPaths);
        }
    }
}
