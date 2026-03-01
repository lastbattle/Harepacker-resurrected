using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaCreator.Wz
{
    /// <summary>
    /// Manages the startup flow for HaCreator, determining the best data source mode
    /// and providing access to available versions.
    /// </summary>
    public class StartupManager
    {
        private readonly HaCreatorConfig _config;
        private readonly VersionManager _versionManager;
        private IDataSource _dataSource;

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        public HaCreatorConfig Config => _config;

        /// <summary>
        /// Gets the version manager for IMG filesystem versions
        /// </summary>
        public VersionManager VersionManager => _versionManager;

        /// <summary>
        /// Gets the active data source
        /// </summary>
        public IDataSource DataSource => _dataSource;

        /// <summary>
        /// Gets whether any IMG versions are available
        /// </summary>
        public bool HasAvailableVersions => _versionManager.VersionCount > 0;

        /// <summary>
        /// Gets the list of available versions
        /// </summary>
        public IReadOnlyList<VersionInfo> AvailableVersions => _versionManager.AvailableVersions;

        /// <summary>
        /// Gets the configured data source mode
        /// </summary>
        public DataSourceMode ConfiguredMode => _config.DataSourceMode;

        /// <summary>
        /// Gets the recommended startup mode based on available data and configuration
        /// </summary>
        public StartupMode RecommendedMode
        {
            get
            {
                // Respect user configuration if it's explicitly set and valid
                switch (_config.DataSourceMode)
                {
                    case DataSourceMode.WzFiles:
                        if (!string.IsNullOrEmpty(_config.Legacy.WzFilePath) &&
                            Directory.Exists(_config.Legacy.WzFilePath))
                        {
                            return StartupMode.WzFiles;
                        }
                        break;

                    case DataSourceMode.Hybrid:
                        // Hybrid mode requires at least IMG versions or WZ path
                        if (HasAvailableVersions ||
                            (!string.IsNullOrEmpty(_config.Legacy.WzFilePath) &&
                             Directory.Exists(_config.Legacy.WzFilePath)))
                        {
                            return StartupMode.Hybrid;
                        }
                        break;

                    case DataSourceMode.ImgFileSystem:
                    default:
                        if (HasAvailableVersions)
                        {
                            return StartupMode.ImgFileSystem;
                        }
                        break;
                }

                // Fallback: prefer IMG if available, otherwise WZ
                if (HasAvailableVersions)
                {
                    return StartupMode.ImgFileSystem;
                }

                return StartupMode.WzFiles;
            }
        }

        /// <summary>
        /// Creates a new StartupManager
        /// </summary>
        public StartupManager()
        {
            _config = HaCreatorConfig.Load();
            _config.EnsureDirectoriesExist();

            _versionManager = new VersionManager(_config.VersionsPath);

            // Enable hot swap if configured
            EnableHotSwapIfConfigured();
        }

        /// <summary>
        /// Enables hot swap for version manager if configured
        /// </summary>
        private void EnableHotSwapIfConfigured()
        {
            if (_config.HotSwap?.Enabled == true && _config.HotSwap.WatchVersions)
            {
                _versionManager.EnableHotSwap(
                    true,
                    _config.HotSwap.DebounceMs,
                    _config.AdditionalVersionPaths);
            }
        }

        /// <summary>
        /// Scans for available versions
        /// </summary>
        public void ScanVersions()
        {
            _versionManager.ScanVersions();
        }

        /// <summary>
        /// Gets the last used version if still available
        /// </summary>
        public VersionInfo GetLastUsedVersion()
        {
            if (string.IsNullOrEmpty(_config.LastUsedVersion))
                return null;

            return _versionManager.GetVersion(_config.LastUsedVersion);
        }

        /// <summary>
        /// Creates and initializes a data source for the specified version
        /// </summary>
        public IDataSource CreateDataSource(VersionInfo version)
        {
            _dataSource?.Dispose();

            var imgDataSource = new ImgFileSystemDataSource(version.DirectoryPath, _config);
            _dataSource = imgDataSource;

            // Enable hot swap for categories if configured
            EnableHotSwapForDataSource(imgDataSource);

            _config.LastUsedVersion = version.Version;
            _config.Save();

            return _dataSource;
        }

        /// <summary>
        /// Enables hot swap for an ImgFileSystemDataSource if configured
        /// </summary>
        private void EnableHotSwapForDataSource(ImgFileSystemDataSource dataSource)
        {
            if (_config.HotSwap?.Enabled == true && _config.HotSwap.WatchCategories)
            {
                dataSource.EnableHotSwap(true, _config.HotSwap.DebounceMs);
            }
        }

        /// <summary>
        /// Creates and initializes a data source for WZ files
        /// </summary>
        public IDataSource CreateWzDataSource(string wzPath, WzMapleVersion mapleVersion)
        {
            _dataSource?.Dispose();

            _config.Legacy.WzFilePath = wzPath;
            _config.Save();

            var wzDataSource = new WzFileDataSource(wzPath, _config);
            wzDataSource.Initialize();
            _dataSource = wzDataSource;

            return _dataSource;
        }

        /// <summary>
        /// Creates a data source based on the configured mode using DataSourceFactory
        /// </summary>
        /// <param name="versionOrPath">For IMG mode: version directory path. For WZ mode: MapleStory path. For Hybrid: IMG version path.</param>
        /// <returns>The created data source</returns>
        public IDataSource CreateDataSourceFromConfig(string versionOrPath)
        {
            _dataSource?.Dispose();

            _dataSource = DataSourceFactory.Create(_config.DataSourceMode, versionOrPath, _config);

            // Update last used version if applicable
            if (_config.DataSourceMode == DataSourceMode.ImgFileSystem)
            {
                string versionName = Path.GetFileName(versionOrPath);
                _config.LastUsedVersion = versionName;
            }
            else if (_config.DataSourceMode == DataSourceMode.WzFiles)
            {
                _config.Legacy.WzFilePath = versionOrPath;
            }

            _config.Save();
            return _dataSource;
        }

        /// <summary>
        /// Creates a hybrid data source that uses IMG filesystem with WZ fallback
        /// </summary>
        /// <param name="imgVersionPath">Path to the IMG version directory</param>
        /// <param name="wzPath">Optional WZ files path for fallback</param>
        /// <returns>The hybrid data source</returns>
        public IDataSource CreateHybridDataSource(string imgVersionPath, string wzPath = null)
        {
            _dataSource?.Dispose();

            // Update config if WZ path provided
            if (!string.IsNullOrEmpty(wzPath))
            {
                _config.Legacy.WzFilePath = wzPath;
            }

            _dataSource = new HybridDataSource(imgVersionPath, _config);

            // Update last used version
            string versionName = Path.GetFileName(imgVersionPath);
            _config.LastUsedVersion = versionName;
            _config.Save();

            return _dataSource;
        }

        /// <summary>
        /// Updates the data source mode in configuration
        /// </summary>
        public void SetDataSourceMode(DataSourceMode mode)
        {
            _config.DataSourceMode = mode;
            _config.Save();
        }

        /// <summary>
        /// Disposes the current data source
        /// </summary>
        public void DisposeDataSource()
        {
            _dataSource?.Dispose();
            _dataSource = null;
        }

        /// <summary>
        /// Saves the current configuration
        /// </summary>
        public void SaveConfig()
        {
            _config.Save();
        }

        /// <summary>
        /// Reloads configuration from disk
        /// </summary>
        public void ReloadConfig()
        {
            var freshConfig = HaCreatorConfig.Load();
            _config.DataSourceMode = freshConfig.DataSourceMode;
            _config.ImgRootPath = freshConfig.ImgRootPath;
            _config.Legacy.WzFilePath = freshConfig.Legacy.WzFilePath;
            _config.Legacy.AllowWzFallback = freshConfig.Legacy.AllowWzFallback;
            _config.Legacy.AutoConvertOnLoad = freshConfig.Legacy.AutoConvertOnLoad;
            _config.Cache.MaxMemoryCacheMB = freshConfig.Cache.MaxMemoryCacheMB;
            _config.Cache.MaxCachedImages = freshConfig.Cache.MaxCachedImages;
            _config.Cache.EnableMemoryMappedFiles = freshConfig.Cache.EnableMemoryMappedFiles;
        }
    }

    /// <summary>
    /// Startup mode options
    /// </summary>
    public enum StartupMode
    {
        /// <summary>
        /// Load from IMG filesystem (new default)
        /// </summary>
        ImgFileSystem,

        /// <summary>
        /// Load from WZ files (legacy)
        /// </summary>
        WzFiles,

        /// <summary>
        /// Hybrid mode - IMG filesystem with WZ fallback
        /// </summary>
        Hybrid
    }
}
