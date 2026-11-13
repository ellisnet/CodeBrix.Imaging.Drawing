using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Text from a registered font: left, center and right alignment about one anchor line, letter spacing
/// (tracking) easing in and out, the measured <see cref="TextShape.Width" /> drawn as a bracket under each line,
/// and the outline bounds (<see cref="DrawingShape.GetBounds" />) drawn as a box.
/// </summary>
internal sealed class TextScene : DemoScene
{
    private const float Anchor = 640;
    private const string Sample = "Animate";

    private readonly DrawingSession session;
    private readonly DrawingLayer moving;

    /// <summary>Builds the anchor line and labels (drawn once; the text is rebuilt every frame).</summary>
    public TextScene()
    {
        session = CreateFrameSession();
        DrawingLayer guides = session.AddLayer("Guides", Dim);
        guides.AddShape(new LineShape(Anchor, ContentTop + 16, Anchor, 470, 1, Color.FromRgba(255, 176, 72, 160)));
        Label(guides, session, "anchor x", Anchor + 6, ContentTop + 30, 12, Accent);
        Label(guides, session, "TextAlign.Left", 40, 190, 13, Dim);
        Label(guides, session, "TextAlign.Center", 40, 300, 13, Dim);
        Label(guides, session, "TextAlign.Right", 40, 410, 13, Dim);
        Label(guides, session, "sizes, all measured before drawing (FontRegistry.MeasureText):", 40, 520, 13, Dim);
        moving = session.AddLayer("Text", Ink);
    }

    /// <inheritdoc />
    public override string Name => "text";

    /// <inheritdoc />
    public override string Title => "Text";

    /// <inheritdoc />
    public override string Description => "TextShape from a registered font: alignment, tracking, measured width (bracket) and outline bounds (box)";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        moving.Clear();
        float tracking = (float)(Easing.SineInOut(0.5 - (0.5 * Math.Cos(2 * Math.PI * seconds / 4))) * 0.45);
        TextAlign[] aligns = { TextAlign.Left, TextAlign.Center, TextAlign.Right };
        for (int i = 0; i < aligns.Length; i++)
        {
            float baseline = 190 + (i * 110);
            TextShape text = new TextShape(Sample, Anchor, baseline, 60, session.Fonts, Ink, aligns[i], tracking);
            moving.AddShape(text);

            // The measured advance width as a bracket, and the outline bounds as a box.
            float under = baseline + 16;
            moving.AddShape(new LineShape(text.Left, under, text.Left + text.Width, under, 2, Accent));
            moving.AddShape(new LineShape(text.Left, under - 6, text.Left, under, 2, Accent));
            moving.AddShape(new LineShape(text.Left + text.Width, under - 6, text.Left + text.Width, under, 2, Accent));
            RectangleF bounds = text.GetBounds().Value;
            moving.AddShape(new RectangleShape(bounds.X, bounds.Y, bounds.Width, bounds.Height, 1, Color.FromRgba(120, 170, 255, 150)));
            Label(moving, session, string.Format(CultureInfo.InvariantCulture, "width {0:0.0}", text.Width), 1240, baseline, 13, Dim, TextAlign.Right);
        }

        Label(moving, session, string.Format(CultureInfo.InvariantCulture, "tracking {0:0.00} em", tracking), 1240, 140, 15, Accent, TextAlign.Right);

        // A row of sizes laid end to end: each one placed with the width the registry measured for the one before.
        float x = 40;
        foreach (float size in new[] { 14f, 22f, 34f, 52f, 80f })
        {
            string word = size.ToString("0", CultureInfo.InvariantCulture) + " px";
            moving.AddShape(new TextShape(word, x, 640, size, session.Fonts, Ink));
            x += session.Fonts.MeasureText(word, size) + 28;
        }

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
