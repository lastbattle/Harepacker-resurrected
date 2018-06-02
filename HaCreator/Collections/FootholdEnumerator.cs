/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class FootholdEnumerator : IEnumerable<FootholdLine>, IEnumerator<FootholdLine>
    {
        bool started = false;
        private FootholdLine firstLine;
        private FootholdAnchor firstAnchor;
        private FootholdLine currLine;
        private FootholdAnchor currAnchor;
        private HashSet<FootholdLine> visited = new HashSet<FootholdLine>();
        private Stack<Tuple<FootholdLine, FootholdAnchor>> stashedLines = new Stack<Tuple<FootholdLine, FootholdAnchor>>();
        
        public FootholdEnumerator(FootholdLine startLine, FootholdAnchor startAnchor)
        {
            firstLine = startLine;
            firstAnchor = startAnchor;
        }

        public IEnumerator<FootholdLine> GetEnumerator()
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
                // First MoveNext should return the starting element
                currLine = firstLine;
                currAnchor = firstAnchor;
                visited.Add(currLine);
                started = true;
                return true;
            }
            FootholdAnchor nextAnchor = currLine.GetOtherAnchor(currAnchor);
            List<FootholdLine> nextLineOpts = nextAnchor.connectedLines.Where(x => !visited.Contains(x)).Cast<FootholdLine>().ToList();
            if (nextLineOpts.Count == 0)
            {
                if (stashedLines.Count == 0)
                {
                    // Enumeration finished
                    return false;
                }
                else
                {
                    // This path finished, pop a new path from the stack
                    var item = stashedLines.Pop();
                    currLine = item.Item1;
                    nextAnchor = item.Item2;
                }
            }
            else if (nextLineOpts.Count == 1)
            {
                currLine = nextLineOpts[0];
                visited.Add(currLine);
            }
            else // more than 1 option, we need to stash
            {
                // Stash all options
                foreach (FootholdLine line in nextLineOpts)
                {
                    visited.Add(line);
                    stashedLines.Push(new Tuple<FootholdLine, FootholdAnchor>(line, nextAnchor));
                }
                // Pull the last one
                var item = stashedLines.Pop();
                currLine = item.Item1;
            }
            currAnchor = nextAnchor;
            return true;
        }

        public void Reset()
        {
            started = false;
            visited.Clear();
            stashedLines.Clear();
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public FootholdLine Current
        {
            get { return currLine; }
        }

        public FootholdAnchor CurrentAnchor
        {
            get { return currAnchor; }
        }
    }
}
