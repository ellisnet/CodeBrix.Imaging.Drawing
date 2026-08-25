// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>Represents an abstract path drawing command.</summary>
internal abstract record PathCommand : IDeepCloneable<PathCommand>
{
    // <inheritdoc />
    public PathCommand DeepClone() => DeepClone(new CloneContext());

    internal PathCommand DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out PathCommand existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            PathCommand clone = this switch
            {
                AddCirclePathCommand addCirclePathCommand => new AddCirclePathCommand(addCirclePathCommand.X, addCirclePathCommand.Y, addCirclePathCommand.Radius),
                AddOvalPathCommand addOvalPathCommand => new AddOvalPathCommand(addOvalPathCommand.Rect),
                AddPolyPathCommand addPolyPathCommand => new AddPolyPathCommand(CloneHelpers.CloneList(addPolyPathCommand.Points, context), addPolyPathCommand.Close),
                AddRectPathCommand addRectPathCommand => new AddRectPathCommand(addRectPathCommand.Rect),
                AddRoundRectPathCommand addRoundRectPathCommand => new AddRoundRectPathCommand(addRoundRectPathCommand.Rect, addRoundRectPathCommand.Rx, addRoundRectPathCommand.Ry),
                ArcToPathCommand arcToPathCommand => new ArcToPathCommand(arcToPathCommand.Rx, arcToPathCommand.Ry, arcToPathCommand.XAxisRotate, arcToPathCommand.LargeArc, arcToPathCommand.Sweep, arcToPathCommand.X, arcToPathCommand.Y),
                ClosePathCommand => new ClosePathCommand(),
                CubicToPathCommand cubicToPathCommand => new CubicToPathCommand(cubicToPathCommand.X0, cubicToPathCommand.Y0, cubicToPathCommand.X1, cubicToPathCommand.Y1, cubicToPathCommand.X2, cubicToPathCommand.Y2),
                LineToPathCommand lineToPathCommand => new LineToPathCommand(lineToPathCommand.X, lineToPathCommand.Y),
                MoveToPathCommand moveToPathCommand => new MoveToPathCommand(moveToPathCommand.X, moveToPathCommand.Y),
                QuadToPathCommand quadToPathCommand => new QuadToPathCommand(quadToPathCommand.X0, quadToPathCommand.Y0, quadToPathCommand.X1, quadToPathCommand.Y1),
                _ => throw new NotSupportedException($"Unsupported {nameof(PathCommand)} type: {GetType().Name}.")
            };

            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}

// <summary>A path command that adds a circle.</summary>
// <param name="X">The X coordinate of the center.</param>
// <param name="Y">The Y coordinate of the center.</param>
// <param name="Radius">The radius of the circle.</param>
internal record AddCirclePathCommand(float X, float Y, float Radius) : PathCommand;

// <summary>A path command that adds an oval inscribed in the specified rectangle.</summary>
// <param name="Rect">The bounding rectangle of the oval.</param>
internal record AddOvalPathCommand(SKRect Rect) : PathCommand;

// <summary>A path command that adds a polygon from a list of points.</summary>
// <param name="Points">The points defining the polygon.</param>
// <param name="Close">Whether to close the polygon.</param>
internal record AddPolyPathCommand(IList<SKPoint> Points, bool Close) : PathCommand;

// <summary>A path command that adds a rectangle.</summary>
// <param name="Rect">The rectangle to add.</param>
internal record AddRectPathCommand(SKRect Rect) : PathCommand;

// <summary>A path command that adds a rounded rectangle.</summary>
// <param name="Rect">The rectangle to round.</param>
// <param name="Rx">The X-axis corner radius.</param>
// <param name="Ry">The Y-axis corner radius.</param>
internal record AddRoundRectPathCommand(SKRect Rect, float Rx, float Ry) : PathCommand;

// <summary>A path command that adds an arc.</summary>
// <param name="Rx">The X-axis radius of the arc ellipse.</param>
// <param name="Ry">The Y-axis radius of the arc ellipse.</param>
// <param name="XAxisRotate">The rotation angle of the arc ellipse X axis.</param>
// <param name="LargeArc">Whether to use the large arc.</param>
// <param name="Sweep">The sweep direction of the arc.</param>
// <param name="X">The X coordinate of the arc endpoint.</param>
// <param name="Y">The Y coordinate of the arc endpoint.</param>
internal record ArcToPathCommand(float Rx, float Ry, float XAxisRotate, SKPathArcSize LargeArc, SKPathDirection Sweep, float X, float Y) : PathCommand;

// <summary>A path command that closes the current contour.</summary>
internal record ClosePathCommand : PathCommand;

// <summary>A path command that adds a cubic Bézier curve.</summary>
// <param name="X0">The X coordinate of the first control point.</param>
// <param name="Y0">The Y coordinate of the first control point.</param>
// <param name="X1">The X coordinate of the second control point.</param>
// <param name="Y1">The Y coordinate of the second control point.</param>
// <param name="X2">The X coordinate of the endpoint.</param>
// <param name="Y2">The Y coordinate of the endpoint.</param>
internal record CubicToPathCommand(float X0, float Y0, float X1, float Y1, float X2, float Y2) : PathCommand;

