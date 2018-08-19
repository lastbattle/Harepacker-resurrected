/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Windows.Forms;
using HaRepackerLib;
using MapleLib.WzLib.WzProperties;

namespace HaRepacker.GUI.Panels.SubPanels
{
    public partial class SoundPlayer : UserControl
    {
        private WzMp3Streamer currAudio;
        //private string currSoundFile = "";
        private WzSoundProperty soundProp;

        public SoundPlayer()
        {
            InitializeComponent();
            Disposed += new EventHandler(SoundPlayer_Disposed);
        }

        void SoundPlayer_Disposed(object sender, EventArgs e)
        {
            if (currAudio != null)
                currAudio.Dispose();
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            AudioTimer.Enabled = false;
            currAudio.Pause();
            PauseButton.Visible = false;
            PlayButton.Visible = true;
        }

        private void TimeBar_Scroll(object sender, EventArgs e)
        {
            PrepareAudioForPlayback();

            if (currAudio != null)
            {
                TrackBar bar = (TrackBar)sender;

                currAudio.Position = (int) (currAudio.Length / 100f * (float) bar.Value); // convert trackbar 0~100 percentage to length position
                UpdateTimerLabel();
            }
        }

        private void AudioTimer_Tick(object sender, EventArgs e)
        {
            if (currAudio == null)
                return;

            UpdateTimerLabel();
        }

        private void UpdateTimerLabel()
        {
            TimeBar.Value = (int)(currAudio.Position / (float)currAudio.Length * 100f);
            TimeSpan time = TimeSpan.FromSeconds(currAudio.Position);
            CurrentPositionLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0');
        }

        /// <summary>
        /// On play button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayButton_Click(object sender, EventArgs e)
        {
            PrepareAudioForPlayback();

            currAudio.Play();
            AudioTimer.Enabled = true;
            PlayButton.Visible = false;
            PauseButton.Visible = true;
        }


        private void PrepareAudioForPlayback()
        {
            if (currAudio == null)
            {
                currAudio = new WzMp3Streamer(soundProp, LoopBox.Checked);
            }
        }

        public WzSoundProperty SoundProperty
        {
            get { return soundProp; }
            set 
            {
                if (PauseButton.Visible == true) PauseButton_Click(null, null);
                soundProp = value;
                //currSoundFile = "";
                if (currAudio != null && !currAudio.Disposed)
                    currAudio.Dispose();
                currAudio = null;
                if (soundProp != null)
                {
                    TimeSpan time = TimeSpan.FromMilliseconds(soundProp.Length);
                    LengthLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0');
                }
                CurrentPositionLabel.Text = "00:00 ";
                TimeBar.Value = 0;
            }
        }

        private void LoopBox_CheckedChanged(object sender, EventArgs e)
        {
            PrepareAudioForPlayback();

            currAudio.Repeat = LoopBox.Checked;
        }
    }
}
