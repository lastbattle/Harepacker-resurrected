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
            string s1Text = (s1_ as TreeNode).Text;
            string s2Text = (s2_ as TreeNode).Text;

            bool isS1Numeric = IsNumeric(s1Text);
            bool isS2Numeric = IsNumeric(s2Text);

            if (isS1Numeric && isS2Numeric)
            {
                int s1val = Convert.ToInt32(s1Text);
                int s2val = Convert.ToInt32(s2Text);

                if (s1val > s2val)
                    return 1;
                else if (s1val < s2val)
                    return -1;
                else if (s1val == s2val)
                    return 0;
            } else if (isS1Numeric && !isS2Numeric)
                return -1;
            else if (!isS1Numeric && isS2Numeric)
                return 1;

            return string.Compare(s1Text, s2Text, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNumeric(string value)
        {
            int parseInt = 0;
            return Int32.TryParse(value, out parseInt);
        }
    }
}