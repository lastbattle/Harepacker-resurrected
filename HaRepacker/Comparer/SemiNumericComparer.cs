using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.Comparer
{
    /// <summary>
    /// Comparer for string names. in ascending order
    /// Compares by Numeric when possible, so it does not sort by name.
    /// </summary>
    public class SemiNumericComparer : IComparer<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(string s1Text, string s2Text)
        {
            bool isS1Numeric = IsNumericString(s1Text);
            bool isS2Numeric = IsNumericString(s2Text);

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
        private static bool IsNumericString(string value)
        {
            int parseInt = 0;
            return Int32.TryParse(value, out parseInt);
        }
    }
}
