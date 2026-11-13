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
/// Paragraphs: one text laid out four times by <see cref="TextLayoutEngine" /> - wrapped to a column and aligned
/// left, centre, right and justified - with each line's baseline and the column box drawn from the layout result's
/// line metrics. Below, a box whose width breathes: the text re-wraps every frame, is limited to two lines, ends in
/// an ellipsis after the last whole word that fits, and is centred vertically in the box.
/// </summary>
internal sealed class ParagraphScene : DemoScene
{
    private const string Text =
        "Text blocks wrap at spaces and hyphens, keep explicit line breaks, and align each line within the column. " +
        "Every line reports its top, height, baseline and width,\nso guides like these come straight from the layout.";

    private const string Trimmed =
        "When the box is too small the last kept line ends in an ellipsis, cut after the last whole word that fits, " +
        "and the lines stay centred vertically in the box however wide it is.";

    private static readonly TextAlign[] Aligns = { TextAlign.Left, TextAlign.Center, TextAlign.Right, TextAlign.Justify };

    private readonly DrawingSession session;
    private readonly DrawingLayer moving;

    /// <summary>Lays the four columns out once; the breathing box is rebuilt every frame.</summary>
    public ParagraphScene()
    {
        session = CreateFrameSession();
        DrawingLayer guides = session.AddLayer("Guides", Dim);
        DrawingLayer text = session.AddLayer("Text", Ink);
        const float columnWidth = 270;
        for (int i = 0; i < Aligns.Length; i++)
        {
            float x = 40 + (i * 306);
            const float y = 140;
            TextLayoutResult layout = session.LayoutText(Text, 20, options: new TextLayoutOptions { MaxWidth = columnWidth, Align = Aligns[i], LineSpacing = 1.2f });
            Label(guides, session, "TextAlign." + Aligns[i], x, 128, 13, Accent);
            guides.AddShape(new RectangleShape(x, y, layout.Width, layout.Height, 1, Color.FromRgba(132, 138, 164, 110)));
            foreach (TextLineMetrics line in layout.Lines)
            {
                guides.AddShape(new LineShape(x + line.Left, y + line.Baseline, x + line.Left + line.Width, y + line.Baseline, 1,
                    Color.FromRgba(255, 176, 72, 90)));
            }
            text.AddShape(new TextBlockShape(layout, x, y, Ink));
        }

        Label(guides, session, "MaxWidth animates; MaxLines = 2, TextTrimming.WordEllipsis, MaxHeight 120 with TextVerticalAlign.Center", 40, 470, 13, Dim);
        moving = session.AddLayer("Box", Ink);
    }

    /// <inheritdoc />
    public override string Name => "paragraph";

    /// <inheritdoc />
    public override string Title => "Paragraphs: wrapping, alignment, trimming";

    /// <inheritdoc />
    public override string Description => "TextLayoutEngine: MaxWidth wrapping, line breaks, Left/Center/Right/Justify, line metrics, ellipsis, vertical alignment";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        moving.Clear();
        float width = (float)(360 + (PingPong(seconds, 8) * 820));
        const float boxHeight = 120;
        TextLayoutResult layout = session.LayoutText(Trimmed, 26, options: new TextLayoutOptions
        {
            MaxWidth = width,
            MaxHeight = boxHeight,
            MaxLines = 2,
            Trimming = TextTrimming.WordEllipsis,
            VerticalAlign = TextVerticalAlign.Center,
            Align = TextAlign.Center,
        });
        moving.AddShape(new RectangleShape(40, 500, width, boxHeight, 1, Color.FromRgba(255, 255, 255, 14), isFilled: true, cornerRadius: 10));
        moving.AddShape(new RectangleShape(40, 500, width, boxHeight, 1.5f, Accent, cornerRadius: 10));
        moving.AddShape(new TextBlockShape(layout, 40, 500, Ink));
        Label(moving, session, string.Format(CultureInfo.InvariantCulture, "MaxWidth {0:0}   lines {1}   truncated {2}", width, layout.LineCount,
            layout.IsTruncated ? "yes" : "no"), 40, 650, 14, Accent);
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
