using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Painting;

/// <summary>
/// Which element owns each pixel of a frame - the painterly tool behind "every brushstroke
/// takes the colour beneath it", bristle textures and dark seams between strokes. Made by
/// <c>DrawingSession.ExportOwnershipMap</c> (or <c>DrawingRenderer.RenderToOwnershipMap</c>):
/// a pixel belongs to the LAST element, in painting order, whose area covers the pixel's
/// centre - the hard-edged pixel rule of <see cref="MaskMeaning.HardEdged"/>, computed from
/// geometry in shared managed code, so both packages produce identical maps. Pixels no element
/// covers hold <see cref="Nobody"/>; <see cref="FillUnowned"/> gives them to the nearest owner.
/// <para>
/// Elements are numbered 0, 1, 2 ... in painting order across the layers the map was made of
/// (bottom layer first, each layer's elements in order); every element on those layers has a
/// number, owning pixels or not. A group or an animated shape is one element.
/// </para>
/// <para>
/// A map is IMMUTABLE and safe to read from any number of threads. Every method that produces
/// numbers does so in a fixed order (sums run row by row, left to right, on one thread), so the
/// results are the same bits in both packages and on every run.
/// </para>
/// </summary>
public sealed class DrawingOwnershipMap
{
    /// <summary>The value of a pixel no element owns.</summary>
    public const int Nobody = -1;

    private readonly int[] _owners;
    private readonly DrawingElement[] _elements;
    private readonly DrawingLayer[] _layers;
    private readonly HardEdgeMatrix[] _matrices;
    private readonly int[] _counts;
    private readonly Rectangle[] _bounds;
    private readonly object _lazyLocker = new object();
    private ElementAxes[] _axes;
    private Dictionary<DrawingElement, int> _indexes;

