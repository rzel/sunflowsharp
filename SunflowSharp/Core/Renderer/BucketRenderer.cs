using System;
using SunflowSharp.Core;
using SunflowSharp.Core.Bucket;
using SunflowSharp.Core.Filter;
using SunflowSharp.Image;
using SunflowSharp.Maths;
using SunflowSharp.Systems;
using System.Threading;

namespace SunflowSharp.Core.Renderer
{
    public class BucketRenderer : ImageSampler
    {
        private Scene scene;
        private IDisplay display;
        // resolution
        private int imageWidth;
        private int imageHeight;
        // bucketing
        private string bucketOrderName;
        private BucketOrder bucketOrder;
        private int bucketSize;
        private int bucketCounter;
        private int[] bucketCoords;
        private bool dumpBuckets;

        // anti-aliasing
        private int minAADepth;
        private int maxAADepth;
        private int superSampling;
        private float contrastThreshold;
        private bool jitter;
        private bool displayAA;

        // derived quantities
        private double invSuperSampling;
        private int subPixelSize;
        private int minStepSize;
        private int maxStepSize;
        private int[] sigma;
        private float thresh;
        private bool useJitter;

        // filtering
        private string filterName;
        private IFilter filter;
        private int fs;
        private float fhs;

        public BucketRenderer()
        {
            bucketSize = 32;
            bucketOrderName = "hilbert";
            displayAA = false;
            contrastThreshold = 0.1f;
            filterName = "box";
            jitter = false; // off by default
            dumpBuckets = false; // for debugging only - not user settable
        }

        public bool prepare(Options options, Scene scene, int w, int h)
        {
            this.scene = scene;
            imageWidth = w;
            imageHeight = h;

            // fetch options
            bucketSize = options.getInt("bucket.size", bucketSize);
            bucketOrderName = options.getstring("bucket.order", bucketOrderName);
            minAADepth = options.getInt("aa.min", minAADepth);
            maxAADepth = options.getInt("aa.max", maxAADepth);
            superSampling = options.getInt("aa.samples", superSampling);
            displayAA = options.getbool("aa.display", displayAA);
            jitter = options.getbool("aa.jitter", jitter);
            contrastThreshold = options.getFloat("aa.contrast", contrastThreshold);

            // limit bucket size and compute number of buckets in each direction
            bucketSize = MathUtils.clamp(bucketSize, 16, 512);
            int numBucketsX = (imageWidth + bucketSize - 1) / bucketSize;
            int numBucketsY = (imageHeight + bucketSize - 1) / bucketSize;
            bucketOrder = BucketOrderFactory.create(bucketOrderName);
            bucketCoords = bucketOrder.getBucketSequence(numBucketsX, numBucketsY);
            // validate AA options
            minAADepth = MathUtils.clamp(minAADepth, -4, 5);
            maxAADepth = MathUtils.clamp(maxAADepth, minAADepth, 5);
            superSampling = MathUtils.clamp(superSampling, 1, 256);
            invSuperSampling = 1.0 / superSampling;
            // compute AA stepping sizes
            subPixelSize = (maxAADepth > 0) ? (1 << maxAADepth) : 1;
            minStepSize = maxAADepth >= 0 ? 1 : 1 << (-maxAADepth);
            if (minAADepth == maxAADepth)
                maxStepSize = minStepSize;
            else
                maxStepSize = minAADepth > 0 ? 1 << minAADepth : subPixelSize << (-minAADepth);
            useJitter = jitter && maxAADepth > 0;
            // compute anti-aliasing contrast thresholds
            contrastThreshold = MathUtils.clamp(contrastThreshold, 0, 1);
            thresh = contrastThreshold * (float)Math.Pow(2.0f, minAADepth);
            // read filter settings from scene
            filterName = options.getstring("filter", filterName);
            filter = FilterFactory.get(filterName);
            // adjust filter
            if (filter == null)
            {
                UI.printWarning(UI.Module.BCKT, "Unrecognized filter type: \"{0}\" - defaulting to box", filterName);
                filter = new BoxFilter(1);
                filterName = "box";
            }
            fhs = filter.getSize() * 0.5f;
            fs = (int)Math.Ceiling(subPixelSize * (fhs - 0.5f));

            // prepare QMC sampling
            sigma = QMC.generateSigmaTable(subPixelSize << 7);
            UI.printInfo(UI.Module.BCKT, "Bucket renderer settings:");
            UI.printInfo(UI.Module.BCKT, "  * Resolution:         {0}x{1}", imageWidth, imageHeight);
            UI.printInfo(UI.Module.BCKT, "  * Bucket size:        {0}", bucketSize);
            UI.printInfo(UI.Module.BCKT, "  * Number of buckets:  {0}x{1}", numBucketsX, numBucketsY);
            if (minAADepth != maxAADepth)
                UI.printInfo(UI.Module.BCKT, "  * Anti-aliasing:      {0} -> {1} (adaptive)", aaDepthTostring(minAADepth), aaDepthTostring(maxAADepth));
            else
                UI.printInfo(UI.Module.BCKT, "  * Anti-aliasing:      {0} (fixed)", aaDepthTostring(minAADepth));
            UI.printInfo(UI.Module.BCKT, "  * Rays per sample:    {0}", superSampling);
            UI.printInfo(UI.Module.BCKT, "  * Subpixel jitter:    {0}", useJitter ? "on" : (jitter ? "auto-off" : "off"));
            UI.printInfo(UI.Module.BCKT, "  * Contrast threshold: {0}", contrastThreshold);
            UI.printInfo(UI.Module.BCKT, "  * Filter type:        {0}", filterName);
            UI.printInfo(UI.Module.BCKT, "  * Filter size:        {0} pixels", filter.getSize());
            return true;
        }

