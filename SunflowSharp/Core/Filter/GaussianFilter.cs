using System;

namespace SunflowSharp.Core.Filter
{
    public class GaussianFilter : IFilter
    {
        private float s;
        private float es2;

        public GaussianFilter(float size)
        {
            s = size;
            es2 = (float)-Math.Exp(-s * s);
        }

        public float getSize()
        {
            return s;
        }

        public float get(float x, float y)
        {
            float gx = (float)Math.Exp(-x * x) + es2;
            float gy = (float)Math.Exp(-y * y) + es2;
            return gx * gy;
        }
    }
}