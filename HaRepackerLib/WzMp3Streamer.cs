/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using NAudio.Wave;
using System.IO;

namespace HaRepackerLib
{
    public class WzMp3Streamer
    {
        private Stream byteStream;
        private Mp3FileReader mpegStream;
        private WaveOut wavePlayer;
        private WzSoundProperty sound;
        private bool repeat;

        public WzMp3Streamer(WzSoundProperty sound, bool repeat)
        {
            this.repeat = repeat;
            this.sound = sound;
            byteStream = new MemoryStream(sound.GetBytes(false));
            mpegStream = new Mp3FileReader(byteStream);
            wavePlayer = new WaveOut(WaveCallbackInfo.FunctionCallback());
            wavePlayer.Init(mpegStream);
            wavePlayer.PlaybackStopped += new EventHandler<StoppedEventArgs>(wavePlayer_PlaybackStopped);
        }

        void wavePlayer_PlaybackStopped(object sender, StoppedEventArgs e)
        {
 	        if (repeat && !disposed)
            {
                mpegStream.Seek(0, SeekOrigin.Begin);
                wavePlayer.Pause();
                wavePlayer.Play();
            }
        }

        private bool disposed = false;
        public bool Disposed
        {
            get { return disposed; }
        }
        public void Dispose()
        {
            disposed = true;
            wavePlayer.Dispose();
            mpegStream.Dispose();
            byteStream.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void Play()
        {
            wavePlayer.Play();
        }

        public void Pause()
        {
            wavePlayer.Pause();
        }

        public bool Repeat
        {
            get { return repeat; }
            set { repeat = value; }
        }

        public int Length
        {
            get { return sound.Length / 1000; }
        }

        public int Position
        {
            get
            {
                return (int)(mpegStream.Position / mpegStream.WaveFormat.AverageBytesPerSecond);
            }
            set
            {
                mpegStream.Seek(value * mpegStream.WaveFormat.AverageBytesPerSecond, SeekOrigin.Begin);
            }
        }
    }
}
