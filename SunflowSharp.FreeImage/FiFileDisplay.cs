using System;
using System.Drawing;
using SunflowSharp.Core;
using SunflowSharp.Core.Display;
using FreeImageAPI;
using System.IO;

namespace SunflowSharp.FreeImage
{
    public class FiFileDisplay : FileDisplay
    {
        public FiFileDisplay(string filename)
            : base(filename)
        {
        }

        public override void imageEnd()
        {
            using (Bitmap b = new Bitmap(bitmap.Width, bitmap.Height))
            {
                for (int i = 0; i < b.Width; i++)
                    for (int j = 0; j < b.Height; j++)
                    {
                        SunflowSharp.Image.Color c = bitmap.getPixel(i, b.Height - j);
                        b.SetPixel(i, j, Color.FromArgb((int)(c.r * 255), (int)(c.g * 255), (int)(c.b * 255)));
                    }
                using (MemoryStream stream = new MemoryStream())
                {
                    b.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                    stream.Seek(0, SeekOrigin.Begin);
                    FIBITMAP fi = FreeImageAPI.FreeImage.LoadFromStream(stream);
                    //fixme: switch based on extension
                    FreeImageAPI.FreeImage.Save(FREE_IMAGE_FORMAT.FIF_PNG, fi, filename, FREE_IMAGE_SAVE_FLAGS.DEFAULT);
                    FreeImageAPI.FreeImage.Unload(fi);
                }
            }
        }
    }
}
