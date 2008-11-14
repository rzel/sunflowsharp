using System;

namespace SunflowSharp.Core.Filter
{
    public class TriangleFilter : IFilter
    {
        private float s, inv;

        public TriangleFilter(float size)
        {
            s = size;
            inv = 1.0f / (s * 0.5f);
        }

        public float getSize()
        {
            return s;
        }

        public float get(float x, float y)
        {
            return (1.0f - Math.Abs(x * inv)) * (1.0f - Math.Abs(y * inv));
        }
    }
}