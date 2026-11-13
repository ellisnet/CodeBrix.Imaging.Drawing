using System;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Pictures as elements. The pictures are painted here with pixel planes (a landscape, and a round emblem with a
/// transparent surround) and handed to the session with <see cref="ImageSource.FromPlaneImage" />. Top: the
/// landscape turning and scaling through its transform while its opacity pulses; a slow zoom and pan made by
/// rebuilding an <see cref="ImageShape" /> with a moving source rectangle; a tint bound to a colour track (day,
/// dusk, night). Bottom: a tiny crop drawn large with nearest and with bilinear sampling; and emblems over
/// transparency (laid on a checkerboard) in a group that turns, one appearing at 2 s (a time window).
/// </summary>
internal sealed class ImageScene : DemoScene
{
    private const int PictureWidth = 320;
    private const int PictureHeight = 200;
    private const int PanelWidth = 360;
    private const int PanelHeight = 250;
    private const int PanelX = 870;
    private const int PanelY = 420;

    private static readonly string[] HeartMap =
    {
        "..XXX...XXX..",
        ".XoowX.XoooX.",
        "XoowwoXoooooX",
        "XoowooooooooX",
        "XoooooooooooX",
        ".XoooooooooX.",
        "..XoooooooX..",
        "...XoooooX...",
        "....XoooX....",
        ".....XoX.....",
        "......X......",
    };

    private readonly ImageSource landscape;
    private readonly ImageSource emblem;
    private readonly ImageSource pixelArt;
    private readonly DrawingSession session;
    private readonly DrawingSession panel;
    private readonly byte[] board;
    private readonly byte[] panelPixels = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Paints the pictures and lays out the demonstrations.</summary>
    public ImageScene()
    {
        landscape = ImageSource.FromPlaneImage(PaintLandscape());
        emblem = ImageSource.FromPlaneImage(PaintEmblem(128));
        pixelArt = ImageSource.FromRgba(PaintPixelArt(), HeartMap[0].Length, HeartMap.Length);
        board = CreateCheckerboard(PanelWidth, PanelHeight).ToRgba();

        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "Transform + Opacity", 220, 380, 14, Dim, TextAlign.Center);
        Label(notes, session, "a moving source rectangle (zoom and pan)", 640, 380, 14, Dim, TextAlign.Center);
        Label(notes, session, "Tint bound to a ColorTrack", 1060, 380, 14, Dim, TextAlign.Center);
        Label(notes, session, "a 13 x 11 pixel picture: Nearest | Linear", 400, 690, 14, Dim, TextAlign.Center);
        Label(notes, session, "over transparency, in a turning group", PanelX + (PanelWidth / 2f), 690, 14, Dim, TextAlign.Center);

        DrawingLayer pictures = session.AddLayer("Pictures", Ink);
        session.ActiveLayer = pictures;

        ImageShape turning = session.DrawImage(landscape, new RectangleF(60, 140, PictureWidth, PictureHeight));
        var middle = new PointF(220, 240);
        session.Animations.Bind(turning, AnimatedProperty.Anchor, new PointTrack { { 0, middle } });
        session.Animations.Bind(turning, AnimatedProperty.Rotation, new FloatTrack { { 0, -12f }, { 4, 12f, EasingKind.SineInOut }, { 8, -12f, EasingKind.SineInOut } });
        session.Animations.Bind(turning, AnimatedProperty.Scale, new FloatTrack { { 0, 0.8f }, { 4, 0.95f, EasingKind.SineInOut }, { 8, 0.8f, EasingKind.SineInOut } });
        session.Animations.Bind(turning, AnimatedProperty.Opacity, new FloatTrack { { 0, 1f }, { 2, 0.35f, EasingKind.SineInOut }, { 4, 1f, EasingKind.SineInOut } }.Loop());

        // Zoom and pan: the source rectangle shrinks towards the sun and drifts.
        session.DrawShape(new AnimatedShape(t =>
        {
            double u = PingPong(t, 8);
            int w = (int)Math.Round(PictureWidth - (220 * u));
            int h = (int)Math.Round(w * PictureHeight / (double)PictureWidth);
            int x = (int)Math.Round((PictureWidth - w) * (0.15 + (0.6 * u)));
            int y = (int)Math.Round((PictureHeight - h) * 0.3);
            return new ImageShape(landscape, new RectangleF(480, 140, PictureWidth, PictureHeight), new Rectangle(x, y, w, h));
        }));

        ImageShape tinted = session.DrawImage(landscape, new RectangleF(900, 140, PictureWidth, PictureHeight));
        var tints = new ColorTrack
        {
            { 0, Color.White },
            { 2.5, Color.FromRgb(255, 170, 110), EasingKind.SineInOut },
            { 5, Color.FromRgb(70, 90, 170), EasingKind.SineInOut },
            { 8, Color.White, EasingKind.SineInOut },
        };
        session.Animations.Bind(tinted, AnimatedProperty.Tint, tints);

        session.DrawShape(new ImageShape(pixelArt, new RectangleF(90, 450, 221, 187), null, ImageSampling.Nearest));
        session.DrawShape(new ImageShape(pixelArt, new RectangleF(490, 450, 221, 187), null, ImageSampling.Linear));

        // Emblems over transparency, in a session of their own laid on a checkerboard.
        panel = CreateSession(PanelWidth, PanelHeight, Color.Transparent);
        panel.AddLayer("Emblems", Ink);
        var group = new DrawingGroup();
        group.Add(new ImageShape(emblem, new RectangleF(60, 75, 100, 100)));
        group.Add(new ImageShape(emblem, new RectangleF(200, 75, 100, 100), tint: Color.FromRgba(150, 220, 255, 200))).StartTime = 2;
        panel.DrawShape(group);
        panel.Animations.Bind(group, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(PanelWidth / 2f, PanelHeight / 2f) } });
        panel.Animations.Bind(group, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 8, 360f } });
    }

    /// <inheritdoc />
    public override string Name => "images";

    /// <inheritdoc />
    public override string Title => "Pictures as elements";

    /// <inheritdoc />
    public override string Description => "ImageSource (from planes, RGBA, encoded bytes), ImageShape: transform, opacity, source rectangle, Tint, Nearest/Linear";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
        panel.Time = seconds;
        panel.ExportPixels(panelPixels, new Size(PanelWidth, PanelHeight), includeBackground: false);
        FrameCompositor.Copy(board, PanelWidth, PanelHeight, rgba, PanelX, PanelY);
        FrameCompositor.Over(panelPixels, PanelWidth, PanelHeight, rgba, PanelX, PanelY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        session.Dispose();
        panel.Dispose();
        landscape.Dispose();
        emblem.Dispose();
        pixelArt.Dispose();
    }

    //A small landscape: a sky gradient, a sun, two rows of noisy hills
    private static DrawingPlaneImage PaintLandscape()
    {
        var image = new DrawingPlaneImage(PictureWidth, PictureHeight);
        image.FillParallel((x, y) =>
        {
            float v = y / (float)PictureHeight;
            var sky = new Vector4(0.30f + (0.55f * v), 0.45f + (0.35f * v), 0.85f - (0.25f * v), 1f);
            float dx = x - 210f, dy = y - 70f;
            float sun = Math.Clamp(1.2f - (MathF.Sqrt((dx * dx) + (dy * dy)) / 26f), 0f, 1f);
            Vector4 color = Vector4.Lerp(sky, new Vector4(1f, 0.92f, 0.6f, 1f), sun);
            double far = 120 + (22 * DrawingNoise.Fractal(DrawingNoiseKind.Gradient, x * 0.012, 0.5, 11, 3));
            double near = 150 + (26 * DrawingNoise.Fractal(DrawingNoiseKind.Gradient, x * 0.02, 3.5, 12, 3));
            color = Vector4.Lerp(color, new Vector4(0.30f, 0.42f, 0.50f, 1f), (float)Math.Clamp(y + 0.5 - far, 0, 1));
            color = Vector4.Lerp(color, new Vector4(0.16f + (0.0004f * x), 0.34f, 0.20f, 1f), (float)Math.Clamp(y + 0.5 - near, 0, 1));
            return color;
        });
        return image;
    }

    //A tiny pixel-art heart from a character map: outline, fill, highlight, transparent surround
    private static byte[] PaintPixelArt()
    {
        int width = HeartMap[0].Length, height = HeartMap.Length;
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (byte r, byte g, byte b, byte a) = HeartMap[y][x] switch
                {
                    'X' => ((byte)60, (byte)20, (byte)40, (byte)255),
                    'o' => ((byte)230, (byte)50, (byte)80, (byte)255),
                    'w' => ((byte)255, (byte)230, (byte)235, (byte)255),
                    _ => ((byte)0, (byte)0, (byte)0, (byte)0),
                };
                int o = ((y * width) + x) * 4;
                rgba[o] = r;
                rgba[o + 1] = g;
                rgba[o + 2] = b;
                rgba[o + 3] = a;
            }
        }

        return rgba;
    }

    //A round emblem: rings of colour, a soft edge fading to transparent
    private static DrawingPlaneImage PaintEmblem(int size)
    {
        var image = new DrawingPlaneImage(size, size);
        float c = size / 2f;
        image.Fill((x, y) =>
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float r = MathF.Sqrt((dx * dx) + (dy * dy)) / c;
            float alpha = Math.Clamp((1f - r) * 12f, 0f, 1f);
            float ring = (MathF.Floor(r * 5f) % 2f) == 0f ? 1f : 0.55f;
            return new Vector4(1f * ring, (0.55f + (0.4f * r)) * ring, 0.2f + (0.3f * (1f - r)), alpha);
        });
        return image;
    }
}
