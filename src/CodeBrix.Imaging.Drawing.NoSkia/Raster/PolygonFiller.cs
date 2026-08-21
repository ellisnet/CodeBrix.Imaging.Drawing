using System;
using System.Collections.Generic;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// The scanline polygon rasterizer at the heart of the managed drawing engine. It fills a
/// set of polygon contours (already in device coordinates) with a fill rule, computing
/// per-pixel coverage by combining vertical supersampling with exact analytic horizontal
/// span coverage, and composites the paint source over the target - honoring the fill
/// context's clip mask, color filter, and blend mode. It can also capture raw coverage
/// into a mask, which is how clip regions are built.
/// </summary>
internal static class PolygonFiller
{
    private const int SubsampleCount = 4;

    private struct Edge
    {
        public float X;          //X at the current subsample line
        public float StepX;      //X advance per subsample line
        public int EndSubline;   //First subsample line index NOT covered by this edge
        public int Winding;      //+1 downward, -1 upward
    }

    /// <summary>
    /// Fills polygons into the target bitmap with a plain paint (source-over, unclipped,
    /// unfiltered).
    /// </summary>
    /// <param name="target">The bitmap to composite into.</param>
    /// <param name="polygons">The polygon contours, in device coordinates.</param>
    /// <param name="fillType">The fill rule deciding which regions are inside.</param>
    /// <param name="paint">The source of per-pixel paint colors.</param>
    /// <param name="antialias">Whether fractional edge coverage composites as partial alpha.</param>
    public static void Fill(DrawingBitmap target, List<List<Vector2>> polygons, DrawingPathFillType fillType,
        PaintSource paint, bool antialias)
        => Fill(target, polygons, fillType, new FillContext { Paint = paint, Antialias = antialias });

    /// <summary>
    /// Fills polygons into the target bitmap per the given fill context.
    /// </summary>
    /// <param name="target">The bitmap to composite into.</param>
    /// <param name="polygons">The polygon contours, in device coordinates.</param>
    /// <param name="fillType">The fill rule deciding which regions are inside.</param>
    /// <param name="context">The paint, clip, filter, blend, and anti-aliasing choices.</param>
    public static void Fill(DrawingBitmap target, List<List<Vector2>> polygons, DrawingPathFillType fillType,
        FillContext context)
    {
        Scan(target.Width, target.Height, polygons, fillType,
            (row, coverage, minX, maxX) => BlendRow(target, row, coverage, minX, maxX, context));
    }

    /// <summary>
    /// Rasterizes polygons into a coverage mask (one 0..1 value per pixel, row-major) -
    /// used to build clip regions.
    /// </summary>
    /// <param name="width">The mask width, in pixels.</param>
    /// <param name="height">The mask height, in pixels.</param>
    /// <param name="polygons">The polygon contours, in device coordinates.</param>
    /// <param name="fillType">The fill rule deciding which regions are inside.</param>
    /// <param name="antialias">
    /// When <c>false</c>, coverage snaps to 0 or 1 at the one-half threshold.
    /// </param>
    /// <returns>The coverage mask.</returns>
    public static float[] Coverage(int width, int height, List<List<Vector2>> polygons,
        DrawingPathFillType fillType, bool antialias)
    {
        var mask = new float[width * height];
        Scan(width, height, polygons, fillType, (row, coverage, minX, maxX) =>
        {
            int rowOffset = row * width;
            for (int x = minX; x <= maxX; x++)
            {
                float value = Math.Clamp(coverage[x], 0f, 1f);
                if (!antialias) { value = value >= 0.5f ? 1f : 0f; }
                mask[rowOffset + x] = value;
            }
        });
        return mask;
    }

