using System;
using System.IO;
using SunflowSharp.Core;
using SunflowSharp.Core.Camera;
using SunflowSharp.Core.Light;
using SunflowSharp.Core.Modifiers;
using SunflowSharp.Core.Primitive;
using SunflowSharp.Core.Shader;
using SunflowSharp.Core.Tesselatable;
using SunflowSharp.Image;
using SunflowSharp.Maths;
using SunflowSharp.Systems;

namespace SunflowSharp.Core.Parser
{
    /**
     * This class provides a static method for loading files in the Sunflow scene
     * file format.
     */
    public class SCParser : SceneParserBase
    {
        private SunflowSharp.Systems.Parser p;
        private int numLightSamples;

        public SCParser()
        {
        }

        public override bool parse(Stream stream, SunflowAPI api)
        {
            //string localDir = Path.GetFullPath(filename);
            numLightSamples = 1;
            Timer timer = new Timer();
            timer.start();
            UI.printInfo(UI.Module.API, "Parsing stream ...");
            try
            {
                p = new Systems.Parser(stream);
                while (true)
                {
                    string token = p.getNextToken();
                    if (token == null)
                        break;
                    if (token == "image")
                    {
                        UI.printInfo(UI.Module.API, "Reading image settings ...");
                        parseImageBlock(api);
                    }
                    else if (token == "background")
                    {
                        UI.printInfo(UI.Module.API, "Reading background ...");
                        parseBackgroundBlock(api);
                    }
                    else if (token == "accel")
                    {
                        UI.printInfo(UI.Module.API, "Reading accelerator type ...");
                        p.getNextToken();
                        UI.printWarning(UI.Module.API, "Setting accelerator type is not recommended - ignoring");
                    }
                    else if (token == "filter")
                    {
                        UI.printInfo(UI.Module.API, "Reading image filter type ...");
                        parseFilter(api);
                    }
                    else if (token == "bucket")
                    {
                        UI.printInfo(UI.Module.API, "Reading bucket settings ...");
                        api.parameter("bucket.size", p.getNextInt());
                        api.parameter("bucket.order", p.getNextToken());
                        api.options(SunflowAPI.DEFAULT_OPTIONS);
                    }
                    else if (token == "photons")
                    {
                        UI.printInfo(UI.Module.API, "Reading photon settings ...");
                        parsePhotonBlock(api);
                    }
                    else if (token == "gi")
                    {
                        UI.printInfo(UI.Module.API, "Reading global illumination settings ...");
                        parseGIBlock(api);
                    }
                    else if (token == "lightserver")
                    {
                        UI.printInfo(UI.Module.API, "Reading light server settings ...");
                        parseLightserverBlock(api);
                    }
                    else if (token == "trace-depths")
                    {
                        UI.printInfo(UI.Module.API, "Reading trace depths ...");
                        parseTraceBlock(api);
                    }
                    else if (token == "camera")
                    {
                        parseCamera(api);
                    }
                    else if (token == "shader")
                    {
                        if (!parseShader(api))
                            return false;
                    }
                    else if (token == "modifier")
                    {
                        if (!parseModifier(api))
                            return false;
                    }
                    else if (token == "override")
                    {
                        api.shaderOverride(p.getNextToken(), p.getNextbool());
                    }
                    else if (token == "object")
                    {
                        parseObjectBlock(api);
                    }
                    else if (token == "instance")
                    {
                        parseInstanceBlock(api);
                    }
                    else if (token == "light")
                    {
                        parseLightBlock(api);
                    }
                    else if (token == "texturepath")
                    {
                        string path = p.getNextToken();
                        //if (!new File(path).isAbsolute())
                        //    path = localDir + File.separator + path;
                        api.addTextureSearchPath(Path.GetFullPath(path));
                    }
                    else if (token == "includepath")
                    {
                        string path = p.getNextToken();
                        //if (!new File(path).isAbsolute())
                        //    path = localDir + File.separator + path;
                        api.addIncludeSearchPath(Path.GetFullPath(path));
                    }
                    else if (token == "include")
                    {
                        string file = p.getNextToken();
                        UI.printInfo(UI.Module.API, "Including: \"{0}\" ...", file);
                        api.parse(file);
                    }
                    else
                        UI.printWarning(UI.Module.API, "Unrecognized token {0}", token);
                }
                p.close();
            }
            catch (Exception e)
            {
                UI.printError(UI.Module.API, "{0}", e);
                return false;
            }
            timer.end();
            UI.printInfo(UI.Module.API, "Done parsing.");
            UI.printInfo(UI.Module.API, "Parsing time: {0}", timer.ToString());
            return true;
        }

        private void parseImageBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            if (p.peekNextToken("resolution"))
            {
                api.parameter("resolutionX", p.getNextInt());
                api.parameter("resolutionY", p.getNextInt());
            }
            if (p.peekNextToken("aa"))
            {
                api.parameter("aa.min", p.getNextInt());
                api.parameter("aa.max", p.getNextInt());
            }
            if (p.peekNextToken("samples"))
                api.parameter("aa.samples", p.getNextInt());
            if (p.peekNextToken("contrast"))
                api.parameter("aa.contrast", p.getNextFloat());
            if (p.peekNextToken("filter"))
                api.parameter("filter", p.getNextToken());
            if (p.peekNextToken("jitter"))
                api.parameter("aa.jitter", p.getNextbool());
            if (p.peekNextToken("show-aa"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: show-aa ignored");
                p.getNextbool();
            }
            if (p.peekNextToken("output"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: output statement ignored");
                p.getNextToken();
            }
            api.options(SunflowAPI.DEFAULT_OPTIONS);
            p.checkNextToken("}");
        }

        private void parseBackgroundBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("color");
            api.parameter("color", parseColor());
            api.shader("background.shader", new ConstantShader());
            api.geometry("background", new Background());
            api.parameter("shaders", "background.shader");
            api.instance("background.instance", "background");
            p.checkNextToken("}");
        }

        private void parseFilter(SunflowAPI api)
        {
            UI.printWarning(UI.Module.API, "Deprecated keyword \"filter\" - set this option in the image block");
            string name = p.getNextToken();
            api.parameter("filter", name);
            api.options(SunflowAPI.DEFAULT_OPTIONS);
            bool hasSizeParams = name == "box" || name == "gaussian" || name == "blackman-harris" || name == "sinc" || name == "triangle";
            if (hasSizeParams)
            {
                p.getNextFloat();
                p.getNextFloat();
            }
        }

