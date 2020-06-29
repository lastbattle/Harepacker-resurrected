using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements Xenko specific audio device
    /// </summary>
    public class XenkoAudioDevice : AudioDevice
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XenkoAudioDevice"/> class.
        /// </summary>
        public XenkoAudioDevice()
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
            return new XenkoSound(nativeSound);
        }
    }
}
