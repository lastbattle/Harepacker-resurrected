using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MapleLib.Img;
using MapleLib.WzLib;

namespace HaRepacker.GUI.HotSwap
{
    /// <summary>
    /// Manages hot-swap functionality for HaRepacker.
    /// Coordinates between file watching, notifications, and UI updates.
    /// </summary>
    public class HotSwapManager : IDisposable
    {
        #region Fields
        private readonly MainForm _mainForm;
        private readonly HotSwapNotificationBar _notificationBar;
        private ImgDirectoryWatcherService _watcherService;
        private readonly ConcurrentDictionary<string, WzNode> _watchedNodes = new();
        private readonly ConcurrentDictionary<string, bool> _unsavedChanges = new();
        private bool _disposed;
        private bool _isEnabled;
        #endregion

        #region Properties
        /// <summary>
        /// Whether hot-swap is currently enabled
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// The notification bar control
        /// </summary>
        public HotSwapNotificationBar NotificationBar => _notificationBar;
        #endregion

        #region Events
        /// <summary>
        /// Raised when an IMG file is reloaded
        /// </summary>
        public event EventHandler<ImgFileReloadedEventArgs> ImgFileReloaded;

        /// <summary>
        /// Raised when a new IMG file is added
        /// </summary>
        public event EventHandler<ImgFileAddedEventArgs> ImgFileAddedToTree;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new HotSwapManager
        /// </summary>
        /// <param name="mainForm">The main form</param>
        public HotSwapManager(MainForm mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));