        private string aaDepthTostring(int depth)
        {
            int pixelAA = (depth) < 0 ? -(1 << (-depth)) : (1 << depth);
            return string.Format("{0}{1} sample{2}", depth < 0 ? "1/" : "", pixelAA * pixelAA, depth == 0 ? "" : "s");
        }

        public void render(IDisplay display)
        {
            this.display = display;
            display.imageBegin(imageWidth, imageHeight, bucketSize);
            // set members variables
            bucketCounter = 0;
            // start task
            UI.taskStart("Rendering", 0, bucketCoords.Length);
            Systems.Timer timer = new Systems.Timer();
            timer.start();
            BucketThread[] renderThreads = new BucketThread[scene.getThreads()];
            for (int i = 0; i < renderThreads.Length; i++)
            {
                renderThreads[i] = new BucketThread(i, this);
                renderThreads[i].setPriority(scene.getThreadPriority());
                renderThreads[i].start();
            }
            for (int i = 0; i < renderThreads.Length; i++)
            {
                try
                {
                    renderThreads[i].join();
                }
                catch (Exception e)
                {
                    UI.printError(UI.Module.BCKT, "Bucket processing thread {0} of {1} was interrupted", i + 1, renderThreads.Length);
                }
            }
            UI.taskStop();
            timer.end();
            UI.printInfo(UI.Module.BCKT, "Render time: {0}", timer.ToString());
            display.imageEnd();
        }

        public class BucketThread// : Thread {
        {
            private int threadID;
            private BucketRenderer renderer;
            private Thread thread;

            public BucketThread(int threadID, BucketRenderer renderer)
            {
                this.threadID = threadID;
                this.renderer = renderer;
                thread = new Thread(new ThreadStart(run));
                thread.IsBackground = true;
            }

            public void run()
            {
                IntersectionState istate = new IntersectionState();
                while (true)
                {
                    int bx, by;
                    lock (renderer)//synchronized (renderer) {
                    {
                        if (renderer.bucketCounter >= renderer.bucketCoords.Length)
                            return;
                        UI.taskUpdate(renderer.bucketCounter);
                        bx = renderer.bucketCoords[renderer.bucketCounter + 0];
                        by = renderer.bucketCoords[renderer.bucketCounter + 1];
                        renderer.bucketCounter += 2;
                    }
                    renderer.renderBucket(renderer.display, bx, by, threadID, istate);
                    if (UI.taskCanceled())
                        return;
                }
            }

            public void setPriority(ThreadPriority prior)
            {
                thread.Priority = prior;
            }

            public void start()
            {
                thread.Start();
            }

            public void stop()
            {
                thread.Abort();
            }

            public void join()
            {
                thread.Join();
            }
        }

