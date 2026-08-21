using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.Fonts;
using CodeBrix.Imaging.Fonts.Unicode;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

/// <summary>
/// A fully managed <see cref="ISvgAssetLoader"/> implementation backed by the CodeBrix.Imaging
/// font and image machinery (no SkiaSharp, no HarfBuzz, no native code). Text measurement,
/// font metrics, and text-to-path conversion are performed against fonts registered in a
/// <see cref="NoSkiaFontRegistry"/>; when a requested family is not registered the first
/// registered font is used as a fallback, and when no fonts are registered at all every text
/// operation returns safe defaults (zero metrics and empty paths) instead of throwing.
/// </summary>
public sealed class ImagingSvgAssetLoader : ISvgAssetLoader
{
    private readonly NoSkiaFontRegistry _fontRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImagingSvgAssetLoader"/> class.
    /// </summary>
    /// <param name="fontRegistry">The registry supplying the fonts used for text operations.</param>
    public ImagingSvgAssetLoader(NoSkiaFontRegistry fontRegistry)
    {
        _fontRegistry = fontRegistry ?? throw new ArgumentNullException(nameof(fontRegistry));
    }

    /// <summary>Gets the registry supplying the fonts used for text operations.</summary>
    public NoSkiaFontRegistry FontRegistry => _fontRegistry;

    /// <summary>
    /// Gets or sets a value indicating whether SVG fonts (fonts defined via SVG
    /// <c>&lt;font&gt;</c> elements) are enabled. Defaults to <c>true</c>.
    /// </summary>
    public bool EnableSvgFonts { get; set; } = true;

    /// <inheritdoc />
    public SKImage LoadImage(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var data = SKImage.FromStream(stream);
        float width = 0f;
        float height = 0f;
        try
        {
            using var memoryStream = new MemoryStream(data, writable: false);
            var info = Image.Identify(memoryStream);
            if (info is { })
            {
                width = info.Width;
                height = info.Height;
            }
        }
        catch (Exception)
        {
            // Undecodable image data: keep the raw bytes but report zero dimensions.
        }

        return new SKImage { Data = data, Width = width, Height = height };
    }

