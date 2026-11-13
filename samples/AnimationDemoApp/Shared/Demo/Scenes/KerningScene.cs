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
/// Kerning: the same words drawn with the font's kerning (the default) and without it, one above the other, with
/// each line's measured width; then a tracked (letter-spaced) line whose tracking eases in and out - shaped as one
/// run with the tracking added between its glyphs, so it stays kerned - beside the same line unkerned.
/// </summary>
internal sealed class KerningScene : DemoScene
{
    private const string Pairs = "AVATAR  WAVE  Tower  Ty  To";
    private const string Tracked = "WAVY TOWER";

    private readonly DrawingSession session;
    private readonly DrawingLayer moving;

    /// <summary>Builds the kerned and unkerned rows (drawn once; the tracked rows are rebuilt every frame).</summary>
    public KerningScene()
    {
        session = CreateFrameSession();
        DrawingLayer rows = session.AddLayer("Rows", Ink);
        Label(rows, session, "kerning on (the default) - the font's own pair adjustments", 40, 140, 14, Dim);
        Label(rows, session, "kerning off - plain advances", 40, 300, 14, Dim);

        var kerned = new TextShape(Pairs, 40, 240, 76, session.Fonts, Ink);
        var plain = new TextShape(Pairs, 40, 400, 76, session.Fonts, Dim, TextAlign.Left, 0f, null, kerning: false);
        rows.AddShape(kerned);
        rows.AddShape(plain);
        Bracket(rows, kerned, 256, Accent);
        Bracket(rows, plain, 416, Dim);
        Label(rows, session, Width(kerned), 1240, 240, 15, Accent, TextAlign.Right);
        Label(rows, session, Width(plain), 1240, 400, 15, Dim, TextAlign.Right);

        Label(rows, session, "tracked text stays kerned: one shaped run, tracking added between its glyphs", 40, 480, 14, Dim);
        moving = session.AddLayer("Tracked", Ink);
    }

    /// <inheritdoc />
    public override string Name => "kerning";

    /// <inheritdoc />
    public override string Title => "Kerning";

    /// <inheritdoc />
    public override string Description => "Text is kerned when the font carries kerning data; TextShape kerning: false switches it off; tracking keeps it";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        moving.Clear();
        float tracking = (float)(PingPong(seconds, 4) * 0.35);
        var kerned = new TextShape(Tracked, 40, 570, 58, session.Fonts, Ink, TextAlign.Left, tracking, null, kerning: true);
        var plain = new TextShape(Tracked, 40, 660, 58, session.Fonts, Dim, TextAlign.Left, tracking, null, kerning: false);
        moving.AddShape(kerned);
        moving.AddShape(plain);
        Bracket(moving, kerned, 584, Accent);
        Bracket(moving, plain, 674, Dim);
        Label(moving, session, string.Format(CultureInfo.InvariantCulture, "tracking {0:0.00} em", tracking), 1240, 520, 15, Accent, TextAlign.Right);
        Label(moving, session, "kerned " + Width(kerned), 1240, 570, 15, Accent, TextAlign.Right);
        Label(moving, session, "unkerned " + Width(plain), 1240, 660, 15, Dim, TextAlign.Right);
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static string Width(TextShape text) => string.Format(CultureInfo.InvariantCulture, "width {0:0.0}", text.Width);

    private static void Bracket(DrawingLayer layer, TextShape text, float y, Color color)
    {
        layer.AddShape(new LineShape(text.Left, y, text.Left + text.Width, y, 2, color));
        layer.AddShape(new LineShape(text.Left, y - 6, text.Left, y, 2, color));
        layer.AddShape(new LineShape(text.Left + text.Width, y - 6, text.Left + text.Width, y, 2, color));
    }
}
