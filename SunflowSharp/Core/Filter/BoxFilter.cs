using System;

namespace SunflowSharp.Core.Filter
{
    public class BoxFilter : IFilter
    {
        private float s;

        public BoxFilter(float size)
        {
            s = size;
        }

        public float getSize()
        {
            return s;
        }

        public float get(float x, float y)
        {
            return 1.0f;
        }
    }
}