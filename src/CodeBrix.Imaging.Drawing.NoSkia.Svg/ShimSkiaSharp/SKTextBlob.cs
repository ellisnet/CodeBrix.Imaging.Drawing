// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents a positioned text blob containing text or glyphs at specified positions.
// </summary>
internal sealed class SKTextBlob : ICloneable, IDeepCloneable<SKTextBlob>
{
    // <summary>Gets the text content, or <c>null</c> if glyphs are used.</summary>
    public string Text { get; private set; }
    // <summary>Gets the glyph identifiers, or <c>null</c> if text is used.</summary>
    public ushort[] Glyphs { get; private set; }
    // <summary>Gets the positions for each character or glyph.</summary>
    public SKPoint[] Points { get; private set; }

    private SKTextBlob()
    {
    }

    // <summary>
    // Creates a positioned text blob from text and point positions.
    // </summary>
    // <param name="text">The text content.</param>
    // <param name="points">The position of each character.</param>
    // <returns>A new <see cref="SKTextBlob"/>.</returns>
    public static SKTextBlob CreatePositioned(string text, SKPoint[] points)
        => new() { Text = text, Points = points };

    // <summary>
    // Creates a positioned text blob from glyph identifiers and point positions.
    // </summary>
    // <param name="glyphs">The glyph identifiers.</param>
    // <param name="points">The position of each glyph.</param>
    // <returns>A new <see cref="SKTextBlob"/>.</returns>
    public static SKTextBlob CreatePositionedGlyphs(ushort[] glyphs, SKPoint[] points)
        => new() { Glyphs = glyphs, Points = points };

    // <summary>Creates a deep clone of this text blob.</summary>
    // <returns>A new <see cref="SKTextBlob"/> that is a deep copy.</returns>
    public SKTextBlob Clone() => DeepClone(new CloneContext());

    // <inheritdoc />
    public SKTextBlob DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKTextBlob DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKTextBlob existing))
        {
            return existing;
        }

        var clone = new SKTextBlob();
        context.Add(this, clone);

        clone.Text = Text;
        clone.Glyphs = CloneHelpers.CloneArray(Glyphs, context);
        clone.Points = CloneHelpers.CloneArray(Points, context);

        return clone;
    }
}
