using System;
using SunflowSharp.Core;
using SunflowSharp.Systems;

namespace SunflowSharp.Core.Gi
{
    public class GIEngineFactory
    {
        public static GIEngine create(Options options)
        {
            string type = options.getstring("gi.engine", null);
            if (type == null || type == "null" || type == "none")
                return null;
            else if (type == "ambocc")
                return new AmbientOcclusionGIEngine(options);
            else if (type == "fake")
                return new FakeGIEngine(options);
            else if (type == "igi")
                return new InstantGI(options);
            else if (type == "irr-cache")
                return new IrradianceCacheGIEngine(options);
            else if (type == "path")
                return new PathTracingGIEngine(options);
            else
            {
                UI.printWarning(UI.Module.LIGHT, "Unrecognized GI engine type \"{0}\" - ignoring", type);
                return null;
            }
        }
    }
}