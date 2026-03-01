using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.GUI {

    /// <summary>
    /// Event handler on field limit change
    /// </summary>
    public class FieldLimitChangedEventArgs : EventArgs {
        public ulong FieldLimit { get; }

        public FieldLimitChangedEventArgs(ulong fieldLimit) {
            FieldLimit = fieldLimit;
        }
    }
}
