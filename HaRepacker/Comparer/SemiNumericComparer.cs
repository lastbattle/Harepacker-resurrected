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
    public class SemiNumericComparer : IComparer<Tuple<string, int, PointF, ImageSource>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(Tuple<string, int, PointF, ImageSource> s1, Tuple<string, int, PointF, ImageSource> s2)
        {
            bool isS1Numeric = IsNumeric(s1);
            bool isS2Numeric = IsNumeric(s2);

            if (isS1Numeric && isS2Numeric)
            {
                int s1val = Convert.ToInt32(s1.Item1);
                int s2val = Convert.ToInt32(s2.Item1);

                if (s1val > s2val) return 1;
                if (s1val < s2val) return -1;
                if (s1val == s2val) return 0;
            }

            if (isS1Numeric && !isS2Numeric)
                return -1;

            if (!isS1Numeric && isS2Numeric)
                return 1;

            return string.Compare(s1.Item1, s2.Item1, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNumeric(Tuple<string, int, PointF, ImageSource> value)
        {
            int parseInt = 0;
            return Int32.TryParse(value.Item1, out parseInt);
        }
    }
}