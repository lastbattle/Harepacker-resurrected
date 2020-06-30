using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements MonoGame specific audio device
    /// </summary>
    public class MonoGameAudioDevice : AudioDevice
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameAudioDevice"/> class.
        /// </summary>
        public MonoGameAudioDevice()
            : base()
        {
        }

        /// <summary>
        /// Creates the sound.
        /// </summary>
        /// <param name="nativeSound">The native sound.</param>
        /// <returns></returns>
        public override SoundBase CreateSound(object nativeSound)
        {
            return new MonoGameSound(nativeSound);
        }
    }
}
