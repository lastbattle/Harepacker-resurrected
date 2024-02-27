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


using System.Drawing;

namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

    public interface IHaUIRenderable {
        /// <summary>
        /// Adds a renderable IHaUIRenderable to this IHaUIRenderable
        /// </summary>
        /// <param name="renderable"></param>
        void AddRenderable(IHaUIRenderable renderable);

        /// <summary>
        // Loop through all elements in the grid, call their Render method,
        // and combine the results into one Bitmap, considering margins, padding, and alignment.
        // This is highly dependent on your specific rendering requirements.
        // Placeholder for the required interface method:
        /// </summary>
        /// <returns></returns>
        Bitmap Render();

        /// <summary>
        // Loop through all elements in the grid, call their Render method,
        // and combine the results into one Bitmap, considering margins, padding, and alignment.
        // This is highly dependent on your specific rendering requirements.
        // Placeholder for the required interface method:
        /// </summary>
        /// <returns></returns>
        //Bitmap RenderTextOnly(Bitmap previousBitmap);

        /// <summary>
        // Implementation of the size logic goes here.
        // You may want to loop through all elements in the grid and calculate the total size,
        // considering margins, padding, and alignment.
        // Placeholder for the required interface method:
        /// </summary>
        /// <returns></returns>
        HaUISize GetSize();


        HaUIInfo GetInfo();
    }
}