        private void parsePhotonBlock(SunflowAPI api)
        {
            int numEmit = 0;
            bool globalEmit = false;
            p.checkNextToken("{");
            if (p.peekNextToken("emit"))
            {
                UI.printWarning(UI.Module.API, "Shared photon emit values are deprectated - specify number of photons to emit per map");
                numEmit = p.getNextInt();
                globalEmit = true;
            }
            if (p.peekNextToken("global"))
            {
                UI.printWarning(UI.Module.API, "Global photon map setting belonds inside the gi block - ignoring");
                if (!globalEmit)
                    p.getNextInt();
                p.getNextToken();
                p.getNextInt();
                p.getNextFloat();
            }
            p.checkNextToken("caustics");
            if (!globalEmit)
                numEmit = p.getNextInt();
            api.parameter("caustics.emit", numEmit);
            api.parameter("caustics", p.getNextToken());
            api.parameter("caustics.gather", p.getNextInt());
            api.parameter("caustics.radius", p.getNextFloat());
            api.options(SunflowAPI.DEFAULT_OPTIONS);
            p.checkNextToken("}");
        }

        private void parseGIBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("type");
            if (p.peekNextToken("irr-cache"))
            {
                api.parameter("gi.engine", "irr-cache");
                p.checkNextToken("samples");
                api.parameter("gi.irr-cache.samples", p.getNextInt());
                p.checkNextToken("tolerance");
                api.parameter("gi.irr-cache.tolerance", p.getNextFloat());
                p.checkNextToken("spacing");
                api.parameter("gi.irr-cache.min_spacing", p.getNextFloat());
                api.parameter("gi.irr-cache.max_spacing", p.getNextFloat());
                // parse global photon map info
                if (p.peekNextToken("global"))
                {
                    api.parameter("gi.irr-cache.gmap.emit", p.getNextInt());
                    api.parameter("gi.irr-cache.gmap", p.getNextToken());
                    api.parameter("gi.irr-cache.gmap.gather", p.getNextInt());
                    api.parameter("gi.irr-cache.gmap.radius", p.getNextFloat());
                }
            }
            else if (p.peekNextToken("path"))
            {
                api.parameter("gi.engine", "path");
                p.checkNextToken("samples");
                api.parameter("gi.path.samples", p.getNextInt());
                if (p.peekNextToken("bounces"))
                {
                    UI.printWarning(UI.Module.API, "Deprecated setting: bounces - use diffuse trace depth instead");
                    p.getNextInt();
                }
            }
            else if (p.peekNextToken("fake"))
            {
                api.parameter("gi.engine", "fake");
                p.checkNextToken("up");
                api.parameter("gi.fake.up", parseVector());
                p.checkNextToken("sky");
                api.parameter("gi.fake.sky", parseColor());
                p.checkNextToken("ground");
                api.parameter("gi.fake.ground", parseColor());
            }
            else if (p.peekNextToken("igi"))
            {
                api.parameter("gi.engine", "igi");
                p.checkNextToken("samples");
                api.parameter("gi.igi.samples", p.getNextInt());
                p.checkNextToken("sets");
                api.parameter("gi.igi.sets", p.getNextInt());
                if (!p.peekNextToken("b"))
                    p.checkNextToken("c");
                api.parameter("gi.igi.c", p.getNextFloat());
                p.checkNextToken("bias-samples");
                api.parameter("gi.igi.bias_samples", p.getNextInt());
            }
            else if (p.peekNextToken("ambocc"))
            {
                api.parameter("gi.engine", "ambocc");
                p.checkNextToken("bright");
                api.parameter("gi.ambocc.bright", parseColor());
                p.checkNextToken("dark");
                api.parameter("gi.ambocc.dark", parseColor());
                p.checkNextToken("samples");
                api.parameter("gi.ambocc.samples", p.getNextInt());
                if (p.peekNextToken("maxdist"))
                    api.parameter("gi.ambocc.maxdist", p.getNextFloat());
            }
            else if (p.peekNextToken("none") || p.peekNextToken("null"))
            {
                // disable GI
                api.parameter("gi.engine", "none");
            }
            else
                UI.printWarning(UI.Module.API, "Unrecognized gi engine type \"{0}\" - ignoring", p.getNextToken());
            api.options(SunflowAPI.DEFAULT_OPTIONS);
            p.checkNextToken("}");
        }