// <summary>A path command that adds a line to a point.</summary>
// <param name="X">The X coordinate of the endpoint.</param>
// <param name="Y">The Y coordinate of the endpoint.</param>
internal record LineToPathCommand(float X, float Y) : PathCommand;

// <summary>A path command that moves the current position to a point.</summary>
// <param name="X">The X coordinate to move to.</param>
// <param name="Y">The Y coordinate to move to.</param>
internal record MoveToPathCommand(float X, float Y) : PathCommand;

// <summary>A path command that adds a quadratic Bézier curve.</summary>
// <param name="X0">The X coordinate of the control point.</param>
// <param name="Y0">The Y coordinate of the control point.</param>
// <param name="X1">The X coordinate of the endpoint.</param>
// <param name="Y1">The Y coordinate of the endpoint.</param>
internal record QuadToPathCommand(float X0, float Y0, float X1, float Y1) : PathCommand;

// <summary>Represents a geometric path composed of drawing commands.</summary>
internal class SKPath : ICloneable, IDeepCloneable<SKPath>
{
    // <summary>Gets or sets the fill type for the path.</summary>
    public SKPathFillType FillType { get; set; }

    // <summary>Gets the list of path commands.</summary>
    public IList<PathCommand> Commands { get; private set; }

    // <summary>Gets a value indicating whether the path contains no commands.</summary>
    public bool IsEmpty => Commands is null || Commands.Count == 0;

    // <summary>Gets the bounding rectangle of the path.</summary>
    public SKRect Bounds => GetBounds();

    // <summary>Initializes a new empty path.</summary>
    public SKPath()
    {
        Commands = new List<PathCommand>();
    }

    // <summary>Creates a deep clone of this path.</summary>
    // <returns>A new <see cref="SKPath"/> instance.</returns>
    public SKPath Clone() => DeepClone(new CloneContext());

