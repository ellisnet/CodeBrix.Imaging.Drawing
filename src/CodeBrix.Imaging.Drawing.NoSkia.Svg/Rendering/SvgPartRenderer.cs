using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.Drawing.Paths;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// One part's override, frozen for one re-recording: as if the element were wrapped in a group
// with this extra transform (in DOCUMENT coordinates, about the pivot) and opacity, optionally
// hidden, and with its stroked paths trimmed.
// </summary>
internal sealed class SvgPartState
{
    public bool IsVisible = true;
    public float Opacity = 1f;
    public bool HasTransform;
    public PointF Translation;
    public float Rotation;
    public float ScaleX = 1f;
    public float ScaleY = 1f;
    public PointF? Pivot;
    public bool HasTrim;
    public float TrimStart;
    public float TrimEnd = 1f;

    // The doc-space transform for one node carrying this override: the pivot is the node's own
    //   centre unless one was given
    public Matrix3x2 GetMatrix(SKRect nodeBounds)
    {
        PointF pivot = Pivot ?? new PointF((nodeBounds.Left + nodeBounds.Right) / 2f, (nodeBounds.Top + nodeBounds.Bottom) / 2f);
        return new DrawingTransform(Translation, Rotation, ScaleX, ScaleY, pivot).ToMatrix();
    }
}

// <summary>
// Re-records a compiled scene the way SvgSceneRenderer.RenderNodeToCanvas does, with per-part
// overrides applied at the nodes whose element id has one. Kept in Rendering/ (the repository's
// own code) so the vendored scene graph stays byte-faithful: the walk below mirrors the vendored
// one step for step, and adds three things - an override's transform and opacity layer opened
// first, as an extra wrapping group would be; hidden parts skipped with their subtree; and stroked
// paths of trimmed parts replaced by their trimmed geometry. Re-vendoring the compiler means
// re-checking this walk against SvgSceneRenderer.RenderNodeToCanvas.
// </summary>
internal static class SvgPartRenderer
{
    // Records the scene with the overrides; the pictures made afresh for trimmed parts are
    //   returned in <paramref name="transient"/> so the caller converts them without caching
    public static SKPicture Record(SvgSceneDocument scene, IReadOnlyDictionary<string, SvgPartState> parts,
        NoSkiaModel model, HashSet<SKPicture> transient)
    {
        SKRect cull = scene.CullRect;
        if (cull.IsEmpty) { cull = scene.Root.TransformedBounds; }
        if (cull.IsEmpty) { return null; }
        var recorder = new SKPictureRecorder();
        SKCanvas canvas = recorder.BeginRecording(cull);
        var walk = new Walk(scene, parts, model, transient);
        walk.Render(scene.Root, canvas, Matrix3x2.Identity, null, true);
        return recorder.EndRecording();
    }

    // The doc-space matrix each override along a node's ancestry applies, outermost last - what
    //   a part's bounds are mapped by to report where it is drawn
    public static Matrix3x2 GetDocumentMatrix(SvgSceneNode node, IReadOnlyDictionary<string, SvgPartState> parts)
    {
        Matrix3x2 total = Matrix3x2.Identity;
        for (SvgSceneNode current = node; current != null; current = current.Parent)
        {
            if (current.ElementId != null && parts.TryGetValue(current.ElementId, out SvgPartState state) && state.HasTransform)
            {
                total *= state.GetMatrix(current.TransformedBounds);
            }
        }
        return total;
    }

    // Shim matrices map points as column vectors; Matrix3x2 as row vectors
    internal static SKMatrix ToShim(Matrix3x2 m) => new SKMatrix
    {
        ScaleX = m.M11,
        SkewX = m.M21,
        TransX = m.M31,
        SkewY = m.M12,
        ScaleY = m.M22,
        TransY = m.M32,
        Persp2 = 1f,
    };

    internal static Matrix3x2 ToNumerics(SKMatrix m) => new Matrix3x2(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);

    private sealed class Walk
    {
        private readonly SvgSceneDocument _scene;
        private readonly IReadOnlyDictionary<string, SvgPartState> _parts;
        private readonly NoSkiaModel _model;
        private readonly HashSet<SKPicture> _transient;

        public Walk(SvgSceneDocument scene, IReadOnlyDictionary<string, SvgPartState> parts, NoSkiaModel model, HashSet<SKPicture> transient)
        {
            _scene = scene;
            _parts = parts;
            _model = model;
            _transient = transient;
        }

