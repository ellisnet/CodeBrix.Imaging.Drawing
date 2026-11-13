using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// Draws elements onto a canvas that is already mapped to CALIBRATED drawing units, honoring
/// each element's transform, opacity, time window, effects and (for groups) mask, and descending
/// into groups and animated shapes. This is the path for anything the animation model adds;
/// plain elements on untransformed layers keep the renderer's original drawing code, so their
/// pixels never change.
/// </summary>
internal static class ElementPainter
{
    //Draws one element. time: the moment whose time windows apply; null draws every element.
    //  context: the device being drawn on (needed for effects; without it effects are skipped)
    internal static void Draw(SKCanvas canvas, DrawingElement element, SKColor inheritedColor, double? time,
        PaintContext context = null)
    {
        if (time.HasValue && !element.IsVisibleAt(time.Value)) { return; }
        byte alpha = DrawingRenderer.ScaleAlpha(255, element.Opacity);
        if (alpha == 0) { return; }

        if (context != null && element.HasEffects)
        {
            //Content (and a group's mask) first, offscreen; then the effects; then the opacity
            EffectProcessor.DrawThrough(canvas, context, element.Effects, alpha, element.Transform.ToMatrix(),
                target => DrawTransformed(target, element, inheritedColor, time, context, 255));
            return;
        }
        DrawTransformed(canvas, element, inheritedColor, time, context, alpha);
    }

    private static void DrawTransformed(SKCanvas canvas, DrawingElement element, SKColor inheritedColor, double? time,
        PaintContext context, byte alpha)
    {
        DrawingTransform transform = element.Transform;
        bool transformed = !transform.IsIdentity;
        if (transformed)
        {
            canvas.Save();
            transform.ApplyTo(canvas);
        }

        //Below full opacity the element is drawn whole into a layer that is then faded, so its
        //  parts (or a group's children) never show through each other; a masked group is drawn
        //  whole into a layer too, so the mask multiplies its combined coverage once
        DrawingMask mask = (element as DrawingGroup)?.Mask;
        bool fading = alpha < 255;
        bool layered = fading || mask != null;
        if (layered)
        {
            using SKPaint layerPaint = fading ? new SKPaint { Color = SKColors.White.WithAlpha(alpha) } : null;
            canvas.SaveLayer(layerPaint);
        }

        switch (element)
        {
            case Stroke stroke:
                DrawStroke(canvas, stroke, inheritedColor);
                break;
            case AnimatedShape animated:
                DrawingShape current = animated.Current;
                if (current != null) { Draw(canvas, current, inheritedColor, time, context); }
                break;
            case DrawingGroup group:
                DrawGroupChildren(canvas, group, group.ColorValue ?? inheritedColor, time, context);
                break;
            case SpriteShape sprite:
                sprite.DrawAt(canvas, time ?? 0.0);
                break;
            case DrawingShape shape:
                shape.Draw(canvas, shape.ColorValue ?? inheritedColor);
                break;
        }

        if (mask != null) { mask.Apply(canvas, time ?? 0.0); }
        if (layered) { canvas.Restore(); }
        if (transformed) { canvas.Restore(); }
    }

    //Draws a group's children in its coordinates (its own transform and opacity are the caller's)
    internal static void DrawGroupChildren(SKCanvas canvas, DrawingGroup group, SKColor color, double? time,
        PaintContext context = null)
    {
        SKColor childColor = group.ContentColorValue ?? color;
        foreach (DrawingElement child in group.GetChildren())
        {
            Draw(canvas, child, childColor, time, context);
        }
    }

    //A freehand stroke in calibrated units: the same round-capped, round-joined polyline (or a
    //  dot for a single point) the renderer draws in canvas units
    internal static void DrawStroke(SKCanvas canvas, Stroke stroke, SKColor color)
    {
        StrokePoint[] points = stroke.GetPoints();
        if (points.Length < 1) { return; }

        if (points.Length == 1)
        {
            using var dotPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = color,
                IsAntialias = true,
            };
            canvas.DrawCircle(new SKPoint(points[0].X, points[0].Y), stroke.Width / 2f, dotPaint);
            return;
        }

        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(new SKPoint(points[0].X, points[0].Y));
        for (int i = 1; i < points.Length; i++)
        {
            pathBuilder.LineTo(new SKPoint(points[i].X, points[i].Y));
        }
        using SKPath path = pathBuilder.Detach();

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = stroke.Width,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };
        canvas.DrawPath(path, strokePaint);
    }
}
