// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Model; //Was previously: namespace Svg.Model;

/// <summary>Represents a span of text associated with a typeface and its advance width.</summary>
/// <param name="Text">The text content of the span.</param>
/// <param name="Advance">The advance width of the span.</param>
/// <param name="Typeface">The typeface used for the span.</param>
public record struct TypefaceSpan(string Text, float Advance, SKTypeface Typeface);
/// <summary>Represents a shaped glyph run with positioned glyphs.</summary>
/// <param name="Glyphs">The glyph identifiers.</param>
/// <param name="Points">The positions for each glyph.</param>
/// <param name="Clusters">The cluster mapping from glyphs to characters.</param>
/// <param name="Advance">The total advance width of the run.</param>
public readonly record struct ShapedGlyphRun(ushort[] Glyphs, SKPoint[] Points, int[] Clusters, float Advance);

/// <summary>Provides asset loading services for SVG rendering.</summary>
public interface ISvgAssetLoader
{
    /// <summary>Gets a value indicating whether SVG fonts are enabled.</summary>
    bool EnableSvgFonts { get; }
    /// <summary>Loads an image from the specified stream.</summary>
    /// <param name="stream">The stream containing the image data.</param>
    /// <returns>The loaded image.</returns>
    SKImage LoadImage(Stream stream);
    /// <summary>Finds typefaces suitable for rendering the specified text.</summary>
    /// <param name="text">The text to find typefaces for.</param>
    /// <param name="paintPreferredTypeface">The paint containing the preferred typeface.</param>
    /// <returns>A list of typeface spans for the text.</returns>
    List<TypefaceSpan> FindTypefaces(string text, SKPaint paintPreferredTypeface);
    /// <summary>Gets font metrics for the specified paint.</summary>
    /// <param name="paint">The paint to get metrics for.</param>
    /// <returns>The font metrics.</returns>
    SKFontMetrics GetFontMetrics(SKPaint paint);
    /// <summary>Measures the width of the specified text.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="paint">The paint to use for measurement.</param>
    /// <param name="bounds">Receives the text bounds.</param>
    /// <returns>The measured text width.</returns>
    float MeasureText(string text, SKPaint paint, ref SKRect bounds);
    /// <summary>Gets the geometric path representing the specified text.</summary>
    /// <param name="text">The text to convert to a path.</param>
    /// <param name="paint">The paint to use for path generation.</param>
    /// <param name="x">The X coordinate for the text origin.</param>
    /// <param name="y">The Y coordinate for the text origin.</param>
    /// <returns>The text path.</returns>
    SKPath GetTextPath(string text, SKPaint paint, float x, float y);
}

/// <summary>Provides options for text reference rendering in SVG documents.</summary>
public interface ISvgTextReferenceRenderingOptions
{
    /// <summary>Gets a value indicating whether text references are enabled.</summary>
    bool EnableTextReferences { get; }
}

/// <summary>Resolves typefaces for individual text runs.</summary>
public interface ISvgTextRunTypefaceResolver
{
    /// <summary>Finds a typeface suitable for rendering the specified text run.</summary>
    /// <param name="text">The text run to find a typeface for.</param>
    /// <param name="paintPreferredTypeface">The paint containing the preferred typeface.</param>
    /// <returns>The resolved typeface.</returns>
    SKTypeface FindRunTypeface(string text, SKPaint paintPreferredTypeface);
}

/// <summary>Resolves shaped glyph runs for text.</summary>
public interface ISvgTextGlyphRunResolver
{
    /// <summary>Attempts to shape a glyph run for the specified text.</summary>
    /// <param name="text">The text to shape.</param>
    /// <param name="paint">The paint to use for shaping.</param>
    /// <param name="shapedRun">When successful, receives the shaped glyph run.</param>
    /// <returns><c>true</c> if shaping was successful; otherwise, <c>false</c>.</returns>
    bool TryShapeGlyphRun(string text, SKPaint paint, out ShapedGlyphRun shapedRun);
}

/// <summary>Resolves shaped glyph runs for text with directional support.</summary>
public interface ISvgTextDirectedGlyphRunResolver
{
    /// <summary>Attempts to shape a glyph run for the specified text with direction.</summary>
    /// <param name="text">The text to shape.</param>
    /// <param name="paint">The paint to use for shaping.</param>
    /// <param name="rightToLeft">Whether the text direction is right-to-left.</param>
    /// <param name="shapedRun">When successful, receives the shaped glyph run.</param>
    /// <returns><c>true</c> if shaping was successful; otherwise, <c>false</c>.</returns>
    bool TryShapeGlyphRun(string text, SKPaint paint, bool rightToLeft, out ShapedGlyphRun shapedRun);
}
