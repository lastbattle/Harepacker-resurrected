using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace HaRepacker.Comparer
{
    public class TreeViewNodeSorter : IComparer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(object s1_, object s2_)
        {
            TreeNode s1 = s1_ as TreeNode;
            TreeNode s2 = s2_ as TreeNode;

            if (IsNumeric(s1) && IsNumeric(s2))
            {
                if (Convert.ToInt32(s1.Text) > Convert.ToInt32(s2.Text)) return 1;
                if (Convert.ToInt32(s1.Text) < Convert.ToInt32(s2.Text)) return -1;
                if (Convert.ToInt32(s1.Text) == Convert.ToInt32(s2.Text)) return 0;
            }

            if (IsNumeric(s1) && !IsNumeric(s2))
                return -1;

            if (!IsNumeric(s1) && IsNumeric(s2))
                return 1;

            return string.Compare(s1.Text, s2.Text, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNumeric(TreeNode value)
        {
            int parseInt = 0;
            return Int32.TryParse(value.Text, out parseInt);
        }
    }
}