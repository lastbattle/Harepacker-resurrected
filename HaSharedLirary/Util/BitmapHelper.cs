using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace HaSharedLibrary.Util
{
    public class BitmapHelper
    { /// <summary>
      /// Takes a bitmap and converts it to an image that can be handled by WPF ImageBrush
      /// </summary>
      /// <param name="src">A bitmap image</param>
      /// <param name="format"></param>
      /// <returns>The image as a BitmapImage for WPF</returns>
        public static BitmapImage Convert(Bitmap src, System.Drawing.Imaging.ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                src.Save(ms, format);
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                ms.Seek(0, SeekOrigin.Begin);
                image.StreamSource = ms;
                image.EndInit();

                return image;
            }
        }
    }
}