    internal DrawingOwnershipMap(int width, int height, int[] owners, DrawingElement[] elements, DrawingLayer[] layers,
        HardEdgeMatrix[] matrices)
    {
        Width = width;
        Height = height;
        _owners = owners;
        _elements = elements;
        _layers = layers;
        _matrices = matrices;
        _counts = new int[elements.Length];
        var left = new int[elements.Length];
        var top = new int[elements.Length];
        var right = new int[elements.Length];
        var bottom = new int[elements.Length];
        Array.Fill(left, int.MaxValue);
        Array.Fill(top, int.MaxValue);
        Array.Fill(right, int.MinValue);
        Array.Fill(bottom, int.MinValue);
        int unowned = 0;
        for (int y = 0, i = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++, i++)
            {
                int owner = owners[i];
                if (owner < 0)
                {
                    unowned++;
                    continue;
                }
                _counts[owner]++;
                if (x < left[owner]) { left[owner] = x; }
                if (x > right[owner]) { right[owner] = x; }
                if (y < top[owner]) { top[owner] = y; }
                bottom[owner] = y;
            }
        }
        UnownedCount = unowned;
        _bounds = new Rectangle[elements.Length];
        for (int e = 0; e < elements.Length; e++)
        {
            _bounds[e] = _counts[e] == 0
                ? Rectangle.Empty
                : new Rectangle(left[e], top[e], right[e] - left[e] + 1, bottom[e] - top[e] + 1);
        }
    }

    /// <summary>The map's width, pixels.</summary>
    public int Width { get; }

    /// <summary>The map's height, pixels.</summary>
    public int Height { get; }

    /// <summary>The map's size, pixels.</summary>
    public Size Size => new Size(Width, Height);

    /// <summary>How many elements the map numbers (whether or not they own pixels).</summary>
    public int ElementCount => _elements.Length;

    /// <summary>How many pixels no element owns.</summary>
    public int UnownedCount { get; }

    /// <summary>
    /// The owner of every pixel, row by row (<c>[y * Width + x]</c>): an element index, or
    /// <see cref="Nobody"/>.
    /// </summary>
    public ReadOnlySpan<int> Owners => _owners;

    /// <summary>The owner of one pixel: an element index, or <see cref="Nobody"/>.</summary>
    /// <param name="x">The column, 0 to <see cref="Width"/> - 1.</param>
    /// <param name="y">The row, 0 to <see cref="Height"/> - 1.</param>
    /// <returns>The owner.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the pixel lies outside the map.</exception>
    public int this[int x, int y]
    {
        get
        {
            if ((uint)x >= (uint)Width) { throw new ArgumentOutOfRangeException(nameof(x)); }
            if ((uint)y >= (uint)Height) { throw new ArgumentOutOfRangeException(nameof(y)); }
            return _owners[(y * Width) + x];
        }
    }

    /// <summary>Returns a copy of <see cref="Owners"/>.</summary>
    /// <returns>A new array, row by row.</returns>
    public int[] ToArray() => (int[])_owners.Clone();

    /// <summary>The element with an index.</summary>
    /// <param name="index">The element's index, 0 to <see cref="ElementCount"/> - 1.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public DrawingElement GetElement(int index)
    {
        CheckIndex(index);
        return _elements[index];
    }

    /// <summary>The layer an element is on.</summary>
    /// <param name="index">The element's index.</param>
    /// <returns>The layer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public DrawingLayer GetLayer(int index)
    {
        CheckIndex(index);
        return _layers[index];
    }

    /// <summary>The index of an element in the map.</summary>
    /// <param name="element">The element.</param>
    /// <returns>Its index; or -1 when it is not one of the map's elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    public int IndexOf(DrawingElement element)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        lock (_lazyLocker)
        {
            if (_indexes == null)
            {
                _indexes = new Dictionary<DrawingElement, int>(ReferenceEqualityComparer.Instance);
                for (int i = 0; i < _elements.Length; i++) { _indexes.TryAdd(_elements[i], i); }
            }
            return _indexes.TryGetValue(element, out int index) ? index : -1;
        }
    }

    /// <summary>How many pixels an element owns.</summary>
    /// <param name="index">The element's index.</param>
    /// <returns>The count; 0 for an element that owns none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public int GetPixelCount(int index)
    {
        CheckIndex(index);
        return _counts[index];
    }

    /// <summary>The smallest pixel rectangle holding every pixel an element owns.</summary>
    /// <param name="index">The element's index.</param>
    /// <returns>The bounds, map pixels; <see cref="Rectangle.Empty"/> for an element that owns none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public Rectangle GetBounds(int index)
    {
        CheckIndex(index);
        return _bounds[index];
    }

    /// <summary>
    /// The axes an element's pixels are measured in: the shape's own
    /// (<see cref="DrawingShape.GetAxes"/>) when it reports them, else
    /// <see cref="ElementAxes.FromBounds(RectangleF)"/> of its bounds, else all zeros.
    /// </summary>
    /// <param name="index">The element's index.</param>
    /// <returns>The axes, in the element's own calibrated coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public ElementAxes GetAxes(int index)
    {
        CheckIndex(index);
        return GetAllAxes()[index];
    }

    /// <summary>
    /// Returns a map in which every unowned pixel takes the owner of the NEAREST owned pixel,
    /// by exact Euclidean distance between pixel positions (computed in integers). TIE RULE:
    /// among owned pixels at the same smallest distance, the one with the smallest x wins, and
    /// among those the one with the smallest y. Owned pixels keep their owner. A map with no
    /// owned pixel at all (or none unowned) comes back unchanged (this same instance).
    /// </summary>
    /// <returns>The filled map.</returns>
    public DrawingOwnershipMap FillUnowned()
    {
        if (UnownedCount == 0 || UnownedCount == _owners.Length) { return this; }
        int[] filled = NearestFill(_owners, Width, Height);
        return new DrawingOwnershipMap(Width, Height, filled, _elements, _layers, _matrices);
    }

    /// <summary>
    /// A plane of the seams between owners: 1 on every pixel whose owner differs from the owner
    /// of the pixel to its left or of the pixel above it (the first column and row compare only
    /// with the neighbours they have; <see cref="Nobody"/> counts as an owner of its own), 0
    /// elsewhere - then blurred with <see cref="DrawingPlane.GaussianBlur(float)"/> when
    /// <paramref name="blurSigma"/> is above 0. Multiply, clamp and blend it for dark outlines
    /// between brushstrokes.
    /// </summary>
    /// <param name="blurSigma">The blur's sigma, pixels; 0 (the default) for the hard 0/1 seams.</param>
    /// <returns>A new plane of the map's size.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="blurSigma"/> is negative or not finite.</exception>
    public DrawingPlane CreateBoundaryPlane(float blurSigma = 0f)
    {
        if (!float.IsFinite(blurSigma) || blurSigma < 0f) { throw new ArgumentOutOfRangeException(nameof(blurSigma)); }
        var plane = new DrawingPlane(Width, Height);
        float[] data = plane.Data;
        int width = Width;
        int[] owners = _owners;
        PlaneKernels.ForRows(Height, owners.Length, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                int owner = owners[i];
                bool seam = (x > 0 && owners[i - 1] != owner) || (y > 0 && owners[i - width] != owner);
                data[i] = seam ? 1f : 0f;
            }
        });
        if (blurSigma > 0f) { plane.GaussianBlur(blurSigma); }
        return plane;
    }

    /// <summary>
    /// The MEAN COLOUR of a source image under each element - colour pickup. Each map pixel
    /// reads ONE source pixel by the nearest rule of <see cref="DrawingPlaneResampling.Nearest"/>
    /// (column <c>floor((2x + 1) * sourceWidth / (2 * Width))</c>, likewise rows - so a source at
    /// the map's size is read pixel for pixel and a half-size one at <c>(x / 2, y / 2)</c>).
    /// The mean is alpha-weighted (premultiplied, so transparent source pixels lend no colour):
    /// with a = source alpha clamped to 0..1, colour = sum(C x a) / sum(a) (0 when sum(a) is 0)
    /// and alpha = sum(a) / pixel count. Sums are double, row by row, left to right, on one
    /// thread. Over an opaque source this is the plain average of the colours.
    /// </summary>
    /// <param name="source">The image to pick colours from (straight alpha), any size.</param>
    /// <returns>One straight-alpha colour per element (X = R, Y = G, Z = B, W = A); transparent black for an element that owns no pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public Vector4[] GetMeanColors(DrawingPlaneImage source)
    {
        if (source == null) { throw new ArgumentNullException(nameof(source)); }
        int[] columns = NearestIndexes(Width, source.Width);
        int[] rows = NearestIndexes(Height, source.Height);
        float[] r = source.R.Data, g = source.G.Data, b = source.B.Data, a = source.A.Data;
        int n = _elements.Length;
        var sumR = new double[n];
        var sumG = new double[n];
        var sumB = new double[n];
        var sumA = new double[n];
        for (int y = 0, i = 0; y < Height; y++)
        {
            int sourceRow = rows[y] * source.Width;
            for (int x = 0; x < Width; x++, i++)
            {
                int owner = _owners[i];
                if (owner < 0) { continue; }
                int s = sourceRow + columns[x];
                float alpha = a[s];
                double weight = alpha > 0f ? (alpha >= 1f ? 1.0 : alpha) : 0.0;
                sumR[owner] += r[s] * weight;
                sumG[owner] += g[s] * weight;
                sumB[owner] += b[s] * weight;
                sumA[owner] += weight;
            }
        }
        return MeanColors(sumR, sumG, sumB, sumA, 1.0);
    }

    /// <summary>
    /// The mean colour of an 8-bit straight-alpha RGBA frame under each element (see
    /// <see cref="GetMeanColors(DrawingPlaneImage)"/> for the sampling rule). The sums are exact
    /// integers: colour = sum(C x A) / (255 x sum(A)) and alpha = sum(A) / (255 x pixel count),
    /// each divided in double.
    /// </summary>
    /// <param name="rgba">The frame: R, G, B, A bytes per pixel, rows top-down, stride width x 4.</param>
    /// <param name="width">The frame's width.</param>
    /// <param name="height">The frame's height.</param>
    /// <returns>One straight-alpha colour per element, channels 0..1; transparent black for an element that owns no pixel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> or <paramref name="height"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rgba"/> is shorter than width x height x 4 bytes.</exception>
    public Vector4[] GetMeanColors(ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height)); }
        if (rgba.Length < (long)width * height * 4) { throw new ArgumentException("The frame is shorter than width x height x 4 bytes.", nameof(rgba)); }
        int[] columns = NearestIndexes(Width, width);
        int[] rows = NearestIndexes(Height, height);
        int n = _elements.Length;
        var sumR = new long[n];
        var sumG = new long[n];
        var sumB = new long[n];
        var sumA = new long[n];
        for (int y = 0, i = 0; y < Height; y++)
        {
            int sourceRow = rows[y] * width;
            for (int x = 0; x < Width; x++, i++)
            {
                int owner = _owners[i];
                if (owner < 0) { continue; }
                int s = (sourceRow + columns[x]) * 4;
                int alpha = rgba[s + 3];
                sumR[owner] += rgba[s] * alpha;
                sumG[owner] += rgba[s + 1] * alpha;
                sumB[owner] += rgba[s + 2] * alpha;
                sumA[owner] += alpha;
            }
        }
        var colors = new Vector4[n];
        for (int e = 0; e < n; e++)
        {
            int count = _counts[e];
            if (count == 0) { continue; }
            long a = sumA[e];
            colors[e] = a == 0
                ? Vector4.Zero
                : new Vector4((float)(sumR[e] / (255.0 * a)), (float)(sumG[e] / (255.0 * a)), (float)(sumB[e] / (255.0 * a)),
                    (float)(a / (255.0 * count)));
        }
        return colors;
    }

    /// <summary>
    /// Paints each element's colour into every pixel it owns: R, G, B and A of the destination
    /// are set to the colour (straight alpha); pixels no element owns are left as they are.
    /// </summary>
    /// <param name="destination">An image of the map's size.</param>
    /// <param name="colors">One colour per element (X = R, Y = G, Z = B, W = A), indexed like the map.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination's size differs from the map's, or there are fewer colours than elements.</exception>
    public void ApplyColors(DrawingPlaneImage destination, IReadOnlyList<Vector4> colors)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        if (colors == null) { throw new ArgumentNullException(nameof(colors)); }
        if (destination.Width != Width || destination.Height != Height) { throw new ArgumentException("The image must be the map's size.", nameof(destination)); }
        if (colors.Count < _elements.Length) { throw new ArgumentException("There must be a colour for every element.", nameof(colors)); }
        Vector4[] table = new Vector4[_elements.Length];
        for (int e = 0; e < table.Length; e++) { table[e] = colors[e]; }
        float[] r = destination.R.Data, g = destination.G.Data, b = destination.B.Data, a = destination.A.Data;
        int width = Width;
        int[] owners = _owners;
        PlaneKernels.ForRows(Height, owners.Length, y =>
        {
            for (int i = y * width, end = i + width; i < end; i++)
            {
                int owner = owners[i];
                if (owner < 0) { continue; }
                Vector4 c = table[owner];
                r[i] = c.X;
                g[i] = c.Y;
                b[i] = c.Z;
                a[i] = c.W;
            }
        });
    }

    /// <summary>
    /// Paints each element's colour into every pixel it owns in an 8-bit straight-alpha RGBA
    /// frame of the map's size; pixels no element owns are left as they are.
    /// </summary>
    /// <param name="rgba">The frame: R, G, B, A bytes per pixel, rows top-down, stride Width x 4.</param>
    /// <param name="colors">One colour per element, indexed like the map.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the frame is shorter than Width x Height x 4 bytes, or there are fewer colours than elements.</exception>
    public void ApplyColors(Span<byte> rgba, IReadOnlyList<Color> colors)
    {
        if (colors == null) { throw new ArgumentNullException(nameof(colors)); }
        if (rgba.Length < (long)_owners.Length * 4) { throw new ArgumentException("The frame is shorter than Width x Height x 4 bytes.", nameof(rgba)); }
        if (colors.Count < _elements.Length) { throw new ArgumentException("There must be a colour for every element.", nameof(colors)); }
        var table = new Rgba32[_elements.Length];
        for (int e = 0; e < table.Length; e++) { table[e] = colors[e].ToRgba32(); }
        for (int i = 0; i < _owners.Length; i++)
        {
            int owner = _owners[i];
            if (owner < 0) { continue; }
            Rgba32 c = table[owner];
            int d = i * 4;
            rgba[d] = c.R;
            rgba[d + 1] = c.G;
            rgba[d + 2] = c.B;
            rgba[d + 3] = c.A;
        }
    }

    /// <summary>
    /// Paints each element's float colour - for example straight from
    /// <see cref="GetMeanColors(ReadOnlySpan{byte}, int, int)"/> - into every pixel it owns in an
    /// 8-bit straight-alpha RGBA frame of the map's size; pixels no element owns are left as they
    /// are. Each channel is converted by the planes' float-to-byte rule (NaN or at most 0 gives 0,
    /// at least 1 gives 255, otherwise <c>(byte)(v * 255 + 0.5)</c>), so the bytes are the same in
    /// both packages.
    /// </summary>
    /// <param name="rgba">The frame: R, G, B, A bytes per pixel, rows top-down, stride Width x 4.</param>
    /// <param name="colors">One colour per element (X = R, Y = G, Z = B, W = A, each 0..1), indexed like the map.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the frame is shorter than Width x Height x 4 bytes, or there are fewer colours than elements.</exception>
    public void ApplyColors(Span<byte> rgba, IReadOnlyList<Vector4> colors)
    {
        if (colors == null) { throw new ArgumentNullException(nameof(colors)); }
        if (colors.Count < _elements.Length) { throw new ArgumentException("There must be a colour for every element.", nameof(colors)); }
        var table = new Color[_elements.Length];
        for (int e = 0; e < table.Length; e++)
        {
            Vector4 c = colors[e];
            table[e] = Color.FromRgba(PlaneKernels.UnitToByte(c.X), PlaneKernels.UnitToByte(c.Y),
                PlaneKernels.UnitToByte(c.Z), PlaneKernels.UnitToByte(c.W));
        }
        ApplyColors(rgba, table);
    }

    /// <summary>
    /// BRUSH TEXTURE: fills a plane with a value per pixel computed from where the pixel lies in
    /// its owner's own axes - bristle ridges from <see cref="ElementPixel.Across"/>, a ramp along
    /// the stroke from <see cref="ElementPixel.NormalizedAlong"/>, and so on. Every owned pixel's
    /// value is <c>function(pixel)</c>; unowned pixels get <paramref name="unownedValue"/>.
    /// Rows are computed in parallel, each value by one thread, so the result is the same for any
    /// thread count PROVIDED the function is pure (depends only on its argument) and thread-safe.
    /// </summary>
    /// <param name="destination">A plane of the map's size; every value is overwritten.</param>
    /// <param name="function">The texture: a value per measured pixel.</param>
    /// <param name="unownedValue">The value of pixels no element owns.</param>
    /// <param name="axes">Optional axes per element index, overriding <see cref="GetAxes(int)"/> where it returns a value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> or <paramref name="function"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the plane's size differs from the map's.</exception>
    public void FillByElement(DrawingPlane destination, Func<ElementPixel, float> function, float unownedValue = 0f,
        Func<int, ElementAxes?> axes = null)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        if (function == null) { throw new ArgumentNullException(nameof(function)); }
        if (destination.Width != Width || destination.Height != Height) { throw new ArgumentException("The plane must be the map's size.", nameof(destination)); }
        Measure[] measures = CreateMeasures(axes);
        float[] data = destination.Data;
        int width = Width;
        int[] owners = _owners;
        PlaneKernels.ForRows(Height, owners.Length, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int owner = owners[row + x];
                data[row + x] = owner < 0 ? unownedValue : function(measures[owner].At(x, y, owner));
            }
        });
    }

    /// <summary>
    /// Measures one pixel in its owner's axes (see <see cref="FillByElement"/>).
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="pixel">The measured pixel, when it has an owner.</param>
    /// <returns><c>true</c> when the pixel has an owner; <c>false</c> (and a default value) when not.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the pixel lies outside the map.</exception>
    public bool TryGetElementPixel(int x, int y, out ElementPixel pixel)
    {
        int owner = this[x, y];
        if (owner < 0)
        {
            pixel = default;
            return false;
        }
        pixel = CreateMeasure(owner, GetAllAxes()[owner]).At(x, y, owner);
        return true;
    }

    private void CheckIndex(int index)
    {
        if ((uint)index >= (uint)_elements.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
    }

    private ElementAxes[] GetAllAxes()
    {
        lock (_lazyLocker)
        {
            if (_axes != null) { return _axes; }
            var axes = new ElementAxes[_elements.Length];
            for (int i = 0; i < axes.Length; i++)
            {
                DrawingElement element = _elements[i];
                ElementAxes? own = (element as DrawingShape)?.GetAxes();
                if (own.HasValue)
                {
                    axes[i] = own.Value;
                    continue;
                }
                RectangleF? bounds = element.GetLocalBounds();
                if (bounds.HasValue && float.IsFinite(bounds.Value.X) && float.IsFinite(bounds.Value.Y)
                    && float.IsFinite(bounds.Value.Width) && float.IsFinite(bounds.Value.Height))
                {
                    axes[i] = ElementAxes.FromBounds(bounds.Value);
                }
            }
            _axes = axes;
            return axes;
        }
    }

    private Measure[] CreateMeasures(Func<int, ElementAxes?> overrides)
    {
        ElementAxes[] axes = GetAllAxes();
        var measures = new Measure[axes.Length];
        for (int i = 0; i < measures.Length; i++)
        {
            ElementAxes? chosen = overrides?.Invoke(i);
            measures[i] = CreateMeasure(i, chosen ?? axes[i]);
        }
        return measures;
    }

    private Measure CreateMeasure(int index, ElementAxes axes)
    {
        bool invertible = _matrices[index].TryInvert(out HardEdgeMatrix inverse);
        return new Measure(inverse, invertible, axes);
    }

    //Takes a pixel centre back into an element's own coordinates and measures it in its axes
    private readonly struct Measure
    {
        private readonly HardEdgeMatrix _inverse;
        private readonly bool _invertible;
        private readonly ElementAxes _axes;
        private readonly double _cos, _sin;

        internal Measure(HardEdgeMatrix inverse, bool invertible, ElementAxes axes)
        {
            _inverse = inverse;
            _invertible = invertible;
            _axes = axes;
            double radians = AngleUnits.ToRadians(axes.Angle);
            _cos = Math.Cos(radians);
            _sin = Math.Sin(radians);
        }

        internal ElementPixel At(int x, int y, int owner)
        {
            if (!_invertible) { return new ElementPixel(x, y, owner, 0.0, 0.0, _axes); }
            _inverse.Transform(x + 0.5, y + 0.5, out double lx, out double ly);
            double dx = lx - _axes.Center.X, dy = ly - _axes.Center.Y;
            double along = (dx * _cos) + (dy * _sin);
            double across = (dy * _cos) - (dx * _sin);
            return new ElementPixel(x, y, owner, along, across, _axes);
        }
    }

    private Vector4[] MeanColors(double[] sumR, double[] sumG, double[] sumB, double[] sumA, double scale)
    {
        var colors = new Vector4[_elements.Length];
        for (int e = 0; e < colors.Length; e++)
        {
            int count = _counts[e];
            double a = sumA[e];
            if (count == 0 || !(a > 0)) { continue; }
            colors[e] = new Vector4((float)(sumR[e] / a), (float)(sumG[e] / a), (float)(sumB[e] / a), (float)(a * scale / count));
        }
        return colors;
    }

    //For each of `count` output positions, the source index by the planes' nearest rule:
    //  floor((2i + 1) * source / (2 * count)), in integers
    private static int[] NearestIndexes(int count, int source)
    {
        var indexes = new int[count];
        for (int i = 0; i < count; i++)
        {
            indexes[i] = (int)Math.Min(source - 1, ((2L * i) + 1) * source / (2L * count));
        }
        return indexes;
    }

    //Exact nearest-owner fill: a separable squared-distance transform carried out in integers.
    //  Pass 1, each column: the row of the nearest owned pixel in that column (ties: the upper).
    //  Pass 2, each row: the lower envelope of the parabolas (x - q)^2 + d(q)^2 over the columns
    //  q, with every breakpoint kept as an exact fraction; at a tie the smaller column wins. The
    //  result is the owner of the nearest owned pixel, smallest x then smallest y among equals.
    internal static int[] NearestFill(int[] owners, int width, int height)
    {
        var nearestRow = new int[owners.Length];
        PlaneKernels.ForRows(width, owners.Length, x =>
        {
            int last = -1;
            for (int y = 0; y < height; y++)
            {
                int i = (y * width) + x;
                if (owners[i] >= 0) { last = y; }
                nearestRow[i] = last;
            }
            int below = -1;
            for (int y = height - 1; y >= 0; y--)
            {
                int i = (y * width) + x;
                if (owners[i] >= 0) { below = y; }
                int above = nearestRow[i];
                if (below >= 0 && (above < 0 || (below - y) < (y - above))) { nearestRow[i] = below; }
            }
        });

        var result = new int[owners.Length];
        PlaneKernels.ForRows(height, owners.Length, y =>
        {
            int row = y * width;
            var vertex = new int[width];
            var boundaryNumerator = new long[width];
            var boundaryDenominator = new long[width];
            var heights = new long[width];
            int top = -1;
            for (int q = 0; q < width; q++)
            {
                int near = nearestRow[row + q];
                if (near < 0) { continue; }
                long d = y - near;
                heights[q] = d * d;
                long numerator = 0, denominator = 1;
                while (top >= 0)
                {
                    int v = vertex[top];
                    numerator = (heights[q] + ((long)q * q)) - (heights[v] + ((long)v * v));
                    denominator = 2L * (q - v);
                    //Pop v when the new parabola already wins at (or before) where v started winning
                    if (top > 0 && numerator * boundaryDenominator[top] <= boundaryNumerator[top] * denominator)
                    {
                        top--;
                        continue;
                    }
                    break;
                }
                top++;
                vertex[top] = q;
                boundaryNumerator[top] = numerator;
                boundaryDenominator[top] = denominator;
            }
            if (top < 0)
            {
                Array.Copy(owners, row, result, row, width);
                return;
            }
            int k = 0;
            for (int x = 0; x < width; x++)
            {
                //Move on only when the next parabola is strictly lower here (ties keep the smaller column)
                while (k < top && boundaryNumerator[k + 1] < (long)x * boundaryDenominator[k + 1]) { k++; }
                int q = vertex[k];
                result[row + x] = owners[(nearestRow[row + q] * width) + q];
            }
        });
        return result;
    }
}
