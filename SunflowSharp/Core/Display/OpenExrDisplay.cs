using System;
using System.IO;
using SunflowSharp.Core;
using SunflowSharp.Image;
using SunflowSharp.Systems;
using SunflowSharp.Systems.Ui;

namespace SunflowSharp.Core.Display
{
    /**
     * This display outputs a tiled OpenEXR file with RGB information.
     */
    public class OpenExrDisplay : IDisplay
    {
        private static byte HALF = 1;
        private static byte FLOAT = 2;
        private static int HALF_SIZE = 2;
        private static int FLOAT_SIZE = 4;

        private static int OE_MAGIC = 20000630;
        private static int OE_EXR_VERSION = 2;
        private static int OE_TILED_FLAG = 0x00000200;

        private static int NO_COMPRESSION = 0;
        private static int RLE_COMPRESSION = 1;
        // private static int ZIPS_COMPRESSION = 2;
        private static int ZIP_COMPRESSION = 3;
        // private static int PIZ_COMPRESSION = 4;
        // private static int PXR24_COMPRESSION = 5;

        private static int RLE_MIN_RUN = 3;
        private static int RLE_MAX_RUN = 127;

        private string filename;
        private BinaryWriter file;
        private long[][] tileOffsets;
        private long tileOffsetsPosition;
        private int tilesX;
        private int tilesY;
        private int tileSize;
        private int compression;
        private byte channelType;
        private int channelSize;
        private byte[] tmpbuf;
        private byte[] comprbuf;
        private object lockObj = new object();

        public OpenExrDisplay(string filename, string compression, string channelType)
        {
            this.filename = filename == null ? "output.exr" : filename;
            if (compression == null || compression == "none")
                this.compression = NO_COMPRESSION;
            else if (compression == "rle")
                this.compression = RLE_COMPRESSION;
            else if (compression == "zip")
                this.compression = ZIP_COMPRESSION;
            else
            {
                UI.printWarning(UI.Module.DISP, "EXR - Compression type was not recognized - defaulting to zip");
                this.compression = ZIP_COMPRESSION;
            }
            if (channelType != null && channelType == "float")
            {
                this.channelType = FLOAT;
                this.channelSize = FLOAT_SIZE;
            }
            else if (channelType != null && channelType == "half")
            {
                this.channelType = HALF;
                this.channelSize = HALF_SIZE;
            }
            else
            {
                UI.printWarning(UI.Module.DISP, "EXR - Channel type was not recognized - defaulting to float");
                this.channelType = FLOAT;
                this.channelSize = FLOAT_SIZE;
            }
        }

        public void setGamma(float gamma)
        {
            UI.printWarning(UI.Module.DISP, "EXR - Gamma correction unsupported - ignoring");
        }

        public void imageBegin(int w, int h, int bucketSize)
        {
            try
            {
                file = new RandomAccessFile(filename, "rw");
                file.setLength(0);
                if (bucketSize <= 0)
                    throw new Exception("Can't use OpenEXR display without buckets.");
                writeRGBHeader(w, h, bucketSize);
            }
            catch (Exception e)
            {
                UI.printError(UI.Module.DISP, "EXR - {0}", e);
            }
        }

        public void imagePrepare(int x, int y, int w, int h, int id)
        {
        }

        public void imageUpdate(int x, int y, int w, int h, Color[] data)
        {
            lock (lockObj)
            {
                try
                {
                    // figure out which openexr tile corresponds to this bucket
                    int tx = x / tileSize;
                    int ty = y / tileSize;
                    writeTile(tx, ty, w, h, data);
                }
                catch (IOException e)
                {
                    UI.printError(UI.Module.DISP, "EXR - {0}", e);
                }
            }
        }

        public void imageFill(int x, int y, int w, int h, Color c)
        {
        }

        public void imageEnd()
        {
            try
            {
                writeTileOffsets();
                file.close();
            }
            catch (Exception e)
            {
                UI.printError(UI.Module.DISP, "EXR - {0}", e);
                Console.WriteLine(e);
            }
        }

