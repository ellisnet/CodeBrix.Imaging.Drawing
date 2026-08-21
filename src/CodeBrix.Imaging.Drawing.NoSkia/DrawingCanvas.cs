using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia.Raster;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws geometry and bitmaps onto a target <see cref="DrawingBitmap"/>, API-compatible
/// with the SkiaSharp <c>SKCanvas</c> type (reduced to the drawing operations this managed
/// implementation renders). All rendering happens on the CPU through an anti-aliased
/// scanline rasterizer: curves are flattened adaptively, strokes are converted to filled
/// outlines, clips become coverage masks, save-layers render to offscreen buffers, and
/// pixels are composited with straight-alpha blending in any <see cref="DrawingBlendMode"/>.
/// </summary>
public sealed class DrawingCanvas : IDisposable
{
    private const float Kappa = 0.5522847498f; //Cubic-Bezier circle-quadrant constant

    private sealed class CanvasState
    {
        public Matrix3x2 Matrix;
        public float[] Clip;
        public DrawingBitmap LayerBitmap; //Non-null when this state opened a save-layer
        public DrawingPaint LayerPaint;
    }

    private readonly DrawingBitmap _baseBitmap;
    private readonly List<CanvasState> _stack = new List<CanvasState>();
    private readonly List<DrawingBitmap> _layerTargets = new List<DrawingBitmap>();
    private Matrix3x2 _matrix = Matrix3x2.Identity;
    private float[] _clip; //null = unclipped; one 0..1 coverage value per pixel otherwise

