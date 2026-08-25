// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents paint settings used when drawing shapes, text, and images.
// </summary>
internal sealed class SKPaint : ICloneable, IDeepCloneable<SKPaint>
{
    // <summary>Gets or sets whether to fill, stroke, or both.</summary>
    public SKPaintStyle Style { get; set; } = SKPaintStyle.Fill;

    // <summary>Gets or sets whether anti-aliasing is enabled.</summary>
    public bool IsAntialias { get; set; } = false;

    // <summary>Gets or sets whether dithering is enabled.</summary>
    public bool IsDither { get; set; } = false;

    // <summary>Gets or sets the stroke width.</summary>
    public float StrokeWidth { get; set; } = 0;

    // <summary>Gets or sets the stroke cap style.</summary>
    public SKStrokeCap StrokeCap { get; set; } = SKStrokeCap.Butt;

    // <summary>Gets or sets the stroke join style.</summary>
    public SKStrokeJoin StrokeJoin { get; set; } = SKStrokeJoin.Miter;

    // <summary>Gets or sets the stroke miter limit.</summary>
    public float StrokeMiter { get; set; } = 4;

    // <summary>Gets or sets the typeface used for text rendering.</summary>
    public SKTypeface Typeface { get; set; } = null;

    // <summary>Gets or sets the text size in points.</summary>
    public float TextSize { get; set; } = 12;

    // <summary>Gets or sets the text alignment.</summary>
    public SKTextAlign TextAlign { get; set; } = SKTextAlign.Left;

    // <summary>Gets or sets whether LCD text rendering is enabled.</summary>
    public bool LcdRenderText { get; set; } = false;

    // <summary>Gets or sets whether sub-pixel text positioning is enabled.</summary>
    public bool SubpixelText { get; set; } = false;

    // <summary>Gets or sets the text encoding.</summary>
    public SKTextEncoding TextEncoding { get; set; } = SKTextEncoding.Utf8;

    // <summary>Gets or sets the paint color.</summary>
    public SKColor? Color { get; set; } = new SKColor(0x00, 0x00, 0x00, 0xFF);

    // <summary>Gets or sets the shader used for filling.</summary>
    public SKShader Shader { get; set; } = null;

    // <summary>Gets or sets the color filter applied to colors.</summary>
    public SKColorFilter ColorFilter { get; set; } = null;

    // <summary>Gets or sets the image filter applied during drawing.</summary>
    public SKImageFilter ImageFilter { get; set; } = null;

    // <summary>Gets or sets the path effect applied to strokes.</summary>
    public SKPathEffect PathEffect { get; set; } = null;

    // <summary>Gets or sets the blend mode used for compositing.</summary>
    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    // <summary>Gets or sets the filter quality for image scaling.</summary>
    public SKFilterQuality FilterQuality { get; set; } = SKFilterQuality.None;

    // <summary>Creates a deep clone of this paint.</summary>
    // <returns>A new <see cref="SKPaint"/> that is a deep copy.</returns>
    public SKPaint Clone() => DeepClone(new CloneContext());

    // <inheritdoc />
    public SKPaint DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKPaint DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPaint existing))
        {
            return existing;
        }

        var clone = new SKPaint();
        context.Add(this, clone);

        clone.Style = Style;
        clone.IsAntialias = IsAntialias;
        clone.IsDither = IsDither;
        clone.StrokeWidth = StrokeWidth;
        clone.StrokeCap = StrokeCap;
        clone.StrokeJoin = StrokeJoin;
        clone.StrokeMiter = StrokeMiter;
        clone.Typeface = Typeface?.DeepClone(context);
        clone.TextSize = TextSize;
        clone.TextAlign = TextAlign;
        clone.LcdRenderText = LcdRenderText;
        clone.SubpixelText = SubpixelText;
        clone.TextEncoding = TextEncoding;
        clone.Color = Color;
        clone.Shader = Shader?.DeepClone(context);
        clone.ColorFilter = ColorFilter?.DeepClone(context);
        clone.ImageFilter = ImageFilter?.DeepClone(context);
        clone.PathEffect = PathEffect?.DeepClone(context);
        clone.BlendMode = BlendMode;
        clone.FilterQuality = FilterQuality;

        return clone;
    }
}
