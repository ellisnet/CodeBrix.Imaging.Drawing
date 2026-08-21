using System;
using System.Text;
using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using Xunit;

namespace CodeBrix.Imaging.Drawing.TestSupport;

/// <summary>
/// Decodes and compares two encoded images pixel by pixel, producing similarity statistics.
/// Comparisons run on premultiplied channel values so that the (meaningless) color of fully
/// transparent pixels never counts as a difference. Two independently written rasterizers
/// never produce bit-identical anti-aliased edges, so parity is asserted as near-identity:
/// a small mean difference and a small fraction of pixels allowed to differ noticeably.
/// </summary>
internal static class ImageComparison
{
    internal sealed class Stats
    {
        public int Width;
        public int Height;
        public int MaxDelta;
        public double MeanDelta;
        public double FractionNoticeable; //pixels with any premultiplied channel delta > 24
        public override string ToString()
            => $"{Width}x{Height}: maxDelta={MaxDelta}, meanDelta={MeanDelta:F3}, " +
               $"noticeablyDifferent={FractionNoticeable:P3}";
    }

    public static Stats Compare(byte[] encodedA, byte[] encodedB)
    {
        using Image<Rgba32> imageA = Image.Load<Rgba32>(encodedA);
        using Image<Rgba32> imageB = Image.Load<Rgba32>(encodedB);

        Assert.True(imageA.Width == imageB.Width && imageA.Height == imageB.Height,
            $"Image sizes differ: {imageA.Width}x{imageA.Height} vs {imageB.Width}x{imageB.Height}");

        var stats = new Stats { Width = imageA.Width, Height = imageA.Height };
        long totalDelta = 0;
        long noticeable = 0;

        for (int y = 0; y < imageA.Height; y++)
        {
            for (int x = 0; x < imageA.Width; x++)
            {
                Rgba32 a = imageA[x, y];
                Rgba32 b = imageB[x, y];

                int deltaR = Math.Abs(Premultiply(a.R, a.A) - Premultiply(b.R, b.A));
                int deltaG = Math.Abs(Premultiply(a.G, a.A) - Premultiply(b.G, b.A));
                int deltaB = Math.Abs(Premultiply(a.B, a.A) - Premultiply(b.B, b.A));
                int deltaA = Math.Abs(a.A - b.A);
                int pixelMax = Math.Max(Math.Max(deltaR, deltaG), Math.Max(deltaB, deltaA));

                stats.MaxDelta = Math.Max(stats.MaxDelta, pixelMax);
                totalDelta += deltaR + deltaG + deltaB + deltaA;
                if (pixelMax > 24) { noticeable++; }
            }
        }

        long pixelCount = (long)imageA.Width * imageA.Height;
        stats.MeanDelta = totalDelta / (double)(pixelCount * 4);
        stats.FractionNoticeable = noticeable / (double)pixelCount;
        return stats;
    }

    public static void AssertSimilar(byte[] encodedA, byte[] encodedB, string label,
        double maxMeanDelta = 1.0, double maxFractionNoticeable = 0.02)
    {
        Stats stats = Compare(encodedA, encodedB);

        var message = new StringBuilder();
        message.Append(label).Append(" - ").Append(stats);
        message.Append($" (limits: meanDelta<={maxMeanDelta}, noticeable<={maxFractionNoticeable:P1})");

        Assert.True(stats.MeanDelta <= maxMeanDelta && stats.FractionNoticeable <= maxFractionNoticeable,
            message.ToString());
    }

    private static int Premultiply(byte channel, byte alpha)
        => alpha == 255 ? channel : ((channel * alpha) + 127) / 255;
}