        public void writeRGBHeader(int w, int h, int tileSize)
        {
            byte[] chanOut = { 0, channelType, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1,
                0, 0, 0 };

            file.Write(ByteUtil.get4Bytes(OE_MAGIC));

            file.Write(ByteUtil.get4Bytes(OE_EXR_VERSION | OE_TILED_FLAG));

            file.Write("channels".getBytes());
            file.Write((byte)0);
            file.Write("chlist".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(55));
            file.Write("R".getBytes());
            file.Write(chanOut);
            file.Write("G".getBytes());
            file.Write(chanOut);
            file.Write("B".getBytes());
            file.Write(chanOut);
            file.Write((byte)0);

            // compression
            file.Write("compression".getBytes());
            file.Write((byte)0);
            file.Write("compression".getBytes());
            file.Write((byte)0);
            file.Write((byte)1);
            file.Write(ByteUtil.get4BytesInv(compression));

            // datawindow =~ image size
            file.Write("dataWindow".getBytes());
            file.Write((byte)0);
            file.Write("box2i".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(0x10));
            file.Write(ByteUtil.get4Bytes(0));
            file.Write(ByteUtil.get4Bytes(0));
            file.Write(ByteUtil.get4Bytes(w - 1));
            file.Write(ByteUtil.get4Bytes(h - 1));

            // dispwindow -> look at openexr.com for more info
            file.Write("displayWindow".getBytes());
            file.Write((byte)0);
            file.Write("box2i".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(0x10));
            file.Write(ByteUtil.get4Bytes(0));
            file.Write(ByteUtil.get4Bytes(0));
            file.Write(ByteUtil.get4Bytes(w - 1));
            file.Write(ByteUtil.get4Bytes(h - 1));

            /*
             * lines in increasing y order = 0 decreasing would be 1
             */
            file.Write("lineOrder".getBytes());
            file.Write((byte)0);
            file.Write("lineOrder".getBytes());
            file.Write((byte)0);
            file.Write((byte)1);
            file.Write(ByteUtil.get4BytesInv(2));

            file.Write("pixelAspectRatio".getBytes());
            file.Write((byte)0);
            file.Write("float".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(4));
            file.Write(ByteUtil.get4Bytes(Float.floatToIntBits(1)));

            // meaningless to a flat (2D) image
            file.Write("screenWindowCenter".getBytes());
            file.Write((byte)0);
            file.Write("v2f".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(8));
            file.Write(ByteUtil.get4Bytes(Float.floatToIntBits(0)));
            file.Write(ByteUtil.get4Bytes(Float.floatToIntBits(0)));

            // meaningless to a flat (2D) image
            file.Write("screenWindowWidth".getBytes());
            file.Write((byte)0);
            file.Write("float".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(4));
            file.Write(ByteUtil.get4Bytes((int)Float.floatToIntBits(1)));

            this.tileSize = tileSize;

            tilesX = (int)((w + tileSize - 1) / tileSize);
            tilesY = (int)((h + tileSize - 1) / tileSize);

            /*
             * twice the space for the compressing buffer, as for ex. the compressor
             * can actually increase the size of the data :) If that happens though,
             * it is not saved into the file, but discarded
             */
            tmpbuf = new byte[tileSize * tileSize * channelSize * 3];
            comprbuf = new byte[tileSize * tileSize * channelSize * 3 * 2];

            tileOffsets = new long[tilesX, tilesY];

            file.Write("tiles".getBytes());
            file.Write((byte)0);
            file.Write("tiledesc".getBytes());
            file.Write((byte)0);
            file.Write(ByteUtil.get4Bytes(9));

            file.Write(ByteUtil.get4Bytes(tileSize));
            file.Write(ByteUtil.get4Bytes(tileSize));

            // ONE_LEVEL tiles, ROUNDING_MODE = not important
            file.Write((byte)0);

            // an attribute with a name of 0 to end the list
            file.Write((byte)0);

            // save a pointer to where the tileOffsets are stored and write dummy
            // fillers for now
            tileOffsetsPosition = file.getFilePointer();
            writeTileOffsets();
        }

        public void writeTileOffsets()
        {
            file.seek(tileOffsetsPosition);
            for (int ty = 0; ty < tilesY; ty++)
                for (int tx = 0; tx < tilesX; tx++)
                    file.write(ByteUtil.get8Bytes(tileOffsets[tx][ty]));
        }

