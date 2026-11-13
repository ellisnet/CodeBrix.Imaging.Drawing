using System;

namespace AnimationDemoApp.Demo;

/// <summary>
/// Places one raw RGBA frame inside another - the way a scene shows a smaller session's frame (a phone-shaped
/// portrait frame, a frame over a checkerboard) inside the demo frame. Both buffers are straight-alpha RGBA, the
/// layout the drawing session's ExportPixels writes.
/// </summary>
internal static class FrameCompositor
{
    /// <summary>Copies a frame into a larger one at a position (no blending).</summary>
    /// <param name="source">The frame to place.</param>
    /// <param name="sourceWidth">Its width.</param>
    /// <param name="sourceHeight">Its height.</param>
    /// <param name="target">The larger frame (<see cref="DemoFrame.Width" /> wide).</param>
    /// <param name="x">Where the source's left edge goes.</param>
    /// <param name="y">Where the source's top edge goes.</param>
    public static void Copy(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int x, int y)
    {
        for (int row = 0; row < sourceHeight; row++)
        {
            source.Slice(row * sourceWidth * 4, sourceWidth * 4).CopyTo(target.Slice((((y + row) * DemoFrame.Width) + x) * 4));
        }
    }

    /// <summary>Draws a frame over a larger one at a position, blending by the source's straight alpha ("source over").</summary>
    /// <param name="source">The frame to place.</param>
    /// <param name="sourceWidth">Its width.</param>
    /// <param name="sourceHeight">Its height.</param>
    /// <param name="target">The larger frame (<see cref="DemoFrame.Width" /> wide).</param>
    /// <param name="x">Where the source's left edge goes.</param>
    /// <param name="y">Where the source's top edge goes.</param>
    public static void Over(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int x, int y)
    {
        for (int row = 0; row < sourceHeight; row++)
        {
            int s = row * sourceWidth * 4;
            int d = (((y + row) * DemoFrame.Width) + x) * 4;
            for (int col = 0; col < sourceWidth; col++, s += 4, d += 4)
            {
                float sa = source[s + 3] / 255f;
                if (sa <= 0f) continue;
                float da = target[d + 3] / 255f;
                float outA = sa + (da * (1f - sa));
                for (int c = 0; c < 3; c++)
                {
                    float value = ((source[s + c] * sa) + (target[d + c] * da * (1f - sa))) / outA;
                    target[d + c] = (byte)Math.Clamp(value + 0.5f, 0f, 255f);
                }

                target[d + 3] = (byte)Math.Clamp((outA * 255f) + 0.5f, 0f, 255f);
            }
        }
    }
}
