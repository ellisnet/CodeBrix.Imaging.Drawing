#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Fills;

/// <summary>
/// How the inside of a filled shape is painted when a flat color is not enough - the
/// optional extra a filled shape (circle, ellipse, rectangle, polygon) or a text shape can
/// carry instead of its single color. A shape without a fill keeps painting in its color
/// (or its layer's color) exactly as before. Fill geometry is expressed in calibrated drawing
/// units, in the same space as the shape itself - NOT relative to the shape's bounds - so a
/// gradient stays put while the shapes that use it move. The concrete kinds are
/// <see cref="LinearGradientFill"/> and <see cref="RadialGradientFill"/>; fills are
/// immutable, so one instance can be shared by any number of shapes. A fill holds no backend
/// resource (the drawing objects it turns into are made for each draw and released with the
/// paint), so it needs no disposing.
/// </summary>
public abstract class ShapeFill
{
    private protected ShapeFill()
    {
        //Only this library derives fill kinds: each must know how to become a backend shader
    }

    //The backend shader for this fill, in calibrated drawing coordinates (shapes draw onto a
    //  canvas already transformed to calibrated space, so the shader inherits that transform).
    //  A NEW shader on every call: a fill holds no backend resource of its own, so it needs no
    //  disposing however many shapes share it. The paint that uses the shader keeps its own
    //  reference (see DrawingShape.CreateFillPaint), so the caller may release it at once
    internal abstract SKShader CreateShader();
}