        private void writeTile(int tileX, int tileY, int w, int h, Color[] tile)  {
        byte[] rgb = new byte[4];

        // setting comprSize to max integer so without compression things
        // don't go awry
        int pixptr = 0, writeSize = 0, comprSize = int.MaxValue;
        int tileRangeX = (tileSize < w) ? tileSize : w;
        int tileRangeY = (tileSize < h) ? tileSize : h;
        int channelBase = tileRangeX * channelSize;

        // lets see if the alignment matches, you can comment this out if
        // need be
        if ((tileSize != tileRangeX) && (tileX == 0))
            Console.Write(" bad X alignment ");
        if ((tileSize != tileRangeY) && (tileY == 0))
            Console.Write(" bad Y alignment ");

        tileOffsets[tileX][tileY] = file.getFilePointer();

        // the tile header: tile's x&y coordinate, levels x&y coordinate and
        // tilesize
        file.Write(ByteUtil.get4Bytes(tileX));
        file.Write(ByteUtil.get4Bytes(tileY));
        file.Write(ByteUtil.get4Bytes(0));
        file.Write(ByteUtil.get4Bytes(0));

        // just in case
        Arrays.fill(tmpbuf, (byte) 0);

        for (int ty = 0; ty < tileRangeY; ty++) {
            for (int tx = 0; tx < tileRangeX; tx++) {
                float[] rgbf = tile[tx + ty * tileRangeX].getRGB();
                for (int component = 0; component < 3; component++) {
                    if (channelType == FLOAT) {
                        rgb = ByteUtil.get4Bytes(ByteUtil.floatToRawIntBits(rgbf[2 - component]));
                        tmpbuf[(channelBase * component) + pixptr + 0] = rgb[0];
                        tmpbuf[(channelBase * component) + pixptr + 1] = rgb[1];
                        tmpbuf[(channelBase * component) + pixptr + 2] = rgb[2];
                        tmpbuf[(channelBase * component) + pixptr + 3] = rgb[3];
                    } else if (channelType == HALF) {
                        rgb = ByteUtil.get2Bytes(ByteUtil.floatToHalf(rgbf[2 - component]));
                        tmpbuf[(channelBase * component) + pixptr + 0] = rgb[0];
                        tmpbuf[(channelBase * component) + pixptr + 1] = rgb[1];
                    }
                }
                pixptr += channelSize;
            }
            pixptr += (tileRangeX * channelSize * 2);
        }

        writeSize = tileRangeX * tileRangeY * channelSize * 3;

        if (compression != NO_COMPRESSION)
            comprSize = compress(compression, tmpbuf, writeSize, comprbuf);

        // lastly, write the size of the tile and the tile itself
        // (compressed or not)
        if (comprSize < writeSize) {
            file.write(ByteUtil.get4Bytes(comprSize));
            file.write(comprbuf, 0, comprSize);
        } else {
            file.write(ByteUtil.get4Bytes(writeSize));
            file.write(tmpbuf, 0, writeSize);
        }
    }

        private static int compress(int tp, byte[] inBytes, int inSize, byte[] outBytes)
        {
            if (inSize == 0)
                return 0;

            int t1 = 0, t2 = (inSize + 1) / 2;
            int inPtr = 0, ret;
            byte[] tmp = new byte[inSize];

            // zip and rle treat the data first, in the same way so I'm not
            // repeating the code
            if ((tp == ZIP_COMPRESSION) || (tp == RLE_COMPRESSION))
            {
                // reorder the pixel data ~ straight from ImfZipCompressor.cpp :)
                while (true)
                {
                    if (inPtr < inSize)
                        tmp[t1++] = inBytes[inPtr++];
                    else
                        break;

                    if (inPtr < inSize)
                        tmp[t2++] = inBytes[inPtr++];
                    else
                        break;
                }

                // Predictor ~ straight from ImfZipCompressor.cpp :)
                t1 = 1;
                int p = tmp[t1 - 1];
                while (t1 < inSize)
                {
                    int d = (int)tmp[t1] - p + (128 + 256);
                    p = (int)tmp[t1];
                    tmp[t1] = (byte)d;
                    t1++;
                }
            }

            // We'll just jump from here to the wanted compress/decompress stuff if
            // need be
            switch (tp)
            {
                case ZIP_COMPRESSION:
                    Deflater def = new Deflater(Deflater.DEFAULT_COMPRESSION, false);
                    def.setInput(tmp, 0, inSize);
                    def.finish();
                    ret = def.deflate(outBytes);
                    return ret;
                case RLE_COMPRESSION:
                    return rleCompress(tmp, inSize, outBytes);
                default:
                    return -1;
            }
        }

        private static int rleCompress(byte[] inBytes, int inLen, byte[] outBytes)
        {
            int runStart = 0, runEnd = 1, outWrite = 0;
            while (runStart < inLen)
            {
                while (runEnd < inLen && inBytes[runStart] == inBytes[runEnd] && (runEnd - runStart - 1) < RLE_MAX_RUN)
                    runEnd++;
                if (runEnd - runStart >= RLE_MIN_RUN)
                {
                    // Compressable run
                    outBytes[outWrite++] = (byte)((runEnd - runStart) - 1);
                    outBytes[outWrite++] = inBytes[runStart];
                    runStart = runEnd;
                }
                else
                {
                    // Uncompressable run
                    while (runEnd < inLen && (((runEnd + 1) >= inLen || inBytes[runEnd] != inBytes[runEnd + 1]) || ((runEnd + 2) >= inLen || inBytes[runEnd + 1] != inBytes[runEnd + 2])) && (runEnd - runStart) < RLE_MAX_RUN)
                        runEnd++;
                    outBytes[outWrite++] = (byte)(runStart - runEnd);
                    while (runStart < runEnd)
                        outBytes[outWrite++] = inBytes[runStart++];
                }
                runEnd++;
            }
            return outWrite;
        }
    }
}