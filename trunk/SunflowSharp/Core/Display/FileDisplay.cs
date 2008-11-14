using System;
using SunflowSharp.Core;
using SunflowSharp.Image;

namespace SunflowSharp.Core.Display
{

    public class FileDisplay : IDisplay
    {
        protected Bitmap bitmap;
        protected string filename;

        public FileDisplay(bool saveImage)
        {
            // a constructor that allows the image to not be saved
            // usefull for benchmarking purposes
            bitmap = null;
            filename = saveImage ? "output.png" : null;
        }

        public FileDisplay(string filename)
        {
            bitmap = null;
            this.filename = filename == null ? "output.png" : filename;
        }

        public virtual void imageBegin(int w, int h, int bucketSize)
        {
            if (bitmap == null || bitmap.Width != w || bitmap.Height != h)
                bitmap = new Bitmap(w, h, filename == null || filename.EndsWith(".hdr"));
        }

        public virtual void imagePrepare(int x, int y, int w, int h, int id)
        {
        }

        public virtual void imageUpdate(int x, int y, int w, int h, Color[] data)
        {
            for (int j = 0, index = 0; j < h; j++)
                for (int i = 0; i < w; i++, index++)
                    bitmap.setPixel(x + i, bitmap.Height - 1 - (y + j), data[index]);
        }

        public virtual void imageFill(int x, int y, int w, int h, Color c)
        {
            Color cg = c;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    bitmap.setPixel(x + i, bitmap.Height - 1 - (y + j), cg);
        }

        public virtual void imageEnd()
        {
            if (filename != null)
                bitmap.save(filename);
        }
    }
}