            // Create notification bar
            _notificationBar = new HotSwapNotificationBar();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initializes and enables hot-swap functionality
        /// </summary>
        public void Enable()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HotSwapManager));

            if (_isEnabled)
                return;

            if (!HotSwapConstants.EnableImgFileWatching)
                return;

            _watcherService = new ImgDirectoryWatcherService(
                HotSwapConstants.DebounceMs,
                HotSwapConstants.TrackContentHash);

            _watcherService.ImgFileModified += OnImgFileModified;
            _watcherService.ImgFileAdded += OnImgFileAdded;
            _watcherService.ImgFileDeleted += OnImgFileDeleted;
            _watcherService.ImgFileRenamed += OnImgFileRenamed;
            _watcherService.WatcherError += OnWatcherError;

            _isEnabled = true;
        }

        /// <summary>
        /// Disables hot-swap functionality
        /// </summary>
        public void Disable()
        {
            if (!_isEnabled)
                return;

            _watcherService?.Dispose();
            _watcherService = null;
            _watchedNodes.Clear();
            _notificationBar.ClearAll();
            _isEnabled = false;
        }

        /// <summary>
        /// Starts watching a directory that has been loaded into the tree
        /// </summary>
        /// <param name="directoryPath">The directory path</param>
        /// <param name="node">The tree node representing this directory</param>
        public void WatchDirectory(string directoryPath, WzNode node)
        {
            if (!_isEnabled || _watcherService == null)
                return;

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                return;

            _watcherService.WatchDirectory(directoryPath);
            _watchedNodes[directoryPath] = node;
        }

        /// <summary>
        /// Stops watching a directory when it's unloaded
        /// </summary>
        /// <param name="directoryPath">The directory path</param>
        public void UnwatchDirectory(string directoryPath)
        {
            if (!_isEnabled || _watcherService == null)
                return;

            _watcherService.UnwatchDirectory(directoryPath);
            _watchedNodes.TryRemove(directoryPath, out _);
        }

        /// <summary>
        /// Records that a file is currently open
        /// </summary>
        /// <param name="filePath">The file path</param>
        public void RecordFileOpen(string filePath)
        {
            _watcherService?.RecordFileState(filePath);
        }

        /// <summary>
        /// Records that a file has been saved (to avoid self-triggered events)
        /// </summary>
        /// <param name="filePath">The file path</param>
        public void BeginSaveOperation(string filePath)
        {
            _watcherService?.IgnorePath(filePath);
        }

        /// <summary>
        /// Completes a save operation and resumes watching
        /// </summary>
        /// <param name="filePath">The file path</param>
        public void EndSaveOperation(string filePath)
        {
            if (_watcherService != null)
            {
                // Delay unignore to ensure watcher doesn't catch our change
                _ = _watcherService.UnignorePathDelayed(filePath, 500);

                // Update recorded state
                _watcherService.RecordFileState(filePath);
            }

            // Clear unsaved changes flag
            _unsavedChanges.TryRemove(filePath, out _);
        }

        /// <summary>
        /// Temporarily ignores all file changes in a directory during a save operation
        /// </summary>
        /// <param name="directoryPath">The directory path to ignore</param>
        public void BeginDirectorySaveOperation(string directoryPath)
        {
            _watcherService?.IgnoreDirectory(directoryPath);
        }

        /// <summary>
        /// Resumes watching a directory after save operation completes
        /// </summary>
        /// <param name="directoryPath">The directory path</param>
        public void EndDirectorySaveOperation(string directoryPath)
        {
            if (_watcherService != null)
            {
                _ = _watcherService.UnignoreDirectoryDelayed(directoryPath, 500);
            }
        }

        /// <summary>
        /// Marks a file as having unsaved changes
        /// </summary>
        /// <param name="filePath">The file path</param>
        public void MarkFileAsModified(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                _unsavedChanges[filePath] = true;
            }
        }

        /// <summary>
        /// Checks if a file has unsaved changes
        /// </summary>
        public bool HasUnsavedChanges(string filePath)
        {
            return _unsavedChanges.TryGetValue(filePath, out var hasChanges) && hasChanges;
        }

        /// <summary>
        /// Checks if a file has been externally modified
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <returns>The type of change detected, or None if unchanged</returns>
        public ImgChangeType CheckForExternalChanges(string filePath)
        {
            return _watcherService?.GetChangeType(filePath) ?? ImgChangeType.None;
        }
        #endregion

        #region Event Handlers
        private void OnImgFileModified(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled)
                return;

            // Auto-reload and show brief notification
            _mainForm.Invoke(new Action(() =>
            {
                ReloadFile(e.FilePath);
                ShowNotification(e.FilePath, e.ChangeType);
            }));
        }

        private void OnImgFileAdded(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled)
                return;

            // Auto-add and show brief notification
            _mainForm.Invoke(new Action(() =>
            {
                AddFileToTree(e.FilePath);
                ShowNotification(e.FilePath, ImgChangeType.Added);
            }));
        }

        private void OnImgFileDeleted(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled)
                return;

            // Auto-remove and show brief notification
            _mainForm.Invoke(new Action(() =>
            {
                RemoveFileFromTree(e.FilePath);
                ShowNotification(e.FilePath, ImgChangeType.Deleted);
            }));
        }

        private void OnImgFileRenamed(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled)
                return;

            // Auto-handle rename and show brief notification
            _mainForm.Invoke(new Action(() =>
            {
                HandleRename(e.OldPath, e.FilePath);
                ShowNotification(e.FilePath, ImgChangeType.Renamed, e.OldPath);
            }));
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"FileSystemWatcher error: {e.GetException()?.Message}");
        }

        private void ShowNotification(string filePath, ImgChangeType changeType, string oldPath = null)
        {
            if (!HotSwapConstants.ShowNotifications)
                return;

            var notification = new FileModificationInfo
            {
                FilePath = filePath,
                ChangeType = changeType,
                OldPath = oldPath,
                DetectedAt = DateTime.Now
            };

            _notificationBar.QueueNotification(notification);
        }
        #endregion

        #region Private Methods
        private void ReloadFile(string filePath)
        {
            try
            {
                // Find the node representing this file
                var node = FindNodeByFilePath(filePath);
                if (node == null)
                    return;

                if (node.Tag is WzImage wzImage)
                {
                    // Reload the WzImage from disk
                    // This depends on the implementation in WzImage - typically need to re-parse
                    wzImage.ParseImage();

                    // Refresh the tree node
                    node.Nodes.Clear();
                    // Re-populate children would be done by tree view expansion

                    ImgFileReloaded?.Invoke(this, new ImgFileReloadedEventArgs(filePath, node));
                }
                else if (node.Tag is VirtualWzDirectory virtualDir)
                {
                    // Refresh the virtual directory
                    virtualDir.Refresh();

                    // Refresh tree node
                    node.Nodes.Clear();
                    foreach (WzDirectory dir in virtualDir.WzDirectories)
                        node.Nodes.Add(new WzNode(dir));
                    foreach (WzImage img in virtualDir.WzImages)
                        node.Nodes.Add(new WzNode(img));

                    ImgFileReloaded?.Invoke(this, new ImgFileReloadedEventArgs(filePath, node));
                }

                // Clear unsaved changes flag
                _unsavedChanges.TryRemove(filePath, out _);

                // Update recorded state
                _watcherService?.RecordFileState(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error reloading {Path.GetFileName(filePath)}:\n{ex.Message}",
                    "Reload Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RemoveFileFromTree(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string directory = Path.GetDirectoryName(filePath);
            var parentNode = FindNodeByDirectoryPath(directory);

            if (parentNode?.Tag is VirtualWzDirectory virtualDir)
            {
                // Remove from VirtualWzDirectory's internal collection
                virtualDir.RemoveImageByPath(filePath);

                // Remove tree node directly from parent's Nodes collection
                for (int i = parentNode.Nodes.Count - 1; i >= 0; i--)
                {
                    if (parentNode.Nodes[i] is WzNode childNode &&
                        childNode.Tag is WzImage img &&
                        (img.Name?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true ||
                         childNode.Text.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        childNode.Remove();
                        break;
                    }
                }
            }

            _unsavedChanges.TryRemove(filePath, out _);
        }

        private void AddFileToTree(string filePath)
        {
            // Find the parent directory node
            string directory = Path.GetDirectoryName(filePath);
            var parentNode = FindNodeByDirectoryPath(directory);
            if (parentNode == null)
                return;

            try
            {
                // Create a new WzImage for the file
                // This depends on the loading mechanism in the application
                if (parentNode.Tag is VirtualWzDirectory virtualDir)
                {
                    // Refresh the directory to pick up the new file
                    virtualDir.Refresh();

                    // Find and add the new image node
                    string fileName = Path.GetFileName(filePath);
                    var newImage = virtualDir.WzImages.FirstOrDefault(img =>
                        img?.Name != null && img.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                    if (newImage != null)
                    {
                        var newNode = new WzNode(newImage);
                        parentNode.Nodes.Add(newNode);

                        ImgFileAddedToTree?.Invoke(this, new ImgFileAddedEventArgs(filePath, newNode));
                    }
                }

                _watcherService?.RecordFileState(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error adding {Path.GetFileName(filePath)} to tree:\n{ex.Message}",
                    "Add Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void HandleRename(string oldPath, string newPath)
        {
            var node = FindNodeByFilePath(oldPath);
            if (node != null)
            {
                // Update the node text
                node.Text = Path.GetFileName(newPath);

                // Update internal tracking
                if (_unsavedChanges.TryRemove(oldPath, out var hasChanges))
                {
                    _unsavedChanges[newPath] = hasChanges;
                }
            }
        }

        private WzNode FindNodeByFilePath(string filePath)
        {
            // Search through all watched nodes to find one that matches
            foreach (var kvp in _watchedNodes)
            {
                var result = FindNodeRecursive(kvp.Value, filePath);
                if (result != null)
                    return result;
            }
            return null;
        }

        private WzNode FindNodeRecursive(WzNode parent, string filePath)
        {
            if (parent == null)
                return null;

            // Check if this node matches - for VirtualWzDirectory
            if (parent.Tag is VirtualWzDirectory virtualDir)
            {
                // Check if the file is in this directory
                string fileName = Path.GetFileName(filePath);
                string expectedPath = virtualDir.GetImageFilePath(fileName);
                if (expectedPath?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Find the child node for this image
                    foreach (WzNode child in parent.Nodes)
                    {
                        if (child.Tag is WzImage img && img.Name?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true)
                            return child;
                    }
                }
            }

            // Check if this node matches - for WzImage, compare by name and parent path
            if (parent.Tag is WzImage wzImage)
            {
                // Match by name for now (more robust matching can be added later)
                string fileName = Path.GetFileName(filePath);
                if (wzImage.Name?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true)
                    return parent;
            }

            // Check children
            foreach (WzNode child in parent.Nodes)
            {
                var result = FindNodeRecursive(child, filePath);
                if (result != null)
                    return result;
            }

            return null;
        }

        private WzNode FindNodeByDirectoryPath(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return null;

            string normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);

            // First check direct mapping
            if (_watchedNodes.TryGetValue(normalizedPath, out var node))
                return node;

            // Search recursively through watched nodes for VirtualWzDirectory matching the path
            foreach (var kvp in _watchedNodes)
            {
                var result = FindDirectoryNodeRecursive(kvp.Value, normalizedPath);
                if (result != null)
                    return result;
            }
            return null;
        }

        private WzNode FindDirectoryNodeRecursive(WzNode parent, string directoryPath)
        {
            if (parent == null)
                return null;

            if (parent.Tag is VirtualWzDirectory virtualDir)
            {
                string dirPath = Path.GetFullPath(virtualDir.FilesystemPath).TrimEnd(Path.DirectorySeparatorChar);
                if (dirPath.Equals(directoryPath, StringComparison.OrdinalIgnoreCase))
                    return parent;
            }

            foreach (WzNode child in parent.Nodes)
            {
                var result = FindDirectoryNodeRecursive(child, directoryPath);
                if (result != null)
                    return result;
            }

            return null;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _notificationBar.Dispose();

            Disable();
        }
        #endregion
    }

    /// <summary>
    /// Event args for when an IMG file is reloaded
    /// </summary>
    public class ImgFileReloadedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public WzNode Node { get; }

        public ImgFileReloadedEventArgs(string filePath, WzNode node)
        {
            FilePath = filePath;
            Node = node;
        }
    }

    /// <summary>
    /// Event args for when a new IMG file is added to the tree
    /// </summary>
    public class ImgFileAddedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public WzNode Node { get; }

        public ImgFileAddedEventArgs(string filePath, WzNode node)
        {
            FilePath = filePath;
            Node = node;
        }
    }
}
