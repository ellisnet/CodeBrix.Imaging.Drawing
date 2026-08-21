using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

/// <summary>
/// Maps the shim (ShimSkiaSharp) intermediate-representation types produced by the SVG
/// scene compiler onto the managed CodeBrix.Imaging.Drawing.NoSkia drawing types, and
/// replays compiled display lists (<see cref="SKPicture"/>) onto a
/// <see cref="DrawingCanvas"/> - the SkiaSharp-free counterpart of CodeBrix.SkiaSvg's
/// <c>SkiaModel</c>. Features the managed canvas cannot express (picture/pattern shaders,
/// Perlin-noise shaders, color spaces, filter quality on paints) degrade gracefully and
/// are noted at their conversion sites.
/// </summary>
public class NoSkiaModel
{
    private const float Kappa = 0.5522847498f; //Cubic-Bezier circle-quadrant constant

    private readonly INoSkiaTextRenderer _textRenderer;
    private readonly INoSkiaImageFilterFactory _imageFilterFactory;
    private readonly Dictionary<SKImage, DrawingBitmap> _imageCache = new Dictionary<SKImage, DrawingBitmap>();

    /// <summary>
    /// Initializes a new <see cref="NoSkiaModel"/>.
    /// </summary>
    /// <param name="textRenderer">
    /// The renderer for text commands; or <c>null</c> to skip text commands.
    /// </param>
    /// <param name="imageFilterFactory">
    /// The factory that builds <see cref="DrawingImageFilter"/> evaluators from shim
    /// image-filter graphs; or <c>null</c> to render save-layers without image filters.
    /// </param>
    public NoSkiaModel(INoSkiaTextRenderer textRenderer = null, INoSkiaImageFilterFactory imageFilterFactory = null)
    {
        _textRenderer = textRenderer;
        _imageFilterFactory = imageFilterFactory;
    }

    /// <summary>Converts a shim <see cref="SKPoint"/> to a drawing point.</summary>
    /// <param name="point">The shim point.</param>
    /// <returns>The corresponding drawing point.</returns>
    public DrawingPoint ToDrawingPoint(SKPoint point)
    {
        return new DrawingPoint(point.X, point.Y);
    }

    /// <summary>Converts a shim <see cref="SKRect"/> to a drawing rectangle.</summary>
    /// <param name="rect">The shim rectangle.</param>
    /// <returns>The corresponding drawing rectangle.</returns>
    public DrawingRect ToDrawingRect(SKRect rect)
    {
        return new DrawingRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>
    /// Converts a shim <see cref="SKMatrix"/> (Skia field convention: ScaleX/SkewX/TransX,
    /// SkewY/ScaleY/TransY) to a row-vector <see cref="Matrix3x2"/>, so that a shim
    /// translate(10, 0) moves points +10 in x. The shim's perspective values have no
    /// affine counterpart and are dropped (SVG transforms never carry perspective).
    /// </summary>
    /// <param name="matrix">The shim matrix.</param>
    /// <returns>The corresponding affine matrix.</returns>
    public Matrix3x2 ToMatrix(SKMatrix matrix)
    {
        //Shim maps points as x' = x*ScaleX + y*SkewX + TransX; Matrix3x2 (row vector)
        //maps as x' = x*M11 + y*M21 + M31 - so SkewX lands in M21 and SkewY in M12.
        return new Matrix3x2(
            matrix.ScaleX, matrix.SkewY,
            matrix.SkewX, matrix.ScaleY,
            matrix.TransX, matrix.TransY);
    }

    /// <summary>Converts a shim <see cref="SKColor"/> to a drawing color.</summary>
    /// <param name="color">The shim color.</param>
    /// <returns>The corresponding drawing color.</returns>
    public DrawingColor ToDrawingColor(SKColor color)
    {
        return new DrawingColor(color.Red, color.Green, color.Blue, color.Alpha);
    }

    /// <summary>Converts a shim <see cref="SKColorF"/> to a drawing color.</summary>
    /// <param name="color">The shim floating-point color.</param>
    /// <returns>The corresponding drawing color.</returns>
    public DrawingColor ToDrawingColor(SKColorF color)
    {
        return new DrawingColor(
            (byte)Math.Clamp((int)((color.Red * 255f) + 0.5f), 0, 255),
            (byte)Math.Clamp((int)((color.Green * 255f) + 0.5f), 0, 255),
            (byte)Math.Clamp((int)((color.Blue * 255f) + 0.5f), 0, 255),
            (byte)Math.Clamp((int)((color.Alpha * 255f) + 0.5f), 0, 255));
    }

    /// <summary>Converts an array of shim colors to an array of drawing colors.</summary>
    /// <param name="colors">The shim colors.</param>
    /// <returns>An array of corresponding drawing colors.</returns>
    public DrawingColor[] ToDrawingColors(SKColor[] colors)
    {
        var drawingColors = new DrawingColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            drawingColors[i] = ToDrawingColor(colors[i]);
        }
        return drawingColors;
    }