        // `original` is the doc-space matrix the node's parent has in the scene as authored (no
        //   overrides): the override's matrix X = P^-1 E P is inserted so that the content lands at
        //   E (P L), inside any ancestor override already on the canvas
        public void Render(SvgSceneNode node, SKCanvas canvas, Matrix3x2 original, SvgPartState trim, bool enableTransform)
        {
            if (node.IsDisplayNone || node.SuppressSubtreeRendering) { return; }

            SvgPartState state = null;
            if (node.ElementId != null) { _parts.TryGetValue(node.ElementId, out state); }
            if (state != null && (!state.IsVisible || state.Opacity <= 0f)) { return; }
            if (state != null && state.HasTrim) { trim = state; }

            int wrappers = 0;
            if (state != null && state.HasTransform)
            {
                Matrix3x2 e = state.GetMatrix(node.TransformedBounds);
                if (Matrix3x2.Invert(original, out Matrix3x2 inverse))
                {
                    //Row vectors: content -> original -> E -> back through the inverse
                    Matrix3x2 x = original * e * inverse;
                    canvas.Save();
                    canvas.SetMatrix(ToShim(x));
                    wrappers++;
                }
            }
            if (state != null && state.Opacity < 1f)
            {
                SKPaint layer = SvgScenePaintingService.GetOpacityPaint(state.Opacity);
                if (layer != null)
                {
                    canvas.SaveLayer(layer);
                    wrappers++;
                }
            }

            RenderNode(node, canvas, original, trim, enableTransform);

            for (int i = 0; i < wrappers; i++) { canvas.Restore(); }
        }

        private void RenderNode(SvgSceneNode node, SKCanvas canvas, Matrix3x2 original, SvgPartState trim, bool enableTransform)
        {
            canvas.Save();

            if (node.Overflow is { } overflow)
            {
                canvas.ClipRect(overflow, SKClipOperation.Intersect);
            }

            Matrix3x2 local = original;
            if (enableTransform && !node.Transform.IsIdentity)
            {
                canvas.SetMatrix(node.Transform);
                local = ToNumerics(node.Transform) * original;
            }

            if (node.Clip is { } clip)
            {
                canvas.ClipRect(clip, SKClipOperation.Intersect);
            }

            if (node.ClipPath is { } clipPath)
            {
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, node.IsAntialias);
            }

            if (node.InnerClip is { } innerClip)
            {
                canvas.ClipRect(innerClip, SKClipOperation.Intersect);
            }

            bool mask = node.MaskPaint is not null && node.MaskNode is not null;
            if (mask)
            {
                canvas.SaveLayer(node.MaskPaint);
            }

            if (node.Opacity is { } opacity)
            {
                canvas.SaveLayer(opacity);
            }

            if (node.Filter is { } filter)
            {
                if (node.FilterClip is { } filterClip)
                {
                    canvas.ClipRect(filterClip, SKClipOperation.Intersect);
                }

                canvas.SaveLayer(filter);
            }

            if (node.IsRenderable && node.LocalModel is { } localModel)
            {
                canvas.DrawPicture(trim != null ? Trimmed(localModel, trim) : localModel);
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                Render(node.Children[i], canvas, local, trim, true);
            }

            if (node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn && mask)
            {
                canvas.SaveLayer(maskDstIn);
                Render(maskNode, canvas, local, null, true);
                canvas.Restore();
            }

            if (node.Filter is not null)
            {
                canvas.Restore();
            }

            if (node.Opacity is not null)
            {
                canvas.Restore();
            }

            if (mask)
            {
                canvas.Restore();
            }

            canvas.Restore();
        }

        // A copy of a node's own drawing whose stroked paths are cut to the trim range (fills,
        //   images and text are left whole)
        private SKPicture Trimmed(SKPicture picture, SvgPartState trim)
        {
            if (trim.TrimStart == 0f && trim.TrimEnd == 1f) { return picture; }
            if (picture.Commands == null) { return picture; }
            var commands = new List<CanvasCommand>(picture.Commands.Count);
            foreach (CanvasCommand command in picture.Commands)
            {
                if (command is DrawPathCanvasCommand draw && draw.Path != null && draw.Paint != null && draw.Paint.Style == SKPaintStyle.Stroke)
                {
                    SKPath path = TrimPath(draw.Path, trim.TrimStart, trim.TrimEnd);
                    if (path != null) { commands.Add(new DrawPathCanvasCommand(path, draw.Paint)); }
                    continue;
                }
                commands.Add(command);
            }
            var trimmed = new SKPicture(picture.CullRect, commands);
            _transient.Add(trimmed);
            return trimmed;
        }

        private SKPath TrimPath(SKPath path, float start, float end)
        {
            DrawingPath drawing = _model.ToDrawingPath(path);
            if (drawing == null) { return null; }
            PathGeometry geometry = PathGeometry.FromDrawingPath(drawing).Trim(start, end);
            if (geometry.IsEmpty) { return null; }
            var result = new SKPath { FillType = path.FillType };
            foreach (PathContour contour in geometry.Contours)
            {
                result.MoveTo(contour.StartPoint.X, contour.StartPoint.Y);
                foreach (PathSegment segment in contour.GetSegments())
                {
                    switch (segment.Kind)
                    {
                        case PathSegmentKind.Quadratic:
                            result.QuadTo(segment.Control1.X, segment.Control1.Y, segment.End.X, segment.End.Y);
                            break;
                        case PathSegmentKind.Cubic:
                            result.CubicTo(segment.Control1.X, segment.Control1.Y, segment.Control2.X, segment.Control2.Y, segment.End.X, segment.End.Y);
                            break;
                        default:
                            result.LineTo(segment.End.X, segment.End.Y);
                            break;
                    }
                }
                if (contour.IsClosed) { result.Close(); }
            }
            return result;
        }
    }
}
