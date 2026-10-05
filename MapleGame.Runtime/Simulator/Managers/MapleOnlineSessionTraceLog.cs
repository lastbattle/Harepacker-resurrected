using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Append-only durable sink for online-session acceptance evidence. Entries
    /// are flushed as they arrive so shutdown and crash traces retain the final
    /// lifecycle transition.
    /// </summary>
    public sealed class MapleOnlineSessionTraceLog : IDisposable
    {
        private readonly object _sync = new();
        private readonly string _path;
        private StreamWriter _writer;
        private bool _disposed;

        public MapleOnlineSessionTraceLog(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            _path = System.IO.Path.GetFullPath(path);
            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            bool existed = File.Exists(_path);
            _writer = new StreamWriter(_path, existed, Encoding.UTF8);
            if (!existed)
                _writer.WriteLine("session-trace-start");
        }

        public string Path => _path;

        public void Append(string entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            lock (_sync)
            {
                ThrowIfDisposed();
                _writer.WriteLine(entry);
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MapleOnlineSessionTraceLog));
        }
    }
}
