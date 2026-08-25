// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents a typeface (font face) identified by family name and style parameters.
// </summary>
internal class SKTypeface : ICloneable, IDeepCloneable<SKTypeface>
{
    // <summary>Gets the font family name.</summary>
    public string FamilyName { get; private set; }
    // <summary>Gets the font weight.</summary>
    public SKFontStyleWeight FontWeight { get; private set; }
    // <summary>Gets the font width.</summary>
    public SKFontStyleWidth FontWidth { get; private set; }
    // <summary>Gets the font slant.</summary>
    public SKFontStyleSlant FontSlant { get; private set; }

    private SKTypeface()
    {
    }

    // <summary>
    // Creates a typeface from the specified family name and style parameters.
    // </summary>
    // <param name="familyName">The font family name.</param>
    // <param name="weight">The font weight.</param>
    // <param name="width">The font width.</param>
    // <param name="slant">The font slant.</param>
    // <returns>A new <see cref="SKTypeface"/>.</returns>
    public static SKTypeface FromFamilyName(
        string familyName,
        SKFontStyleWeight weight,
        SKFontStyleWidth width,
        SKFontStyleSlant slant)
    {
        return new()
        {
            FamilyName = familyName,
            FontWeight = weight,
            FontWidth = width,
            FontSlant = slant
        };
    }

    // <summary>Creates a deep clone of this typeface.</summary>
    // <returns>A new <see cref="SKTypeface"/> that is a deep copy.</returns>
    public SKTypeface Clone() => DeepClone(new CloneContext());

    // <inheritdoc />
    public SKTypeface DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKTypeface DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKTypeface existing))
        {
            return existing;
        }

        var clone = new SKTypeface();
        context.Add(this, clone);

        clone.FamilyName = FamilyName;
        clone.FontWeight = FontWeight;
        clone.FontWidth = FontWidth;
        clone.FontSlant = FontSlant;

        return clone;
    }
}
