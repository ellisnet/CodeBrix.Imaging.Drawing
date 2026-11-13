using System;
using System.Runtime.InteropServices;
using System.Threading;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Rendering;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Effects;

/// <summary>
/// What part of a layer's or a group's content is shown: only inside (or only outside) a path,
/// only where another layer's elements are (a matte), or scaled by a pixel plane (an alpha mask).
/// Assign it to <see cref="DrawingLayer.Mask"/> or
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.DrawingGroup.Mask"/>; to mask one element, put it
/// in a group. Immutable (a plane is copied when the mask is made); one mask may be shared.
/// <para>
/// OWNERSHIP: a mask belongs to whoever made it - assigning it to a layer or group does not hand
/// it over. A plane mask (<see cref="FromPlane(DrawingPlane, RectangleF, bool)"/>) keeps its
/// copy of the plane as a backend bitmap; <see cref="Dispose"/> releases that bitmap at once
/// (otherwise it is released when the mask is collected) - do so when the mask is no longer
/// assigned anywhere, for example after replacing a per-frame plane mask, and never while a
/// render that uses it is running. A disposed plane mask covers nothing: the content it masks
/// is hidden (or shown whole, when inverted). Path and layer masks hold no backend resource, so
/// disposing them changes nothing.
/// </para>
/// <para>
/// A mask is always in the SAME coordinates as the content it masks: a layer's mask in the
/// layer's element coordinates (so it follows the layer's transform and the camera, as its
/// elements do), a group's mask in the group's coordinates (it moves, turns and scales with
/// the group). The content is drawn whole first and the mask then multiplies its coverage once,
/// anti-aliased by default, so overlapping elements never show a seam along the mask's edge.
/// </para>
/// </summary>
public sealed class DrawingMask : IDisposable
{
    private static long s_lastId;

    private readonly int _planeWidth;
    private readonly int _planeHeight;
    private SKBitmap _planeBitmap;
    private bool _disposed;
    private readonly long _id = Interlocked.Increment(ref s_lastId);

    private DrawingMask(DrawingMaskKind kind, PathGeometry geometry, bool antialias, DrawingLayer layer,
        SKBitmap planeBitmap, RectangleF destination, bool inverted)
    {
        Kind = kind;
        Geometry = geometry;
        IsAntialiased = antialias;
        Layer = layer;
        _planeBitmap = planeBitmap;
        _planeWidth = planeBitmap?.Width ?? 0;
        _planeHeight = planeBitmap?.Height ?? 0;
        Destination = destination;
        IsInverted = inverted;
    }

    /// <summary>What the mask keeps.</summary>
    public DrawingMaskKind Kind { get; }

    /// <summary>The path of an <see cref="DrawingMaskKind.Inside"/> or <see cref="DrawingMaskKind.Outside"/> mask; <c>null</c> otherwise.</summary>
    public PathGeometry Geometry { get; }

    /// <summary>Indicates whether a path mask's edge is anti-aliased (true unless asked otherwise).</summary>
    public bool IsAntialiased { get; }

    /// <summary>The layer whose elements make a <see cref="DrawingMaskKind.Layer"/> mask; <c>null</c> otherwise.</summary>
    public DrawingLayer Layer { get; }

    /// <summary>Where a <see cref="DrawingMaskKind.Plane"/> mask's plane is stretched to, in the masked content's coordinates; empty otherwise.</summary>
    public RectangleF Destination { get; }

    /// <summary>The plane's width in pixels for a <see cref="DrawingMaskKind.Plane"/> mask; 0 otherwise.</summary>
    public int PlaneWidth => _planeWidth;

    /// <summary>The plane's height in pixels for a <see cref="DrawingMaskKind.Plane"/> mask; 0 otherwise.</summary>
    public int PlaneHeight => _planeHeight;

    /// <summary>
    /// Indicates whether the mask keeps the opposite of what it describes: true for
    /// <see cref="DrawingMaskKind.Outside"/>, and for an inverted layer or plane mask.
    /// </summary>
    public bool IsInverted { get; }