    /// <inheritdoc />
    public List<TypefaceSpan> FindTypefaces(string text, SKPaint paintPreferredTypeface)
    {
        var ret = new List<TypefaceSpan>();

        if (string.IsNullOrEmpty(text))
        {
            return ret;
        }

        var preferredTypeface = paintPreferredTypeface?.Typeface;
        var weight = preferredTypeface?.FontWeight ?? SKFontStyleWeight.Normal;
        var width = preferredTypeface?.FontWidth ?? SKFontStyleWidth.Normal;
        var slant = preferredTypeface?.FontSlant ?? SKFontStyleSlant.Upright;
        var style = ToFontStyle(preferredTypeface);
        var textSize = paintPreferredTypeface?.TextSize ?? 0f;

        FontFamily preferredFamily;
        var hasPreferredFamily =
            _fontRegistry.TryFindFamily(preferredTypeface?.FamilyName, out preferredFamily) ||
            _fontRegistry.TryGetFirstFamily(out preferredFamily);
        if (!hasPreferredFamily)
        {
            // No fonts registered at all: return a single span covering everything so the
            // caller can still lay the text out with the paint it already has.
            ret.Add(new TypefaceSpan(text, 0f, preferredTypeface));
            return ret;
        }

        var allFamilies = _fontRegistry.GetFamilies();
        var coverageCache = new Dictionary<string, CodeBrix.Imaging.Fonts.FontMetrics>(StringComparer.OrdinalIgnoreCase);

        CodeBrix.Imaging.Fonts.FontMetrics GetMetrics(FontFamily family)
        {
            if (!coverageCache.TryGetValue(family.Name, out var metrics))
            {
                metrics = family.CreateFont(1f, style).FontMetrics;
                coverageCache[family.Name] = metrics;
            }

            return metrics;
        }

        bool MatchCharacter(int codepoint, out FontFamily matched)
        {
            if (CanRenderCodepoint(GetMetrics(preferredFamily), codepoint))
            {
                matched = preferredFamily;
                return true;
            }

            foreach (var family in allFamilies)
            {
                if (CanRenderCodepoint(GetMetrics(family), codepoint))
                {
                    matched = family;
                    return true;
                }
            }

            matched = default;
            return false;
        }

        var currentHasFamily = false;
        FontFamily currentFamily = default;
        var currentTypefaceStartIndex = 0;
        var i = 0;

        void YieldCurrentTypefaceText()
        {
            var currentTypefaceText = text.Substring(currentTypefaceStartIndex, i - currentTypefaceStartIndex);
            var advanceFamily = currentHasFamily ? currentFamily : preferredFamily;
            var advance = MeasureAdvance(currentTypefaceText, advanceFamily, style, textSize);
            var typeface = currentHasFamily
                ? SKTypeface.FromFamilyName(currentFamily.Name, weight, width, slant)
                : null;
            ret.Add(new TypefaceSpan(currentTypefaceText, advance, typeface));
        }

        for (; i < text.Length; i++)
        {
            var hasMatch = MatchCharacter(char.ConvertToUtf32(text, i), out var matchedFamily);
            if (currentHasFamily && char.IsWhiteSpace(text, i))
            {
                // Keep whitespace in the active span so the run stays attached to the
                // surrounding text instead of splitting on a font fallback for spaces.
                hasMatch = true;
                matchedFamily = currentFamily;
            }

            if (i == 0)
            {
                currentHasFamily = hasMatch;
                currentFamily = matchedFamily;
            }
            else if (currentHasFamily != hasMatch ||
                     (currentHasFamily && hasMatch &&
                      !string.Equals(currentFamily.Name, matchedFamily.Name, StringComparison.OrdinalIgnoreCase)))
            {
                YieldCurrentTypefaceText();

                currentTypefaceStartIndex = i;
                currentHasFamily = hasMatch;
                currentFamily = matchedFamily;
            }

            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        YieldCurrentTypefaceText();

        return ret;
    }

    /// <inheritdoc />
    public SKFontMetrics GetFontMetrics(SKPaint paint)
    {
        var font = ResolveFont(paint);
        if (font is null)
        {
            return default;
        }

        var metrics = font.FontMetrics;
        var scale = font.Size / metrics.UnitsPerEm;
        var ascent = -(metrics.Ascender * scale);
        var descent = Math.Abs(metrics.Descender) * scale;
        return new SKFontMetrics
        {
            Top = ascent,
            Ascent = ascent,
            Descent = descent,
            Bottom = descent,
            Leading = metrics.LineGap * scale,
            // The font tables express the underline position with negative values below the
            // baseline and the strikeout position with positive values above it; the Skia
            // convention this shim mirrors is the opposite sign in both cases.
            UnderlinePosition = -(metrics.UnderlinePosition * scale),
            UnderlineThickness = metrics.UnderlineThickness * scale,
            StrikeoutPosition = -(metrics.StrikeoutPosition * scale),
            StrikeoutThickness = metrics.StrikeoutSize * scale
        };
    }

    /// <inheritdoc />
    public float MeasureText(string text, SKPaint paint, ref SKRect bounds)
    {
        bounds = default;

        var font = ResolveFont(paint);
        if (font is null || string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var options = CreateTextOptions(font);
        var advance = TextMeasurer.Measure(text, options).Width;
        var ink = TextMeasurer.MeasureBounds(text, options);

        // MeasureBounds reports coordinates from the top-left of the layout box; convert to
        // Skia semantics where the text origin sits on the baseline (top is typically negative).
        var ascent = font.FontMetrics.Ascender * font.Size / font.FontMetrics.UnitsPerEm;
        bounds = new SKRect(ink.Left, ink.Top - ascent, ink.Right, ink.Bottom - ascent);

        return advance;
    }

    /// <inheritdoc />
    public SKPath GetTextPath(string text, SKPaint paint, float x, float y)
    {
        var path = new SKPath();

        var font = ResolveFont(paint);
        if (font is null || string.IsNullOrEmpty(text))
        {
            return path;
        }

        var options = CreateTextOptions(font);

        var alignedX = x;
        var textAlign = paint?.TextAlign ?? SKTextAlign.Left;
        if (textAlign != SKTextAlign.Left)
        {
            var advance = TextMeasurer.Measure(text, options).Width;
            alignedX = textAlign == SKTextAlign.Center ? x - advance / 2f : x - advance;
        }

        // The layout origin is the top-left of the text box; offset it upward by the ascent so
        // that the requested Y coordinate lands on the text baseline (Skia semantics).
        var ascent = font.FontMetrics.Ascender * font.Size / font.FontMetrics.UnitsPerEm;
        options.Origin = new Vector2(alignedX, y - ascent);

        TextRenderer.RenderTextTo(new PathGlyphRenderer(path), text, options);

        return path;
    }

    private static TextOptions CreateTextOptions(Font font)
    {
        // At 72 DPI one point equals one pixel, so the paint's TextSize maps 1:1 to user units.
        return new TextOptions(font)
        {
            Dpi = 72f
        };
    }

    private Font ResolveFont(SKPaint paint)
    {
        if (paint is null || paint.TextSize <= 0f)
        {
            return null;
        }

        var typeface = paint.Typeface;
        FontFamily family;
        if (!_fontRegistry.TryFindFamily(typeface?.FamilyName, out family) &&
            !_fontRegistry.TryGetFirstFamily(out family))
        {
            return null;
        }

        return family.CreateFont(paint.TextSize, ToFontStyle(typeface));
    }

    private static FontStyle ToFontStyle(SKTypeface typeface)
    {
        var style = FontStyle.Regular;
        if (typeface is null)
        {
            return style;
        }

        if ((int)typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold)
        {
            style |= FontStyle.Bold;
        }

        if (typeface.FontSlant != SKFontStyleSlant.Upright)
        {
            style |= FontStyle.Italic;
        }

        return style;
    }

    private static float MeasureAdvance(string text, FontFamily family, FontStyle style, float textSize)
    {
        if (string.IsNullOrEmpty(text) || textSize <= 0f)
        {
            return 0f;
        }

        var options = CreateTextOptions(family.CreateFont(textSize, style));
        return TextMeasurer.Measure(text, options).Width;
    }

    private static bool CanRenderCodepoint(CodeBrix.Imaging.Fonts.FontMetrics metrics, int codepoint)
    {
        foreach (var glyphMetrics in metrics.GetGlyphMetrics(new CodePoint(codepoint), ColorFontSupport.None))
        {
            return glyphMetrics.GlyphType != GlyphType.Fallback;
        }

        return false;
    }

    /// <summary>
    /// An <see cref="IGlyphRenderer"/> that records glyph outlines into a shim
    /// <see cref="SKPath"/> as move/line/quad/cubic/close commands.
    /// </summary>
    private sealed class PathGlyphRenderer : IGlyphRenderer
    {
        private readonly SKPath _path;

        public PathGlyphRenderer(SKPath path)
        {
            _path = path;
        }

        public void BeginText(FontRectangle bounds)
        {
        }

        public bool BeginGlyph(FontRectangle bounds, GlyphRendererParameters parameters)
        {
            return true;
        }

        public void BeginFigure()
        {
        }

        public void MoveTo(Vector2 point)
        {
            _path.MoveTo(point.X, point.Y);
        }

        public void LineTo(Vector2 point)
        {
            _path.LineTo(point.X, point.Y);
        }

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
        {
            _path.QuadTo(secondControlPoint.X, secondControlPoint.Y, point.X, point.Y);
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        {
            _path.CubicTo(
                secondControlPoint.X, secondControlPoint.Y,
                thirdControlPoint.X, thirdControlPoint.Y,
                point.X, point.Y);
        }

        public void EndFigure()
        {
            _path.Close();
        }

        public void EndGlyph()
        {
        }

        public void EndText()
        {
        }
    }
}
