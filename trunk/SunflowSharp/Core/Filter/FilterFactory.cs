using System;

namespace SunflowSharp.Core.Filter
{
    public class FilterFactory
    {
        public static IFilter get(string filter)
        {
            if (filter == "box")
                return new BoxFilter(1);
            else if (filter == "gaussian")
                return new GaussianFilter(3);
            else if (filter == "mitchell")
                return new MitchellFilter();
            else if (filter == "catmull-rom")
                return new CatmullRomFilter();
            else if (filter == "blackman-harris")
                return new BlackmanHarrisFilter(4);
            else if (filter == "sinc")
                return new SincFilter(4);
            else if (filter == "lanczos")
                return new LanczosFilter();
            else if (filter == "triangle")
                return new TriangleFilter(2);
            else
                return null;
        }
    }
}