    /// <summary>Indicates whether <see cref="Dispose"/> has been called (a disposed plane mask covers nothing).</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Keeps only the content inside a path.</summary>
    /// <param name="geometry">The path (its fill rule decides what is inside).</param>
    /// <param name="antialias"><c>true</c> (the default) for an anti-aliased edge.</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    public static DrawingMask Inside(PathGeometry geometry, bool antialias = true)
        => new DrawingMask(DrawingMaskKind.Inside, geometry ?? throw new ArgumentNullException(nameof(geometry)),
            antialias, null, null, RectangleF.Empty, false);

    /// <summary>Keeps only the content outside a path.</summary>
    /// <param name="geometry">The path (its fill rule decides what is inside).</param>
    /// <param name="antialias"><c>true</c> (the default) for an anti-aliased edge.</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    public static DrawingMask Outside(PathGeometry geometry, bool antialias = true)
        => new DrawingMask(DrawingMaskKind.Outside, geometry ?? throw new ArgumentNullException(nameof(geometry)),
            antialias, null, null, RectangleF.Empty, true);

    /// <summary>
    /// Keeps the content only where another layer's elements cover it - their coverage as they
    /// are drawn (anti-aliased edges and their colours' alpha count), in the masked content's
    /// coordinates, at the render's time (time windows apply). The mask layer's own opacity,
    /// transform, mask, effects and visibility are not used, and it may or may not be in the
    /// session: hide it (<see cref="DrawingLayer.IsVisible"/> <c>false</c>, which also spares the
    /// live render its cache), give it Opacity 0, or leave it out of the session to use it only as
    /// a matte.
    /// </summary>
    /// <param name="layer">The layer whose elements are the mask.</param>
    /// <param name="invert"><c>true</c> to keep the content only where the layer does NOT cover it.</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    public static DrawingMask FromLayer(DrawingLayer layer, bool invert = false)
        => new DrawingMask(DrawingMaskKind.Layer, null, true, layer ?? throw new ArgumentNullException(nameof(layer)),
            null, RectangleF.Empty, invert);

    /// <summary>
    /// Scales the content's coverage by a pixel plane's values (0 = hidden, 1 = shown; clamped),
    /// the plane stretched over <paramref name="destination"/> with bilinear sampling. Outside
    /// that rectangle the mask is 0 (nothing shown), or 1 when inverted. The plane is copied (as
    /// 8-bit values, rounded by the planes' float-to-byte rule), so later changes to it do not
    /// reach the mask - make a new mask to animate one.
    /// </summary>
    /// <param name="plane">The mask values.</param>
    /// <param name="destination">Where the plane lies, in the masked content's coordinates.</param>
    /// <param name="invert"><c>true</c> to scale by one minus the values.</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plane"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination is empty or not finite.</exception>
    public static DrawingMask FromPlane(DrawingPlane plane, RectangleF destination, bool invert = false)
    {
        if (plane == null) { throw new ArgumentNullException(nameof(plane)); }
        if (!(destination.Width > 0f) || !(destination.Height > 0f) || !Single.IsFinite(destination.X) || !Single.IsFinite(destination.Y)
            || !Single.IsFinite(destination.Width) || !Single.IsFinite(destination.Height))
        {
            throw new ArgumentException("The destination must be a finite, non-empty rectangle.", nameof(destination));
        }
        return new DrawingMask(DrawingMaskKind.Plane, null, true, null, CreatePlaneBitmap(plane), destination, invert);
    }

    /// <summary>
    /// Scales the content's coverage by a pixel plane laid over the content's coordinates one
    /// unit per pixel, from (0, 0) - the natural fit when the calibration size equals the plane's
    /// size (for example a mask made with <c>ExportMask</c> at that size).
    /// </summary>
    /// <param name="plane">The mask values.</param>
    /// <param name="invert"><c>true</c> to scale by one minus the values.</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plane"/> is null.</exception>
    public static DrawingMask FromPlane(DrawingPlane plane, bool invert = false)
    {
        if (plane == null) { throw new ArgumentNullException(nameof(plane)); }
        return FromPlane(plane, new RectangleF(0f, 0f, plane.Width, plane.Height), invert);
    }

