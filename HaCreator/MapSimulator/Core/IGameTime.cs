using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Interface for game time abstraction.
    /// Allows for deterministic testing by providing a mockable time source.
    /// </summary>
    public interface IGameTime
    {
        /// <summary>
        /// Gets the total elapsed game time since the game started
        /// </summary>
        TimeSpan TotalGameTime { get; }

        /// <summary>
        /// Gets the elapsed time since the last update
        /// </summary>
        TimeSpan ElapsedGameTime { get; }

        /// <summary>
        /// Gets the total elapsed time in milliseconds (convenience property)
        /// </summary>
        int TotalMilliseconds { get; }

        /// <summary>
        /// Gets the elapsed time in milliseconds since last update (convenience property)
        /// </summary>
        int ElapsedMilliseconds { get; }

        /// <summary>
        /// Gets the elapsed time in seconds since last update (convenience property)
        /// </summary>
        float ElapsedSeconds { get; }

        /// <summary>
        /// Gets the current tick count (similar to Environment.TickCount)
        /// </summary>
        int TickCount { get; }

        /// <summary>
        /// Whether the game is running slowly
        /// </summary>
        bool IsRunningSlowly { get; }
    }

    /// <summary>
    /// Default implementation that wraps XNA GameTime
    /// </summary>
    public class XnaGameTime : IGameTime
    {
        private readonly GameTime _gameTime;

        public XnaGameTime(GameTime gameTime)
        {
            _gameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
        }

        public TimeSpan TotalGameTime => _gameTime.TotalGameTime;
        public TimeSpan ElapsedGameTime => _gameTime.ElapsedGameTime;
        public int TotalMilliseconds => (int)_gameTime.TotalGameTime.TotalMilliseconds;
        public int ElapsedMilliseconds => (int)_gameTime.ElapsedGameTime.TotalMilliseconds;
        public float ElapsedSeconds => (float)_gameTime.ElapsedGameTime.TotalSeconds;
        public int TickCount => (int)_gameTime.TotalGameTime.TotalMilliseconds;
        public bool IsRunningSlowly => _gameTime.IsRunningSlowly;

        /// <summary>
        /// Implicit conversion from GameTime for convenience
        /// </summary>
        public static implicit operator XnaGameTime(GameTime gameTime) => new XnaGameTime(gameTime);
    }

    /// <summary>
    /// Mock implementation for deterministic testing.
    /// Allows manual control of time for predictable test results.
    /// </summary>
    public class MockGameTime : IGameTime
    {
        private TimeSpan _totalTime = TimeSpan.Zero;
        private TimeSpan _elapsedTime = TimeSpan.Zero;
        private bool _isRunningSlowly = false;

        public TimeSpan TotalGameTime => _totalTime;
        public TimeSpan ElapsedGameTime => _elapsedTime;
        public int TotalMilliseconds => (int)_totalTime.TotalMilliseconds;
        public int ElapsedMilliseconds => (int)_elapsedTime.TotalMilliseconds;
        public float ElapsedSeconds => (float)_elapsedTime.TotalSeconds;
        public int TickCount => (int)_totalTime.TotalMilliseconds;
        public bool IsRunningSlowly => _isRunningSlowly;

        /// <summary>
        /// Advances time by the specified amount
        /// </summary>
        /// <param name="milliseconds">Milliseconds to advance</param>
        public void Advance(int milliseconds)
        {
            _elapsedTime = TimeSpan.FromMilliseconds(milliseconds);
            _totalTime = _totalTime.Add(_elapsedTime);
        }

        /// <summary>
        /// Advances time by the specified TimeSpan
        /// </summary>
        public void Advance(TimeSpan elapsed)
        {
            _elapsedTime = elapsed;
            _totalTime = _totalTime.Add(_elapsedTime);
        }

        /// <summary>
        /// Sets the total game time directly
        /// </summary>
        public void SetTotalTime(TimeSpan totalTime)
        {
            _totalTime = totalTime;
        }

        /// <summary>
        /// Sets the elapsed time for the current frame
        /// </summary>
        public void SetElapsedTime(TimeSpan elapsedTime)
        {
            _elapsedTime = elapsedTime;
        }

        /// <summary>
        /// Sets whether the game is running slowly
        /// </summary>
        public void SetIsRunningSlowly(bool isRunningSlowly)
        {
            _isRunningSlowly = isRunningSlowly;
        }

        /// <summary>
        /// Resets the mock to initial state
        /// </summary>
        public void Reset()
        {
            _totalTime = TimeSpan.Zero;
            _elapsedTime = TimeSpan.Zero;
            _isRunningSlowly = false;
        }

        /// <summary>
        /// Creates a mock with a specific starting time
        /// </summary>
        public static MockGameTime CreateWithTime(int totalMilliseconds, int elapsedMilliseconds = 16)
        {
            var mock = new MockGameTime();
            mock._totalTime = TimeSpan.FromMilliseconds(totalMilliseconds);
            mock._elapsedTime = TimeSpan.FromMilliseconds(elapsedMilliseconds);
            return mock;
        }
    }

    /// <summary>
    /// Extension methods for IGameTime
    /// </summary>
    public static class GameTimeExtensions
    {
        /// <summary>
        /// Converts an XNA GameTime to IGameTime
        /// </summary>
        public static IGameTime ToInterface(this GameTime gameTime)
        {
            return new XnaGameTime(gameTime);
        }
    }
}
