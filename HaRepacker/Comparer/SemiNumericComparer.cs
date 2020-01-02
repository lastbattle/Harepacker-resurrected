using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HaRepacker.Comparer
{
    /// <summary>
    /// Comparer for string names. in ascending order
    /// </summary>
    public class SemiNumericComparer : IComparer<Tuple<string, int, Tuple<PointF, PointF>, ImageSource>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(Tuple<string, int, Tuple<PointF, PointF>, ImageSource> s1, Tuple<string, int, Tuple<PointF, PointF>, ImageSource> s2)
        {
            string s1Text = s1.Item1;
            string s2Text = s2.Item1;

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
            }
            else if (isS1Numeric && !isS2Numeric)
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