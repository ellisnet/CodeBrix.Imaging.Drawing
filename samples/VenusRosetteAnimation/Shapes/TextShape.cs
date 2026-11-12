using System;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;

namespace VenusRosetteAnimation.Shapes;

/// <summary>
/// A line of text in the bundled Open Sans (never a system font), with optional letter spacing.
/// </summary>
internal sealed class TextShape : DrawingShape
{
    private static readonly Lazy<SKTypeface> OpenSans = new Lazy<SKTypeface>(LoadOpenSans);

    private readonly string text;
    private readonly float x;
    private readonly float y;
    private readonly float size;
    private readonly SKTextAlign align;
    private readonly float tracking;

    /// <summary>Creates the text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="x">The anchor x (left, centre or right edge, by <paramref name="align" />).</param>
    /// <param name="y">The baseline y.</param>
    /// <param name="size">The font size, pixels.</param>
    /// <param name="color">The colour.</param>
    /// <param name="align">The alignment about <paramref name="x" />.</param>
    /// <param name="tracking">Extra space between letters, as a fraction of the font size.</param>
    public TextShape(string text, float x, float y, float size, Color color, SKTextAlign align = SKTextAlign.Left, float tracking = 0)
        : base(1, color)
    {
        this.text = text ?? string.Empty;
        this.x = x;
        this.y = y;
        this.size = size;
        this.align = align;
        this.tracking = tracking * size;
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        if (text.Length == 0) return;
        using SKFont font = new SKFont(OpenSans.Value, size) { Subpixel = true, Edging = SKFontEdging.Antialias };
        using SKPaint paint = new SKPaint { Color = color, IsAntialias = true };
        if (tracking == 0)
        {
            canvas.DrawText(text, x, y, align, font, paint);
            return;
        }

        float total = 0;
        float[] advances = new float[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            advances[i] = font.MeasureText(text[i].ToString());
            total += advances[i] + (i < text.Length - 1 ? tracking : 0);
        }

        float pen = align switch
        {
            SKTextAlign.Center => x - (total / 2),
            SKTextAlign.Right => x - total,
            _ => x,
        };
        for (int i = 0; i < text.Length; i++)
        {
            canvas.DrawText(text[i].ToString(), pen, y, SKTextAlign.Left, font, paint);
            pen += advances[i] + tracking;
        }
    }

    private static SKTypeface LoadOpenSans()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "Fonts", "OpenSans-Regular.ttf");
        return SKTypeface.FromFile(path) ?? throw new FileNotFoundException("The bundled Open Sans font is missing.", path);
    }
}
