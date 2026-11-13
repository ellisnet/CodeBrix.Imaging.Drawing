using System;

namespace CodeBrix.Imaging.Drawing.Formations;

//Exact Euclidean distance transform of a pixel set (Felzenszwalb and Huttenlocher's lower
//  envelope of parabolas, columns then rows), in units: pixel spacing unitX across, unitY down.
//  Double arithmetic in a fixed order - identical in both packages
internal static class MaskDistance
{
    //For every pixel, the distance from its centre to the nearest pixel centre on the other side:
    //  with fromInside, inside pixels measure to the nearest outside pixel; without, outside pixels
    //  measure to the nearest inside one. Infinity when there is none
    public static float[] Compute(bool[] inside, int width, int height, double unitX, double unitY, bool fromInside)
    {
        int n = Math.Max(width, height);
        var f = new double[n];
        var d = new double[n];
        var v = new int[n];
        var z = new double[n + 1];
        var squared = new double[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool feature = inside[(y * width) + x] != fromInside;
                f[y] = feature ? 0.0 : Double.PositiveInfinity;
            }
            Transform(f, height, unitY, d, v, z);
            for (int y = 0; y < height; y++) { squared[(y * width) + x] = d[y]; }
        }
        var result = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) { f[x] = squared[(y * width) + x]; }
            Transform(f, width, unitX, d, v, z);
            for (int x = 0; x < width; x++) { result[(y * width) + x] = (float)Math.Sqrt(d[x]); }
        }
        return result;
    }

    private static void Transform(double[] f, int n, double spacing, double[] d, int[] v, double[] z)
    {
        int k = -1;
        for (int q = 0; q < n; q++)
        {
            if (Double.IsPositiveInfinity(f[q])) { continue; }
            double crossing = Double.NegativeInfinity;
            while (k >= 0)
            {
                int p = v[k];
                double zq = q * spacing;
                double zp = p * spacing;
                crossing = ((f[q] + (zq * zq)) - (f[p] + (zp * zp))) / (2.0 * (zq - zp));
                if (crossing <= z[k]) { k--; } else { break; }
            }
            k++;
            v[k] = q;
            z[k] = k == 0 ? Double.NegativeInfinity : crossing;
            z[k + 1] = Double.PositiveInfinity;
        }
        for (int p = 0, j = 0; p < n; p++)
        {
            if (k < 0) { d[p] = Double.PositiveInfinity; continue; }
            double zp = p * spacing;
            while (z[j + 1] < zp) { j++; }
            double offset = (p - v[j]) * spacing;
            d[p] = (offset * offset) + f[v[j]];
        }
    }
}
