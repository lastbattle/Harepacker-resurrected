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

            bool isS1Numeric = IsNumeric(s1);
            bool isS2Numeric = IsNumeric(s2);

            if (isS1Numeric && isS2Numeric)
            {
                int s1val = Convert.ToInt32(s1.Text);
                int s2val = Convert.ToInt32(s2.Text);

                if (s1val > s2val) return 1;
                if (s1val < s2val) return -1;
                if (s1val == s2val) return 0;
            }

            if (isS1Numeric && !isS2Numeric)
                return -1;

            if (!isS1Numeric && isS2Numeric)
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