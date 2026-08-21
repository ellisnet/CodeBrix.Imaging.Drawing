// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Base class for drawable objects that can produce an <see cref="SKPicture"/> snapshot.
/// </summary>
public abstract class SKDrawable : ICloneable, IDeepCloneable<SKDrawable>
{
    /// <summary>Gets the bounding rectangle of the drawable content.</summary>
    public SKRect Bounds => OnGetBounds();

    /// <summary>Creates a snapshot picture of this drawable using its own bounds.</summary>
    /// <returns>An <see cref="SKPicture"/> containing the drawn content.</returns>
    public SKPicture Snapshot() => Snapshot(OnGetBounds());

    /// <summary>
    /// Creates a snapshot picture of this drawable using the specified bounds.
    /// </summary>
    /// <param name="bounds">The recording bounds.</param>
    /// <returns>An <see cref="SKPicture"/> containing the drawn content.</returns>
    public SKPicture Snapshot(SKRect bounds)
    {
        var skPictureRecorder = new SKPictureRecorder();
        var skCanvas = skPictureRecorder.BeginRecording(bounds);
        OnDraw(skCanvas);
        return skPictureRecorder.EndRecording();
    }

    /// <summary>Called to draw the content onto a canvas.</summary>
    /// <param name="canvas">The canvas to draw on.</param>
    protected virtual void OnDraw(SKCanvas canvas)
    {
    }

    /// <summary>Called to compute the bounding rectangle.</summary>
    /// <returns>The bounding rectangle.</returns>
    protected virtual SKRect OnGetBounds() => SKRect.Empty;

    /// <summary>Creates a deep clone of this drawable.</summary>
    /// <returns>A new <see cref="SKDrawable"/> that is a deep copy.</returns>
    public abstract SKDrawable Clone();

    /// <inheritdoc />
    public virtual SKDrawable DeepClone() => Clone();

    object ICloneable.Clone() => Clone();
}
