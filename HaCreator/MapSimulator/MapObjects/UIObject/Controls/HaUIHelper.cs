/* Copyright(c) 2023, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/


namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

    public class HaUIHelper {

        /// <summary>
        /// Calculates the offset for the alignment (start, center, end)
        /// </summary>
        /// <param name="total"></param>
        /// <param name="child"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        public static int CalculateAlignmentOffset(int total, int child, HaUIAlignment alignment) {
            switch (alignment) {
                case HaUIAlignment.Center:
                    return (total - child) / 2;
                case HaUIAlignment.End:
                    return total - child;
                default: // HaUIAlignment.Start
                    return 0;
            }
        }
    }
}