    // <inheritdoc />
    public SKPath DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKPath DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPath existing))
        {
            return existing;
        }

        var clone = new SKPath();
        context.Add(this, clone);

        clone.FillType = FillType;
        clone.Commands = CloneHelpers.CloneList(Commands, context, command => command.DeepClone(context));

        return clone;
    }

    private SKRect GetBounds()
    {
        if (Commands is null || Commands.Count == 0)
        {
            return SKRect.Empty;
        }

        var bounds = new SKRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        var last = new SKPoint();
        var haveLast = false;

        foreach (var pathCommand in Commands)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                    {
                        var x = moveToPathCommand.X;
                        var y = moveToPathCommand.Y;
                        SKPathBoundsHelper.ComputePointBounds(x, y, ref bounds);
                        last = new SKPoint(x, y);
                        haveLast = true;
                    }
                    break;
                case LineToPathCommand lineToPathCommand:
                    {
                        var x = lineToPathCommand.X;
                        var y = lineToPathCommand.Y;
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddLineBounds(last.X, last.Y, x, y, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(x, y, ref bounds);
                        }
                        last = new SKPoint(x, y);
                        haveLast = true;
                    }
                    break;
                case ArcToPathCommand arcToPathCommand:
                    {
                        var end = new SKPoint(arcToPathCommand.X, arcToPathCommand.Y);
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddArcBounds(last, end, arcToPathCommand.Rx, arcToPathCommand.Ry, arcToPathCommand.XAxisRotate, arcToPathCommand.LargeArc, arcToPathCommand.Sweep, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(end.X, end.Y, ref bounds);
                        }
                        last = end;
                        haveLast = true;
                    }
                    break;
                case QuadToPathCommand quadToPathCommand:
                    {
                        var p1 = new SKPoint(quadToPathCommand.X0, quadToPathCommand.Y0);
                        var p2 = new SKPoint(quadToPathCommand.X1, quadToPathCommand.Y1);
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddQuadBounds(last, p1, p2, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(p1.X, p1.Y, ref bounds);
                            SKPathBoundsHelper.ComputePointBounds(p2.X, p2.Y, ref bounds);
                        }
                        last = p2;
                        haveLast = true;
                    }
                    break;
                case CubicToPathCommand cubicToPathCommand:
                    {
                        var p1 = new SKPoint(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                        var p2 = new SKPoint(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                        var p3 = new SKPoint(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddCubicBounds(last, p1, p2, p3, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(p1.X, p1.Y, ref bounds);
                            SKPathBoundsHelper.ComputePointBounds(p2.X, p2.Y, ref bounds);
                            SKPathBoundsHelper.ComputePointBounds(p3.X, p3.Y, ref bounds);
                        }
                        last = p3;
                        haveLast = true;
                    }
                    break;
                case ClosePathCommand _:
                    break;
                case AddRectPathCommand addRectPathCommand:
                    {
                        var rect = addRectPathCommand.Rect;
                        SKPathBoundsHelper.ComputePointBounds(rect.Left, rect.Top, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        last = rect.BottomRight;
                        haveLast = true;
                    }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    {
                        var rect = addRoundRectPathCommand.Rect;
                        SKPathBoundsHelper.ComputePointBounds(rect.Left, rect.Top, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        last = rect.BottomRight;
                        haveLast = true;
                    }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                    {
                        var rect = addOvalPathCommand.Rect;
                        SKPathBoundsHelper.ComputePointBounds(rect.Left, rect.Top, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        last = rect.BottomRight;
                        haveLast = true;
                    }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                    {
                        var x = addCirclePathCommand.X;
                        var y = addCirclePathCommand.Y;
                        var radius = addCirclePathCommand.Radius;
                        SKPathBoundsHelper.ComputePointBounds(x - radius, y - radius, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(x + radius, y + radius, ref bounds);
                        last = new SKPoint(x + radius, y + radius);
                        haveLast = true;
                    }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                    {
                        if (addPolyPathCommand.Points is { })
                        {
                            var points = addPolyPathCommand.Points;
                            foreach (var point in points)
                            {
                                SKPathBoundsHelper.ComputePointBounds(point.X, point.Y, ref bounds);
                            }
                            if (points.Count > 0)
                            {
                                last = points[points.Count - 1];
                                haveLast = true;
                            }
                        }
                    }
                    break;
            }
        }

        return bounds;
    }

    // <summary>Moves the current position to the specified point.</summary>
    // <param name="x">The X coordinate.</param>
    // <param name="y">The Y coordinate.</param>
    public void MoveTo(float x, float y)
        => Commands?.Add(new MoveToPathCommand(x, y));

    // <summary>Adds a line from the current position to the specified point.</summary>
    // <param name="x">The X coordinate of the endpoint.</param>
    // <param name="y">The Y coordinate of the endpoint.</param>
    public void LineTo(float x, float y)
        => Commands?.Add(new LineToPathCommand(x, y));

    // <summary>Adds an arc from the current position to the specified point.</summary>
    // <param name="rx">The X-axis radius of the arc ellipse.</param>
    // <param name="ry">The Y-axis radius of the arc ellipse.</param>
    // <param name="xAxisRotate">The rotation angle of the arc ellipse X axis.</param>
    // <param name="largeArc">Whether to use the large arc.</param>
    // <param name="sweep">The sweep direction of the arc.</param>
    // <param name="x">The X coordinate of the endpoint.</param>
    // <param name="y">The Y coordinate of the endpoint.</param>
    public void ArcTo(float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y)
        => Commands?.Add(new ArcToPathCommand(rx, ry, xAxisRotate, largeArc, sweep, x, y));

    // <summary>Adds a quadratic Bézier curve from the current position.</summary>
    // <param name="x0">The X coordinate of the control point.</param>
    // <param name="y0">The Y coordinate of the control point.</param>
    // <param name="x1">The X coordinate of the endpoint.</param>
    // <param name="y1">The Y coordinate of the endpoint.</param>
    public void QuadTo(float x0, float y0, float x1, float y1)
        => Commands?.Add(new QuadToPathCommand(x0, y0, x1, y1));

    // <summary>Adds a cubic Bézier curve from the current position.</summary>
    // <param name="x0">The X coordinate of the first control point.</param>
    // <param name="y0">The Y coordinate of the first control point.</param>
    // <param name="x1">The X coordinate of the second control point.</param>
    // <param name="y1">The Y coordinate of the second control point.</param>
    // <param name="x2">The X coordinate of the endpoint.</param>
    // <param name="y2">The Y coordinate of the endpoint.</param>
    public void CubicTo(float x0, float y0, float x1, float y1, float x2, float y2)
        => Commands?.Add(new CubicToPathCommand(x0, y0, x1, y1, x2, y2));

    // <summary>Closes the current contour.</summary>
    public void Close()
        => Commands?.Add(new ClosePathCommand());

    // <summary>Adds a rectangle to the path.</summary>
    // <param name="rect">The rectangle to add.</param>
    public void AddRect(SKRect rect)
        => Commands?.Add(new AddRectPathCommand(rect));

    // <summary>Adds a rounded rectangle to the path.</summary>
    // <param name="rect">The rectangle to round.</param>
    // <param name="rx">The X-axis corner radius.</param>
    // <param name="ry">The Y-axis corner radius.</param>
    public void AddRoundRect(SKRect rect, float rx, float ry)
        => Commands?.Add(new AddRoundRectPathCommand(rect, rx, ry));

    // <summary>Adds an oval inscribed in the specified rectangle.</summary>
    // <param name="rect">The bounding rectangle of the oval.</param>
    public void AddOval(SKRect rect)
        => Commands?.Add(new AddOvalPathCommand(rect));

    // <summary>Adds a circle to the path.</summary>
    // <param name="x">The X coordinate of the center.</param>
    // <param name="y">The Y coordinate of the center.</param>
    // <param name="radius">The radius of the circle.</param>
    public void AddCircle(float x, float y, float radius)
        => Commands?.Add(new AddCirclePathCommand(x, y, radius));

    // <summary>Adds a polygon defined by an array of points.</summary>
    // <param name="points">The points defining the polygon.</param>
    // <param name="close">Whether to close the polygon.</param>
    public void AddPoly(SKPoint[] points, bool close = true)
        => Commands?.Add(new AddPolyPathCommand(points, close));
}
