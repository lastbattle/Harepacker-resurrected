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

namespace HaRepacker
{
    public class WzMp3Streamer
    {
        private Stream byteStream;

        private Mp3FileReader mpegStream;
        private WaveFileReader waveFileStream;

        private WaveOut wavePlayer;
        private WzSoundProperty sound;
        private bool repeat;

        private bool playbackSuccessfully = true;

        public WzMp3Streamer(WzSoundProperty sound, bool repeat)
        {
            this.repeat = repeat;
            this.sound = sound;
            byteStream = new MemoryStream(sound.GetBytes(false));

            wavePlayer = new WaveOut(WaveCallbackInfo.FunctionCallback());
            try
            {
                mpegStream = new Mp3FileReader(byteStream);
                wavePlayer.Init(mpegStream);
            }
            catch (System.InvalidOperationException)
            {
                try
                {
                    waveFileStream = new WaveFileReader(byteStream);
                    wavePlayer.Init(waveFileStream);
                }
                catch (FormatException)
                {
                    playbackSuccessfully = false;
                }
                //InvalidDataException
            }
            wavePlayer.PlaybackStopped += new EventHandler<StoppedEventArgs>(wavePlayer_PlaybackStopped);
        }

        void wavePlayer_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (repeat && !disposed)
            {
                if (mpegStream != null)
                    mpegStream.Seek(0, SeekOrigin.Begin);
                else
                    waveFileStream.Seek(0, SeekOrigin.Begin);

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
            if (!playbackSuccessfully)
                return;

            disposed = true;
            wavePlayer.Dispose();
            if (mpegStream != null)
            {
                mpegStream.Dispose();
                mpegStream = null;
            }
            if (waveFileStream != null)
            {
                waveFileStream.Dispose();
                waveFileStream = null;
            }
            byteStream.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void Play()
        {
            if (!playbackSuccessfully)
                return;

            wavePlayer.Play();
        }

        public void Pause()
        {
            if (!playbackSuccessfully)
                return;

            wavePlayer.Pause();
        }

        public void Stop()
        {
            if (!playbackSuccessfully) return;
            wavePlayer.Stop();
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
                if (mpegStream != null)
                    return (int)(mpegStream.Position / mpegStream.WaveFormat.AverageBytesPerSecond);
                else if (waveFileStream != null)
                    return (int)(waveFileStream.Position / waveFileStream.WaveFormat.AverageBytesPerSecond);

                return 0;
            }
            set
            {
                if (mpegStream != null)
                    mpegStream.Seek(value * mpegStream.WaveFormat.AverageBytesPerSecond, SeekOrigin.Begin);
                else if (waveFileStream != null)
                    waveFileStream.Seek(value * waveFileStream.WaveFormat.AverageBytesPerSecond, SeekOrigin.Begin);
            }
        }
    }
}
