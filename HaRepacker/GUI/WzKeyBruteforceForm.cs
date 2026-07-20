using System;
using System.Buffers.Binary;
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
    public partial class WzKeyBruteforceForm : ThemedDialogWindow
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

            Closed += WzKeyBruteforceForm_FormClosed;

            Title = WpfDialogSupport.Text(typeof(WzKeyBruteforceForm), "$this.Text", "Brute-force WZ key");
            button_startStop.Content = WpfDialogSupport.Text(typeof(WzKeyBruteforceForm), "button_startStop.Text", "Start brute-forcing");

            bIsLoaded = true;
        }

        private void WzKeyBruteforceForm_FormClosed(object sender, EventArgs e)
        {
            if (t_runningTask != null)
            {
                _cts.Cancel();
                Volatile.Write(ref wzKeyBruteforceCompleted, 1);
            }
        }

        /// <summary>
        /// Process command key on the form
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        private void button_startStop_Click(object sender, EventArgs e)
        {
            StartWzKeyBruteforcing(Dispatcher.CurrentDispatcher);
        }

        #region WZ IV Key bruteforcing
        private Task t_runningTask = null;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private long wzKeyBruteforceTries = 0;
        private DateTime wzKeyBruteforceStartTime = DateTime.Now;
        private int wzKeyBruteforceCompleted = 0;
        private long foundIvCandidate = -1;

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
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                string wzPath = dialog.FileName;

                // Show splash screen
                button_startStop.IsEnabled = false;
                button_startStop.Content = UiLocalization.Translate("Brute-forcing...");


                // Reset variables
                wzKeyBruteforceTries = 0;
                wzKeyBruteforceStartTime = DateTime.Now;
                Volatile.Write(ref wzKeyBruteforceCompleted, 0);
                Interlocked.Exchange(ref foundIvCandidate, -1);

                int processorCount = Math.Max(1, Environment.ProcessorCount * 3);

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
                t_runningTask = Task.Run(
                    () => RunWzKeyBruteforce(wzPath, processorCount, currentDispatcher),
                    _cts.Token);
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
            if (Volatile.Read(ref wzKeyBruteforceCompleted) != 0)
            {
                aTimer_wzKeyBruteforce.Stop();
                aTimer_wzKeyBruteforce = null;
            }

            Dispatcher.BeginInvoke(() =>
            {
                TicksToRelativeTimeConverter ticksToRelativeTimeConverter = new TicksToRelativeTimeConverter();
                label_duration.Text = ticksToRelativeTimeConverter.Convert(DateTime.Now.Ticks - wzKeyBruteforceStartTime.Ticks, null, null, CultureInfo.CurrentCulture) as string;

                label_ivTries.Text = Interlocked.Read(ref wzKeyBruteforceTries).ToString();
            });
        }

        /// <summary>
        /// Tests common keys first, then scans the complete 32-bit IV space in AES batches.
        /// </summary>
        /// <param name="wzPath"></param>
        /// <param name="processorCount"></param>
        /// <param name="currentDispatcher"></param>
        private void RunWzKeyBruteforce(string wzPath, int processorCount, Dispatcher currentDispatcher)
        {
            try
            {
                WzKeyBruteforceProbe probe = new WzKeyBruteforceProbe(wzPath);

                uint[] commonCandidates =
                {
                    0,
                    BinaryPrimitives.ReadUInt32LittleEndian(WzAESConstant.WZ_GMSIV),
                    BinaryPrimitives.ReadUInt32LittleEndian(WzAESConstant.WZ_MSEAIV),
                };

                foreach (uint candidate in commonCandidates.Distinct())
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref wzKeyBruteforceTries);
                    if (probe.TryCandidate(candidate) && TryPublishFoundKey(candidate, currentDispatcher))
                        return;
                }

                const ulong candidateCount = 1UL << 32;
                ParallelOptions parallelOptions = new ParallelOptions
                {
                    CancellationToken = _cts.Token,
                    MaxDegreeOfParallelism = processorCount,
                };

                Parallel.For(0, processorCount, parallelOptions, (workerId, loopState) =>
                {
                    ulong rangeStart = candidateCount * (ulong)workerId / (ulong)processorCount;
                    ulong rangeEnd = candidateCount * (ulong)(workerId + 1) / (ulong)processorCount;

                    using WzKeyBruteforceProbe.Worker worker = probe.CreateWorker();
                    uint? found = worker.FindFirst(
                        rangeStart,
                        rangeEnd,
                        _cts.Token,
                        () => Volatile.Read(ref wzKeyBruteforceCompleted) != 0,
                        processed => Interlocked.Add(ref wzKeyBruteforceTries, processed));

                    if (found.HasValue && TryPublishFoundKey(found.Value, currentDispatcher))
                        loopState.Stop();
                });

                if (Volatile.Read(ref wzKeyBruteforceCompleted) == 0)
                {
                    Volatile.Write(ref wzKeyBruteforceCompleted, 1);
                    currentDispatcher.BeginInvoke(() =>
                    {
                        button_startStop.IsEnabled = true;
                        button_startStop.Content = UiLocalization.Translate("Start brute-forcing");
                        MessageBox.Show(
                            UiLocalization.Translate("No encryption key was found."),
                            UiLocalization.Translate("Error"));
                    });
                }
            }
            catch (OperationCanceledException)
            {
                Volatile.Write(ref wzKeyBruteforceCompleted, 1);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref wzKeyBruteforceCompleted, 1);
                ErrorLogger.Log(ErrorLevel.Critical, "[WzKeyBruteforceForm] " + ex);
                currentDispatcher.BeginInvoke(() =>
                {
                    button_startStop.IsEnabled = true;
                    button_startStop.Content = UiLocalization.Translate("Start brute-forcing");
                    MessageBox.Show(ex.Message, UiLocalization.Translate("Error"));
                });
            }
        }

        private bool TryPublishFoundKey(uint candidate, Dispatcher currentDispatcher)
        {
            if (Interlocked.CompareExchange(ref foundIvCandidate, candidate, -1) != -1)
                return false;

            Volatile.Write(ref wzKeyBruteforceCompleted, 1);

            byte[] bytes = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, candidate);
            string hexStr = HexTool.ToString(bytes);

            Debug.WriteLine("Found key. Key = " + hexStr);
            ErrorLogger.Log(ErrorLevel.Info, $"[WzKeyBruteforceForm] WzKey found: {hexStr}");

            currentDispatcher.BeginInvoke(() =>
            {
                MessageBox.Show(
                    UiLocalization.Translate("Found the encryption key to the WZ file:") + "\r\n" + hexStr,
                    UiLocalization.Translate("Success"));

                button_startStop.IsEnabled = true;
                button_startStop.Content = UiLocalization.Translate("Start brute-forcing");
                label_key.Text = hexStr;
            });
            return true;
        }
        #endregion
    }
}
