// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.IO;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents an encoded image backed by raw byte data.
// </summary>
internal class SKImage : ICloneable, IDeepCloneable<SKImage>
{
    // <summary>Gets or sets the raw encoded image data.</summary>
    public byte[] Data { get; set; }

    // <summary>Gets or sets the image width in pixels.</summary>
    public float Width { get; set; }

    // <summary>Gets or sets the image height in pixels.</summary>
    public float Height { get; set; }

    // <summary>
    // Reads all bytes from the specified stream and returns them as an array.
    // </summary>
    // <param name="sourceStream">The stream to read from.</param>
    // <returns>A byte array containing the stream content.</returns>
    public static byte[] FromStream(Stream sourceStream)
    {
        using var memoryStream = new MemoryStream();
        sourceStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    // <summary>Creates a deep clone of this image.</summary>
    // <returns>A new <see cref="SKImage"/> that is a deep copy.</returns>
    public SKImage Clone() => DeepClone(new CloneContext());

    // <inheritdoc />
    public SKImage DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKImage DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKImage existing))
        {
            return existing;
        }

        var clone = new SKImage();
        context.Add(this, clone);

        clone.Data = CloneHelpers.CloneArray(Data, context);
        clone.Width = Width;
        clone.Height = Height;

        return clone;
    }
}
