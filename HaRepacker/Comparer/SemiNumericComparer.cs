using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
            bool isS1Numeric = int.TryParse(s1Text, out int s1val);
            bool isS2Numeric = int.TryParse(s2Text, out int s2val);

            if (isS1Numeric && isS2Numeric)
            {
                if (s1val > s2val)
                    return 1;
                if (s1val < s2val)
                    return -1;
                if (s1val == s2val)
                    return 0;
            }
            else if (isS1Numeric)
            {
                return -1;
            }
            else if (isS2Numeric)
            {
                return 1;
            }

            return string.Compare(s1Text, s2Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}