    //The state a render cache compares: the mask's identity, plus (for a layer mask) that
    //  layer's content at the time
    internal long GetStamp(double time)
    {
        ulong hash = DrawingElement.MixStamp(0x243F6A8885A308D3UL, (ulong)_id);
        if (Kind == DrawingMaskKind.Plane && _planeBitmap == null) { hash = DrawingElement.MixStamp(hash, 1UL); }
        if (Kind != DrawingMaskKind.Layer) { return (long)hash; }
        hash = DrawingElement.MixStamp(hash, (ulong)Layer.ResetVersion);
        DrawingElement[] elements = Layer.GetElements();
        hash = DrawingElement.MixStamp(hash, (ulong)elements.Length);
        foreach (DrawingElement element in elements)
        {
            if (!element.IsDynamic) { continue; }
            bool visible = element.IsVisibleAt(time);
            hash = DrawingElement.MixStamp(hash, visible ? 1UL : 2UL);
            if (visible) { hash = DrawingElement.MixStamp(hash, (ulong)element.GetRenderStamp(time)); }
        }
        return (long)hash;
    }

    //Multiplies what is already drawn in the current (save-)layer of a canvas mapped to the
    //  masked content's coordinates by the mask: the mask's coverage is drawn into a layer that
    //  is composited destination-in (keep where covered) or destination-out (keep where not)
    internal void Apply(SKCanvas canvas, double time)
    {
        using var compositePaint = new SKPaint { BlendMode = IsInverted ? SKBlendMode.DstOut : SKBlendMode.DstIn };
        canvas.SaveLayer(compositePaint);
        switch (Kind)
        {
            case DrawingMaskKind.Inside:
            case DrawingMaskKind.Outside:
            {
                using SKPath path = Geometry.ToBackendPath();
                using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.White, IsAntialias = IsAntialiased };
                canvas.DrawPath(path, fill);
                break;
            }
            case DrawingMaskKind.Plane:
            {
                SKBitmap bitmap = _planeBitmap;
                if (bitmap == null) { break; } //disposed: covers nothing
                var destination = new SKRect(Destination.Left, Destination.Top, Destination.Right, Destination.Bottom);
                canvas.DrawBitmap(bitmap, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), null);
                break;
            }
            default:
            {
                SKColor color = Layer.ColorValue;
                foreach (DrawingElement element in Layer.GetElements())
                {
                    if (element.IsDynamic && !element.IsVisibleAt(time)) { continue; }
                    ElementPainter.Draw(canvas, element, color, time);
                }
                break;
            }
        }
        canvas.Restore();
    }

    /// <summary>
    /// Releases a plane mask's backend bitmap now instead of when the mask is collected; after
    /// this the mask covers nothing. Does nothing for path and layer masks (they hold no backend
    /// resource) and when called again. Do not dispose a mask while a render that uses it runs.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        SKBitmap bitmap = _planeBitmap;
        _planeBitmap = null;
        bitmap?.Dispose();
    }

    //A white bitmap whose straight alpha holds the plane's values
    private static SKBitmap CreatePlaneBitmap(DrawingPlane plane)
    {
        var bytes = new byte[plane.Width * plane.Height * 4];
        float[] data = plane.Data;
        for (int i = 0; i < data.Length; i++)
        {
            float v = data[i];
            byte alpha = PlaneKernels.UnitToByte(v);
            int o = i * 4;
            bytes[o] = 255;
            bytes[o + 1] = 255;
            bytes[o + 2] = 255;
            bytes[o + 3] = alpha;
        }
        return CreateStraightBitmap(bytes, plane.Width, plane.Height);
    }

    //A bitmap holding straight-alpha RGBA bytes (both backends store an unpremultiplied bitmap's
    //  bytes as given)
    internal static SKBitmap CreateStraightBitmap(byte[] rgba, int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        int rowBytes = bitmap.RowBytes;
        IntPtr pixels = bitmap.GetPixels();
        if (rowBytes == width * 4)
        {
            Marshal.Copy(rgba, 0, pixels, rgba.Length);
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(rgba, y * width * 4, pixels + (y * rowBytes), width * 4);
            }
        }
        return bitmap;
    }
}
