
namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements abstract Sound
    /// </summary>
    public abstract class SoundBase
    {
        /// <summary>
        /// Gets the state.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public abstract SoundState State { get; }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public abstract float Volume { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundBase"/> class.
        /// </summary>
        /// <param name="nativeSound">The native sound.</param>
        public SoundBase(object nativeSound)
        {
        }

        /// <summary>
        /// Plays this instance.
        /// </summary>
        public abstract void Play();

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Pauses this instance.
        /// </summary>
        public abstract void Pause();
    }
}