    /// <summary>
    /// Creates a canvas that draws onto the given bitmap. The canvas does not take
    /// ownership of the bitmap.
    /// </summary>
    /// <param name="bitmap">The bitmap to draw onto.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    public DrawingCanvas(DrawingBitmap bitmap)
    {
        _baseBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    /// <summary>The current total transform matrix.</summary>
    public Matrix3x2 TotalMatrix => _matrix;

    private DrawingBitmap CurrentTarget
        => _layerTargets.Count > 0 ? _layerTargets[_layerTargets.Count - 1] : _baseBitmap;

    /// <summary>
    /// Replaces every pixel of the current target with the given color (ignoring the
    /// current transform and clip).
    /// </summary>
    /// <param name="color">The color to clear to.</param>
    public void Clear(DrawingColor color) => CurrentTarget.Erase(color);

    /// <summary>
    /// Saves the current transform and clip onto a stack.
    /// </summary>
    /// <returns>The number of saved states before this save.</returns>
    public int Save()
    {
        _stack.Add(new CanvasState { Matrix = _matrix, Clip = _clip });
        return _stack.Count - 1;
    }

    /// <summary>
    /// Saves the current state and redirects subsequent drawing into a transparent
    /// offscreen layer. When the matching <see cref="Restore"/> runs, the layer is
    /// composited onto the surface below it using the given paint's alpha, color filter,
    /// blend mode, and image filter - exactly the mechanism SkiaSharp uses for group
    /// opacity, masks, and filter effects.
    /// </summary>
    /// <param name="paint">
    /// The paint applied when the layer is composited; or <c>null</c> for a plain layer.
    /// </param>
    /// <returns>The number of saved states before this save.</returns>
    public int SaveLayer(DrawingPaint paint)
    {
        var layer = new DrawingBitmap(new DrawingImageInfo(
            _baseBitmap.Width, _baseBitmap.Height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        _stack.Add(new CanvasState
        {
            Matrix = _matrix,
            Clip = _clip,
            LayerBitmap = layer,
            LayerPaint = paint?.Clone(),
        });
        _layerTargets.Add(layer);
        return _stack.Count - 1;
    }

    /// <summary>
    /// Saves the current state and redirects subsequent drawing into a plain transparent
    /// offscreen layer.
    /// </summary>
    /// <returns>The number of saved states before this save.</returns>
    public int SaveLayer() => SaveLayer(null);

    /// <summary>
    /// Restores the most recently saved state; when it opened a save-layer, the layer is
    /// composited onto the surface below it.
    /// </summary>
    public void Restore()
    {
        if (_stack.Count == 0) { return; }

        CanvasState state = _stack[_stack.Count - 1];
        _stack.RemoveAt(_stack.Count - 1);
        _matrix = state.Matrix;
        _clip = state.Clip;

        if (state.LayerBitmap != null)
        {
            _layerTargets.Remove(state.LayerBitmap);
            CompositeLayer(state.LayerBitmap, state.LayerPaint);
            state.LayerBitmap.Dispose();
        }
    }

    /// <summary>
    /// Restores saved states until the given count (as returned by <see cref="Save"/> or
    /// <see cref="SaveLayer(DrawingPaint)"/>) remains.
    /// </summary>
    /// <param name="count">The number of saved states to keep.</param>
    public void RestoreToCount(int count)
    {
        if (count < 0) { count = 0; }
        while (_stack.Count > count) { Restore(); }
    }

    /// <summary>
    /// Scales the coordinate space uniformly.
    /// </summary>
    /// <param name="scale">The scale factor for both axes.</param>
    public void Scale(float scale) => Concat(Matrix3x2.CreateScale(scale));

    /// <summary>
    /// Scales the coordinate space.
    /// </summary>
    /// <param name="scaleX">The horizontal scale factor.</param>
    /// <param name="scaleY">The vertical scale factor.</param>
    public void Scale(float scaleX, float scaleY) => Concat(Matrix3x2.CreateScale(scaleX, scaleY));

    /// <summary>
    /// Scales the coordinate space about a pivot point.
    /// </summary>
    /// <param name="scaleX">The horizontal scale factor.</param>
    /// <param name="scaleY">The vertical scale factor.</param>
    /// <param name="pivotX">The horizontal position of the pivot.</param>
    /// <param name="pivotY">The vertical position of the pivot.</param>
    public void Scale(float scaleX, float scaleY, float pivotX, float pivotY)
        => Concat(Matrix3x2.CreateScale(scaleX, scaleY, new Vector2(pivotX, pivotY)));

    /// <summary>
    /// Translates the coordinate space.
    /// </summary>
    /// <param name="deltaX">The horizontal offset.</param>
    /// <param name="deltaY">The vertical offset.</param>
    public void Translate(float deltaX, float deltaY) => Concat(Matrix3x2.CreateTranslation(deltaX, deltaY));

    /// <summary>
    /// Rotates the coordinate space.
    /// </summary>
    /// <param name="degrees">The rotation, in degrees, clockwise.</param>
    public void RotateDegrees(float degrees) => Concat(Matrix3x2.CreateRotation(degrees * MathF.PI / 180f));

    /// <summary>
    /// Applies an arbitrary transform ahead of the current one.
    /// </summary>
    /// <param name="matrix">The transform to apply.</param>
    public void Concat(Matrix3x2 matrix) => _matrix = matrix * _matrix;

    /// <summary>
    /// Replaces the total transform matrix.
    /// </summary>
    /// <param name="matrix">The new total transform.</param>
    public void SetMatrix(Matrix3x2 matrix) => _matrix = matrix;

    /// <summary>
    /// Resets the total transform matrix to identity.
    /// </summary>
    public void ResetMatrix() => _matrix = Matrix3x2.Identity;

    /// <summary>
    /// Combines an axis-aligned rectangle (under the current transform) into the clip.
    /// </summary>
    /// <param name="rect">The rectangle to clip with.</param>
    /// <param name="operation">How the rectangle combines with the current clip.</param>
    /// <param name="antialias">Whether the clip edge carries fractional coverage.</param>
    public void ClipRect(DrawingRect rect, DrawingClipOperation operation = DrawingClipOperation.Intersect,
        bool antialias = false)
    {
        var builder = new DrawingPathBuilder();
        builder.AddRect(rect);
        using DrawingPath path = builder.Detach();
        ClipPath(path, operation, antialias);
    }

    /// <summary>
    /// Combines a path (under the current transform) into the clip.
    /// </summary>
    /// <param name="path">The path to clip with.</param>
    /// <param name="operation">How the path combines with the current clip.</param>
    /// <param name="antialias">Whether the clip edge carries fractional coverage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public void ClipPath(DrawingPath path, DrawingClipOperation operation = DrawingClipOperation.Intersect,
        bool antialias = false)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        List<FlattenedContour> contours = PathFlattener.Flatten(
            path, _matrix, PathFlattener.DevicePixelTolerance);
        var polygons = new List<List<Vector2>>(contours.Count);
        foreach (FlattenedContour contour in contours)
        {
            if (contour.Points.Count >= 3) { polygons.Add(contour.Points); }
        }

        float[] shapeCoverage = PolygonFiller.Coverage(
            _baseBitmap.Width, _baseBitmap.Height, polygons, path.FillType, antialias);

        var newClip = new float[shapeCoverage.Length];
        if (operation == DrawingClipOperation.Intersect)
        {
            for (int i = 0; i < newClip.Length; i++)
            {
                newClip[i] = (_clip?[i] ?? 1f) * shapeCoverage[i];
            }
        }
        else
        {
            for (int i = 0; i < newClip.Length; i++)
            {
                newClip[i] = (_clip?[i] ?? 1f) * (1f - shapeCoverage[i]);
            }
        }
        _clip = newClip;
    }

    /// <summary>
    /// Draws a straight line, always stroked with the paint's stroke geometry (matching
    /// SkiaSharp, regardless of the paint's style).
    /// </summary>
    /// <param name="x1">The horizontal position of the start point.</param>
    /// <param name="y1">The vertical position of the start point.</param>
    /// <param name="x2">The horizontal position of the end point.</param>
    /// <param name="y2">The vertical position of the end point.</param>
    /// <param name="paint">The paint to draw with.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="paint"/> is null.</exception>
    public void DrawLine(float x1, float y1, float x2, float y2, DrawingPaint paint)
    {
        if (paint == null) { throw new ArgumentNullException(nameof(paint)); }

        var contour = new FlattenedContour();
        contour.Points.Add(new Vector2(x1, y1));
        contour.Points.Add(new Vector2(x2, y2));
        StrokeContours(new List<FlattenedContour> { contour }, paint);
    }

    /// <summary>
    /// Draws a straight line, always stroked with the paint's stroke geometry.
    /// </summary>
    /// <param name="start">The start point.</param>
    /// <param name="end">The end point.</param>
    /// <param name="paint">The paint to draw with.</param>
    public void DrawLine(DrawingPoint start, DrawingPoint end, DrawingPaint paint)
        => DrawLine(start.X, start.Y, end.X, end.Y, paint);

    /// <summary>
    /// Draws an axis-aligned rectangle, filled and/or stroked per the paint's style.
    /// </summary>
    /// <param name="rect">The rectangle to draw.</param>
    /// <param name="paint">The paint to draw with.</param>
    public void DrawRect(DrawingRect rect, DrawingPaint paint)
    {
        var builder = new DrawingPathBuilder();
        builder.AddRect(rect);
        using DrawingPath path = builder.Detach();
        DrawPath(path, paint);
    }

    /// <summary>
    /// Draws an axis-aligned rectangle with rounded corners, filled and/or stroked per the
    /// paint's style.
    /// </summary>
    /// <param name="rect">The rectangle to draw.</param>
    /// <param name="radiusX">The horizontal corner radius.</param>
    /// <param name="radiusY">The vertical corner radius.</param>
    /// <param name="paint">The paint to draw with.</param>
    public void DrawRoundRect(DrawingRect rect, float radiusX, float radiusY, DrawingPaint paint)
    {
        radiusX = Math.Clamp(radiusX, 0, rect.Width / 2f);
        radiusY = Math.Clamp(radiusY, 0, rect.Height / 2f);
        if (radiusX <= 0 || radiusY <= 0)
        {
            DrawRect(rect, paint);
            return;
        }

        float left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom;
        float controlX = radiusX * (1 - Kappa);
        float controlY = radiusY * (1 - Kappa);

        var builder = new DrawingPathBuilder();
        builder.MoveTo(left + radiusX, top);
        builder.LineTo(right - radiusX, top);
        builder.CubicTo(new DrawingPoint(right - controlX, top), new DrawingPoint(right, top + controlY), new DrawingPoint(right, top + radiusY));
        builder.LineTo(right, bottom - radiusY);
        builder.CubicTo(new DrawingPoint(right, bottom - controlY), new DrawingPoint(right - controlX, bottom), new DrawingPoint(right - radiusX, bottom));
        builder.LineTo(left + radiusX, bottom);
        builder.CubicTo(new DrawingPoint(left + controlX, bottom), new DrawingPoint(left, bottom - controlY), new DrawingPoint(left, bottom - radiusY));
        builder.LineTo(left, top + radiusY);
        builder.CubicTo(new DrawingPoint(left, top + controlY), new DrawingPoint(left + controlX, top), new DrawingPoint(left + radiusX, top));
        builder.Close();

        using DrawingPath path = builder.Detach();
        DrawPath(path, paint);
    }

    /// <summary>
    /// Draws an axis-aligned ellipse, filled and/or stroked per the paint's style.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center.</param>
    /// <param name="centerY">The vertical position of the center.</param>
    /// <param name="radiusX">The horizontal radius.</param>
    /// <param name="radiusY">The vertical radius.</param>
    /// <param name="paint">The paint to draw with.</param>
    public void DrawOval(float centerX, float centerY, float radiusX, float radiusY, DrawingPaint paint)
    {
        using DrawingPath path = BuildOvalPath(centerX, centerY, radiusX, radiusY);
        DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a circle, filled and/or stroked per the paint's style.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center.</param>
    /// <param name="centerY">The vertical position of the center.</param>
    /// <param name="radius">The circle's radius.</param>
    /// <param name="paint">The paint to draw with.</param>
    public void DrawCircle(float centerX, float centerY, float radius, DrawingPaint paint)
        => DrawOval(centerX, centerY, radius, radius, paint);

    /// <summary>
    /// Draws a circle, filled and/or stroked per the paint's style.
    /// </summary>
    /// <param name="center">The circle's center.</param>
    /// <param name="radius">The circle's radius.</param>
    /// <param name="paint">The paint to draw with.</param>
    public void DrawCircle(DrawingPoint center, float radius, DrawingPaint paint)
        => DrawOval(center.X, center.Y, radius, radius, paint);

    /// <summary>
    /// Draws a path, filled and/or stroked per the paint's style.
    /// </summary>
    /// <param name="path">The path to draw.</param>
    /// <param name="paint">The paint to draw with.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="paint"/> is null.</exception>
    public void DrawPath(DrawingPath path, DrawingPaint paint)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (paint == null) { throw new ArgumentNullException(nameof(paint)); }

        if (paint.Style == DrawingPaintStyle.Fill || paint.Style == DrawingPaintStyle.StrokeAndFill)
        {
            List<FlattenedContour> contours = PathFlattener.Flatten(
                path, _matrix, PathFlattener.DevicePixelTolerance);
            var polygons = new List<List<Vector2>>(contours.Count);
            foreach (FlattenedContour contour in contours)
            {
                if (contour.Points.Count >= 3) { polygons.Add(contour.Points); }
            }
            PolygonFiller.Fill(CurrentTarget, polygons, path.FillType, CreateFillContext(paint));
        }

        if (paint.Style == DrawingPaintStyle.Stroke || paint.Style == DrawingPaintStyle.StrokeAndFill)
        {
            float userTolerance = PathFlattener.DevicePixelTolerance / MaxScale();
            List<FlattenedContour> contours = PathFlattener.Flatten(path, Matrix3x2.Identity, userTolerance);
            StrokeContours(contours, paint);
        }
    }

    /// <summary>
    /// Draws a bitmap with its top-left corner at the given position (at its natural size,
    /// under the current transform).
    /// </summary>
    /// <param name="bitmap">The bitmap to draw.</param>
    /// <param name="point">The position of the bitmap's top-left corner.</param>
    /// <param name="sampling">The sampling to use when pixels are transformed.</param>
    /// <param name="paint">An optional paint whose alpha, filters, and blend mode apply to the draw.</param>
    public void DrawBitmap(DrawingBitmap bitmap, DrawingPoint point, DrawingSamplingOptions sampling,
        DrawingPaint paint = null)
    {
        if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }
        DrawBitmap(bitmap, DrawingRect.Create(point.X, point.Y, bitmap.Width, bitmap.Height), sampling, paint);
    }

