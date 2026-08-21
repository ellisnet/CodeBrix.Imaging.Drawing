using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Contains font metric information describing the vertical layout of a typeface.
/// </summary>
public struct SKFontMetrics
{
    /// <summary>Gets or sets the greatest distance above the baseline (negative value).</summary>
    public float Top { get; set; }
    /// <summary>Gets or sets the recommended distance above the baseline (negative value).</summary>
    public float Ascent { get; set; }
    /// <summary>Gets or sets the recommended distance below the baseline (positive value).</summary>
    public float Descent { get; set; }
    /// <summary>Gets or sets the greatest distance below the baseline (positive value).</summary>
    public float Bottom { get; set; }
    /// <summary>Gets or sets the recommended spacing between lines.</summary>
    public float Leading { get; set; }
    /// <summary>Gets or sets the position of the strikeout line relative to the baseline, if available.</summary>
    public float? StrikeoutPosition { get; set; }
    /// <summary>Gets or sets the thickness of the strikeout line, if available.</summary>
    public float? StrikeoutThickness { get; set; }
    /// <summary>Gets or sets the position of the underline relative to the baseline, if available.</summary>
    public float? UnderlinePosition { get; set; }
    /// <summary>Gets or sets the thickness of the underline, if available.</summary>
    public float? UnderlineThickness { get; set; }
}
