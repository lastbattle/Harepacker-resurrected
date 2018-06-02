/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapEditor
{
    public class Scheduler : IDisposable
    {
        private Dictionary<Action, int> clients;
        private Thread schedThread = null;

        public Scheduler(Dictionary<Action, int> clients)
        {
            this.clients = clients;
            if (clients.Count > 0)
            {
                schedThread = new Thread(new ThreadStart(SchedulerProc));
                schedThread.Start();
            }
        }

        private void SchedulerProc()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Dictionary<Action, int> nextTimes = new Dictionary<Action,int>(clients);
            while (!Program.AbortThreads)
            {
                // Get nearest action
                Action nearestAction = null;
                int nearestTime = int.MaxValue;
                foreach (KeyValuePair<Action, int> nextActionTime in nextTimes)
                {
                    if (nextActionTime.Value < nearestTime)
                    {
                        nearestAction = nextActionTime.Key;
                        nearestTime = nextActionTime.Value;
                    }
                }

                // If we have spare time, sleep it
                long currTime = sw.ElapsedMilliseconds;
                if (currTime < (long)nearestTime)
                {
                    // We can safely cast to int since nobody will ever add a timer with an interval > MAXINT
                    Thread.Sleep((int)((long)nearestTime - currTime));
                }
                // It is now guaranteed we are at (or past) the time needed to nearestAction, so we will execute it
                nearestAction.Invoke();
                // Update nearestAction's next wakeup time
                nextTimes[nearestAction] = nearestTime + clients[nearestAction];
            }
        }

        public void Dispose()
        {
            if (schedThread != null)
            {
                schedThread.Join();
                schedThread = null;
            }
        }
    }
}
