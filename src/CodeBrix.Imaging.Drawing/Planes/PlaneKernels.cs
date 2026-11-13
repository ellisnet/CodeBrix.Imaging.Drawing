using System;
using System.Threading.Tasks;

namespace CodeBrix.Imaging.Drawing.Planes;

//The arithmetic shared by DrawingPlane, DrawingPlaneImage, DrawingTransition and the motion-blur
//  helpers. Everything here is plain managed float/integer arithmetic over arrays, so the two
//  packages compile it to the same code and produce bit-identical results. Work is split across
//  rows only; every output value is computed by exactly one thread with a fixed operation order,
//  so the result never depends on how many threads ran or how they were scheduled.
internal static class PlaneKernels
{
    //Below this many pixels an operation runs on the calling thread - the thread hand-off would
    //  cost more than it saves
    internal const int ParallelThreshold = 16384;

    [ThreadStatic]
    private static int _maxDegreeOfParallelism;

    //Test hook: the largest number of threads the row-parallel operations started on THIS thread
    //  may use (0 or less = no limit). Results are identical for every value; the tests use it to
    //  prove exactly that.
    internal static int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set => _maxDegreeOfParallelism = value;
    }

    internal static void ForRows(int height, int pixelCount, Action<int> body)
        => ForRows(height, pixelCount, body, _maxDegreeOfParallelism);

    internal static void ForRows(int height, int pixelCount, Action<int> body, int maxDegreeOfParallelism)
    {
        if (height <= 0) { return; }
        if (maxDegreeOfParallelism == 1 || pixelCount < ParallelThreshold || height == 1)
        {
            for (int y = 0; y < height; y++) { body(y); }
            return;
        }
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : -1,
        };
        Parallel.For(0, height, options, body);
    }

    //---------------------------------------------------------------------------------------------
    //8-bit conversion

    //The library's float -> byte rule for 0..1 values: NaN and values at or below 0 give 0, values
    //  at or above 1 give 255, everything else is (byte)(value * 255 + 0.5) - round half up
    internal static byte UnitToByte(float value)
    {
        if (!(value > 0f)) { return 0; }
        if (value >= 1f) { return 255; }
        return (byte)((value * 255f) + 0.5f);
    }

    //The same rule for values already on the 0..255 scale
    internal static byte LevelToByte(float level)
    {
        if (!(level > 0f)) { return 0; }
        if (level >= 255f) { return 255; }
        return (byte)(level + 0.5f);
    }

    internal static float ByteToUnit(byte value) => value / 255f;

    internal static float Clamp01(float value)
    {
        if (!(value > 0f)) { return 0f; }
        return value >= 1f ? 1f : value;
    }

    //---------------------------------------------------------------------------------------------
    //Gaussian blur

    //Kernel radius rule: (int)(4 * sigma + 0.5) - the kernel is truncated at four standard
    //  deviations. Every step is in double: each weight exp(-0.5 * i * i / (sigma * sigma)), with
    //  sigma widened from float, the sum of the weights (added from -radius to +radius), and each
    //  weight divided by that sum. Only the normalised weight is rounded, once, to float.
    internal static float[] GaussianKernel(float sigma, out int radius)
    {
        double s = sigma;
        radius = (int)((4.0 * s) + 0.5);
        var weights = new double[(2 * radius) + 1];
        double sum = 0;
        for (int i = -radius; i <= radius; i++)
        {
            double weight = Math.Exp(-0.5 * i * i / (s * s));
            weights[i + radius] = weight;
            sum += weight;
        }
        var kernel = new float[weights.Length];
        for (int i = 0; i < kernel.Length; i++) { kernel[i] = (float)(weights[i] / sum); }
        return kernel;
    }

    //Mirror edge rule ("half-sample symmetric"): index -1 reads 0, -2 reads 1, width reads
    //  width - 1, width + 1 reads width - 2 - repeated for kernels wider than the plane
    internal static int Mirror(int i, int size)
    {
        while (i < 0 || i >= size)
        {
            if (i < 0) { i = -i - 1; }
            if (i >= size) { i = (2 * size) - i - 1; }
        }
        return i;
    }

    //Separable blur: a horizontal pass into a scratch plane, then a vertical pass into the
    //  destination. Each output value is the sum, in kernel order from -radius to +radius and
    //  starting from 0, of weight * sample. source and destination may be the same array.
    internal static void Blur(float[] source, float[] destination, int width, int height, float sigma)
    {
        int count = width * height;
        if (!(sigma > 0f))
        {
            if (!ReferenceEquals(source, destination)) { Array.Copy(source, destination, count); }
            return;
        }

        float[] kernel = GaussianKernel(sigma, out int radius);
        var across = new float[count];
        ForRows(height, count, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                float total = 0f;
                if (x >= radius && x + radius < width)
                {
                    int start = row + x - radius;
                    for (int k = 0; k < kernel.Length; k++) { total += kernel[k] * source[start + k]; }
                }
                else
                {
                    for (int k = -radius; k <= radius; k++) { total += kernel[k + radius] * source[row + Mirror(x + k, width)]; }
                }
                across[row + x] = total;
            }
        });

        ForRows(height, count, y =>
        {
            int row = y * width;
            Array.Clear(destination, row, width);
            for (int k = -radius; k <= radius; k++)
            {
                float weight = kernel[k + radius];
                int from = Mirror(y + k, height) * width;
                for (int x = 0; x < width; x++) { destination[row + x] += weight * across[from + x]; }
            }
        });
    }

    //---------------------------------------------------------------------------------------------
    //Resampling

    //Bilinear sampling positions along one axis.
    //  Pixel-centre grid (alignCorners false): output pixel i's centre (i + 0.5) maps to source
    //    position (i + 0.5) * from / to - 0.5, clamped to 0 .. from - 1.
    //  Corner-aligned grid (alignCorners true): position i * (from - 1) / (to - 1), so the first
    //    and last output pixels land exactly on the first and last source pixels.
    //  Positions are computed in double; the left index is the floor (capped at from - 2 so the
    //    right neighbour exists) and the fraction (position - left) is stored as float.
    internal static void BilinearAxis(int from, int to, bool alignCorners, int[] index, float[] fraction)
    {
        for (int i = 0; i < to; i++)
        {
            if (from == 1)
            {
                index[i] = 0;
                fraction[i] = 0f;
                continue;
            }
            double position;
            if (alignCorners)
            {
                position = to == 1 ? 0.0 : i * (from - 1.0) / (to - 1.0);
            }
            else
            {
                position = ((i + 0.5) * from / to) - 0.5;
            }
            if (position < 0) { position = 0; }
            if (position > from - 1) { position = from - 1; }
            int left = Math.Min((int)Math.Floor(position), from - 2);
            index[i] = left;
            fraction[i] = (float)(position - left);
        }
    }

    //Nearest sampling: output pixel i reads source pixel floor((2i + 1) * from / (2 * to)) - the
    //  source pixel whose span contains the output pixel's centre - in exact integer arithmetic
    internal static int NearestIndex(int i, int from, int to)
    {
        long n = (((2L * i) + 1L) * from) / (2L * to);
        return (int)Math.Min(n, from - 1);
    }

    internal static void ResizeNearest(float[] source, int sourceWidth, int sourceHeight, float[] destination, int width, int height)
    {
        var columns = new int[width];
        for (int x = 0; x < width; x++) { columns[x] = NearestIndex(x, sourceWidth, width); }
        ForRows(height, width * height, y =>
        {
            int from = NearestIndex(y, sourceHeight, height) * sourceWidth;
            int row = y * width;
            for (int x = 0; x < width; x++) { destination[row + x] = source[from + columns[x]]; }
        });
    }

    //Bilinear value = (p00 * w00) + (p10 * w10) + (p01 * w01) + (p11 * w11), summed left to right,
    //  with w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy
    internal static void ResizeBilinear(float[] source, int sourceWidth, int sourceHeight, float[] destination, int width, int height, bool alignCorners)
    {
        var columns = new int[width];
        var fx = new float[width];
        var rows = new int[height];
        var fy = new float[height];
        BilinearAxis(sourceWidth, width, alignCorners, columns, fx);
        BilinearAxis(sourceHeight, height, alignCorners, rows, fy);
        int stepX = sourceWidth > 1 ? 1 : 0;
        int stepY = sourceHeight > 1 ? sourceWidth : 0;
        ForRows(height, width * height, y =>
        {
            int top = rows[y] * sourceWidth;
            float v = fy[y];
            float iv = 1f - v;
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                float u = fx[x];
                float iu = 1f - u;
                int p00 = top + columns[x];
                float w00 = iu * iv, w10 = u * iv, w01 = iu * v, w11 = u * v;
                destination[row + x] = (source[p00] * w00) + (source[p00 + stepX] * w10)
                    + (source[p00 + stepY] * w01) + (source[p00 + stepY + stepX] * w11);
            }
        });
    }

    //Box downsample by a whole factor. Output size is ceiling(size / factor); each output pixel is
    //  the mean of the factor x factor block it covers (fewer pixels for a partial block at the
    //  right or bottom edge). Per block: each block row is summed left to right starting from its
    //  first value, the row sums are added top to bottom starting from 0, and the total is divided
    //  by the number of pixels as a float.
    internal static void Downsample(float[] source, int sourceWidth, int sourceHeight, int factor, float[] destination, int width, int height)
    {
        ForRows(height, width * height, y =>
        {
            int y0 = y * factor;
            int y1 = Math.Min(y0 + factor, sourceHeight);
            for (int x = 0; x < width; x++)
            {
                int x0 = x * factor;
                int x1 = Math.Min(x0 + factor, sourceWidth);
                float sum = 0f;
                for (int sy = y0; sy < y1; sy++)
                {
                    int row = sy * sourceWidth;
                    float rowSum = source[row + x0];
                    for (int sx = x0 + 1; sx < x1; sx++) { rowSum += source[row + sx]; }
                    sum += rowSum;
                }
                destination[(y * width) + x] = sum / ((x1 - x0) * (y1 - y0));
            }
        });
    }

    //---------------------------------------------------------------------------------------------
    //Pixel mixing (shared by Lerp, the transitions and FadeThroughColor)

    //Mixes straight-alpha colour a towards b by t (0..1), in premultiplied space. When the two
    //  alphas are equal the premultiplied mix reduces exactly to the straight mix, which is
    //  what is computed (so opaque frames give a * (1 - t) + b * t with no rounding drift).
    //  t at or below 0 returns a unchanged and t at or above 1 returns b unchanged.
    internal static void MixUnit(
        float ar, float ag, float ab, float aa,
        float br, float bg, float bb, float ba,
        float t, out float r, out float g, out float b, out float a)
    {
        if (!(t > 0f)) { r = ar; g = ag; b = ab; a = aa; return; }
        if (t >= 1f) { r = br; g = bg; b = bb; a = ba; return; }
        float s = 1f - t;
        if (aa == ba)
        {
            r = (ar * s) + (br * t);
            g = (ag * s) + (bg * t);
            b = (ab * s) + (bb * t);
            a = aa;
            return;
        }
        float wa = aa * s, wb = ba * t;
        a = wa + wb;
        if (a == 0f)
        {
            r = 0f; g = 0f; b = 0f; a = 0f;
            return;
        }
        r = ((ar * wa) + (br * wb)) / a;
        g = ((ag * wa) + (bg * wb)) / a;
        b = ((ab * wa) + (bb * wb)) / a;
    }

    //The same mix on straight-alpha RGBA bytes, computed in float on the 0..255 scale: equal
    //  alphas give (byte)(ca * s + cb * t + 0.5) per colour channel with alpha kept; otherwise
    //  A = Aa * s + Ab * t and C = (Ca * Aa * s + Cb * Ab * t) / A, each rounded with LevelToByte
    //  (all zeros when A is 0). The inputs are passed by value, so the destination may be the
    //  memory either input came from.
    internal static void MixBytes(
        byte ar, byte ag, byte ab, byte aa,
        byte br, byte bg, byte bb, byte ba,
        float t, Span<byte> destination, int offset)
    {
        if (!(t > 0f))
        {
            destination[offset] = ar;
            destination[offset + 1] = ag;
            destination[offset + 2] = ab;
            destination[offset + 3] = aa;
            return;
        }
        if (t >= 1f)
        {
            destination[offset] = br;
            destination[offset + 1] = bg;
            destination[offset + 2] = bb;
            destination[offset + 3] = ba;
            return;
        }
        float s = 1f - t;
        if (aa == ba)
        {
            destination[offset] = (byte)((ar * s) + (br * t) + 0.5f);
            destination[offset + 1] = (byte)((ag * s) + (bg * t) + 0.5f);
            destination[offset + 2] = (byte)((ab * s) + (bb * t) + 0.5f);
            destination[offset + 3] = aa;
            return;
        }
        float wa = aa * s, wb = ba * t;
        float alpha = wa + wb;
        if (!(alpha > 0f))
        {
            destination[offset] = 0;
            destination[offset + 1] = 0;
            destination[offset + 2] = 0;
            destination[offset + 3] = 0;
            return;
        }
        destination[offset] = LevelToByte(((ar * wa) + (br * wb)) / alpha);
        destination[offset + 1] = LevelToByte(((ag * wa) + (bg * wb)) / alpha);
        destination[offset + 2] = LevelToByte(((ab * wa) + (bb * wb)) / alpha);
        destination[offset + 3] = LevelToByte(alpha);
    }

    internal static void MixBytes(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, Span<byte> destination, int offset, float t)
        => MixBytes(a[offset], a[offset + 1], a[offset + 2], a[offset + 3],
            b[offset], b[offset + 1], b[offset + 2], b[offset + 3], t, destination, offset);
}
