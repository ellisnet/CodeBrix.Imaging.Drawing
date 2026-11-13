using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Clipping and masks. Top: stripes shown only inside a star - the mask of a group, so it turns with the group;
/// a card with a keyhole cut out of it (a layer mask "outside a path") over dots moving behind; a spotlight - a
/// layer whose "inside a circle" mask is replaced every frame as the light moves. Bottom: a layer matte (moving
/// stripes shown only where another layer's text is) and a dissolve made by an alpha mask from a noise plane whose
/// threshold rises.
/// </summary>
internal sealed class ClipMaskScene : DemoScene
{
    private const int NoiseWidth = 145;
    private const int NoiseHeight = 60;

    private readonly DrawingSession session;
    private readonly DrawingLayer spotlight;
    private readonly DrawingLayer dissolve;
    private readonly DrawingPlane noise;
    private readonly DrawingPlane dissolveMask = new DrawingPlane(NoiseWidth, NoiseHeight);
    private static readonly RectangleF DissolveArea = new RectangleF(680, 450, 540, 220);

    /// <summary>Builds the five demonstrations.</summary>
    public ClipMaskScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "a group's mask: inside a star (turns with it)", 230, 410, 14, Dim, TextAlign.Center);
        Label(notes, session, "a layer mask: outside a keyhole", 640, 410, 14, Dim, TextAlign.Center);
        Label(notes, session, "a mask replaced per frame (a spotlight)", 1050, 410, 14, Dim, TextAlign.Center);
        Label(notes, session, "a layer matte: shown where another layer's text is", 330, 690, 14, Dim, TextAlign.Center);
        Label(notes, session, "an alpha mask from a noise plane (dissolve)", 950, 690, 14, Dim, TextAlign.Center);

        // Inside a star: a group of stripes and dots, masked by a star in the group's coordinates.
        DrawingLayer stars = session.AddLayer("Star", Ink);
        session.ActiveLayer = stars;
        var striped = new DrawingGroup();
        for (int i = 0; i < 14; i++)
        {
            byte shade = (byte)(120 + (i * 9));
            striped.Add(new RectangleShape(60 + (i * 26), 90, 27, 340, 1, Color.FromRgb(shade, (byte)(60 + (i * 12)), (byte)(220 - (i * 10))), isFilled: true));
        }

        for (int i = 0; i < 5; i++) striped.Add(new CircleShape(150 + (i * 40), 180 + (i * 40), 18, 1, Color.FromRgba(255, 255, 255, 170), isFilled: true));
        striped.Mask = DrawingMask.Inside(PathGeometry.Star(new PointF(230, 260), 135, 58, 5));
        session.DrawShape(striped);
        session.Animations.Bind(striped, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(230, 260) } });
        session.Animations.Bind(striped, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 8, 144f } });

        // Outside a keyhole: dots drift behind a card with a hole cut through it.
        DrawingLayer behind = session.AddLayer("Behind", Accent);
        session.ActiveLayer = behind;
        var dots = new DrawingGroup();
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 20; column++)
            {
                dots.Add(new CircleShape(420 + (column * 36) + ((row % 2) * 18), 70 + (row * 48), 10, 1,
                    Color.FromRgb((byte)(200 + (row * 6)), (byte)(100 + (column * 7)), 80), isFilled: true));
            }
        }

        session.DrawShape(dots);
        session.Animations.Bind(dots, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 8, new PointF(-180, 40) } });
        behind.Mask = DrawingMask.Inside(PathGeometry.RoundedRectangle(new RectangleF(470, 120, 340, 270), 16));
        DrawingLayer card = session.AddLayer("Card", Color.FromRgb(60, 66, 90));
        session.ActiveLayer = card;
        session.DrawRectangle(470, 120, 340, 270, filled: true, cornerRadius: 16);
        session.DrawText("THROUGH THE KEYHOLE", 640, 368, 16, Color.FromRgb(200, 205, 225), TextAlign.Center, 0.12f);
        PathGeometry keyhole = PathGeometry.Combine(
            PathGeometry.Circle(new PointF(640, 210), 48),
            PathGeometry.FromPoints(new[] { new PointF(615, 230), new PointF(665, 230), new PointF(690, 330), new PointF(590, 330) }, true));
        card.Mask = DrawingMask.Outside(keyhole);
        card.Effects = DrawingEffects.DropShadow(new PointF(0, 0), 10, Color.FromRgba(0, 0, 0, 200));

        // The spotlight: a dim copy underneath, the bright copy shown inside a moving circle.
        DrawingLayer dimCopy = session.AddLayer("Dim copy", Color.FromRgba(236, 236, 244, 40));
        spotlight = session.AddLayer("Spotlight", Ink);
        foreach (DrawingLayer layer in new[] { dimCopy, spotlight })
        {
            session.ActiveLayer = layer;
            bool bright = layer == spotlight;
            session.DrawText("Spotlight", 1050, 210, 54, bright ? Accent : (Color?)null, TextAlign.Center);
            session.DrawText("a circular mask follows", 1050, 260, 22, null, TextAlign.Center);
            session.DrawText("the light from frame to frame", 1050, 292, 22, null, TextAlign.Center);
            session.DrawRectangle(900, 320, 300, 50, filled: !bright, thickness: 3, cornerRadius: 25,
                color: bright ? Color.FromRgb(120, 180, 255) : (Color?)null);
        }

        // A layer matte: moving stripes shown only where the matte layer's text is (the matte is not in the session).
        var matte = new DrawingLayer("Matte", Color.White);
        matte.AddShape(new TextShape("MATTE", 340, 625, 165, session.Fonts, align: TextAlign.Center));
        DrawingLayer stripes = session.AddLayer("Stripes", Ink);
        session.ActiveLayer = stripes;
        session.DrawShape(new RectangleShape(40, 440, 600, 230, new LinearGradientFill(new PointF(40, 0), new PointF(640, 0),
            Color.FromRgb(255, 120, 80), Color.FromRgb(120, 120, 255))));
        var moving = new DrawingGroup(Color.FromRgba(255, 255, 255, 110));
        for (int i = 0; i < 30; i++) moving.Add(new LineShape(-200 + (i * 40), 680, -60 + (i * 40), 430, 12));
        session.DrawShape(moving);
        session.Animations.Bind(moving, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 8, new PointF(160, 0) } });
        stripes.Mask = DrawingMask.FromLayer(matte);

        // A dissolve: an alpha mask from noise, its threshold rising and falling.
        noise = DrawingNoise.Fill(new DrawingPlane(NoiseWidth, NoiseHeight), DrawingNoiseKind.Gradient, 21, 0.08, octaves: 4);
        float low = Single.MaxValue, high = Single.MinValue;
        foreach (float value in noise.Data) { low = Math.Min(low, value); high = Math.Max(high, value); }
        noise.Add(-low).Multiply(1f / (high - low));
        dissolve = session.AddLayer("Dissolve", Ink);
        session.ActiveLayer = dissolve;
        session.DrawShape(new RectangleShape(DissolveArea.X, DissolveArea.Y, DissolveArea.Width, DissolveArea.Height,
            new RadialGradientFill(new PointF(950, 560), 300, Color.FromRgb(255, 230, 140), Color.FromRgb(200, 60, 120)), 18));
        session.DrawText("DISSOLVE", 950, 585, 72, Color.FromRgb(40, 20, 60), TextAlign.Center, 0.08f);
    }

    /// <inheritdoc />
    public override string Name => "clip-mask";

    /// <inheritdoc />
    public override string Title => "Clipping and masks";

    /// <inheritdoc />
    public override string Description => "DrawingMask.Inside/Outside (paths), FromLayer (mattes), FromPlane (alpha masks); on DrawingLayer.Mask and DrawingGroup.Mask";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        double angle = seconds * Math.PI * 2 / 4;
        var light = new PointF(1050 + (float)(120 * Math.Cos(angle)), 260 + (float)(70 * Math.Sin(angle * 2)));
        spotlight.Mask = DrawingMask.Inside(PathGeometry.Circle(light, 85));

        float sweep = (float)PingPong(seconds, 8);
        float threshold = (sweep * sweep * 1.25f) - 0.15f;
        float[] source = noise.Data, target = dissolveMask.Data;
        for (int i = 0; i < source.Length; i++) target[i] = Math.Clamp((source[i] - threshold) * 10f, 0f, 1f);
        dissolve.Mask = DrawingMask.FromPlane(dissolveMask, DissolveArea);

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
