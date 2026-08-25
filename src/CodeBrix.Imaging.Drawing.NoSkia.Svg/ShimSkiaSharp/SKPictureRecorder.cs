// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Records drawing commands into an <see cref="SKPicture"/>.
// </summary>
internal sealed class SKPictureRecorder : ICloneable, IDeepCloneable<SKPictureRecorder>
{
    // <summary>Gets the recording bounds.</summary>
    public SKRect CullRect { get; private set; }

    // <summary>Gets the canvas that records drawing commands.</summary>
    public SKCanvas RecordingCanvas { get; private set; }

    // <summary>
    // Begins recording drawing commands within the specified bounds.
    // </summary>
    // <param name="cullRect">The bounding rectangle for the recording.</param>
    // <returns>A canvas to draw on.</returns>
    public SKCanvas BeginRecording(SKRect cullRect)
    {
        CullRect = cullRect;

        RecordingCanvas = new SKCanvas(new List<CanvasCommand>(), SKMatrix.Identity);

        return RecordingCanvas;
    }

    // <summary>
    // Finishes recording and returns the captured picture.
    // </summary>
    // <returns>An <see cref="SKPicture"/> containing the recorded drawing commands.</returns>
    public SKPicture EndRecording()
    {
        var picture = new SKPicture(CullRect, RecordingCanvas?.Commands);

        CullRect = SKRect.Empty;
        RecordingCanvas = null;

        return picture;
    }

    // <summary>Creates a deep clone of this recorder.</summary>
    // <returns>A new <see cref="SKPictureRecorder"/> that is a deep copy.</returns>
    public SKPictureRecorder Clone() => DeepClone(new CloneContext());

    // <inheritdoc />
    public SKPictureRecorder DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKPictureRecorder DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPictureRecorder existing))
        {
            return existing;
        }

        var clone = new SKPictureRecorder();
        context.Add(this, clone);

        clone.CullRect = CullRect;
        clone.RecordingCanvas = RecordingCanvas?.DeepClone(context);

        return clone;
    }
}