    /// <summary>
    /// Draws a bitmap scaled into the given destination rectangle (under the current
    /// transform).
    /// </summary>
    /// <param name="bitmap">The bitmap to draw.</param>
    /// <param name="destination">The rectangle the bitmap is scaled into.</param>
    /// <param name="sampling">The sampling to use when pixels are transformed.</param>
    /// <param name="paint">An optional paint whose alpha, filters, and blend mode apply to the draw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    public void DrawBitmap(DrawingBitmap bitmap, DrawingRect destination, DrawingSamplingOptions sampling,
        DrawingPaint paint = null)
    {
        if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }
        DrawBitmap(bitmap, DrawingRect.Create(0, 0, bitmap.Width, bitmap.Height), destination, sampling, paint);
    }

    /// <summary>
    /// Draws a region of a bitmap scaled into the given destination rectangle (under the
    /// current transform).
    /// </summary>
    /// <param name="bitmap">The bitmap to draw.</param>
    /// <param name="source">The region of the bitmap to draw.</param>
    /// <param name="destination">The rectangle the region is scaled into.</param>
    /// <param name="sampling">The sampling to use when pixels are transformed.</param>
    /// <param name="paint">An optional paint whose alpha, filters, and blend mode apply to the draw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    public void DrawBitmap(DrawingBitmap bitmap, DrawingRect source, DrawingRect destination,
        DrawingSamplingOptions sampling, DrawingPaint paint = null)
    {
        if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }
        if (destination.IsEmpty || source.IsEmpty) { return; }

        //The map from source-region coordinates to device coordinates
        Matrix3x2 sourceToDevice =
            Matrix3x2.CreateTranslation(-source.Left, -source.Top)
            * Matrix3x2.CreateScale(destination.Width / source.Width, destination.Height / source.Height)
            * Matrix3x2.CreateTranslation(destination.Left, destination.Top)
            * _matrix;

        float alphaScale = (paint?.Color.Alpha ?? (byte)255) / 255f;
        if (alphaScale <= 0 && (paint?.BlendMode ?? DrawingBlendMode.SrcOver) == DrawingBlendMode.SrcOver) { return; }

        bool plainComposite = _clip == null
            && (paint?.BlendMode ?? DrawingBlendMode.SrcOver) == DrawingBlendMode.SrcOver
            && paint?.ColorFilter == null
            && source.Left == 0 && source.Top == 0
            && source.Width == bitmap.Width && source.Height == bitmap.Height;
        if (plainComposite && TryBlitDirect(bitmap, sourceToDevice, alphaScale)) { return; }

        if (!Matrix3x2.Invert(sourceToDevice, out Matrix3x2 deviceToSource)) { return; }

        var corners = new List<Vector2>
        {
            Vector2.Transform(new Vector2(source.Left, source.Top), sourceToDevice),
            Vector2.Transform(new Vector2(source.Right, source.Top), sourceToDevice),
            Vector2.Transform(new Vector2(source.Right, source.Bottom), sourceToDevice),
            Vector2.Transform(new Vector2(source.Left, source.Bottom), sourceToDevice),
        };

        bool linear = sampling.UseCubic || sampling.Filter == DrawingFilterMode.Linear;
        var context = new FillContext
        {
            Paint = new BitmapPaintSource(bitmap, deviceToSource, linear, alphaScale, source),
            ColorFilter = paint?.ColorFilter,
            BlendMode = paint?.BlendMode ?? DrawingBlendMode.SrcOver,
            ClipMask = _clip,
            Antialias = paint?.IsAntialias ?? false,
        };
        PolygonFiller.Fill(CurrentTarget, new List<List<Vector2>> { corners },
            DrawingPathFillType.Winding, context);
    }

    /// <summary>
    /// Draws an image with its top-left corner at the given position.
    /// </summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="point">The position of the image's top-left corner.</param>
    /// <param name="sampling">The sampling to use when pixels are transformed.</param>
    /// <param name="paint">An optional paint whose alpha, filters, and blend mode apply to the draw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public void DrawImage(DrawingImage image, DrawingPoint point, DrawingSamplingOptions sampling,
        DrawingPaint paint = null)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        DrawBitmap(image.BitmapRef, point, sampling, paint);
    }

    /// <summary>
    /// Draws an image scaled into the given destination rectangle.
    /// </summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="destination">The rectangle the image is scaled into.</param>
    /// <param name="sampling">The sampling to use when pixels are transformed.</param>
    /// <param name="paint">An optional paint whose alpha, filters, and blend mode apply to the draw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public void DrawImage(DrawingImage image, DrawingRect destination, DrawingSamplingOptions sampling,
        DrawingPaint paint = null)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        DrawBitmap(image.BitmapRef, destination, sampling, paint);
    }

    /// <summary>
    /// Draws a region of an image scaled into the given destination rectangle.
    /// </summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="source">The region of the image to draw.</param>
    /// <param name="destination">The rectangle the region is scaled into.</param>
    /// <param name="sampling">The sampling to use when pixels are transformed.</param>
    /// <param name="paint">An optional paint whose alpha, filters, and blend mode apply to the draw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public void DrawImage(DrawingImage image, DrawingRect source, DrawingRect destination,
        DrawingSamplingOptions sampling, DrawingPaint paint = null)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        DrawBitmap(image.BitmapRef, source, destination, sampling, paint);
    }

    /// <summary>
    /// Completes any pending drawing. The managed implementation draws synchronously, so
    /// this is a no-op kept for API compatibility.
    /// </summary>
    public void Flush()
    {
    }

    /// <summary>
    /// Releases the canvas (the target bitmap is not disposed; any un-restored save-layers
    /// are discarded).
    /// </summary>
    public void Dispose()
    {
        foreach (DrawingBitmap layer in _layerTargets)
        {
            layer.Dispose();
        }
        _layerTargets.Clear();
        _stack.Clear();
    }

    private void CompositeLayer(DrawingBitmap layer, DrawingPaint paint)
    {
        DrawingBitmap content = layer;
        DrawingBitmap filtered = null;
        if (paint?.ImageFilter != null)
        {
            filtered = paint.ImageFilter.Apply(layer);
            content = filtered;
        }

        try
        {
            DrawingBitmap target = CurrentTarget;
            byte[] sourcePixels = content.PixelBuffer;
            bool sourceBgra = content.IsBgra;
            byte[] targetPixels = target.PixelBuffer;
            bool targetBgra = target.IsBgra;
            float alphaScale = (paint?.Color.Alpha ?? (byte)255) / 255f;
            DrawingColorFilter colorFilter = paint?.ColorFilter;
            DrawingBlendMode mode = paint?.BlendMode ?? DrawingBlendMode.SrcOver;

            int width = Math.Min(content.Width, target.Width);
            int height = Math.Min(content.Height, target.Height);
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * content.RowBytes;
                int targetRow = y * target.RowBytes;
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = sourceRow + (x * 4);
                    byte c0 = sourcePixels[sourceOffset];
                    byte c1 = sourcePixels[sourceOffset + 1];
                    byte c2 = sourcePixels[sourceOffset + 2];
                    float alpha = (sourcePixels[sourceOffset + 3] / 255f) * alphaScale;

                    float red = (sourceBgra ? c2 : c0) / 255f;
                    float green = c1 / 255f;
                    float blue = (sourceBgra ? c0 : c2) / 255f;
                    colorFilter?.Apply(ref red, ref green, ref blue, ref alpha);

                    if (mode == DrawingBlendMode.SrcOver)
                    {
                        if (alpha <= 0) { continue; }
                        PolygonFiller.BlendPixel(targetPixels, targetRow + (x * 4), targetBgra,
                            red, green, blue, alpha);
                    }
                    else
                    {
                        PolygonFiller.BlendPixelWithMode(targetPixels, targetRow + (x * 4), targetBgra,
                            mode, red, green, blue, alpha, 1f);
                    }
                }
            }
        }
        finally
        {
            filtered?.Dispose();
        }
    }

    private FillContext CreateFillContext(DrawingPaint paint)
    {
        PaintSource source;
        float alphaScale = paint.Color.Alpha / 255f;
        if (paint.Shader != null)
        {
            Matrix3x2 localToDevice = (paint.Shader.LocalMatrix ?? Matrix3x2.Identity) * _matrix;
            Matrix3x2.Invert(localToDevice, out Matrix3x2 deviceToLocal);
            source = new ShaderPaintSource(paint.Shader, deviceToLocal, alphaScale);
        }
        else
        {
            source = new SolidPaintSource(paint.Color);
        }

        return new FillContext
        {
            Paint = source,
            ColorFilter = paint.ColorFilter,
            BlendMode = paint.BlendMode,
            ClipMask = _clip,
            Antialias = paint.IsAntialias,
        };
    }

    private void StrokeContours(List<FlattenedContour> contours, DrawingPaint paint)
    {
        float maxScale = MaxScale();
        if (maxScale <= 0) { return; }

        if (paint.PathEffect != null)
        {
            contours = DashSplitter.Split(contours, paint.PathEffect.Intervals, paint.PathEffect.Phase);
        }

        //A zero stroke width requests a hairline: one device pixel wide
        float width = paint.StrokeWidth > 0 ? paint.StrokeWidth : 1f / maxScale;
        float arcTolerance = PathFlattener.DevicePixelTolerance / maxScale;

        List<List<Vector2>> outline = StrokeBuilder.BuildOutline(
            contours, width, paint.StrokeCap, paint.StrokeJoin, paint.StrokeMiter, arcTolerance);

        foreach (List<Vector2> polygon in outline)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                polygon[i] = Vector2.Transform(polygon[i], _matrix);
            }
        }

        PolygonFiller.Fill(CurrentTarget, outline, DrawingPathFillType.Winding, CreateFillContext(paint));
    }

    private static DrawingPath BuildOvalPath(float centerX, float centerY, float radiusX, float radiusY)
    {
        float controlX = radiusX * Kappa;
        float controlY = radiusY * Kappa;

        var builder = new DrawingPathBuilder();
        builder.MoveTo(centerX + radiusX, centerY);
        builder.CubicTo(
            new DrawingPoint(centerX + radiusX, centerY + controlY),
            new DrawingPoint(centerX + controlX, centerY + radiusY),
            new DrawingPoint(centerX, centerY + radiusY));
        builder.CubicTo(
            new DrawingPoint(centerX - controlX, centerY + radiusY),
            new DrawingPoint(centerX - radiusX, centerY + controlY),
            new DrawingPoint(centerX - radiusX, centerY));
        builder.CubicTo(
            new DrawingPoint(centerX - radiusX, centerY - controlY),
            new DrawingPoint(centerX - controlX, centerY - radiusY),
            new DrawingPoint(centerX, centerY - radiusY));
        builder.CubicTo(
            new DrawingPoint(centerX + controlX, centerY - radiusY),
            new DrawingPoint(centerX + radiusX, centerY - controlY),
            new DrawingPoint(centerX + radiusX, centerY));
        builder.Close();
        return builder.Detach();
    }

    private float MaxScale()
    {
        //The largest singular value of the matrix's 2x2 linear part
        float a = (_matrix.M11 * _matrix.M11) + (_matrix.M12 * _matrix.M12);
        float b = (_matrix.M21 * _matrix.M21) + (_matrix.M22 * _matrix.M22);
        float c = (_matrix.M11 * _matrix.M21) + (_matrix.M12 * _matrix.M22);
        float difference = MathF.Sqrt(((a - b) * (a - b)) + (4 * c * c));
        return MathF.Sqrt(Math.Max(0f, (a + b + difference) / 2f));
    }

    private bool TryBlitDirect(DrawingBitmap source, Matrix3x2 sourceToDevice, float alphaScale)
    {
        //Fast path for the common 1:1 blit: unit scale, no rotation, integral translation
        const float epsilon = 1e-3f;
        if (MathF.Abs(sourceToDevice.M11 - 1) > epsilon || MathF.Abs(sourceToDevice.M22 - 1) > epsilon
            || MathF.Abs(sourceToDevice.M12) > epsilon || MathF.Abs(sourceToDevice.M21) > epsilon)
        {
            return false;
        }

        float translateX = sourceToDevice.M31;
        float translateY = sourceToDevice.M32;
        int offsetX = (int)MathF.Round(translateX);
        int offsetY = (int)MathF.Round(translateY);
        if (MathF.Abs(translateX - offsetX) > epsilon || MathF.Abs(translateY - offsetY) > epsilon)
        {
            return false;
        }

        DrawingBitmap targetBitmap = CurrentTarget;
        byte[] sourcePixels = source.PixelBuffer;
        bool sourceBgra = source.IsBgra;
        byte[] targetPixels = targetBitmap.PixelBuffer;
        bool targetBgra = targetBitmap.IsBgra;

        int startX = Math.Max(0, offsetX);
        int endX = Math.Min(targetBitmap.Width, offsetX + source.Width);
        int startY = Math.Max(0, offsetY);
        int endY = Math.Min(targetBitmap.Height, offsetY + source.Height);

        for (int y = startY; y < endY; y++)
        {
            int sourceRow = (y - offsetY) * source.RowBytes;
            int targetRow = y * targetBitmap.RowBytes;
            for (int x = startX; x < endX; x++)
            {
                int sourceOffset = sourceRow + ((x - offsetX) * 4);
                byte c0 = sourcePixels[sourceOffset];
                byte c1 = sourcePixels[sourceOffset + 1];
                byte c2 = sourcePixels[sourceOffset + 2];
                float alpha = (sourcePixels[sourceOffset + 3] / 255f) * alphaScale;
                if (alpha <= 0) { continue; }

                float red = (sourceBgra ? c2 : c0) / 255f;
                float green = c1 / 255f;
                float blue = (sourceBgra ? c0 : c2) / 255f;
                PolygonFiller.BlendPixel(targetPixels, targetRow + (x * 4), targetBgra, red, green, blue, alpha);
            }
        }

        return true;
    }
}
