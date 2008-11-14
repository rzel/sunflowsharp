using System;
using SunflowSharp.Core;
using SunflowSharp.Systems;

namespace SunflowSharp.Core.Bucket
{
    public class BucketOrderFactory
    {
        public static BucketOrder create(string order)
        {
            bool flip = false;
            if (order.StartsWith("inverse") || order.StartsWith("invert") || order.StartsWith("reverse"))
            {
                string[] tokens = order.Split(StringConsts.Whitespace, StringSplitOptions.RemoveEmptyEntries);//"\\s+");
                if (tokens.Length == 2)
                {
                    order = tokens[1];
                    flip = true;
                }
            }
            BucketOrder o = null;
            if (order == "row")
                o = new RowBucketOrder();
            else if (order == "column")
                o = new ColumnBucketOrder();
            else if (order == "diagonal")
                o = new DiagonalBucketOrder();
            else if (order == "spiral")
                o = new SpiralBucketOrder();
            else if (order == "hilbert")
                o = new HilbertBucketOrder();
            else if (order == "random")
                o = new RandomBucketOrder();
            if (o == null)
            {
                UI.printWarning(UI.Module.BCKT, "Unrecognized bucket ordering: \"%s\" - using hilbert", order);
                return new HilbertBucketOrder();
            }
            else
            {
                if (flip)
                    o = new InvertedBucketOrder(o);
                return o;
            }
        }
    }
}