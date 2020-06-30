using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Audio;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements MonoGame specific sound
    /// </summary>
    public class MonoGameSound : SoundBase
    {
        private SoundEffect sound;
        private SoundEffectInstance source;

        /// <summary>
        /// Gets the state.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public override SoundState State
        {
            get { return source == null ? SoundState.Stopped : (SoundState)(int) source.State; }
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
                if (source != null)
                {
                    return source.Volume;
                }

                return 0;
            }

            set
            {
                if (source != null)
                {
                    source.Volume = value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SunBurnSound"/> class.
        /// </summary>
        /// <param name="nativeSound">The native sound.</param>
        public MonoGameSound(object nativeSound)
            : base(nativeSound)
        {
            sound = nativeSound as SoundEffect;
            if (sound != null)
            {
                source = sound.CreateInstance();
            }
        }

        /// <summary>
        /// Plays this instance.
        /// </summary>
        public override void Play()
        {
            if (sound == null || source == null)
            {
                return;
            }            

            source.Play();            
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public override void Stop()
        {
            if (sound == null || source == null)
            {
                return;
            }

            source.Stop();
        }

        /// <summary>
        /// Pauses this instance.
        /// </summary>
        public override void Pause()
        {
            if (sound == null || source == null)
            {
                return;
            }

            source.Pause();
        }
    }
}
