using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// The whole-frame fade: one composition faded in and out with <see cref="DrawingSession.Opacity" /> (eased),
/// shown twice - exported over its opaque black background (a fade from and to black), and exported without its
/// background so the fade lives in the alpha channel, laid over a checkerboard. Its caption layer also blinks on
/// its own through <see cref="DrawingLayer.Opacity" />.
/// </summary>
internal sealed class FadeScene : DemoScene
{
    private const int BoxWidth = 560;
    private const int BoxHeight = 500;
    private const int LeftX = 40;
    private const int RightX = 680;
    private const int BoxY = 150;

    private readonly DrawingSession frame;
    private readonly DrawingLayer notes;
    private readonly DrawingSession composition;
    private readonly DrawingLayer caption;
    private readonly byte[] box = new byte[BoxWidth * BoxHeight * 4];

    /// <summary>Builds the composition and the frame around it.</summary>
    public FadeScene()
    {
        frame = CreateFrameSession();
        DrawingLayer backdrop = frame.AddLayer("Backdrop", Dim);
        frame.ActiveLayer = backdrop;
        Label(backdrop, frame, "ExportPixels, background included: fades from and to black", LeftX, BoxY - 14, 13, Dim);
        Label(backdrop, frame, "ExportPixels, includeBackground: false - the fade is in the alpha", RightX, BoxY - 14, 13, Dim);
        backdrop.AddShape(new RectangleShape(RightX, BoxY, BoxWidth, BoxHeight, 1, Color.FromRgb(70, 72, 84), isFilled: true));
        for (int row = 0; row < BoxHeight; row += 20)
        {
            for (int col = (row / 20 % 2) * 20; col < BoxWidth; col += 40)
            {
                backdrop.AddShape(new RectangleShape(RightX + col, BoxY + row, 20, 20, 1, Color.FromRgb(110, 112, 124), isFilled: true));
            }
        }

        notes = frame.AddLayer("Notes", Ink);

        composition = CreateSession(BoxWidth, BoxHeight, Color.Black);
        composition.AddLayer("Sky", Color.Black);
        composition.DrawShape(new RectangleShape(0, 0, BoxWidth, BoxHeight, new LinearGradientFill(new PointF(0, 0), new PointF(0, BoxHeight), new[]
        {
            new ColorStop(0f, Color.FromRgb(30, 40, 110)), new ColorStop(0.7f, Color.FromRgb(200, 90, 120)), new ColorStop(1f, Color.FromRgb(255, 190, 120)),
        })));
        composition.ActiveLayer = composition.AddLayer("Shapes", Color.White);
        composition.DrawShape(new CircleShape(280, 300, 120, new RadialGradientFill(new PointF(250, 270), 150, Color.FromRgb(255, 250, 220), Color.FromRgb(255, 140, 60))));
        composition.DrawCircle(120, 120, 40, filled: true, color: Color.FromRgb(240, 240, 255));
        composition.DrawRectangle(380, 380, 140, 80, filled: true, color: Color.FromRgb(40, 30, 60), cornerRadius: 12);
        caption = composition.AddLayer("Caption", Color.White);
        composition.ActiveLayer = caption;
        composition.DrawText("Fade in, hold, fade out", BoxWidth / 2f, 470, 30, Color.White, TextAlign.Center);
    }

    /// <inheritdoc />
    public override string Name => "fade";

    /// <inheritdoc />
    public override string Title => "Whole-frame fade";

    /// <inheritdoc />
    public override string Description => "DrawingSession.Opacity fades every layer as one group; DrawingLayer.Opacity fades one layer; the exported alpha carries it";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        double opacity = seconds < 2 ? Easing.CubicOut(seconds / 2)
            : seconds < 5 ? 1
            : Easing.CubicIn(1 - ((seconds - 5) / 2));
        composition.Opacity = (float)opacity;
        caption.Opacity = 1f - (0.75f * (float)Easing.Steps((seconds % 1.2) / 1.2, 2)); // a two-step blink

        notes.Clear();
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "session.Opacity = {0:0.00}", opacity), LeftX, BoxY + BoxHeight + 34, 16, Accent);
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "caption layer Opacity = {0:0.00}", caption.Opacity), RightX, BoxY + BoxHeight + 34, 16, Accent);
        ExportFrame(frame, rgba);

        composition.ExportPixels(box, new Size(BoxWidth, BoxHeight), includeBackground: true);
        FrameCompositor.Copy(box, BoxWidth, BoxHeight, rgba, LeftX, BoxY);
        composition.ExportPixels(box, new Size(BoxWidth, BoxHeight), includeBackground: false);
        FrameCompositor.Over(box, BoxWidth, BoxHeight, rgba, RightX, BoxY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        composition.Dispose();
    }
}
