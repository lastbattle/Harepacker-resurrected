/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class AnchorEnumerator : IEnumerable<FootholdAnchor>, IEnumerator<FootholdAnchor>
    {
        private bool started = false;
        private FootholdAnchor first;
        private FootholdAnchor curr;
        private HashSet<FootholdAnchor> visited = new HashSet<FootholdAnchor>();
        private Stack<FootholdAnchor> toVisit = new Stack<FootholdAnchor>();


        public AnchorEnumerator(FootholdAnchor start)
        {
            first = start;
        }

        public IEnumerator<FootholdAnchor> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool MoveNext()
        {
            if (!started)
            {
                curr = first;
                started = true;
                return true;
            }
            List<FootholdAnchor> anchors = new List<FootholdAnchor>();
            foreach (FootholdLine line in curr.connectedLines)
            {
                FootholdAnchor anchor = line.GetOtherAnchor(curr);
                if (!visited.Contains(anchor))
                {
                    anchors.Add(anchor);
                }
            }
            if (anchors.Count == 0)
            {
                if (toVisit.Count == 0)
                {
                    return false;
                }
                else
                {
                    curr = toVisit.Pop();
                }
            }
            else if (anchors.Count == 1)
            {
                curr = anchors[0];
                visited.Add(curr);
            }
            else
            {
                foreach (FootholdAnchor anchor in anchors)
                {
                    visited.Add(anchor);
                    toVisit.Push(anchor);
                }
                curr = toVisit.Pop();
            }
            return true;
        }

        public void Reset()
        {
            started = false;
            visited.Clear();
            toVisit.Clear();
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public FootholdAnchor Current
        {
            get { return curr; }
        }
    }
}
