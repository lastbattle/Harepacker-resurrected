using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.Comparer
{
    /// <summary>
    /// Comparer for string names. in ascending order
    /// </summary>
    public class SemiNumericComparer : IComparer<Tuple<string, int, PointF, Bitmap>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(Tuple<string, int, PointF, Bitmap> s1, Tuple<string, int, PointF, Bitmap> s2)
        {
            if (IsNumeric(s1) && IsNumeric(s2))
            {
                if (Convert.ToInt32(s1.Item1) > Convert.ToInt32(s2.Item1)) return 1;
                if (Convert.ToInt32(s1.Item1) < Convert.ToInt32(s2.Item1)) return -1;
                if (Convert.ToInt32(s1.Item1) == Convert.ToInt32(s2.Item1)) return 0;
            }

            if (IsNumeric(s1) && !IsNumeric(s2))
                return -1;

            if (!IsNumeric(s1) && IsNumeric(s2))
                return 1;

            return string.Compare(s1.Item1, s2.Item1, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNumeric(Tuple<string, int, PointF, Bitmap> value)
        {
            int parseInt = 0;
            return Int32.TryParse(value.Item1, out parseInt);
        }
    }
}