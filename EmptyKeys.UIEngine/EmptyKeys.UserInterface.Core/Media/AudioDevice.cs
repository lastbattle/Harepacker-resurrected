
namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements abstract Audio Device
    /// </summary>
    public abstract class AudioDevice
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioDevice"/> class.
        /// </summary>
        public AudioDevice()
        {
        }

        /// <summary>
        /// Creates the sound.
        /// </summary>
        /// <param name="nativeSound">The native sound.</param>
        /// <returns></returns>
        public abstract SoundBase CreateSound(object nativeSound);        
    }
}