        private void renderBucket(IDisplay display, int bx, int by, int threadID, IntersectionState istate)
        {
            // pixel sized extents
            int x0 = bx * bucketSize;
            int y0 = by * bucketSize;
            int bw = Math.Min(bucketSize, imageWidth - x0);
            int bh = Math.Min(bucketSize, imageHeight - y0);

            // prepare bucket
            display.imagePrepare(x0, y0, bw, bh, threadID);

            Color[] bucketRGB = new Color[bw * bh];

            // subpixel extents
            int sx0 = x0 * subPixelSize - fs;
            int sy0 = y0 * subPixelSize - fs;
            int sbw = bw * subPixelSize + fs * 2;
            int sbh = bh * subPixelSize + fs * 2;

            // round up to align with maximum step size
            sbw = (sbw + (maxStepSize - 1)) & (~(maxStepSize - 1));
            sbh = (sbh + (maxStepSize - 1)) & (~(maxStepSize - 1));
            // extra padding as needed
            if (maxStepSize > 1)
            {
                sbw++;
                sbh++;
            }
            // allocate bucket memory
            ImageSample[] samples = new ImageSample[sbw * sbh];
            // allocate samples and compute jitter offsets
            float invSubPixelSize = 1.0f / subPixelSize;
            for (int y = 0, index = 0; y < sbh; y++)
            {
                for (int x = 0; x < sbw; x++, index++)
                {
                    int sx = sx0 + x;
                    int sy = sy0 + y;
                    int j = sx & (sigma.Length - 1);
                    int k = sy & (sigma.Length - 1);
                    int i = j * sigma.Length + sigma[k];
                    float dx = useJitter ? (float)sigma[k] / (float)sigma.Length : 0.5f;
                    float dy = useJitter ? (float)sigma[j] / (float)sigma.Length : 0.5f;
                    float rx = (sx + dx) * invSubPixelSize;
                    float ry = (sy + dy) * invSubPixelSize;
                    ry = imageHeight - ry - 1;
                    samples[index] = new ImageSample(rx, ry, i);
                }
            }
            for (int x = 0; x < sbw - 1; x += maxStepSize)
                for (int y = 0; y < sbh - 1; y += maxStepSize)
                    refineSamples(samples, sbw, x, y, maxStepSize, thresh, istate);
            if (dumpBuckets)
            {
                UI.printInfo(UI.Module.BCKT, "Dumping bucket [{0}, {1}] to file ...", bx, by);
                Bitmap bitmap = new Bitmap(sbw, sbh, true);
                for (int y = sbh - 1, index = 0; y >= 0; y--)
                    for (int x = 0; x < sbw; x++, index++)
                        bitmap.setPixel(x, y, samples[index].c.copy().toNonLinear());
                bitmap.save(string.Format("bucket_{0}_{1}.png", bx, by));
            }
            if (displayAA)
            {
                // color coded image of what is visible
                float invArea = invSubPixelSize * invSubPixelSize;
                for (int y = 0, index = 0; y < bh; y++)
                {
                    for (int x = 0; x < bw; x++, index++)
                    {
                        int sampled = 0;
                        for (int i = 0; i < subPixelSize; i++)
                        {
                            for (int j = 0; j < subPixelSize; j++)
                            {
                                int sx = x * subPixelSize + fs + i;
                                int sy = y * subPixelSize + fs + j;
                                int s = sx + sy * sbw;
                                sampled += samples[s].sampled() ? 1 : 0;
                            }
                        }
                        bucketRGB[index] = new Color(sampled * invArea);
                    }
                }
            }
            else
            {
                // filter samples into pixels
                float cy = imageHeight - 1 - (y0 + 0.5f);
                for (int y = 0, index = 0; y < bh; y++, cy--)
                {
                    float cx = x0 + 0.5f;
                    for (int x = 0; x < bw; x++, index++, cx++)
                    {
                        Color c = Color.black();
                        float weight = 0.0f;
                        for (int j = -fs, sy = y * subPixelSize; j <= fs; j++, sy++)
                        {
                            for (int i = -fs, sx = x * subPixelSize, s = sx + sy * sbw; i <= fs; i++, sx++, s++)
                            {
                                float dx = samples[s].rx - cx;
                                if (Math.Abs(dx) > fhs)
                                    continue;
                                float dy = samples[s].ry - cy;
                                if (Math.Abs(dy) > fhs)
                                    continue;
                                float f = filter.get(dx, dy);
                                c.madd(f, samples[s].c);
                                weight += f;
                            }
                        }
                        c.mul(1.0f / weight);
                        bucketRGB[index] = c;
                    }
                }
            }
            // update pixels
            display.imageUpdate(x0, y0, bw, bh, bucketRGB);
        }

        private void computeSubPixel(ImageSample sample, IntersectionState istate)
        {
            float x = sample.rx;
            float y = sample.ry;
            double q0 = QMC.halton(1, sample.i);
            double q1 = QMC.halton(2, sample.i);
            double q2 = QMC.halton(3, sample.i);
            if (superSampling > 1)
            {
                // multiple sampling
                sample.add(scene.getRadiance(istate, x, y, q1, q2, q0, sample.i));
                for (int i = 1; i < superSampling; i++)
                {
                    double time = QMC.mod1(q0 + i * invSuperSampling);
                    double lensU = QMC.mod1(q1 + QMC.halton(0, i));
                    double lensV = QMC.mod1(q2 + QMC.halton(1, i));
                    sample.add(scene.getRadiance(istate, x, y, lensU, lensV, time, sample.i + i));
                }
                sample.scale((float)invSuperSampling);
            }
            else
            {
                // single sample
                sample.set(scene.getRadiance(istate, x, y, q1, q2, q0, sample.i));
            }
        }

