using System;
using System.Diagnostics;
using SunflowSharp.Systems;

namespace SunflowSharp.Maths
{

    public class QMC
    {
        private const int NUM = 128;
        private static int[][] _SIGMA = null;
        private static int[] _PRIMES = null;
        private static object lockObj = new object();

        public static int[][] SIGMA
        {
            get
            {
                if (_SIGMA == null || _PRIMES == null)
                    lock (lockObj)//we don't want to lock every time
                        if (_SIGMA == null || _PRIMES == null)
                            BuildSigPri();
                return _SIGMA;
            }
            set
            {
                _SIGMA = value;
            }
        }

        public static int[] PRIMES
        {
            get
            {
                if (_SIGMA == null || _PRIMES == null)
                    lock (lockObj)
                        if (_SIGMA == null || _PRIMES == null) 
                            BuildSigPri();
                return _PRIMES;
            }
            set
            {
                _PRIMES = value;
            }
        }

        private static void BuildSigPri()
        {
            UI.printInfo(UI.Module.QMC, "Initializing Faure scrambling tables ...");
            SIGMA = new int[NUM][];
            PRIMES = new int[NUM];

            // build table of first primes
            PRIMES[0] = 2;
            for (int i = 1; i < PRIMES.Length; i++)
                PRIMES[i] = nextPrime(PRIMES[i - 1]);
            int[][] table = new int[PRIMES[PRIMES.Length - 1] + 1][];
            table[2] = new int[2];
            table[2][0] = 0;
            table[2][1] = 1;
            for (int i = 3; i <= PRIMES[PRIMES.Length - 1]; i++)
            {
                table[i] = new int[i];
                if ((i & 1) == 0)
                {
                    int[] prev = table[i >> 1];
                    for (int j = 0; j < prev.Length; j++)
                        table[i][j] = 2 * prev[j];
                    for (int j = 0; j < prev.Length; j++)
                        table[i][prev.Length + j] = 2 * prev[j] + 1;
                }
                else
                {
                    int[] prev = table[i - 1];
                    int med = (i - 1) >> 1;
                    for (int j = 0; j < med; j++)
                        table[i][j] = prev[j] + ((prev[j] >= med) ? 1 : 0);
                    table[i][med] = med;
                    for (int j = 0; j < med; j++)
                        table[i][med + j + 1] = prev[j + med] + ((prev[j + med] >= med) ? 1 : 0);
                }
            }
            for (int i = 0; i < PRIMES.Length; i++)
            {
                int p = PRIMES[i];
                SIGMA[i] = new int[p];
                Buffer.BlockCopy(table[p], 0, SIGMA[i], 0, p);
            }
        }
        //static {
        //    UI.printInfo(UI.Module.QMC, "Initializing Faure scrambling tables ...");
        //    // build table of first primes
        //    PRIMES[0] = 2;
        //    for (int i = 1; i < PRIMES.Length; i++)
        //        PRIMES[i] = nextPrime(PRIMES[i - 1]);
        //    int[][] table = new int[PRIMES[PRIMES.Length - 1] + 1][];
        //    table[2] = new int[2];
        //    table[2][0] = 0;
        //    table[2][1] = 1;
        //    for (int i = 3; i <= PRIMES[PRIMES.Length - 1]; i++) {
        //        table[i] = new int[i];
        //        if ((i & 1) == 0) {
        //            int[] prev = table[i >> 1];
        //            for (int j = 0; j < prev.Length; j++)
        //                table[i][j] = 2 * prev[j];
        //            for (int j = 0; j < prev.Length; j++)
        //                table[i][prev.Length + j] = 2 * prev[j] + 1;
        //        } else {
        //            int[] prev = table[i - 1];
        //            int med = (i - 1) >> 1;
        //            for (int j = 0; j < med; j++)
        //                table[i][j] = prev[j] + ((prev[j] >= med) ? 1 : 0);
        //            table[i][med] = med;
        //            for (int j = 0; j < med; j++)
        //                table[i][med + j + 1] = prev[j + med] + ((prev[j + med] >= med) ? 1 : 0);
        //        }
        //    }
        //    for (int i = 0; i < PRIMES.Length; i++) {
        //        int p = PRIMES[i];
        //        SIGMA[i] = new int[p];
        //        System.arraycopy(table[p], 0, SIGMA[i], 0, p);
        //    }
        //}