        private void parseLightserverBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            if (p.peekNextToken("shadows"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: shadows setting ignored");
                p.getNextbool();
            }
            if (p.peekNextToken("direct-samples"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: use samples keyword in area light definitions");
                numLightSamples = p.getNextInt();
            }
            if (p.peekNextToken("glossy-samples"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: use samples keyword in glossy shader definitions");
                p.getNextInt();
            }
            if (p.peekNextToken("max-depth"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: max-depth setting - use trace-depths block instead");
                int d = p.getNextInt();
                api.parameter("depths.diffuse", 1);
                api.parameter("depths.reflection", d - 1);
                api.parameter("depths.refraction", 0);
                api.options(SunflowAPI.DEFAULT_OPTIONS);
            }
            if (p.peekNextToken("global"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: global settings ignored - use photons block instead");
                p.getNextbool();
                p.getNextInt();
                p.getNextInt();
                p.getNextInt();
                p.getNextFloat();
            }
            if (p.peekNextToken("caustics"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: caustics settings ignored - use photons block instead");
                p.getNextbool();
                p.getNextInt();
                p.getNextFloat();
                p.getNextInt();
                p.getNextFloat();
            }
            if (p.peekNextToken("irr-cache"))
            {
                UI.printWarning(UI.Module.API, "Deprecated: irradiance cache settings ignored - use gi block instead");
                p.getNextInt();
                p.getNextFloat();
                p.getNextFloat();
                p.getNextFloat();
            }
            p.checkNextToken("}");
        }

        private void parseTraceBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            if (p.peekNextToken("diff"))
                api.parameter("depths.diffuse", p.getNextInt());
            if (p.peekNextToken("refl"))
                api.parameter("depths.reflection", p.getNextInt());
            if (p.peekNextToken("refr"))
                api.parameter("depths.refraction", p.getNextInt());
            p.checkNextToken("}");
            api.options(SunflowAPI.DEFAULT_OPTIONS);
        }

        private void parseCamera(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("type");
            string type = p.getNextToken();
            UI.printInfo(UI.Module.API, "Reading %s camera ...", type);
            parseCameraTransform(api);
            string name = api.getUniqueName("camera");
            if (type == "pinhole")
            {
                p.checkNextToken("fov");
                api.parameter("fov", p.getNextFloat());
                p.checkNextToken("aspect");
                api.parameter("aspect", p.getNextFloat());
                api.camera(name, new PinholeLens());
            }
            else if (type == "thinlens")
            {
                p.checkNextToken("fov");
                api.parameter("fov", p.getNextFloat());
                p.checkNextToken("aspect");
                api.parameter("aspect", p.getNextFloat());
                p.checkNextToken("fdist");
                api.parameter("focus.distance", p.getNextFloat());
                p.checkNextToken("lensr");
                api.parameter("lens.radius", p.getNextFloat());
                if (p.peekNextToken("sides"))
                    api.parameter("lens.sides", p.getNextInt());
                if (p.peekNextToken("rotation"))
                    api.parameter("lens.rotation", p.getNextFloat());
                api.camera(name, new ThinLens());
            }
            else if (type == "spherical")
            {
                // no extra arguments
                api.camera(name, new SphericalLens());
            }
            else if (type == "fisheye")
            {
                // no extra arguments
                api.camera(name, new FisheyeLens());
            }
            else
            {
                UI.printWarning(UI.Module.API, "Unrecognized camera type: {0}", p.getNextToken());
                p.checkNextToken("}");
                return;
            }
            p.checkNextToken("}");
            if (name != null)
            {
                api.parameter("camera", name);
                api.options(SunflowAPI.DEFAULT_OPTIONS);
            }
        }

        private void parseCameraTransform(SunflowAPI api)
        {
            if (p.peekNextToken("steps"))
            {
                // motion blur camera
                int n = p.getNextInt();
                api.parameter("transform.steps", n);
                for (int i = 0; i < n; i++)
                    parseCameraMatrix(i, api);
            }
            else
                parseCameraMatrix(-1, api);
        }

        private void parseCameraMatrix(int index, SunflowAPI api)
        {
            string offset = index < 0 ? "" : string.Format("[{0}]", index);
            if (p.peekNextToken("transform"))
            {
                // advanced camera
                api.parameter(string.Format("transform%s", offset), parseMatrix());
            }
            else
            {
                if (index >= 0)
                    p.checkNextToken("{");
                // regular camera specification
                p.checkNextToken("eye");
                api.parameter(string.Format("eye{0}", offset), parsePoint());
                p.checkNextToken("target");
                api.parameter(string.Format("target{0}", offset), parsePoint());
                p.checkNextToken("up");
                api.parameter(string.Format("up{0}", offset), parseVector());
                if (index >= 0)
                    p.checkNextToken("}");
            }
        }

        private bool parseShader(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("name");
            string name = p.getNextToken();
            UI.printInfo(UI.Module.API, "Reading shader: {0} ...", name);
            p.checkNextToken("type");
            if (p.peekNextToken("diffuse"))
            {
                if (p.peekNextToken("diff"))
                {
                    api.parameter("diffuse", parseColor());
                    api.shader(name, new DiffuseShader());
                }
                else if (p.peekNextToken("texture"))
                {
                    api.parameter("texture", p.getNextToken());
                    api.shader(name, new TexturedDiffuseShader());
                }
                else
                    UI.printWarning(UI.Module.API, "Unrecognized option in diffuse shader block: {0}", p.getNextToken());
            }
            else if (p.peekNextToken("phong"))
            {
                string tex = null;
                if (p.peekNextToken("texture"))
                    api.parameter("texture", tex = p.getNextToken());
                else
                {
                    p.checkNextToken("diff");
                    api.parameter("diffuse", parseColor());
                }
                p.checkNextToken("spec");
                api.parameter("specular", parseColor());
                api.parameter("power", p.getNextFloat());
                if (p.peekNextToken("samples"))
                    api.parameter("samples", p.getNextInt());
                if (tex != null)
                    api.shader(name, new TexturedPhongShader());
                else
                    api.shader(name, new PhongShader());
            }
            else if (p.peekNextToken("amb-occ") || p.peekNextToken("amb-occ2"))
            {
                string tex = null;
                if (p.peekNextToken("diff") || p.peekNextToken("bright"))
                    api.parameter("bright", parseColor());
                else if (p.peekNextToken("texture"))
                    api.parameter("texture", tex = p.getNextToken());
                if (p.peekNextToken("dark"))
                {
                    api.parameter("dark", parseColor());
                    p.checkNextToken("samples");
                    api.parameter("samples", p.getNextInt());
                    p.checkNextToken("dist");
                    api.parameter("maxdist", p.getNextFloat());
                }
                if (tex == null)
                    api.shader(name, new AmbientOcclusionShader());
                else
                    api.shader(name, new TexturedAmbientOcclusionShader());
            }
            else if (p.peekNextToken("mirror"))
            {
                p.checkNextToken("refl");
                api.parameter("color", parseColor());
                api.shader(name, new MirrorShader());
            }
            else if (p.peekNextToken("glass"))
            {
                p.checkNextToken("eta");
                api.parameter("eta", p.getNextFloat());
                p.checkNextToken("color");
                api.parameter("color", parseColor());
                if (p.peekNextToken("absorbtion.distance"))
                    api.parameter("absorbtion.distance", p.getNextFloat());
                if (p.peekNextToken("absorbtion.color"))
                    api.parameter("absorbtion.color", parseColor());
                api.shader(name, new GlassShader());
            }
            else if (p.peekNextToken("shiny"))
            {
                string tex = null;
                if (p.peekNextToken("texture"))
                    api.parameter("texture", tex = p.getNextToken());
                else
                {
                    p.checkNextToken("diff");
                    api.parameter("diffuse", parseColor());
                }
                p.checkNextToken("refl");
                api.parameter("shiny", p.getNextFloat());
                if (tex == null)
                    api.shader(name, new ShinyDiffuseShader());
                else
                    api.shader(name, new TexturedShinyDiffuseShader());
            }
            else if (p.peekNextToken("ward"))
            {
                string tex = null;
                if (p.peekNextToken("texture"))
                    api.parameter("texture", tex = p.getNextToken());
                else
                {
                    p.checkNextToken("diff");
                    api.parameter("diffuse", parseColor());
                }
                p.checkNextToken("spec");
                api.parameter("specular", parseColor());
                p.checkNextToken("rough");
                api.parameter("roughnessX", p.getNextFloat());
                api.parameter("roughnessY", p.getNextFloat());
                if (p.peekNextToken("samples"))
                    api.parameter("samples", p.getNextInt());
                if (tex != null)
                    api.shader(name, new TexturedWardShader());
                else
                    api.shader(name, new AnisotropicWardShader());
            }
            else if (p.peekNextToken("view-caustics"))
            {
                api.shader(name, new ViewCausticsShader());
            }
            else if (p.peekNextToken("view-irradiance"))
            {
                api.shader(name, new ViewIrradianceShader());
            }
            else if (p.peekNextToken("view-global"))
            {
                api.shader(name, new ViewGlobalPhotonsShader());
            }
            else if (p.peekNextToken("constant"))
            {
                // backwards compatibility -- peek only
                p.peekNextToken("color");
                api.parameter("color", parseColor());
                api.shader(name, new ConstantShader());
            }
            else if (p.peekNextToken("janino"))
            {
                string code = p.getNextCodeBlock();
                try
                {
                    IShader shader = null;//fixme:(Shader) ClassBodyEvaluator.createFastClassBodyEvaluator(new Scanner(null, new stringReader(code)), Shader.class, ClassLoader.getSystemClassLoader());
                    api.shader(name, shader);
                }
                catch (Exception e)
                {
                    UI.printDetailed(UI.Module.API, "Compiling: {0}", code);
                    UI.printError(UI.Module.API, "{0}", e);
                    return false;
                }
            }
            else if (p.peekNextToken("id"))
            {
                api.shader(name, new IDShader());
            }
            else if (p.peekNextToken("uber"))
            {
                if (p.peekNextToken("diff"))
                    api.parameter("diffuse", parseColor());
                if (p.peekNextToken("diff.texture"))
                    api.parameter("diffuse.texture", p.getNextToken());
                if (p.peekNextToken("diff.blend"))
                    api.parameter("diffuse.blend", p.getNextFloat());
                if (p.peekNextToken("refl") || p.peekNextToken("spec"))
                    api.parameter("specular", parseColor());
                if (p.peekNextToken("texture"))
                {
                    // deprecated
                    UI.printWarning(UI.Module.API, "Deprecated uber shader parameter \"texture\" - please use \"diffuse.texture\" and \"diffuse.blend\" instead");
                    api.parameter("diffuse.texture", p.getNextToken());
                    api.parameter("diffuse.blend", p.getNextFloat());
                }
                if (p.peekNextToken("spec.texture"))
                    api.parameter("specular.texture", p.getNextToken());
                if (p.peekNextToken("spec.blend"))
                    api.parameter("specular.blend", p.getNextFloat());
                if (p.peekNextToken("glossy"))
                    api.parameter("glossyness", p.getNextFloat());
                if (p.peekNextToken("samples"))
                    api.parameter("samples", p.getNextInt());
                api.shader(name, new UberShader());
            }
            else
                UI.printWarning(UI.Module.API, "Unrecognized shader type: {0}", p.getNextToken());
            p.checkNextToken("}");
            return true;
        }

        private bool parseModifier(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("name");
            string name = p.getNextToken();
            UI.printInfo(UI.Module.API, "Reading shader: {0} ...", name);
            p.checkNextToken("type");
            if (p.peekNextToken("bump"))
            {
                p.checkNextToken("texture");
                api.parameter("texture", p.getNextToken());
                p.checkNextToken("scale");
                api.parameter("scale", p.getNextFloat());
                api.modifier(name, new BumpMappingModifier());
            }
            else if (p.peekNextToken("normalmap"))
            {
                p.checkNextToken("texture");
                api.parameter("texture", p.getNextToken());
                api.modifier(name, new NormalMapModifier());
            }
            else
            {
                UI.printWarning(UI.Module.API, "Unrecognized modifier type: {0}", p.getNextToken());
            }
            p.checkNextToken("}");
            return true;
        }

        private void parseObjectBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            bool noInstance = false;
            Matrix4 transform = null;
            string name = null;
            string[] shaders = null;
            string[] modifiers = null;
            if (p.peekNextToken("noinstance"))
            {
                // this indicates that the geometry is to be created, but not
                // instanced into the scene
                noInstance = true;
            }
            else
            {
                // these are the parameters to be passed to the instance
                if (p.peekNextToken("shaders"))
                {
                    int n = p.getNextInt();
                    shaders = new string[n];
                    for (int i = 0; i < n; i++)
                        shaders[i] = p.getNextToken();
                }
                else
                {
                    p.checkNextToken("shader");
                    shaders = new string[] { p.getNextToken() };
                }
                if (p.peekNextToken("modifiers"))
                {
                    int n = p.getNextInt();
                    modifiers = new string[n];
                    for (int i = 0; i < n; i++)
                        modifiers[i] = p.getNextToken();
                }
                else if (p.peekNextToken("modifier"))
                    modifiers = new string[] { p.getNextToken() };
                if (p.peekNextToken("transform"))
                    transform = parseMatrix();
            }
            if (p.peekNextToken("accel"))
                api.parameter("accel", p.getNextToken());
            p.checkNextToken("type");
            string type = p.getNextToken();
            if (p.peekNextToken("name"))
                name = p.getNextToken();
            else
                name = api.getUniqueName(type);
            if (type == "mesh")
            {
                UI.printWarning(UI.Module.API, "Deprecated object type: mesh");
                UI.printInfo(UI.Module.API, "Reading mesh: {0} ...", name);
                int numVertices = p.getNextInt();
                int numTriangles = p.getNextInt();
                float[] points = new float[numVertices * 3];
                float[] normals = new float[numVertices * 3];
                float[] uvs = new float[numVertices * 2];
                for (int i = 0; i < numVertices; i++)
                {
                    p.checkNextToken("v");
                    points[3 * i + 0] = p.getNextFloat();
                    points[3 * i + 1] = p.getNextFloat();
                    points[3 * i + 2] = p.getNextFloat();
                    normals[3 * i + 0] = p.getNextFloat();
                    normals[3 * i + 1] = p.getNextFloat();
                    normals[3 * i + 2] = p.getNextFloat();
                    uvs[2 * i + 0] = p.getNextFloat();
                    uvs[2 * i + 1] = p.getNextFloat();
                }
                int[] triangles = new int[numTriangles * 3];
                for (int i = 0; i < numTriangles; i++)
                {
                    p.checkNextToken("t");
                    triangles[i * 3 + 0] = p.getNextInt();
                    triangles[i * 3 + 1] = p.getNextInt();
                    triangles[i * 3 + 2] = p.getNextInt();
                }
                // create geometry
                api.parameter("triangles", triangles);
                api.parameter("points", "point", "vertex", points);
                api.parameter("normals", "vector", "vertex", normals);
                api.parameter("uvs", "texcoord", "vertex", uvs);
                api.geometry(name, new TriangleMesh());
            }
            else if (type == "flat-mesh")
            {
                UI.printWarning(UI.Module.API, "Deprecated object type: flat-mesh");
                UI.printInfo(UI.Module.API, "Reading flat mesh: {0} ...", name);
                int numVertices = p.getNextInt();
                int numTriangles = p.getNextInt();
                float[] points = new float[numVertices * 3];
                float[] uvs = new float[numVertices * 2];
                for (int i = 0; i < numVertices; i++)
                {
                    p.checkNextToken("v");
                    points[3 * i + 0] = p.getNextFloat();
                    points[3 * i + 1] = p.getNextFloat();
                    points[3 * i + 2] = p.getNextFloat();
                    p.getNextFloat();
                    p.getNextFloat();
                    p.getNextFloat();
                    uvs[2 * i + 0] = p.getNextFloat();
                    uvs[2 * i + 1] = p.getNextFloat();
                }
                int[] triangles = new int[numTriangles * 3];
                for (int i = 0; i < numTriangles; i++)
                {
                    p.checkNextToken("t");
                    triangles[i * 3 + 0] = p.getNextInt();
                    triangles[i * 3 + 1] = p.getNextInt();
                    triangles[i * 3 + 2] = p.getNextInt();
                }
                // create geometry
                api.parameter("triangles", triangles);
                api.parameter("points", "point", "vertex", points);
                api.parameter("uvs", "texcoord", "vertex", uvs);
                api.geometry(name, new TriangleMesh());
            }
            else if (type == "sphere")
            {
                UI.printInfo(UI.Module.API, "Reading sphere ...");
                api.geometry(name, new Sphere());
                if (transform == null && !noInstance)
                {
                    // legacy method of specifying transformation for spheres
                    p.checkNextToken("c");
                    float x = p.getNextFloat();
                    float y = p.getNextFloat();
                    float z = p.getNextFloat();
                    p.checkNextToken("r");
                    float radius = p.getNextFloat();
                    api.parameter("transform", Matrix4.translation(x, y, z).multiply(Matrix4.scale(radius)));
                    api.parameter("shaders", shaders);
                    if (modifiers != null)
                        api.parameter("modifiers", modifiers);
                    api.instance(name + ".instance", name);
                    noInstance = true; // disable future auto-instancing because
                    // instance has already been created
                }
            }
            else if (type == "banchoff")
            {
                UI.printInfo(UI.Module.API, "Reading banchoff ...");
                api.geometry(name, new BanchoffSurface());
            }
            else if (type == "torus")
            {
                UI.printInfo(UI.Module.API, "Reading torus ...");
                p.checkNextToken("r");
                api.parameter("radiusInner", p.getNextFloat());
                api.parameter("radiusOuter", p.getNextFloat());
                api.geometry(name, new Torus());
            }
            else if (type == "plane")
            {
                UI.printInfo(UI.Module.API, "Reading plane ...");
                p.checkNextToken("p");
                api.parameter("center", parsePoint());
                if (p.peekNextToken("n"))
                {
                    api.parameter("normal", parseVector());
                }
                else
                {
                    p.checkNextToken("p");
                    api.parameter("point1", parsePoint());
                    p.checkNextToken("p");
                    api.parameter("point2", parsePoint());
                }
                api.geometry(name, new Plane());
            }
            else if (type == "cornellbox")
            {
                UI.printInfo(UI.Module.API, "Reading cornell box ...");
                if (transform != null)
                    UI.printWarning(UI.Module.API, "Instancing is not supported on cornell box -- ignoring transform");
                p.checkNextToken("corner0");
                api.parameter("corner0", parsePoint());
                p.checkNextToken("corner1");
                api.parameter("corner1", parsePoint());
                p.checkNextToken("left");
                api.parameter("leftColor", parseColor());
                p.checkNextToken("right");
                api.parameter("rightColor", parseColor());
                p.checkNextToken("top");
                api.parameter("topColor", parseColor());
                p.checkNextToken("bottom");
                api.parameter("bottomColor", parseColor());
                p.checkNextToken("back");
                api.parameter("backColor", parseColor());
                p.checkNextToken("emit");
                api.parameter("radiance", parseColor());
                if (p.peekNextToken("samples"))
                    api.parameter("samples", p.getNextInt());
                new CornellBox().init(name, api);
                noInstance = true; // instancing is handled natively by the init
                // method
            }
            else if (type == "generic-mesh")
            {
                UI.printInfo(UI.Module.API, "Reading generic mesh: {0} ... ", name);
                // parse vertices
                p.checkNextToken("points");
                int np = p.getNextInt();
                api.parameter("points", "point", "vertex", parseFloatArray(np * 3));
                // parse triangle indices
                p.checkNextToken("triangles");
                int nt = p.getNextInt();
                api.parameter("triangles", parseIntArray(nt * 3));
                // parse normals
                p.checkNextToken("normals");
                if (p.peekNextToken("vertex"))
                    api.parameter("normals", "vector", "vertex", parseFloatArray(np * 3));
                else if (p.peekNextToken("facevarying"))
                    api.parameter("normals", "vector", "facevarying", parseFloatArray(nt * 9));
                else
                    p.checkNextToken("none");
                // parse texture coordinates
                p.checkNextToken("uvs");
                if (p.peekNextToken("vertex"))
                    api.parameter("uvs", "texcoord", "vertex", parseFloatArray(np * 2));
                else if (p.peekNextToken("facevarying"))
                    api.parameter("uvs", "texcoord", "facevarying", parseFloatArray(nt * 6));
                else
                    p.checkNextToken("none");
                if (p.peekNextToken("face_shaders"))
                    api.parameter("faceshaders", parseIntArray(nt));
                api.geometry(name, new TriangleMesh());
            }
            else if (type == "hair")
            {
                UI.printInfo(UI.Module.API, "Reading hair curves: {0} ... ", name);
                p.checkNextToken("segments");
                api.parameter("segments", p.getNextInt());
                p.checkNextToken("width");
                api.parameter("widths", p.getNextFloat());
                p.checkNextToken("points");
                api.parameter("points", "point", "vertex", parseFloatArray(p.getNextInt()));
                api.geometry(name, new Hair());
            }
            else if (type == "janino-tesselatable")
            {
                UI.printInfo(UI.Module.API, "Reading procedural primitive: {0} ... ", name);
                string code = p.getNextCodeBlock();
                try
                {
                    ITesselatable tess = null;//fixme:(Tesselatable) ClassBodyEvaluator.createFastClassBodyEvaluator(new Scanner(null, new stringReader(code)), Tesselatable.class, ClassLoader.getSystemClassLoader());
                    api.geometry(name, tess);
                }
                catch (Exception e)
                {
                    UI.printDetailed(UI.Module.API, "Compiling: {0}", code);
                    UI.printError(UI.Module.API, "{0}", e);
                    noInstance = true;
                }
            }
            else if (type == "teapot")
            {
                UI.printInfo(UI.Module.API, "Reading teapot: {0} ... ", name);
                bool hasTesselationArguments = false;
                if (p.peekNextToken("subdivs"))
                {
                    api.parameter("subdivs", p.getNextInt());
                    hasTesselationArguments = true;
                }
                if (p.peekNextToken("smooth"))
                {
                    api.parameter("smooth", p.getNextbool());
                    hasTesselationArguments = true;
                }
                if (hasTesselationArguments)
                    api.geometry(name, (ITesselatable)new Teapot());
                else
                    api.geometry(name, (PrimitiveList)new Teapot());
            }
            else if (type == "gumbo")
            {
                UI.printInfo(UI.Module.API, "Reading gumbo:{0} ... ", name);
                bool hasTesselationArguments = false;
                if (p.peekNextToken("subdivs"))
                {
                    api.parameter("subdivs", p.getNextInt());
                    hasTesselationArguments = true;
                }
                if (p.peekNextToken("smooth"))
                {
                    api.parameter("smooth", p.getNextbool());
                    hasTesselationArguments = true;
                }
                if (hasTesselationArguments)
                    api.geometry(name, (ITesselatable)new Gumbo());
                else
                    api.geometry(name, (PrimitiveList)new Gumbo());
            }
            else if (type == "julia")
            {
                UI.printInfo(UI.Module.API, "Reading julia fractal: {0} ... ", name);
                if (p.peekNextToken("q"))
                {
                    api.parameter("cw", p.getNextFloat());
                    api.parameter("cx", p.getNextFloat());
                    api.parameter("cy", p.getNextFloat());
                    api.parameter("cz", p.getNextFloat());
                }
                if (p.peekNextToken("iterations"))
                    api.parameter("iterations", p.getNextInt());
                if (p.peekNextToken("epsilon"))
                    api.parameter("epsilon", p.getNextFloat());
                api.geometry(name, new JuliaFractal());
            }
            else if (type == "particles" || type == "dlasurface")
            {
                if (type == "dlasurface")
                    UI.printWarning(UI.Module.API, "Deprecated object type: \"dlasurface\" - please use \"particles\" instead");
                p.checkNextToken("filename");
                string filename = p.getNextToken();
                bool littleEndian = false;
                if (p.peekNextToken("little_endian"))
                    littleEndian = true;
                UI.printInfo(UI.Module.USER, "Loading particle file: {0}", filename);
                //File file = new File(filename);
                //FileInputStream stream = new FileInputStream(filename);
                //MappedByteBuffer map = stream.getChannel().map(FileChannel.MapMode.READ_ONLY, 0, file.Length());
                //if (littleEndian)
                //    map.order(ByteOrder.LITTLE_ENDIAN);
                //FloatBuffer buffer = map.asFloatBuffer();
                BinaryReader reader = new BinaryReader(File.OpenRead(filename));
                float[] data = new float[reader.BaseStream.Length / 4];
                for (int i = 0; i < data.Length; i++)
                    data[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);//buffer.get(i);
                reader.Close();
                api.parameter("particles", "point", "vertex", data);
                if (p.peekNextToken("num"))
                    api.parameter("num", p.getNextInt());
                else
                    api.parameter("num", data.Length / 3);
                p.checkNextToken("radius");
                api.parameter("radius", p.getNextFloat());
                api.geometry(name, new ParticleSurface());
            }
            else if (type == "file-mesh")
            {
                UI.printInfo(UI.Module.API, "Reading file mesh: {0} ... ", name);
                p.checkNextToken("filename");
                api.parameter("filename", p.getNextToken());
                if (p.peekNextToken("smooth_normals"))
                    api.parameter("smooth_normals", p.getNextbool());
                api.geometry(name, new FileMesh());
            }
            else if (type == "bezier-mesh")
            {
                UI.printInfo(UI.Module.API, "Reading bezier mesh: {0} ... ", name);
                p.checkNextToken("n");
                int nu, nv;
                api.parameter("nu", nu = p.getNextInt());
                api.parameter("nv", nv = p.getNextInt());
                if (p.peekNextToken("wrap"))
                {
                    api.parameter("uwrap", p.getNextbool());
                    api.parameter("vwrap", p.getNextbool());
                }
                p.checkNextToken("points");
                float[] points = new float[3 * nu * nv];
                for (int i = 0; i < points.Length; i++)
                    points[i] = p.getNextFloat();
                api.parameter("points", "point", "vertex", points);
                if (p.peekNextToken("subdivs"))
                    api.parameter("subdivs", p.getNextInt());
                if (p.peekNextToken("smooth"))
                    api.parameter("smooth", p.getNextbool());
                api.geometry(name, (ITesselatable)new BezierMesh());
            }
            else
            {
                UI.printWarning(UI.Module.API, "Unrecognized object type: {0}", p.getNextToken());
                noInstance = true;
            }
            if (!noInstance)
            {
                // create instance
                api.parameter("shaders", shaders);
                if (modifiers != null)
                    api.parameter("modifiers", modifiers);
                if (transform != null)
                    api.parameter("transform", transform);
                api.instance(name + ".instance", name);
            }
            p.checkNextToken("}");
        }

        private void parseInstanceBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("name");
            string name = p.getNextToken();
            UI.printInfo(UI.Module.API, "Reading instance: {0} ...", name);
            p.checkNextToken("geometry");
            string geoname = p.getNextToken();
            p.checkNextToken("transform");
            api.parameter("transform", parseMatrix());
            string[] shaders;
            if (p.peekNextToken("shaders"))
            {
                int n = p.getNextInt();
                shaders = new string[n];
                for (int i = 0; i < n; i++)
                    shaders[i] = p.getNextToken();
            }
            else
            {
                p.checkNextToken("shader");
                shaders = new string[] { p.getNextToken() };
            }
            api.parameter("shaders", shaders);
            string[] modifiers = null;
            if (p.peekNextToken("modifiers"))
            {
                int n = p.getNextInt();
                modifiers = new string[n];
                for (int i = 0; i < n; i++)
                    modifiers[i] = p.getNextToken();
            }
            else if (p.peekNextToken("modifier"))
                modifiers = new string[] { p.getNextToken() };
            if (modifiers != null)
                api.parameter("modifiers", modifiers);
            api.instance(name, geoname);
            p.checkNextToken("}");
        }

        private void parseLightBlock(SunflowAPI api)
        {
            p.checkNextToken("{");
            p.checkNextToken("type");
            if (p.peekNextToken("mesh"))
            {
                UI.printWarning(UI.Module.API, "Deprecated light type: mesh");
                p.checkNextToken("name");
                string name = p.getNextToken();
                UI.printInfo(UI.Module.API, "Reading light mesh: {0} ...", name);
                p.checkNextToken("emit");
                api.parameter("radiance", parseColor());
                int samples = numLightSamples;
                if (p.peekNextToken("samples"))
                    samples = p.getNextInt();
                else
                    UI.printWarning(UI.Module.API, "Samples keyword not found - defaulting to {0}", samples);
                api.parameter("samples", samples);
                int numVertices = p.getNextInt();
                int numTriangles = p.getNextInt();
                float[] points = new float[3 * numVertices];
                int[] triangles = new int[3 * numTriangles];
                for (int i = 0; i < numVertices; i++)
                {
                    p.checkNextToken("v");
                    points[3 * i + 0] = p.getNextFloat();
                    points[3 * i + 1] = p.getNextFloat();
                    points[3 * i + 2] = p.getNextFloat();
                    // ignored
                    p.getNextFloat();
                    p.getNextFloat();
                    p.getNextFloat();
                    p.getNextFloat();
                    p.getNextFloat();
                }
                for (int i = 0; i < numTriangles; i++)
                {
                    p.checkNextToken("t");
                    triangles[3 * i + 0] = p.getNextInt();
                    triangles[3 * i + 1] = p.getNextInt();
                    triangles[3 * i + 2] = p.getNextInt();
                }
                api.parameter("points", "point", "vertex", points);
                api.parameter("triangles", triangles);
                TriangleMeshLight mesh = new TriangleMeshLight();
                mesh.init(name, api);
            }
            else if (p.peekNextToken("point"))
            {
                UI.printInfo(UI.Module.API, "Reading point light ...");
                Color pow;
                if (p.peekNextToken("color"))
                {
                    pow = parseColor();
                    p.checkNextToken("power");
                    float po = p.getNextFloat();
                    pow.mul(po);
                }
                else
                {
                    UI.printWarning(UI.Module.API, "Deprecated color specification - please use color and power instead");
                    p.checkNextToken("power");
                    pow = parseColor();
                }
                p.checkNextToken("p");
                api.parameter("center", parsePoint());
                api.parameter("power", pow);
                api.light(api.getUniqueName("pointlight"), new PointLight());
            }
            else if (p.peekNextToken("spherical"))
            {
                UI.printInfo(UI.Module.API, "Reading spherical light ...");
                p.checkNextToken("color");
                Color pow = parseColor();
                p.checkNextToken("radiance");
                pow.mul(p.getNextFloat());
                api.parameter("radiance", pow);
                p.checkNextToken("center");
                api.parameter("center", parsePoint());
                p.checkNextToken("radius");
                api.parameter("radius", p.getNextFloat());
                p.checkNextToken("samples");
                api.parameter("samples", p.getNextInt());
                SphereLight light = new SphereLight();
                light.init(api.getUniqueName("spherelight"), api);
            }
            else if (p.peekNextToken("directional"))
            {
                UI.printInfo(UI.Module.API, "Reading directional light ...");
                p.checkNextToken("source");
                Point3 s = parsePoint();
                api.parameter("source", s);
                p.checkNextToken("target");
                Point3 t = parsePoint();
                api.parameter("dir", Point3.sub(t, s, new Vector3()));
                p.checkNextToken("radius");
                api.parameter("radius", p.getNextFloat());
                p.checkNextToken("emit");
                Color e = parseColor();
                if (p.peekNextToken("intensity"))
                {
                    float i = p.getNextFloat();
                    e.mul(i);
                }
                else
                    UI.printWarning(UI.Module.API, "Deprecated color specification - please use emit and intensity instead");
                api.parameter("radiance", e);
                api.light(api.getUniqueName("dirlight"), new DirectionalSpotlight());
            }
            else if (p.peekNextToken("ibl"))
            {
                UI.printInfo(UI.Module.API, "Reading image based light ...");
                p.checkNextToken("image");
                api.parameter("texture", p.getNextToken());
                p.checkNextToken("center");
                api.parameter("center", parseVector());
                p.checkNextToken("up");
                api.parameter("up", parseVector());
                p.checkNextToken("lock");
                api.parameter("fixed", p.getNextbool());
                int samples = numLightSamples;
                if (p.peekNextToken("samples"))
                    samples = p.getNextInt();
                else
                    UI.printWarning(UI.Module.API, "Samples keyword not found - defaulting to {0}", samples);
                api.parameter("samples", samples);
                ImageBasedLight ibl = new ImageBasedLight();
                ibl.init(api.getUniqueName("ibl"), api);
            }
            else if (p.peekNextToken("meshlight"))
            {
                p.checkNextToken("name");
                string name = p.getNextToken();
                UI.printInfo(UI.Module.API, "Reading meshlight: {0} ...", name);
                p.checkNextToken("emit");
                Color e = parseColor();
                if (p.peekNextToken("radiance"))
                {
                    float r = p.getNextFloat();
                    e.mul(r);
                }
                else
                    UI.printWarning(UI.Module.API, "Deprecated color specification - please use emit and radiance instead");
                api.parameter("radiance", e);
                int samples = numLightSamples;
                if (p.peekNextToken("samples"))
                    samples = p.getNextInt();
                else
                    UI.printWarning(UI.Module.API, "Samples keyword not found - defaulting to {0}", samples);
                api.parameter("samples", samples);
                // parse vertices
                p.checkNextToken("points");
                int np = p.getNextInt();
                api.parameter("points", "point", "vertex", parseFloatArray(np * 3));
                // parse triangle indices
                p.checkNextToken("triangles");
                int nt = p.getNextInt();
                api.parameter("triangles", parseIntArray(nt * 3));
                TriangleMeshLight mesh = new TriangleMeshLight();
                mesh.init(name, api);
            }
            else if (p.peekNextToken("sunsky"))
            {
                p.checkNextToken("up");
                api.parameter("up", parseVector());
                p.checkNextToken("east");
                api.parameter("east", parseVector());
                p.checkNextToken("sundir");
                api.parameter("sundir", parseVector());
                p.checkNextToken("turbidity");
                api.parameter("turbidity", p.getNextFloat());
                if (p.peekNextToken("samples"))
                    api.parameter("samples", p.getNextInt());
                SunSkyLight sunsky = new SunSkyLight();
                sunsky.init(api.getUniqueName("sunsky"), api);
            }
            else
                UI.printWarning(UI.Module.API, "Unrecognized object type: {0}", p.getNextToken());
            p.checkNextToken("}");
        }

        private Color parseColor()
        {
            if (p.peekNextToken("{"))
            {
                string space = p.getNextToken();
                Color c = null;
                if (space == "sRGB nonlinear")
                {
                    float r = p.getNextFloat();
                    float g = p.getNextFloat();
                    float b = p.getNextFloat();
                    c = new Color(r, g, b);
                    c.toLinear();
                }
                else if (space == "sRGB linear")
                {
                    float r = p.getNextFloat();
                    float g = p.getNextFloat();
                    float b = p.getNextFloat();
                    c = new Color(r, g, b);
                }
                else
                    UI.printWarning(UI.Module.API, "Unrecognized color space: {0}", space);
                p.checkNextToken("}");
                return c;
            }
            else
            {
                float r = p.getNextFloat();
                float g = p.getNextFloat();
                float b = p.getNextFloat();
                return new Color(r, g, b);
            }
        }

        private Point3 parsePoint()
        {
            float x = p.getNextFloat();
            float y = p.getNextFloat();
            float z = p.getNextFloat();
            return new Point3(x, y, z);
        }

        private Vector3 parseVector()
        {
            float x = p.getNextFloat();
            float y = p.getNextFloat();
            float z = p.getNextFloat();
            return new Vector3(x, y, z);
        }

        private int[] parseIntArray(int size)
        {
            int[] data = new int[size];
            for (int i = 0; i < size; i++)
                data[i] = p.getNextInt();
            return data;
        }

        private float[] parseFloatArray(int size)
        {
            float[] data = new float[size];
            for (int i = 0; i < size; i++)
                data[i] = p.getNextFloat();
            return data;
        }

        private Matrix4 parseMatrix()
        {
            if (p.peekNextToken("row"))
            {
                return new Matrix4(parseFloatArray(16), true);
            }
            else if (p.peekNextToken("col"))
            {
                return new Matrix4(parseFloatArray(16), false);
            }
            else
            {
                Matrix4 m = Matrix4.IDENTITY;
                p.checkNextToken("{");
                while (!p.peekNextToken("}"))
                {
                    Matrix4 t = null;
                    if (p.peekNextToken("translate"))
                    {
                        float x = p.getNextFloat();
                        float y = p.getNextFloat();
                        float z = p.getNextFloat();
                        t = Matrix4.translation(x, y, z);
                    }
                    else if (p.peekNextToken("scaleu"))
                    {
                        float s = p.getNextFloat();
                        t = Matrix4.scale(s);
                    }
                    else if (p.peekNextToken("scale"))
                    {
                        float x = p.getNextFloat();
                        float y = p.getNextFloat();
                        float z = p.getNextFloat();
                        t = Matrix4.scale(x, y, z);
                    }
                    else if (p.peekNextToken("rotatex"))
                    {
                        float angle = p.getNextFloat();
                        t = Matrix4.rotateX((float)MathUtils.toRadians(angle));
                    }
                    else if (p.peekNextToken("rotatey"))
                    {
                        float angle = p.getNextFloat();
                        t = Matrix4.rotateY((float)MathUtils.toRadians(angle));
                    }
                    else if (p.peekNextToken("rotatez"))
                    {
                        float angle = p.getNextFloat();
                        t = Matrix4.rotateZ((float)MathUtils.toRadians(angle));
                    }
                    else if (p.peekNextToken("rotate"))
                    {
                        float x = p.getNextFloat();
                        float y = p.getNextFloat();
                        float z = p.getNextFloat();
                        float angle = p.getNextFloat();
                        t = Matrix4.rotate(x, y, z, (float)MathUtils.toRadians(angle));
                    }
                    else
                        UI.printWarning(UI.Module.API, "Unrecognized transformation type: {0}", p.getNextToken());
                    if (t != null)
                        m = t.multiply(m);
                }
                return m;
            }
        }
    }
}