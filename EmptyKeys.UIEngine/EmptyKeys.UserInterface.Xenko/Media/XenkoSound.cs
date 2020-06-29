using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Audio;

namespace EmptyKeys.UserInterface.Media
{
    public class XenkoSound : SoundBase
    {
        private Sound sound;
        private SoundInstance soundInstance;

        /// <summary>
        /// Gets the state.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public override SoundState State
        {
            get { return soundInstance == null ? SoundState.Stopped : (SoundState)(int)soundInstance.PlayState; }
        }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public override float Volume
        {
            get
            {
                if (soundInstance != null)
                {
                    return soundInstance.Volume;
                }

                return 0;
            }

            set
            {
                if (soundInstance != null)
                {
                    soundInstance.Volume = value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SunBurnSound"/> class.
        /// </summary>
        /// <param name="nativeSound">The native sound.</param>
        public XenkoSound(object nativeSound)
            : base(nativeSound)
        {
            sound = nativeSound as Sound;
            if (sound != null)
            {
                soundInstance = sound.CreateInstance();
            }
        }

        /// <summary>
        /// Plays this instance.
        /// </summary>
        public override void Play()
        {
            if (sound == null)
            {
                return;
            }

            if (soundInstance == null)
            {
                soundInstance = sound.CreateInstance();
            }

            soundInstance.Play();
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public override void Stop()
        {
            if (sound == null)
            {
                return;
            }

            if (soundInstance == null)
            {
                soundInstance = sound.CreateInstance();
            }

            soundInstance.Stop();
        }

        /// <summary>
        /// Pauses this instance.
        /// </summary>
        public override void Pause()
        {
            if (sound == null)
            {
                return;
            }

            if (soundInstance == null)
            {
                soundInstance = sound.CreateInstance();
            }

            soundInstance.Pause();
        }
    }
}
