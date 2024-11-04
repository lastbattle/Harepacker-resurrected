using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace HaRepacker.Comparer
{
    public class TreeViewNodeSorter : IComparer
    {
        private readonly TreeNode _startNode;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="startNode">The starting node to sort from. If this is null, everything will be sorted.</param>
        public TreeViewNodeSorter(TreeNode startNode)
        {
            _startNode = startNode;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(object obj1, object obj2)
        {
            TreeNode node1 = (TreeNode)obj1;
            TreeNode node2 = (TreeNode)obj2;

            if (_startNode != null && node1?.Parent != _startNode)
            {
                return -1;
            }

            string text1 = node1?.Text;
            string text2 = node2?.Text;

            bool isS1Numeric = int.TryParse(text1, out int num1);
            bool isS2Numeric = int.TryParse(text2, out int num2);

            if (isS1Numeric && isS2Numeric)
            {
                if (num1 > num2)
                    return 1;
                if (num1 < num2)
                    return -1;
                if (num1 == num2)
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

            return string.Compare(text1, text2, StringComparison.OrdinalIgnoreCase);
        }
    }
}