        private static int nextPrime(int p)
        {
            p = p + (p & 1) + 1;
            while (true)
            {
                int div = 3;
                bool isPrime = true;
                while (isPrime && ((div * div) <= p))
                {
                    isPrime = ((p % div) != 0);
                    div += 2;
                }
                if (isPrime)
                    return p;
                p += 2;
            }
        }

        //http://java.sun.com/docs/books/tutorial/java/nutsandbolts/op3.html, lookup >>> operator
        public static double riVDC(int bits, int r)
        {
            bits = (int)((bits << 16) | ((uint)bits >> 16));//((uint)bits >>> 16);
            bits = (int)(((bits & 0x00ff00ff) << 8) | (((uint)bits & 0xff00ff00) >> 8));//>>>
            bits = (int)(((bits & 0x0f0f0f0f) << 4) | (((uint)bits & 0xf0f0f0f0) >> 4));//>>>
            bits = (int)(((bits & 0x33333333) << 2) | (((uint)bits & 0xcccccccc) >> 2));//>>>
            bits = (int)(((bits & 0x55555555) << 1) | (((uint)bits & 0xaaaaaaaa) >> 1));//>>>
            bits ^= r;
            return (double)(bits & 0xFFFFFFFFL) / (double)0x100000000L;
        }

        public static double riS(int i, int r)
        {
            for (int v = 1 << 31; i != 0; i >>= 1, v ^= (int)((uint)v >> 1))//>>>
                if ((i & 1) != 0)
                    r ^= v;
            return (double)r / (double)0x100000000L;
        }

        public static double riLP(int i, int r)
        {
            for (int v = 1 << 31; i != 0; i >>= 1, v |= (int)((uint)v >> 1))//>>>
                if ((i & 1) != 0)
                    r ^= v;
            return (double)r / (double)0x100000000L;
        }

        public static double halton(int d, int i)
        {
            // generalized Halton sequence
            lock (lockObj)
            {
                switch (d)
                {
                    case 0:
                        {
                            i = (int)((i << 16) | ((uint)i >> 16));//>>>
                            i = (int)(((i & 0x00ff00ff) << 8) | ((uint)(i & 0xff00ff00) >> 8));//>>>
                            i = (int)(((i & 0x0f0f0f0f) << 4) | ((uint)(i & 0xf0f0f0f0) >> 4));//>>>
                            i = (int)(((i & 0x33333333) << 2) | ((uint)(i & 0xcccccccc) >> 2));//>>>
                            i = (int)(((i & 0x55555555) << 1) | ((uint)(i & 0xaaaaaaaa) >> 1));//>>>
                            return (double)(i & 0xFFFFFFFFL) / (double)0x100000000L;
                        }
                    case 1:
                        {
                            double v = 0;
                            double inv = 1.0 / 3;
                            double p;
                            int n;
                            for (p = inv, n = i; n != 0; p *= inv, n /= 3)
                                v += (n % 3) * p;
                            return v;
                        }
                    default: break;
                }
                int basei = PRIMES[d];
                int[] perm = SIGMA[d];
                double v1 = 0;
                double inv1 = 1.0 / basei;
                double p1;
                int n1;
                for (p1 = inv1, n1 = i; n1 != 0; p1 *= inv1, n1 /= basei)
                    v1 += perm[n1 % basei] * p1;
                return v1;
            }
        }

        public static double mod1(double x)
        {
            // assumes x >= 0
            return x - (int)x;
        }

        public static int[] generateSigmaTable(int n)
        {
            Debug.Assert((n & (n - 1)) == 0);
            int[] sigma = new int[n];
            for (int i = 0; i < n; i++)
            {
                int digit = n;
                sigma[i] = 0;
                for (int bits = i; bits != 0; bits >>= 1)
                {
                    digit >>= 1;
                    if ((bits & 1) != 0)
                        sigma[i] += digit;
                }
            }
            return sigma;
        }
    }
}