    /// <summary>Converts an array of shim floating-point colors to an array of drawing colors.</summary>
    /// <param name="colors">The shim floating-point colors.</param>
    /// <returns>An array of corresponding drawing colors.</returns>
    public DrawingColor[] ToDrawingColors(SKColorF[] colors)
    {
        var drawingColors = new DrawingColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            drawingColors[i] = ToDrawingColor(colors[i]);
        }
        return drawingColors;
    }

    /// <summary>Converts a shim <see cref="SKPaintStyle"/> to a drawing paint style.</summary>
    /// <param name="paintStyle">The shim paint style.</param>
    /// <returns>The corresponding drawing paint style.</returns>
    public DrawingPaintStyle ToDrawingPaintStyle(SKPaintStyle paintStyle)
    {
        return paintStyle switch
        {
            SKPaintStyle.Fill => DrawingPaintStyle.Fill,
            SKPaintStyle.Stroke => DrawingPaintStyle.Stroke,
            SKPaintStyle.StrokeAndFill => DrawingPaintStyle.StrokeAndFill,
            _ => DrawingPaintStyle.Fill
        };
    }

    /// <summary>Converts a shim <see cref="SKStrokeCap"/> to a drawing stroke cap.</summary>
    /// <param name="strokeCap">The shim stroke cap.</param>
    /// <returns>The corresponding drawing stroke cap.</returns>
    public DrawingStrokeCap ToDrawingStrokeCap(SKStrokeCap strokeCap)
    {
        return strokeCap switch
        {
            SKStrokeCap.Butt => DrawingStrokeCap.Butt,
            SKStrokeCap.Round => DrawingStrokeCap.Round,
            SKStrokeCap.Square => DrawingStrokeCap.Square,
            _ => DrawingStrokeCap.Butt
        };
    }

    /// <summary>Converts a shim <see cref="SKStrokeJoin"/> to a drawing stroke join.</summary>
    /// <param name="strokeJoin">The shim stroke join.</param>
    /// <returns>The corresponding drawing stroke join.</returns>
    public DrawingStrokeJoin ToDrawingStrokeJoin(SKStrokeJoin strokeJoin)
    {
        return strokeJoin switch
        {
            SKStrokeJoin.Miter => DrawingStrokeJoin.Miter,
            SKStrokeJoin.Round => DrawingStrokeJoin.Round,
            SKStrokeJoin.Bevel => DrawingStrokeJoin.Bevel,
            _ => DrawingStrokeJoin.Miter
        };
    }

    /// <summary>Converts a shim <see cref="SKShaderTileMode"/> to a drawing shader tile mode.</summary>
    /// <param name="shaderTileMode">The shim shader tile mode.</param>
    /// <returns>The corresponding drawing shader tile mode.</returns>
    public DrawingShaderTileMode ToDrawingShaderTileMode(SKShaderTileMode shaderTileMode)
    {
        return shaderTileMode switch
        {
            SKShaderTileMode.Clamp => DrawingShaderTileMode.Clamp,
            SKShaderTileMode.Repeat => DrawingShaderTileMode.Repeat,
            SKShaderTileMode.Mirror => DrawingShaderTileMode.Mirror,
            SKShaderTileMode.Decal => DrawingShaderTileMode.Decal,
            _ => DrawingShaderTileMode.Clamp
        };
    }

    /// <summary>Converts a shim <see cref="SKBlendMode"/> to a drawing blend mode.</summary>
    /// <param name="blendMode">The shim blend mode.</param>
    /// <returns>The corresponding drawing blend mode.</returns>
    public DrawingBlendMode ToDrawingBlendMode(SKBlendMode blendMode)
    {
        return blendMode switch
        {
            SKBlendMode.Clear => DrawingBlendMode.Clear,
            SKBlendMode.Src => DrawingBlendMode.Src,
            SKBlendMode.Dst => DrawingBlendMode.Dst,
            SKBlendMode.SrcOver => DrawingBlendMode.SrcOver,
            SKBlendMode.DstOver => DrawingBlendMode.DstOver,
            SKBlendMode.SrcIn => DrawingBlendMode.SrcIn,
            SKBlendMode.DstIn => DrawingBlendMode.DstIn,
            SKBlendMode.SrcOut => DrawingBlendMode.SrcOut,
            SKBlendMode.DstOut => DrawingBlendMode.DstOut,
            SKBlendMode.SrcATop => DrawingBlendMode.SrcATop,
            SKBlendMode.DstATop => DrawingBlendMode.DstATop,
            SKBlendMode.Xor => DrawingBlendMode.Xor,
            SKBlendMode.Plus => DrawingBlendMode.Plus,
            SKBlendMode.Modulate => DrawingBlendMode.Modulate,
            SKBlendMode.Screen => DrawingBlendMode.Screen,
            SKBlendMode.Overlay => DrawingBlendMode.Overlay,
            SKBlendMode.Darken => DrawingBlendMode.Darken,
            SKBlendMode.Lighten => DrawingBlendMode.Lighten,
            SKBlendMode.ColorDodge => DrawingBlendMode.ColorDodge,
            SKBlendMode.ColorBurn => DrawingBlendMode.ColorBurn,
            SKBlendMode.HardLight => DrawingBlendMode.HardLight,
            SKBlendMode.SoftLight => DrawingBlendMode.SoftLight,
            SKBlendMode.Difference => DrawingBlendMode.Difference,
            SKBlendMode.Exclusion => DrawingBlendMode.Exclusion,
            SKBlendMode.Multiply => DrawingBlendMode.Multiply,
            SKBlendMode.Hue => DrawingBlendMode.Hue,
            SKBlendMode.Saturation => DrawingBlendMode.Saturation,
            SKBlendMode.Color => DrawingBlendMode.Color,
            SKBlendMode.Luminosity => DrawingBlendMode.Luminosity,
            _ => DrawingBlendMode.Clear
        };
    }

    /// <summary>Converts a shim <see cref="SKClipOperation"/> to a drawing clip operation.</summary>
    /// <param name="clipOperation">The shim clip operation.</param>
    /// <returns>The corresponding drawing clip operation.</returns>
    public DrawingClipOperation ToDrawingClipOperation(SKClipOperation clipOperation)
    {
        return clipOperation switch
        {
            SKClipOperation.Difference => DrawingClipOperation.Difference,
            SKClipOperation.Intersect => DrawingClipOperation.Intersect,
            _ => DrawingClipOperation.Difference
        };
    }

    /// <summary>Converts a shim <see cref="SKPathFillType"/> to a drawing path fill type.</summary>
    /// <param name="pathFillType">The shim path fill type.</param>
    /// <returns>The corresponding drawing path fill type.</returns>
    public DrawingPathFillType ToDrawingPathFillType(SKPathFillType pathFillType)
    {
        return pathFillType switch
        {
            SKPathFillType.Winding => DrawingPathFillType.Winding,
            SKPathFillType.EvenOdd => DrawingPathFillType.EvenOdd,
            _ => DrawingPathFillType.Winding
        };
    }

    /// <summary>Converts a shim <see cref="SKFilterQuality"/> to drawing sampling options.</summary>
    /// <param name="filterQuality">The shim filter quality.</param>
    /// <returns>The corresponding drawing sampling options.</returns>
    public DrawingSamplingOptions ToDrawingSamplingOptions(SKFilterQuality filterQuality)
    {
        return filterQuality switch
        {
            SKFilterQuality.None => new DrawingSamplingOptions(DrawingFilterMode.Nearest),
            SKFilterQuality.Low => new DrawingSamplingOptions(DrawingFilterMode.Linear),
            SKFilterQuality.Medium => new DrawingSamplingOptions(DrawingFilterMode.Linear, DrawingMipmapMode.Linear),
            SKFilterQuality.High => new DrawingSamplingOptions(DrawingCubicResampler.Mitchell),
            _ => new DrawingSamplingOptions(DrawingFilterMode.Nearest)
        };
    }

    /// <summary>
    /// Decodes a shim <see cref="SKImage"/>'s encoded bytes into a drawing bitmap. Decoded
    /// bitmaps are cached per shim image instance, so a picture drawn repeatedly decodes
    /// each embedded image only once.
    /// </summary>
    /// <param name="image">The shim image.</param>
    /// <returns>The decoded bitmap; or <c>null</c> when the image carries no decodable bytes.</returns>
    public DrawingBitmap ToDrawingBitmap(SKImage image)
    {
        if (image?.Data == null)
        {
            return null;
        }

        if (_imageCache.TryGetValue(image, out DrawingBitmap cached) && !cached.IsDisposed)
        {
            return cached;
        }

        DrawingBitmap decoded = DrawingBitmap.Decode(image.Data);
        if (decoded != null)
        {
            _imageCache[image] = decoded;
        }
        return decoded;
    }

    /// <summary>Converts a shim <see cref="SKShader"/> to a drawing shader.</summary>
    /// <param name="shader">The shim shader.</param>
    /// <returns>
    /// The corresponding drawing shader; or <c>null</c> for shader kinds the managed
    /// canvas cannot express (picture/pattern shaders and Perlin-noise shaders), in which
    /// case the paint falls back to its plain color. Shim color spaces are dropped - the
    /// managed canvas always works in sRGB.
    /// </returns>
    public DrawingShader ToDrawingShader(SKShader shader)
    {
        switch (shader)
        {
            case ColorShader colorShader:
                {
                    //ColorSpace dropped: the managed canvas has no color-space support
                    return DrawingShader.CreateColor(ToDrawingColor(colorShader.Color));
                }
            case LinearGradientShader linearGradientShader:
                {
                    if (linearGradientShader.Colors == null || linearGradientShader.Colors.Length == 0
                        || linearGradientShader.ColorPos == null)
                    {
                        return null;
                    }

                    return DrawingShader.CreateLinearGradient(
                        ToDrawingPoint(linearGradientShader.Start),
                        ToDrawingPoint(linearGradientShader.End),
                        ToDrawingColors(linearGradientShader.Colors),
                        linearGradientShader.ColorPos,
                        ToDrawingShaderTileMode(linearGradientShader.Mode),
                        linearGradientShader.LocalMatrix is { } linearLocal ? ToMatrix(linearLocal) : (Matrix3x2?)null);
                }
            case RadialGradientShader radialGradientShader:
                {
                    if (radialGradientShader.Colors == null || radialGradientShader.Colors.Length == 0
                        || radialGradientShader.ColorPos == null)
                    {
                        return null;
                    }

                    return DrawingShader.CreateRadialGradient(
                        ToDrawingPoint(radialGradientShader.Center),
                        radialGradientShader.Radius,
                        ToDrawingColors(radialGradientShader.Colors),
                        radialGradientShader.ColorPos,
                        ToDrawingShaderTileMode(radialGradientShader.Mode),
                        radialGradientShader.LocalMatrix is { } radialLocal ? ToMatrix(radialLocal) : (Matrix3x2?)null);
                }
            case TwoPointConicalGradientShader twoPointConicalGradientShader:
                {
                    if (twoPointConicalGradientShader.Colors == null || twoPointConicalGradientShader.Colors.Length == 0
                        || twoPointConicalGradientShader.ColorPos == null)
                    {
                        return null;
                    }

                    return DrawingShader.CreateTwoPointConicalGradient(
                        ToDrawingPoint(twoPointConicalGradientShader.Start),
                        twoPointConicalGradientShader.StartRadius,
                        ToDrawingPoint(twoPointConicalGradientShader.End),
                        twoPointConicalGradientShader.EndRadius,
                        ToDrawingColors(twoPointConicalGradientShader.Colors),
                        twoPointConicalGradientShader.ColorPos,
                        ToDrawingShaderTileMode(twoPointConicalGradientShader.Mode),
                        twoPointConicalGradientShader.LocalMatrix is { } conicalLocal ? ToMatrix(conicalLocal) : (Matrix3x2?)null);
                }
            case PictureShader _:
                {
                    //The managed canvas has no bitmap/picture-tiling shader, so SVG
                    //<pattern> fills cannot be replayed; returning null leaves the paint
                    //on its plain color (graceful degradation).
                    return null;
                }
            case PerlinNoiseFractalNoiseShader _:
            case PerlinNoiseTurbulenceShader _:
                {
                    //Perlin-noise shaders (SVG feTurbulence) are not supported - dropped
                    return null;
                }
            default:
                return null;
        }
    }

    /// <summary>Converts a shim <see cref="SKColorFilter"/> to a drawing color filter.</summary>
    /// <param name="colorFilter">The shim color filter.</param>
    /// <returns>The corresponding drawing color filter, or <c>null</c>.</returns>
    public DrawingColorFilter ToDrawingColorFilter(SKColorFilter colorFilter)
    {
        switch (colorFilter)
        {
            case BlendModeColorFilter blendModeColorFilter:
                {
                    return DrawingColorFilter.CreateBlendMode(
                        ToDrawingColor(blendModeColorFilter.Color),
                        ToDrawingBlendMode(blendModeColorFilter.Mode));
                }
            case ColorMatrixColorFilter colorMatrixColorFilter:
                {
                    if (colorMatrixColorFilter.Matrix == null || colorMatrixColorFilter.Matrix.Length != 20)
                    {
                        return null;
                    }
                    return DrawingColorFilter.CreateColorMatrix(colorMatrixColorFilter.Matrix);
                }
            case LumaColorColorFilter _:
                {
                    return DrawingColorFilter.CreateLumaColor();
                }
            case TableColorFilter tableColorFilter:
                {
                    if (tableColorFilter.TableA == null
                        || tableColorFilter.TableR == null
                        || tableColorFilter.TableG == null
                        || tableColorFilter.TableB == null)
                    {
                        return null;
                    }
                    return DrawingColorFilter.CreateTable(
                        tableColorFilter.TableA,
                        tableColorFilter.TableR,
                        tableColorFilter.TableG,
                        tableColorFilter.TableB);
                }
            default:
                {
                    return null;
                }
        }
    }

    /// <summary>
    /// Converts a shim <see cref="SKImageFilter"/> graph through the optional
    /// <see cref="INoSkiaImageFilterFactory"/>.
    /// </summary>
    /// <param name="imageFilter">The shim image filter.</param>
    /// <returns>
    /// The built evaluator; or <c>null</c> when no factory was provided or the factory
    /// cannot evaluate the graph (the save-layer then composites without the filter).
    /// </returns>
    public DrawingImageFilter ToDrawingImageFilter(SKImageFilter imageFilter)
    {
        if (imageFilter == null || _imageFilterFactory == null)
        {
            return null;
        }
        return _imageFilterFactory.Create(imageFilter);
    }

    /// <summary>Converts a shim <see cref="SKPathEffect"/> to a drawing path effect (dashing only).</summary>
    /// <param name="pathEffect">The shim path effect.</param>
    /// <returns>The corresponding drawing path effect, or <c>null</c>.</returns>
    public DrawingPathEffect ToDrawingPathEffect(SKPathEffect pathEffect)
    {
        switch (pathEffect)
        {
            case DashPathEffect dashPathEffect:
                {
                    //DrawingPathEffect.CreateDash validates strictly; unusable interval
                    //sets (which Skia would silently ignore) drop the effect instead
                    float[] intervals = dashPathEffect.Intervals;
                    if (intervals == null || intervals.Length < 2 || intervals.Length % 2 != 0)
                    {
                        return null;
                    }
                    float sum = 0;
                    foreach (float interval in intervals)
                    {
                        if (interval < 0) { return null; }
                        sum += interval;
                    }
                    if (sum <= 0) { return null; }

                    return DrawingPathEffect.CreateDash(intervals, dashPathEffect.Phase);
                }
            default:
                {
                    return null;
                }
        }
    }

    /// <summary>Converts a shim <see cref="SKPaint"/> to a drawing paint.</summary>
    /// <param name="paint">The shim paint.</param>
    /// <returns>The corresponding drawing paint, or <c>null</c>.</returns>
    public DrawingPaint ToDrawingPaint(SKPaint paint)
    {
        if (paint == null)
        {
            return null;
        }

        //Typeface/text properties, dithering, LCD rendering, and filter quality have no
        //counterpart on the managed paint and are dropped (filter quality is honored
        //separately when images are drawn)
        return new DrawingPaint
        {
            Style = ToDrawingPaintStyle(paint.Style),
            IsAntialias = paint.IsAntialias,
            StrokeWidth = paint.StrokeWidth,
            StrokeCap = ToDrawingStrokeCap(paint.StrokeCap),
            StrokeJoin = ToDrawingStrokeJoin(paint.StrokeJoin),
            StrokeMiter = paint.StrokeMiter,
            Color = paint.Color is { } color ? ToDrawingColor(color) : DrawingColor.Empty,
            Shader = ToDrawingShader(paint.Shader),
            ColorFilter = ToDrawingColorFilter(paint.ColorFilter),
            ImageFilter = ToDrawingImageFilter(paint.ImageFilter),
            PathEffect = ToDrawingPathEffect(paint.PathEffect),
            BlendMode = ToDrawingBlendMode(paint.BlendMode),
        };
    }

    /// <summary>Converts a shim <see cref="SKPath"/> to a drawing path.</summary>
    /// <param name="path">The shim path.</param>
    /// <returns>The corresponding drawing path.</returns>
    public DrawingPath ToDrawingPath(SKPath path)
    {
        return ToDrawingPath(path, null);
    }

    /// <summary>
    /// Converts a shim <see cref="SKPath"/> to a drawing path, optionally transforming
    /// every point. Endpoint arcs are converted to cubic Béziers, and
    /// oval/circle/round-rect contours become cubic approximations, since the managed path
    /// carries only move/line/quad/cubic/close verbs.
    /// </summary>
    /// <param name="path">The shim path.</param>
    /// <param name="transform">An optional transform applied to every emitted point.</param>
    /// <returns>The corresponding drawing path.</returns>
    public DrawingPath ToDrawingPath(SKPath path, Matrix3x2? transform)
    {
        var builder = new DrawingPathBuilder();
        builder.SetFillType(ToDrawingPathFillType(path.FillType));
        AppendPath(path, transform, builder);
        return builder.Detach();
    }

    /// <summary>
    /// Converts a shim <see cref="ClipPath"/> to a single drawing path holding the union
    /// of its member clips (each transformed by its own transform composed with the clip
    /// path's transform). The union is approximated by appending every member's contours
    /// under a nonzero-winding fill - exact for the single-member clips SVG produces
    /// almost exclusively. Nested intersect-clips are not merged here; the replayer
    /// applies them as additional canvas clips.
    /// </summary>
    /// <param name="clipPath">The shim clip path.</param>
    /// <returns>The corresponding drawing path, or <c>null</c>.</returns>
    public DrawingPath ToDrawingPath(ClipPath clipPath)
    {
        if (clipPath?.Clips == null)
        {
            return null;
        }

        Matrix3x2 outer = clipPath.Transform is { } outerMatrix ? ToMatrix(outerMatrix) : Matrix3x2.Identity;
        return BuildClipUnionPath(clipPath, outer, null);
    }

    /// <summary>Replays a single canvas command onto a drawing canvas.</summary>
    /// <param name="command">The canvas command to replay.</param>
    /// <param name="canvas">The target drawing canvas.</param>
    public void Draw(CanvasCommand command, DrawingCanvas canvas)
    {
        switch (command)
        {
            case ClipPathCanvasCommand clipPathCanvasCommand:
                {
                    if (clipPathCanvasCommand.ClipPath != null)
                    {
                        ApplyClipPath(
                            clipPathCanvasCommand.ClipPath,
                            Matrix3x2.Identity,
                            canvas,
                            ToDrawingClipOperation(clipPathCanvasCommand.Operation),
                            clipPathCanvasCommand.Antialias);
                    }
                    break;
                }
            case ClipRectCanvasCommand clipRectCanvasCommand:
                {
                    canvas.ClipRect(
                        ToDrawingRect(clipRectCanvasCommand.Rect),
                        ToDrawingClipOperation(clipRectCanvasCommand.Operation),
                        clipRectCanvasCommand.Antialias);
                    break;
                }
            case SaveCanvasCommand _:
                {
                    canvas.Save();
                    break;
                }
            case RestoreCanvasCommand _:
                {
                    canvas.Restore();
                    break;
                }
            case SetMatrixCanvasCommand setMatrixCanvasCommand:
                {
                    //Mirroring SkiaModel: concatenate the recorded DELTA matrix onto the
                    //current transform (never SetMatrix with the recorded total), so any
                    //base transform applied to the canvas before replay - e.g. output
                    //scaling - survives
                    canvas.Concat(ToMatrix(setMatrixCanvasCommand.DeltaMatrix));
                    break;
                }
            case SaveLayerCanvasCommand saveLayerCanvasCommand:
                {
                    if (saveLayerCanvasCommand.Paint != null)
                    {
                        using DrawingPaint layerPaint = ToDrawingPaint(saveLayerCanvasCommand.Paint);
                        canvas.SaveLayer(layerPaint);
                    }
                    else
                    {
                        canvas.SaveLayer();
                    }
                    break;
                }
            case DrawImageCanvasCommand drawImageCanvasCommand:
                {
                    if (drawImageCanvasCommand.Image != null)
                    {
                        DrawingBitmap bitmap = ToDrawingBitmap(drawImageCanvasCommand.Image);
                        if (bitmap != null)
                        {
                            using DrawingPaint imagePaint = ToDrawingPaint(drawImageCanvasCommand.Paint);
                            canvas.DrawBitmap(
                                bitmap,
                                ToDrawingRect(drawImageCanvasCommand.Source),
                                ToDrawingRect(drawImageCanvasCommand.Dest),
                                ToDrawingSamplingOptions(drawImageCanvasCommand.Paint?.FilterQuality ?? SKFilterQuality.None),
                                imagePaint);
                        }
                    }
                    break;
                }
            case DrawPictureCanvasCommand drawPictureCanvasCommand:
                {
                    if (drawPictureCanvasCommand.Picture != null)
                    {
                        Draw(drawPictureCanvasCommand.Picture, canvas);
                    }
                    break;
                }
            case DrawPathCanvasCommand drawPathCanvasCommand:
                {
                    if (drawPathCanvasCommand.Path != null && drawPathCanvasCommand.Paint != null)
                    {
                        using DrawingPath path = ToDrawingPath(drawPathCanvasCommand.Path);
                        using DrawingPaint paint = ToDrawingPaint(drawPathCanvasCommand.Paint);
                        canvas.DrawPath(path, paint);
                    }
                    break;
                }
            case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                {
                    if (drawTextBlobCanvasCommand.TextBlob?.Points != null && drawTextBlobCanvasCommand.Paint != null)
                    {
                        _textRenderer?.DrawTextBlob(
                            drawTextBlobCanvasCommand.TextBlob,
                            drawTextBlobCanvasCommand.X,
                            drawTextBlobCanvasCommand.Y,
                            drawTextBlobCanvasCommand.Paint,
                            canvas);
                    }
                    break;
                }
            case DrawTextCanvasCommand drawTextCanvasCommand:
                {
                    if (drawTextCanvasCommand.Paint != null)
                    {
                        _textRenderer?.DrawText(
                            drawTextCanvasCommand.Text,
                            drawTextCanvasCommand.X,
                            drawTextCanvasCommand.Y,
                            drawTextCanvasCommand.Paint,
                            canvas);
                    }
                    break;
                }
            case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                {
                    if (drawTextOnPathCanvasCommand.Path != null && drawTextOnPathCanvasCommand.Paint != null)
                    {
                        _textRenderer?.DrawTextOnPath(
                            drawTextOnPathCanvasCommand.Text,
                            drawTextOnPathCanvasCommand.Path,
                            drawTextOnPathCanvasCommand.HOffset,
                            drawTextOnPathCanvasCommand.VOffset,
                            drawTextOnPathCanvasCommand.Paint,
                            canvas);
                    }
                    break;
                }
        }
    }

    /// <summary>Replays a shim picture's display list onto a drawing canvas.</summary>
    /// <param name="picture">The shim picture to replay.</param>
    /// <param name="canvas">The target drawing canvas.</param>
    public void Draw(SKPicture picture, DrawingCanvas canvas)
    {
        if (picture?.Commands == null)
        {
            return;
        }

        foreach (CanvasCommand canvasCommand in picture.Commands)
        {
            Draw(canvasCommand, canvas);
        }
    }

    private void ApplyClipPath(ClipPath clipPath, Matrix3x2 outerTransform, DrawingCanvas canvas,
        DrawingClipOperation operation, bool antialias)
    {
        if (clipPath?.Clips == null || clipPath.Clips.Count == 0)
        {
            return;
        }

        Matrix3x2 clipPathTransform =
            (clipPath.Transform is { } transform ? ToMatrix(transform) : Matrix3x2.Identity) * outerTransform;

        var nestedClips = new List<(ClipPath Clip, Matrix3x2 Transform)>();
        DrawingPath unionPath = BuildClipUnionPath(clipPath, clipPathTransform, nestedClips);
        if (unionPath == null)
        {
            return;
        }

        using (unionPath)
        {
            canvas.ClipPath(unionPath, operation, antialias);
        }

        //The managed path type has no boolean operations, so intersect-clips nested under
        //this clip path are applied as additional canvas clips instead. This is exact for
        //single-member clip paths; for multi-member unions it over-restricts slightly
        //(the nested clip constrains every member, not just its own). Nested clips are
        //only meaningful for intersect semantics, so they are skipped for Difference.
        if (operation == DrawingClipOperation.Intersect)
        {
            if (clipPath.Clip?.Clips != null && clipPath.Clip.Clips.Count > 0)
            {
                ApplyClipPath(clipPath.Clip, clipPathTransform, canvas, DrawingClipOperation.Intersect, antialias);
            }

            foreach ((ClipPath nested, Matrix3x2 nestedTransform) in nestedClips)
            {
                ApplyClipPath(nested, nestedTransform, canvas, DrawingClipOperation.Intersect, antialias);
            }
        }
    }

    private DrawingPath BuildClipUnionPath(ClipPath clipPath, Matrix3x2 clipPathTransform,
        List<(ClipPath Clip, Matrix3x2 Transform)> nestedClips)
    {
        var builder = new DrawingPathBuilder();
        var fillType = DrawingPathFillType.Winding;
        var anyContours = false;

        foreach (PathClip clip in clipPath.Clips)
        {
            if (clip.Path == null)
            {
                return null;
            }

            Matrix3x2 memberTransform =
                (clip.Transform is { } transform ? ToMatrix(transform) : Matrix3x2.Identity) * clipPathTransform;
            AppendPath(clip.Path, memberTransform.IsIdentity ? (Matrix3x2?)null : memberTransform, builder);
            anyContours = true;

            if (clipPath.Clips.Count == 1)
            {
                fillType = ToDrawingPathFillType(clip.Path.FillType);
            }

            if (nestedClips != null && clip.Clip?.Clips != null && clip.Clip.Clips.Count > 0)
            {
                nestedClips.Add((clip.Clip, memberTransform));
            }
        }

        if (!anyContours)
        {
            return null;
        }

        builder.SetFillType(fillType);
        return builder.Detach();
    }

    private void AppendPath(SKPath path, Matrix3x2? transform, DrawingPathBuilder builder)
    {
        if (path?.Commands == null)
        {
            return;
        }

        //The current point and contour start are tracked in the path's own (untransformed)
        //coordinates - the arc-to-cubic conversion needs them there
        var current = new SKPoint();
        var contourStart = new SKPoint();
        var haveCurrent = false;

        foreach (PathCommand pathCommand in path.Commands)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                    {
                        builder.MoveTo(TransformPoint(moveToPathCommand.X, moveToPathCommand.Y, transform));
                        current = new SKPoint(moveToPathCommand.X, moveToPathCommand.Y);
                        contourStart = current;
                        haveCurrent = true;
                        break;
                    }
                case LineToPathCommand lineToPathCommand:
                    {
                        builder.LineTo(TransformPoint(lineToPathCommand.X, lineToPathCommand.Y, transform));
                        current = new SKPoint(lineToPathCommand.X, lineToPathCommand.Y);
                        haveCurrent = true;
                        break;
                    }
                case ArcToPathCommand arcToPathCommand:
                    {
                        if (!haveCurrent)
                        {
                            //Skia treats an arc with no current point as starting at the origin
                            builder.MoveTo(TransformPoint(0, 0, transform));
                            current = new SKPoint();
                            contourStart = current;
                            haveCurrent = true;
                        }
                        AppendArcAsCubics(builder, transform, current, arcToPathCommand);
                        current = new SKPoint(arcToPathCommand.X, arcToPathCommand.Y);
                        break;
                    }
                case QuadToPathCommand quadToPathCommand:
                    {
                        builder.QuadTo(
                            TransformPoint(quadToPathCommand.X0, quadToPathCommand.Y0, transform),
                            TransformPoint(quadToPathCommand.X1, quadToPathCommand.Y1, transform));
                        current = new SKPoint(quadToPathCommand.X1, quadToPathCommand.Y1);
                        haveCurrent = true;
                        break;
                    }
                case CubicToPathCommand cubicToPathCommand:
                    {
                        builder.CubicTo(
                            TransformPoint(cubicToPathCommand.X0, cubicToPathCommand.Y0, transform),
                            TransformPoint(cubicToPathCommand.X1, cubicToPathCommand.Y1, transform),
                            TransformPoint(cubicToPathCommand.X2, cubicToPathCommand.Y2, transform));
                        current = new SKPoint(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                        haveCurrent = true;
                        break;
                    }
                case ClosePathCommand _:
                    {
                        builder.Close();
                        current = contourStart;
                        break;
                    }
                case AddRectPathCommand addRectPathCommand:
                    {
                        SKRect rect = addRectPathCommand.Rect;
                        builder.MoveTo(TransformPoint(rect.Left, rect.Top, transform));
                        builder.LineTo(TransformPoint(rect.Right, rect.Top, transform));
                        builder.LineTo(TransformPoint(rect.Right, rect.Bottom, transform));
                        builder.LineTo(TransformPoint(rect.Left, rect.Bottom, transform));
                        builder.Close();
                        current = new SKPoint(rect.Right, rect.Bottom);
                        contourStart = current;
                        haveCurrent = true;
                        break;
                    }
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    {
                        AppendRoundRect(builder, transform, addRoundRectPathCommand.Rect,
                            addRoundRectPathCommand.Rx, addRoundRectPathCommand.Ry);
                        current = addRoundRectPathCommand.Rect.BottomRight;
                        contourStart = current;
                        haveCurrent = true;
                        break;
                    }
                case AddOvalPathCommand addOvalPathCommand:
                    {
                        SKRect rect = addOvalPathCommand.Rect;
                        AppendOval(builder, transform,
                            rect.Left + (rect.Width / 2f), rect.Top + (rect.Height / 2f),
                            rect.Width / 2f, rect.Height / 2f);
                        current = rect.BottomRight;
                        contourStart = current;
                        haveCurrent = true;
                        break;
                    }
                case AddCirclePathCommand addCirclePathCommand:
                    {
                        AppendOval(builder, transform,
                            addCirclePathCommand.X, addCirclePathCommand.Y,
                            addCirclePathCommand.Radius, addCirclePathCommand.Radius);
                        current = new SKPoint(
                            addCirclePathCommand.X + addCirclePathCommand.Radius,
                            addCirclePathCommand.Y + addCirclePathCommand.Radius);
                        contourStart = current;
                        haveCurrent = true;
                        break;
                    }
                case AddPolyPathCommand addPolyPathCommand:
                    {
                        IList<SKPoint> points = addPolyPathCommand.Points;
                        if (points != null && points.Count > 0)
                        {
                            builder.MoveTo(TransformPoint(points[0].X, points[0].Y, transform));
                            for (int i = 1; i < points.Count; i++)
                            {
                                builder.LineTo(TransformPoint(points[i].X, points[i].Y, transform));
                            }
                            if (addPolyPathCommand.Close)
                            {
                                builder.Close();
                            }
                            current = points[points.Count - 1];
                            contourStart = points[0];
                            haveCurrent = true;
                        }
                        break;
                    }
            }
        }
    }

    private void AppendRoundRect(DrawingPathBuilder builder, Matrix3x2? transform, SKRect rect,
        float radiusX, float radiusY)
    {
        radiusX = Math.Clamp(radiusX, 0, rect.Width / 2f);
        radiusY = Math.Clamp(radiusY, 0, rect.Height / 2f);
        if (radiusX <= 0 || radiusY <= 0)
        {
            builder.MoveTo(TransformPoint(rect.Left, rect.Top, transform));
            builder.LineTo(TransformPoint(rect.Right, rect.Top, transform));
            builder.LineTo(TransformPoint(rect.Right, rect.Bottom, transform));
            builder.LineTo(TransformPoint(rect.Left, rect.Bottom, transform));
            builder.Close();
            return;
        }

        float left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom;
        float controlX = radiusX * (1 - Kappa);
        float controlY = radiusY * (1 - Kappa);

        builder.MoveTo(TransformPoint(left + radiusX, top, transform));
        builder.LineTo(TransformPoint(right - radiusX, top, transform));
        builder.CubicTo(
            TransformPoint(right - controlX, top, transform),
            TransformPoint(right, top + controlY, transform),
            TransformPoint(right, top + radiusY, transform));
        builder.LineTo(TransformPoint(right, bottom - radiusY, transform));
        builder.CubicTo(
            TransformPoint(right, bottom - controlY, transform),
            TransformPoint(right - controlX, bottom, transform),
            TransformPoint(right - radiusX, bottom, transform));
        builder.LineTo(TransformPoint(left + radiusX, bottom, transform));
        builder.CubicTo(
            TransformPoint(left + controlX, bottom, transform),
            TransformPoint(left, bottom - controlY, transform),
            TransformPoint(left, bottom - radiusY, transform));
        builder.LineTo(TransformPoint(left, top + radiusY, transform));
        builder.CubicTo(
            TransformPoint(left, top + controlY, transform),
            TransformPoint(left + controlX, top, transform),
            TransformPoint(left + radiusX, top, transform));
        builder.Close();
    }

    private void AppendOval(DrawingPathBuilder builder, Matrix3x2? transform,
        float centerX, float centerY, float radiusX, float radiusY)
    {
        float controlX = radiusX * Kappa;
        float controlY = radiusY * Kappa;

        builder.MoveTo(TransformPoint(centerX + radiusX, centerY, transform));
        builder.CubicTo(
            TransformPoint(centerX + radiusX, centerY + controlY, transform),
            TransformPoint(centerX + controlX, centerY + radiusY, transform),
            TransformPoint(centerX, centerY + radiusY, transform));
        builder.CubicTo(
            TransformPoint(centerX - controlX, centerY + radiusY, transform),
            TransformPoint(centerX - radiusX, centerY + controlY, transform),
            TransformPoint(centerX - radiusX, centerY, transform));
        builder.CubicTo(
            TransformPoint(centerX - radiusX, centerY - controlY, transform),
            TransformPoint(centerX - controlX, centerY - radiusY, transform),
            TransformPoint(centerX, centerY - radiusY, transform));
        builder.CubicTo(
            TransformPoint(centerX + controlX, centerY - radiusY, transform),
            TransformPoint(centerX + radiusX, centerY - controlY, transform),
            TransformPoint(centerX + radiusX, centerY, transform));
        builder.Close();
    }

    //Converts an SVG endpoint arc to cubic Bézier segments (the standard a2c algorithm
    //from SVG 1.1 appendix F.6.5) - SkiaModel leans on Skia's native ArcTo; here the
    //managed path holds only Bézier verbs, so the conversion happens up front.
    private void AppendArcAsCubics(DrawingPathBuilder builder, Matrix3x2? transform,
        SKPoint start, ArcToPathCommand arc)
    {
        double x1 = start.X, y1 = start.Y;
        double x2 = arc.X, y2 = arc.Y;
        double rx = Math.Abs(arc.Rx);
        double ry = Math.Abs(arc.Ry);

        if (x1 == x2 && y1 == y2)
        {
            return; //Zero-length arc: nothing to draw (per the SVG spec)
        }

        if (rx <= 0 || ry <= 0)
        {
            builder.LineTo(TransformPoint(arc.X, arc.Y, transform));
            return;
        }

        bool largeArc = arc.LargeArc == SKPathArcSize.Large;
        bool sweep = arc.Sweep == SKPathDirection.Clockwise; //SVG sweep-flag = 1

        double phi = arc.XAxisRotate * Math.PI / 180.0;
        double cosPhi = Math.Cos(phi);
        double sinPhi = Math.Sin(phi);

        //F.6.5.1: transform the midpoint into the arc's axis-aligned frame
        double dx2 = (x1 - x2) / 2.0;
        double dy2 = (y1 - y2) / 2.0;
        double x1p = (cosPhi * dx2) + (sinPhi * dy2);
        double y1p = (-sinPhi * dx2) + (cosPhi * dy2);

        //F.6.6.2: scale radii up when they cannot span the endpoints
        double lambda = ((x1p * x1p) / (rx * rx)) + ((y1p * y1p) / (ry * ry));
        if (lambda > 1)
        {
            double scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        //F.6.5.2: center in the primed frame
        double rxSq = rx * rx;
        double rySq = ry * ry;
        double numerator = (rxSq * rySq) - (rxSq * y1p * y1p) - (rySq * x1p * x1p);
        double denominator = (rxSq * y1p * y1p) + (rySq * x1p * x1p);
        double coefficient = denominator <= 0 ? 0 : Math.Sqrt(Math.Max(0, numerator / denominator));
        if (largeArc == sweep)
        {
            coefficient = -coefficient;
        }
        double cxp = coefficient * (rx * y1p / ry);
        double cyp = -coefficient * (ry * x1p / rx);

        //F.6.5.3: center in the original frame
        double cx = (cosPhi * cxp) - (sinPhi * cyp) + ((x1 + x2) / 2.0);
        double cy = (sinPhi * cxp) + (cosPhi * cyp) + ((y1 + y2) / 2.0);

        //F.6.5.5/6: start angle and sweep extent
        double ux = (x1p - cxp) / rx;
        double uy = (y1p - cyp) / ry;
        double vx = (-x1p - cxp) / rx;
        double vy = (-y1p - cyp) / ry;
        double theta1 = VectorAngle(1, 0, ux, uy);
        double deltaTheta = VectorAngle(ux, uy, vx, vy);
        if (!sweep && deltaTheta > 0)
        {
            deltaTheta -= 2 * Math.PI;
        }
        else if (sweep && deltaTheta < 0)
        {
            deltaTheta += 2 * Math.PI;
        }

        //Split into segments no wider than a quadrant; approximate each with one cubic
        int segmentCount = Math.Max(1, (int)Math.Ceiling(Math.Abs(deltaTheta) / (Math.PI / 2.0)));
        double segmentSweep = deltaTheta / segmentCount;
        double alpha = 4.0 / 3.0 * Math.Tan(segmentSweep / 4.0);

        double theta = theta1;
        for (int i = 0; i < segmentCount; i++)
        {
            double thetaEnd = theta + segmentSweep;

            EllipsePoint(cx, cy, rx, ry, cosPhi, sinPhi, theta, out double startPointX, out double startPointY,
                out double startTangentX, out double startTangentY);
            EllipsePoint(cx, cy, rx, ry, cosPhi, sinPhi, thetaEnd, out double endPointX, out double endPointY,
                out double endTangentX, out double endTangentY);

            builder.CubicTo(
                TransformPoint((float)(startPointX + (alpha * startTangentX)), (float)(startPointY + (alpha * startTangentY)), transform),
                TransformPoint((float)(endPointX - (alpha * endTangentX)), (float)(endPointY - (alpha * endTangentY)), transform),
                TransformPoint((float)endPointX, (float)endPointY, transform));

            theta = thetaEnd;
        }
    }

    private static void EllipsePoint(double cx, double cy, double rx, double ry,
        double cosPhi, double sinPhi, double theta,
        out double pointX, out double pointY, out double tangentX, out double tangentY)
    {
        double cosTheta = Math.Cos(theta);
        double sinTheta = Math.Sin(theta);
        pointX = cx + (rx * cosTheta * cosPhi) - (ry * sinTheta * sinPhi);
        pointY = cy + (rx * cosTheta * sinPhi) + (ry * sinTheta * cosPhi);
        tangentX = (-rx * sinTheta * cosPhi) - (ry * cosTheta * sinPhi);
        tangentY = (-rx * sinTheta * sinPhi) + (ry * cosTheta * cosPhi);
    }

    private static double VectorAngle(double ux, double uy, double vx, double vy)
    {
        double dot = (ux * vx) + (uy * vy);
        double length = Math.Sqrt(((ux * ux) + (uy * uy)) * ((vx * vx) + (vy * vy)));
        if (length <= 0)
        {
            return 0;
        }

        double angle = Math.Acos(Math.Clamp(dot / length, -1.0, 1.0));
        if (((ux * vy) - (uy * vx)) < 0)
        {
            angle = -angle;
        }
        return angle;
    }

    private static DrawingPoint TransformPoint(float x, float y, Matrix3x2? transform)
    {
        if (transform == null)
        {
            return new DrawingPoint(x, y);
        }

        Vector2 transformed = Vector2.Transform(new Vector2(x, y), transform.Value);
        return new DrawingPoint(transformed.X, transformed.Y);
    }
}
