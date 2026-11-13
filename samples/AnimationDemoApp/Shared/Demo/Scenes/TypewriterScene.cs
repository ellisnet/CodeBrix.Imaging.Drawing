using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Revealing text over time. Top left: a typewriter - <see cref="TextBlockShape.RevealCount" /> bound to a track,
/// one character at a time, with a blinking caret placed from the layout's cluster boxes. Top right: words fading in
/// with <see cref="TextBlockShape.RevealSoftness" />. Bottom: the block split into word pieces and line pieces
/// (<see cref="TextBlockShape.CreatePieceGroup" />) animated one after another by
/// <see cref="DrawingAnimations.BindStagger" /> - words rising and growing, lines sliding in.
/// </summary>
internal sealed class TypewriterScene : DemoScene
{
    private const string Typed = "A typewriter reveal counts characters: spaces take their turn, and the caret follows the layout.";
    private const string Faded = "A softer reveal counts words and fades each one in over the two before it.";
    private const string Staggered = "Staggered words rise into place one after another";
    private const string Lines = "Lines can arrive\none at a time,\nsliding in from the left.";

    private readonly DrawingSession session;

    /// <summary>Builds the blocks, their pieces and every binding.</summary>
    public TypewriterScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "RevealCount bound to a track (characters); caret from Clusters[n].Bounds", 40, 128, 13, Dim);
        Label(notes, session, "RevealUnit = Word, RevealSoftness = 2", 680, 128, 13, Dim);
        Label(notes, session, "CreatePieceGroup(TextUnit.Word) + BindStagger (offset, scale, BackOut)", 40, 410, 13, Dim);
        Label(notes, session, "CreatePieceGroup(TextUnit.Line) + BindStagger (slide)", 680, 410, 13, Dim);

        DrawingLayer text = session.AddLayer("Text", Ink);
        session.ActiveLayer = text;

        // The typewriter: one more character every 1/24 s, and a caret after the last one shown.
        TextLayoutResult typedLayout = session.LayoutText(Typed, 30, options: new TextLayoutOptions { MaxWidth = 560, LineSpacing = 1.2f });
        var typed = new TextBlockShape(typedLayout, 40, 150, Ink);
        session.DrawShape(typed);
        var typing = new FloatTrack { { 0.3, 0f }, { 0.3 + (typedLayout.ClusterCount / 24.0), typedLayout.ClusterCount } };
        session.Animations.Bind(typed, AnimatedProperty.RevealCount, typing);
        session.DrawShape(new AnimatedShape(t => Caret(typedLayout, typing.Evaluate(t), t)));

        // Words fading in.
        TextLayoutResult fadedLayout = session.LayoutText(Faded, 30, options: new TextLayoutOptions { MaxWidth = 560, LineSpacing = 1.2f });
        var faded = new TextBlockShape(fadedLayout, 680, 150, Color.FromRgb(140, 200, 255)) { RevealUnit = TextUnit.Word, RevealSoftness = 2f };
        session.DrawShape(faded);
        session.Animations.Bind(faded, AnimatedProperty.RevealFraction, new FloatTrack { { 0.5, 0f }, { 4.5, 1f, EasingKind.SineInOut } });

        // Words as pieces, each rising and growing into place.
        TextLayoutResult staggeredLayout = session.LayoutText(Staggered, 40, options: new TextLayoutOptions { MaxWidth = 560, LineSpacing = 1.15f });
        DrawingGroup words = new TextBlockShape(staggeredLayout, 40, 440, Accent).CreatePieceGroup(TextUnit.Word);
        session.DrawShape(words);
        session.Animations.BindStagger(words.GetChildren(), new StaggerOptions
        {
            StartTime = 1.0, Interval = 0.18, Duration = 0.7, Easing = EasingKind.BackOut,
            FromOffset = new PointF(0, 40), FromScale = 0.4f,
        });

        // Lines as pieces, sliding in.
        TextLayoutResult lineLayout = session.LayoutText(Lines, 36, options: new TextLayoutOptions { LineSpacing = 1.25f });
        DrawingGroup lines = new TextBlockShape(lineLayout, 680, 440, Ink).CreatePieceGroup(TextUnit.Line);
        session.DrawShape(lines);
        session.Animations.BindStagger(lines.GetChildren(), new StaggerOptions
        {
            StartTime = 1.5, Interval = 0.6, Duration = 0.9, Easing = EasingKind.CubicOut, FromOffset = new PointF(-120, 0),
        });
    }

    /// <inheritdoc />
    public override string Name => "typewriter";

    /// <inheritdoc />
    public override string Title => "Typewriter and staggered text";

    /// <inheritdoc />
    public override string Description => "TextBlockShape RevealCount / RevealFraction / RevealSoftness, pieces per word and line, DrawingAnimations.BindStagger";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    //A thin bar after the last character shown, blinking twice a second
    private static DrawingShape Caret(TextLayoutResult layout, float count, double seconds)
    {
        if ((int)(seconds * 2) % 2 == 1) return null;
        IReadOnlyList<TextCluster> clusters = layout.Clusters;
        int shown = Math.Clamp((int)Math.Floor(count), 0, clusters.Count);
        RectangleF box;
        if (shown < clusters.Count)
        {
            box = clusters[shown].Bounds;
        }
        else
        {
            TextLineMetrics last = layout.Lines[layout.LineCount - 1];
            box = new RectangleF(last.Left + last.Width, last.Top, 0, last.Height);
        }
        return new RectangleShape(40 + box.X, 150 + box.Y + (box.Height * 0.12f), 3, box.Height * 0.8f, 1, Accent, isFilled: true);
    }
}
