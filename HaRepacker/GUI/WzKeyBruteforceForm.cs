/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using System.IO;
using MapleLib.WzLib.Util;
using System.Diagnostics;
using HaRepacker.GUI.Panels;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using System.Linq;
using MapleLib.PacketLib;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using System.Threading;
using HaRepacker.Converter;
using System.Globalization;
using MapleLib.Helpers;

namespace HaRepacker.GUI
{
    public partial class WzKeyBruteforceForm : Form
    {

        private bool bIsLoaded = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="panel"></param>
        /// <param name="wzNode"></param>
        public WzKeyBruteforceForm()
        {
            InitializeComponent();

            FormClosed += WzKeyBruteforceForm_FormClosed;

            bIsLoaded = true;
        }

        private void WzKeyBruteforceForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (t_runningTask != null)
            {
                _cts.Cancel();
                wzKeyBruteforceCompleted = true;
            }
        }

        /// <summary>
        /// Process command key on the form
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // ...
            if (keyData == (Keys.Escape))
            {
                Close(); // exit window
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void button_startStop_Click(object sender, EventArgs e)
        {
            StartWzKeyBruteforcing(Dispatcher.CurrentDispatcher);
        }

        #region WZ IV Key bruteforcing
        private Task t_runningTask = null;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private ulong wzKeyBruteforceTries = 0;
        private DateTime wzKeyBruteforceStartTime = DateTime.Now;
        private bool wzKeyBruteforceCompleted = false;

        private System.Timers.Timer aTimer_wzKeyBruteforce = null;

        /// <summary>
        /// Find needles in a haystack o_O
        /// </summary>
        /// <param name="currentDispatcher"></param>
        private void StartWzKeyBruteforcing(Dispatcher currentDispatcher)
        {
            // Generate WZ keys via a test WZ file
            using (OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWz,
                Filter = string.Format("{0}|TamingMob.wz", HaRepacker.Properties.Resources.WzFilter), // Use the smallest possible file
                Multiselect = false
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                // Show splash screen
                button_startStop.Enabled = false;
                button_startStop.Text = "Brute-forcing...";


                // Reset variables
                wzKeyBruteforceTries = 0;
                wzKeyBruteforceStartTime = DateTime.Now;
                wzKeyBruteforceCompleted = false;


                int processorCount = Environment.ProcessorCount * 3; // 8 core = 16 (with ht, smt) , multiply by 3 seems to be the magic number. it falls off after 4
                List<int> cpuIds = new List<int>();
                for (int cpuId_ = 0; cpuId_ < processorCount; cpuId_++)
                {
                    cpuIds.Add(cpuId_);
                }

                // UI update thread
                if (aTimer_wzKeyBruteforce != null)
                {
                    aTimer_wzKeyBruteforce.Stop();
                    aTimer_wzKeyBruteforce = null;
                }
                aTimer_wzKeyBruteforce = new System.Timers.Timer();
                aTimer_wzKeyBruteforce.Elapsed += new ElapsedEventHandler(OnWzIVKeyUIUpdateEvent);
                aTimer_wzKeyBruteforce.Interval = 2000;
                aTimer_wzKeyBruteforce.Enabled = true;


                // Key finder thread
                t_runningTask = Task.Run(() =>
                {
                    Thread.Sleep(1000); // delay 3 seconds before starting

                    var parallelOption = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = processorCount,
                    };
                    ParallelLoopResult loop = Parallel.ForEach(cpuIds, parallelOption, cpuId =>
                    {
                        WzKeyBruteforceComputeTask(cpuId, processorCount, dialog, currentDispatcher);
                    });
                }, _cts.Token);
            }
        }

        /// <summary>
        /// UI Updating thread
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnWzIVKeyUIUpdateEvent(object source, ElapsedEventArgs e)
        {
            if (aTimer_wzKeyBruteforce == null)
                return;
            if (wzKeyBruteforceCompleted)
            {
                aTimer_wzKeyBruteforce.Stop();
                aTimer_wzKeyBruteforce = null;
            }

            this.BeginInvoke(() =>
            {
                TicksToRelativeTimeConverter ticksToRelativeTimeConverter = new TicksToRelativeTimeConverter();
                label_duration.Text = ticksToRelativeTimeConverter.Convert(DateTime.Now.Ticks - wzKeyBruteforceStartTime.Ticks, null, null, CultureInfo.CurrentCulture) as string;

                label_ivTries.Text = wzKeyBruteforceTries.ToString();
            });
        }

        /// <summary>
        /// Internal compute task for figuring out the WzKey automaticagically 
        /// </summary>
        /// <param name="cpuId_"></param>
        /// <param name="processorCount"></param>
        /// <param name="dialog"></param>
        /// <param name="currentDispatcher"></param>
        private void WzKeyBruteforceComputeTask(int cpuId_, int processorCount, OpenFileDialog dialog, Dispatcher currentDispatcher)
        {
            int cpuId = cpuId_;

            // try bruteforce keys
            const long startValue = int.MinValue;
            const long endValue = int.MaxValue;

            long lookupRangePerCPU = (endValue - startValue) / processorCount;

            Debug.WriteLine("CPUID {0}. Looking up from {1} to {2}. [Range = {3}]  TEST: {4} {5}",
                cpuId,
                (startValue + (lookupRangePerCPU * cpuId)),
                (startValue + (lookupRangePerCPU * (cpuId + 1))),
                lookupRangePerCPU,
                (lookupRangePerCPU * cpuId), (lookupRangePerCPU * (cpuId + 1)));

            for (long i = (startValue + (lookupRangePerCPU * cpuId)); i < (startValue + (lookupRangePerCPU * (cpuId + 1))); i++)  // 2 bill key pairs? o_O
            {
                if (wzKeyBruteforceCompleted)
                    break;

                byte[] bytes = new byte[4];
                unsafe
                {
                    fixed (byte* pbytes = &bytes[0])
                    {
                        *(int*)pbytes = (int)i;
                    }
                }
                bool tryDecrypt = WzTool.TryBruteforcingWzIVKey(dialog.FileName, bytes);
                //Debug.WriteLine("{0} = {1}", cpuId, HexTool.ToString(new PacketWriter(bytes).ToArray()));
                if (tryDecrypt)
                {
                    wzKeyBruteforceCompleted = true;


                    PacketWriter writer = new PacketWriter(4);
                    writer.WriteBytes(bytes);

                    string hexStr = HexTool.ToString(writer.ToArray());

                    MessageBox.Show("Found the encryption key to the WZ file:\r\n" + HexTool.ToString(writer.ToArray()), "Success");
                    Debug.WriteLine("Found key. Key = " + hexStr);

                    string error = string.Format("[WzKeyBruteforceForm] WzKey found: {0}", hexStr);
                    ErrorLogger.Log(ErrorLevel.Info, error);


                    // Hide panel splash sdcreen
                    Action action = () =>
                    {
                        button_startStop.Enabled = true;
                        button_startStop.Text = "Start brute-forcing";

                        label_key.Text = hexStr;
                    };
                    currentDispatcher.BeginInvoke(action);
                    break;
                }
                wzKeyBruteforceTries++;
            }
        }
        #endregion
    }
}