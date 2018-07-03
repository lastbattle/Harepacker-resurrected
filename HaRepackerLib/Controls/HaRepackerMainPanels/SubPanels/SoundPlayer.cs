/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;

namespace HaRepackerLib.Controls
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
            if (currAudio != null)
                currAudio.Position = ((TrackBar)sender).Value;
        }

        private void AudioTimer_Tick(object sender, EventArgs e)
        {
            if (currAudio == null) return;
            TimeBar.Value = (int)currAudio.Position;
            TimeSpan time = TimeSpan.FromSeconds(currAudio.Position);
            CurrentPositionLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0') + " /";
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (currAudio == null)
            {
                //currSoundFile = Path.GetTempFileName();
                //soundProp.SaveToFile(currSoundFile);
                currAudio = new WzMp3Streamer(soundProp, LoopBox.Checked);
                TimeBar.Maximum = (int)currAudio.Length;
                TimeBar.Minimum = 0;
                currAudio.Play();
            }
            else
            {
                currAudio.Play();
            }
            AudioTimer.Enabled = true;
            PlayButton.Visible = false;
            PauseButton.Visible = true;
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
                CurrentPositionLabel.Text = "00:00 /";
                TimeBar.Value = 0;
            }
        }

        private void LoopBox_CheckedChanged(object sender, EventArgs e)
        {
            currAudio.Repeat = LoopBox.Checked;
        }
    }
}
