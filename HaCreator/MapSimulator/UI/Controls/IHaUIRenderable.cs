using System.Drawing;

namespace HaCreator.MapSimulator.UI.Controls {

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
