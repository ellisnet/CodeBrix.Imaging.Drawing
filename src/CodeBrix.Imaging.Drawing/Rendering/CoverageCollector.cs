using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;

namespace CodeBrix.Imaging.Drawing.Rendering;

//Turns drawn elements into the areas they cover in OUTPUT pixels, for the hard-edged mask and
//  the ownership map: an element counts when it would draw at the render time (inside its time
//  window, opacity above 0 - the same skip rules the renderer uses); its geometry is mapped by
//  its own transform, its groups' transforms, the layer view (camera with parallax, then the
//  layer transform) and the calibrated-to-output fit, composed in double. Paint is ignored:
//  colours and their alpha, fills, effects and masks do not change what an element covers.
internal static class CoverageCollector
{
    //How closely curves are followed, in OUTPUT pixels
    internal const double FrameTolerance = 0.1;

    internal readonly struct Area
    {
        internal Area(PathGeometry geometry, HardEdgeMatrix matrix, float tolerance)
        {
            Geometry = geometry;
            Matrix = matrix;
            Tolerance = tolerance;
        }

        internal PathGeometry Geometry { get; }

        internal HardEdgeMatrix Matrix { get; }

        internal float Tolerance { get; }
    }

    //The map from an element's own coordinates (before its own transform) to the output frame
    //  on a layer: the layer's view, then the fit (scale, then the drawing rectangle's offset)
    internal static HardEdgeMatrix LayerMatrix(Matrix3x2 view, float scale, float left, float top)
        => HardEdgeMatrix.From(view).Then(new HardEdgeMatrix(scale, 0, 0, scale, left, top));

    //The areas one element covers at the time, given the map from its PARENT's coordinates
    internal static void Collect(DrawingElement element, HardEdgeMatrix parent, double time, List<Area> areas)
    {
        if (!element.IsVisibleAt(time)) { return; }
        if (DrawingRenderer.ScaleAlpha(255, element.Opacity) == 0) { return; }
        HardEdgeMatrix matrix = OwnMatrix(element, parent);

        switch (element)
        {
            case Stroke stroke:
                AddArea(areas, StrokeArea(stroke, ToleranceFor(matrix)), matrix);
                break;
            case AnimatedShape animated:
                if (animated.Current != null) { Collect(animated.Current, matrix, time, areas); }
                break;
            case DrawingGroup group:
                foreach (DrawingElement child in group.GetChildren()) { Collect(child, matrix, time, areas); }
                break;
            case DrawingShape shape:
                float tolerance = ToleranceFor(matrix);
                if (!(tolerance > 0f)) { return; }
                IReadOnlyList<PathGeometry> covered = shape.GetCoverageAreas(tolerance);
                if (covered == null) { return; }
                foreach (PathGeometry geometry in covered) { AddArea(areas, geometry, matrix, tolerance); }
                break;
        }
    }

    //The element's own coordinates -> output: its transform, then its parent's map
    internal static HardEdgeMatrix OwnMatrix(DrawingElement element, HardEdgeMatrix parent)
        => element.Transform.IsIdentity ? parent : HardEdgeMatrix.From(element.Transform.ToMatrix()).Then(parent);

    //Fills the covered pixels of the areas through the writer
    internal static void Rasterize(List<Area> areas, int width, int height, HardEdgeRasterizer.SpanWriter writer)
    {
        foreach (Area area in areas)
        {
            if (area.Geometry == null || area.Geometry.IsEmpty) { continue; }
            HardEdgeRasterizer.Fill(area.Geometry.Flatten(area.Tolerance), area.Matrix, area.Geometry.FillRule, width, height, writer);
        }
    }

    //A curve tolerance in the element's own units that keeps the flattening within
    //  FrameTolerance output pixels (0 when the map collapses everything)
    private static float ToleranceFor(HardEdgeMatrix matrix)
    {
        double stretch = matrix.MaxStretch;
        if (!(stretch > 0) || !double.IsFinite(stretch)) { return 0f; }
        double tolerance = FrameTolerance / stretch;
        return (float)Math.Min(Math.Max(tolerance, 1e-6), 1e6);
    }

    private static void AddArea(List<Area> areas, PathGeometry geometry, HardEdgeMatrix matrix)
        => AddArea(areas, geometry, matrix, ToleranceFor(matrix));

    private static void AddArea(List<Area> areas, PathGeometry geometry, HardEdgeMatrix matrix, float tolerance)
    {
        if (geometry == null || geometry.IsEmpty || !(tolerance > 0f)) { return; }
        areas.Add(new Area(geometry, matrix, tolerance));
    }

    //A freehand stroke: its polyline outlined with round caps and joins at its width (a single
    //  point is a dot of that diameter) - the shape the renderer draws
    internal static PathGeometry StrokeArea(Stroke stroke, float tolerance)
    {
        if (!(tolerance > 0f)) { return null; }
        StrokePoint[] points = stroke.GetPoints();
        if (points.Length < 1) { return null; }
        if (points.Length == 1)
        {
            return PathGeometry.Circle(new PointF(points[0].X, points[0].Y), stroke.Width / 2f);
        }
        PathGeometry line = stroke.GetOutline();
        return line?.ToStrokeOutline(new PathStrokeStyle(stroke.Width, PathStrokeCap.Round, PathStrokeJoin.Round), tolerance);
    }
}