        private void refineSamples(ImageSample[] samples, int sbw, int x, int y, int stepSize, float thresh, IntersectionState istate)
        {
            int dx = stepSize;
            int dy = stepSize * sbw;
            int i00 = x + y * sbw;
            ImageSample s00 = samples[i00];
            ImageSample s01 = samples[i00 + dy];
            ImageSample s10 = samples[i00 + dx];
            ImageSample s11 = samples[i00 + dx + dy];
            if (!s00.sampled())
                computeSubPixel(s00, istate);
            if (!s01.sampled())
                computeSubPixel(s01, istate);
            if (!s10.sampled())
                computeSubPixel(s10, istate);
            if (!s11.sampled())
                computeSubPixel(s11, istate);
            if (stepSize > minStepSize)
            {
                if (s00.isDifferent(s01, thresh) || s00.isDifferent(s10, thresh) || s00.isDifferent(s11, thresh) || s01.isDifferent(s11, thresh) || s10.isDifferent(s11, thresh) || s01.isDifferent(s10, thresh))
                {
                    stepSize >>= 1;
                    thresh *= 2;
                    refineSamples(samples, sbw, x, y, stepSize, thresh, istate);
                    refineSamples(samples, sbw, x + stepSize, y, stepSize, thresh, istate);
                    refineSamples(samples, sbw, x, y + stepSize, stepSize, thresh, istate);
                    refineSamples(samples, sbw, x + stepSize, y + stepSize, stepSize, thresh, istate);
                    return;
                }
            }

            // interpolate remaining samples
            float ds = 1.0f / stepSize;
            for (int i = 0; i <= stepSize; i++)
                for (int j = 0; j <= stepSize; j++)
                    if (!samples[x + i + (y + j) * sbw].processed())
                        ImageSample.bilerp(samples[x + i + (y + j) * sbw], s00, s01, s10, s11, i * ds, j * ds);
        }

        private class ImageSample
        {
            public float rx, ry;
            public int i, n;
            public Color c;
            public Instance instance;
            public IShader shader;
            public float nx, ny, nz;

            public ImageSample(float rx, float ry, int i)
            {
                this.rx = rx;
                this.ry = ry;
                this.i = i;
                n = 0;
                c = null;
                instance = null;
                shader = null;
                nx = ny = nz = 1;
            }

            public void set(ShadingState state)
            {
                if (state == null)
                    c = Color.BLACK;
                else
                {
                    c = state.getResult();
                    checkNanInf();
                    shader = state.getShader();
                    instance = state.getInstance();
                    if (state.getNormal() != null)
                    {
                        nx = state.getNormal().x;
                        ny = state.getNormal().y;
                        nz = state.getNormal().z;
                    }
                }
                n = 1;
            }

            public void add(ShadingState state)
            {
                if (n == 0)
                    c = Color.black();
                if (state != null)
                {
                    c.add(state.getResult());
                    checkNanInf();
                }
                n++;
            }

            void checkNanInf()
            {
                if (c.isNan())
                    UI.printError(UI.Module.BCKT, "NaN shading sample!");
                else if (c.isInf())
                    UI.printError(UI.Module.BCKT, "Inf shading sample!");

            }

            public void scale(float s)
            {
                c.mul(s);
            }

            public bool processed()
            {
                return c != null;
            }

            public bool sampled()
            {
                return n > 0;
            }

            public bool isDifferent(ImageSample sample, float thresh)
            {
                if (instance != sample.instance)
                    return true;
                if (shader != sample.shader)
                    return true;
                if (Color.hasContrast(c, sample.c, thresh))
                    return true;
                // only compare normals if this pixel has not been averaged
                float dot = (nx * sample.nx + ny * sample.ny + nz * sample.nz);
                return dot < 0.9f;
            }

            public static ImageSample bilerp(ImageSample result, ImageSample i00, ImageSample i01, ImageSample i10, ImageSample i11, float dx, float dy)
            {
                float k00 = (1.0f - dx) * (1.0f - dy);
                float k01 = (1.0f - dx) * dy;
                float k10 = dx * (1.0f - dy);
                float k11 = dx * dy;
                Color c00 = i00.c;
                Color c01 = i01.c;
                Color c10 = i10.c;
                Color c11 = i11.c;
                Color c = Color.mul(k00, c00);
                c.madd(k01, c01);
                c.madd(k10, c10);
                c.madd(k11, c11);
                result.c = c;
                return result;
            }
        }
    }
}