    private static void Scan(int width, int height, List<List<Vector2>> polygons,
        DrawingPathFillType fillType, Action<int, float[], int, int> emitRow)
    {
        if (polygons.Count == 0 || width < 1 || height < 1) { return; }

        int totalSublines = height * SubsampleCount;

        //Collect the edges of every polygon, bucketed by the first subsample line they cover
        var edges = new List<(int StartSubline, Edge Edge)>();
        float maxYf = float.MinValue;

        foreach (List<Vector2> polygon in polygons)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 from = polygon[i];
                Vector2 to = polygon[(i + 1) % polygon.Count];
                if (from.Y == to.Y) { continue; } //Horizontal edges never cross a scanline

                int winding = from.Y < to.Y ? 1 : -1;
                Vector2 top = winding > 0 ? from : to;
                Vector2 bottom = winding > 0 ? to : from;

                maxYf = Math.Max(maxYf, bottom.Y);

                //Subsample line k samples at y = (k + 0.5) / SubsampleCount
                int startSubline = (int)MathF.Ceiling((top.Y * SubsampleCount) - 0.5f);
                int endSubline = (int)MathF.Ceiling((bottom.Y * SubsampleCount) - 0.5f);
                int clippedStart = Math.Max(startSubline, 0);
                endSubline = Math.Min(endSubline, totalSublines);
                if (clippedStart >= endSubline) { continue; }

                float inverseSlope = (bottom.X - top.X) / (bottom.Y - top.Y);
                float firstY = (clippedStart + 0.5f) / SubsampleCount;
                edges.Add((clippedStart, new Edge
                {
                    X = top.X + ((firstY - top.Y) * inverseSlope),
                    StepX = inverseSlope / SubsampleCount,
                    EndSubline = endSubline,
                    Winding = winding,
                }));
            }
        }

        if (edges.Count == 0) { return; }
        edges.Sort((a, b) => a.StartSubline.CompareTo(b.StartSubline));

        int firstRow = edges[0].StartSubline / SubsampleCount;
        int lastRow = Math.Min(height - 1, (int)MathF.Floor(maxYf));
        if (lastRow < firstRow) { return; }

        var active = new List<Edge>();
        var crossings = new List<(float X, int Winding)>();
        var coverage = new float[width];
        float subWeight = 1f / SubsampleCount;
        int nextEdgeIndex = 0;
        int rowMinX = width;
        int rowMaxX = -1;

        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int sub = 0; sub < SubsampleCount; sub++)
            {
                int subline = (row * SubsampleCount) + sub;

                while (nextEdgeIndex < edges.Count && edges[nextEdgeIndex].StartSubline <= subline)
                {
                    active.Add(edges[nextEdgeIndex].Edge);
                    nextEdgeIndex++;
                }

                if (active.Count > 0)
                {
                    crossings.Clear();
                    for (int i = active.Count - 1; i >= 0; i--)
                    {
                        Edge edge = active[i];
                        if (edge.EndSubline <= subline)
                        {
                            active.RemoveAt(i);
                            continue;
                        }
                        crossings.Add((edge.X, edge.Winding));
                        edge.X += edge.StepX;
                        active[i] = edge;
                    }

                    if (crossings.Count > 1)
                    {
                        crossings.Sort((a, b) => a.X.CompareTo(b.X));

                        int winding = 0;
                        for (int i = 0; i < crossings.Count - 1; i++)
                        {
                            winding += crossings[i].Winding;
                            bool inside = fillType == DrawingPathFillType.EvenOdd
                                ? (winding & 1) != 0
                                : winding != 0;
                            if (!inside) { continue; }

                            AddSpan(coverage, width, crossings[i].X, crossings[i + 1].X, subWeight,
                                ref rowMinX, ref rowMaxX);
                        }
                    }
                }
            }

            if (rowMaxX >= rowMinX)
            {
                emitRow(row, coverage, rowMinX, rowMaxX);
                Array.Clear(coverage, rowMinX, rowMaxX - rowMinX + 1);
                rowMinX = width;
                rowMaxX = -1;
            }
        }
    }

    private static void AddSpan(float[] coverage, int width, float spanStart, float spanEnd, float weight,
        ref int rowMinX, ref int rowMaxX)
    {
        if (spanEnd <= 0 || spanStart >= width) { return; }
        spanStart = Math.Max(spanStart, 0);
        spanEnd = Math.Min(spanEnd, width);
        if (spanEnd <= spanStart) { return; }

        int firstPixel = (int)spanStart;
        int lastPixel = (int)MathF.Ceiling(spanEnd) - 1;
        if (lastPixel >= width) { lastPixel = width - 1; }
        if (lastPixel < firstPixel) { lastPixel = firstPixel; }

        if (firstPixel == lastPixel)
        {
            coverage[firstPixel] += (spanEnd - spanStart) * weight;
        }
        else
        {
            coverage[firstPixel] += (firstPixel + 1 - spanStart) * weight;
            for (int i = firstPixel + 1; i < lastPixel; i++)
            {
                coverage[i] += weight;
            }
            coverage[lastPixel] += (spanEnd - lastPixel) * weight;
        }

        rowMinX = Math.Min(rowMinX, firstPixel);
        rowMaxX = Math.Max(rowMaxX, lastPixel);
    }

    private static void BlendRow(DrawingBitmap target, int row, float[] coverage, int minX, int maxX,
        FillContext context)
    {
        byte[] pixels = target.PixelBuffer;
        bool isBgra = target.IsBgra;
        int rowOffset = row * target.RowBytes;
        int clipOffset = row * target.Width;
        bool simpleBlend = context.BlendMode == DrawingBlendMode.SrcOver;

        for (int x = minX; x <= maxX; x++)
        {
            float pixelCoverage = coverage[x];
            if (pixelCoverage <= 0) { continue; }
            if (context.Antialias)
            {
                if (pixelCoverage > 1) { pixelCoverage = 1; }
            }
            else
            {
                if (pixelCoverage < 0.5f) { continue; }
                pixelCoverage = 1;
            }

            if (context.ClipMask != null)
            {
                pixelCoverage *= context.ClipMask[clipOffset + x];
                if (pixelCoverage <= 0) { continue; }
            }

            context.Paint.GetColor(x, row, out float red, out float green, out float blue, out float alpha);
            context.ColorFilter?.Apply(ref red, ref green, ref blue, ref alpha);

            int offset = rowOffset + (x * 4);
            if (simpleBlend)
            {
                float sourceAlpha = alpha * pixelCoverage;
                if (sourceAlpha <= 0) { continue; }
                BlendPixel(pixels, offset, isBgra, red, green, blue, sourceAlpha);
            }
            else
            {
                BlendPixelWithMode(pixels, offset, isBgra, context.BlendMode,
                    red, green, blue, alpha, pixelCoverage);
            }
        }
    }

    /// <summary>
    /// Composites one straight-alpha source color over one pixel of a straight-alpha
    /// buffer (source-over).
    /// </summary>
    /// <param name="pixels">The target pixel buffer.</param>
    /// <param name="offset">The byte offset of the target pixel.</param>
    /// <param name="isBgra">Whether the buffer stores blue-first pixels.</param>
    /// <param name="red">The source red component, 0..1.</param>
    /// <param name="green">The source green component, 0..1.</param>
    /// <param name="blue">The source blue component, 0..1.</param>
    /// <param name="sourceAlpha">The source alpha (already multiplied by coverage), 0..1.</param>
    internal static void BlendPixel(byte[] pixels, int offset, bool isBgra,
        float red, float green, float blue, float sourceAlpha)
    {
        if (sourceAlpha >= 1f)
        {
            pixels[offset] = ToByte(isBgra ? blue : red);
            pixels[offset + 1] = ToByte(green);
            pixels[offset + 2] = ToByte(isBgra ? red : blue);
            pixels[offset + 3] = 255;
            return;
        }

        float destAlpha = pixels[offset + 3] / 255f;
        float destComponent0 = pixels[offset] / 255f;
        float destComponent1 = pixels[offset + 1] / 255f;
        float destComponent2 = pixels[offset + 2] / 255f;
        float destRed = isBgra ? destComponent2 : destComponent0;
        float destBlue = isBgra ? destComponent0 : destComponent2;

        float inverse = 1f - sourceAlpha;
        float outAlpha = sourceAlpha + (destAlpha * inverse);
        if (outAlpha <= 0)
        {
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
            return;
        }

        //Straight-alpha source-over: blend in premultiplied space, then un-premultiply
        float outRed = ((red * sourceAlpha) + (destRed * destAlpha * inverse)) / outAlpha;
        float outGreen = ((green * sourceAlpha) + (destComponent1 * destAlpha * inverse)) / outAlpha;
        float outBlue = ((blue * sourceAlpha) + (destBlue * destAlpha * inverse)) / outAlpha;

        pixels[offset] = ToByte(isBgra ? outBlue : outRed);
        pixels[offset + 1] = ToByte(outGreen);
        pixels[offset + 2] = ToByte(isBgra ? outRed : outBlue);
        pixels[offset + 3] = ToByte(outAlpha);
    }

    /// <summary>
    /// Composites one straight-alpha source color onto one pixel with an arbitrary blend
    /// mode, honoring partial coverage by interpolating between the destination and the
    /// fully blended result (Skia's anti-aliased blending model).
    /// </summary>
    /// <param name="pixels">The target pixel buffer.</param>
    /// <param name="offset">The byte offset of the target pixel.</param>
    /// <param name="isBgra">Whether the buffer stores blue-first pixels.</param>
    /// <param name="mode">The blend mode.</param>
    /// <param name="red">The source red component, 0..1.</param>
    /// <param name="green">The source green component, 0..1.</param>
    /// <param name="blue">The source blue component, 0..1.</param>
    /// <param name="alpha">The source alpha, 0..1 (NOT multiplied by coverage).</param>
    /// <param name="coverage">The pixel coverage, 0..1.</param>
    internal static void BlendPixelWithMode(byte[] pixels, int offset, bool isBgra, DrawingBlendMode mode,
        float red, float green, float blue, float alpha, float coverage)
    {
        float destAlpha = pixels[offset + 3] / 255f;
        float destComponent0 = pixels[offset] / 255f;
        float destComponent1 = pixels[offset + 1] / 255f;
        float destComponent2 = pixels[offset + 2] / 255f;
        float destRed = isBgra ? destComponent2 : destComponent0;
        float destBlue = isBgra ? destComponent0 : destComponent2;

        Blender.Blend(mode, red, green, blue, alpha,
            destRed, destComponent1, destBlue, destAlpha,
            out float outRed, out float outGreen, out float outBlue, out float outAlpha);

        if (coverage < 1f)
        {
            //Interpolate premultiplied components between destination and blended result
            float blendedPremulRed = outRed * outAlpha;
            float blendedPremulGreen = outGreen * outAlpha;
            float blendedPremulBlue = outBlue * outAlpha;
            float destPremulRed = destRed * destAlpha;
            float destPremulGreen = destComponent1 * destAlpha;
            float destPremulBlue = destBlue * destAlpha;

            float mixedAlpha = (destAlpha * (1 - coverage)) + (outAlpha * coverage);
            float mixedPremulRed = (destPremulRed * (1 - coverage)) + (blendedPremulRed * coverage);
            float mixedPremulGreen = (destPremulGreen * (1 - coverage)) + (blendedPremulGreen * coverage);
            float mixedPremulBlue = (destPremulBlue * (1 - coverage)) + (blendedPremulBlue * coverage);

            outAlpha = mixedAlpha;
            if (outAlpha > 0)
            {
                outRed = mixedPremulRed / outAlpha;
                outGreen = mixedPremulGreen / outAlpha;
                outBlue = mixedPremulBlue / outAlpha;
            }
            else
            {
                outRed = outGreen = outBlue = 0;
            }
        }

        pixels[offset] = ToByte(isBgra ? outBlue : outRed);
        pixels[offset + 1] = ToByte(outGreen);
        pixels[offset + 2] = ToByte(isBgra ? outRed : outBlue);
        pixels[offset + 3] = ToByte(outAlpha);
    }

    private static byte ToByte(float value)
    {
        float scaled = (value * 255f) + 0.5f;
        if (scaled <= 0) { return 0; }
        if (scaled >= 255f) { return 255; }
        return (byte)scaled;
    }
}
