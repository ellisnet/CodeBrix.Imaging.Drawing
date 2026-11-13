using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Frame-to-frame transitions on 8-bit RGBA frames: two small frames drawn once with drawing sessions, mixed by
/// <see cref="DrawingTransition.Apply(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, int, int, float)" /> in
/// six ways at once while the fraction eases from 0 to 1 and back. Fraction 0 is the outgoing frame and 1 the
/// incoming frame exactly; slides and pushes move whole pixels.
/// </summary>
internal sealed class TransitionScene : DemoScene
{
    private const int PanelWidth = 384;
    private const int PanelHeight = 216;
    private static readonly int[] PanelX = { 32, 448, 864 };
    private static readonly int[] PanelY = { 116, 400 };

    private static readonly (string Caption, DrawingTransition Transition)[] Transitions =
    {
        ("Dissolve()", DrawingTransition.Dissolve()),
        ("Wipe(Right, softness: 60)", DrawingTransition.Wipe(DrawingTransitionDirection.Right, 60f)),
        ("Slide(Left)", DrawingTransition.Slide(DrawingTransitionDirection.Left)),
        ("Push(Up)", DrawingTransition.Push(DrawingTransitionDirection.Up)),
        ("FadeThroughColor(Black)", DrawingTransition.FadeThroughColor(Color.Black)),
        ("Wipe(Down) - hard edge", DrawingTransition.Wipe(DrawingTransitionDirection.Down)),
    };

    private readonly DrawingSession frame;
    private readonly DrawingLayer notes;
    private readonly byte[] outgoing;
    private readonly byte[] incoming;
    private readonly byte[] mixed = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Draws the two frames and the frame's captions.</summary>
    public TransitionScene()
    {
        outgoing = DrawCard("A", Color.FromRgb(240, 120, 60), Color.FromRgb(120, 30, 70), Color.FromRgb(255, 230, 160));
        incoming = DrawCard("B", Color.FromRgb(40, 150, 200), Color.FromRgb(20, 40, 100), Color.FromRgb(200, 255, 220));

        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Ink);
        frame.ActiveLayer = captions;
        for (int i = 0; i < Transitions.Length; i++)
        {
            Label(captions, frame, "DrawingTransition." + Transitions[i].Caption, PanelX[i % 3], PanelY[i / 3] + PanelHeight + 22, 14, Ink);
        }

        notes = frame.AddLayer("Notes", Accent);
    }

    /// <inheritdoc />
    public override string Name => "transitions";

    /// <inheritdoc />
    public override string Title => "Transitions";

    /// <inheritdoc />
    public override string Description => "Dissolve, wipe, slide, push and fade through a colour between two frames - premultiplied, exact at 0 and 1";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        float fraction = (float)Math.Clamp((PingPong(seconds, 4) * 1.25) - 0.125, 0, 1);  // short holds at 0 and 1

        notes.Clear();
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "fraction = {0:0.000}", fraction), DemoFrame.Width - 32, 74, 16, Accent, TextAlign.Right);
        ExportFrame(frame, rgba);

        for (int i = 0; i < Transitions.Length; i++)
        {
            Transitions[i].Transition.Apply(outgoing, incoming, mixed, PanelWidth, PanelHeight, fraction);
            FrameCompositor.Copy(mixed, PanelWidth, PanelHeight, rgba, PanelX[i % 3], PanelY[i / 3]);
        }
    }

    //One opaque "card": a gradient, a ring and a big letter
    private static byte[] DrawCard(string letter, Color top, Color bottom, Color ink)
    {
        using DrawingSession card = CreateSession(PanelWidth, PanelHeight, bottom);
        card.AddLayer("Card", ink);
        card.DrawShape(new RectangleShape(0, 0, PanelWidth, PanelHeight, new LinearGradientFill(new PointF(0, 0), new PointF(0, PanelHeight), top, bottom)));
        card.DrawCircle(110, 108, 64, 10, ink);
        card.DrawText(letter, 270, 150, 120, ink, TextAlign.Center);
        return card.ExportPixels(new Size(PanelWidth, PanelHeight));
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();
}
