using System;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;
using CodeBrix.Imaging.PixelFormats;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Sprites from sprite sheets that are GENERATED here, in code (no image files): a bouncing-ball sheet of eight
/// 96 x 96 cells drawn with a small drawing session, and a pixel-art flame of twelve 16 x 24 cells painted with
/// noise in pixel planes. Top: the ball sheet itself, its current cell outlined. Middle: the same sheet played by
/// time as a loop, a ping-pong and a "hold" (it waits, plays once and stops), and one stepped by hand through
/// its FrameIndex. Right: the flame scaled up six times with nearest sampling, with bilinear sampling, and tinted.
/// </summary>
internal sealed class SpriteScene : DemoScene
{
    private const int Cell = 96;
    private const int BallFrames = 8;
    private const int FlameWidth = 16;
    private const int FlameHeight = 24;
    private const int FlameFrames = 12;

    private readonly DrawingSession session;
    private readonly ImageSource ballImage;
    private readonly SpriteSheet balls;
    private readonly ImageSource flameImage;
    private readonly SpriteSheet flames;
    private readonly SpriteShape looping;
    private readonly DrawingLayer now;

    /// <summary>Generates the two sheets and lays out the sprites.</summary>
    public SpriteScene()
    {
        ballImage = ImageSource.FromRgba(BuildBallSheet(), Cell * BallFrames, Cell);
        balls = SpriteSheet.FromGrid(ballImage, BallFrames, 1);
        flameImage = ImageSource.FromRgba(BuildFlameSheet(), FlameWidth * FlameFrames, FlameHeight);
        flames = SpriteSheet.FromGrid(flameImage, FlameFrames, 1);

        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "the generated sheet (8 cells), the loop's current cell outlined", 40, 130, 14, Dim);
        Label(notes, session, "Loop, 12 fps", 130, 548, 14, Dim, TextAlign.Center);
        Label(notes, session, "PingPong, 12 fps", 345, 548, 14, Dim, TextAlign.Center);
        Label(notes, session, "Hold (starts at 2 s)", 560, 548, 14, Dim, TextAlign.Center);
        Label(notes, session, "FrameIndex from a track", 775, 548, 14, Dim, TextAlign.Center);
        Label(notes, session, "flame from noise planes:", 1086, 548, 14, Dim, TextAlign.Center);
        Label(notes, session, "nearest | bilinear | tinted", 1086, 568, 14, Dim, TextAlign.Center);
        Label(notes, session, "SpriteSheet.FromGrid cuts each cell into its own picture, so cells never bleed into each other", 40, 660, 14, Dim);

        DrawingLayer sprites = session.AddLayer("Sprites", Ink);
        session.ActiveLayer = sprites;
        session.DrawRectangle(40, 146, Cell * BallFrames, Cell, filled: true, color: Color.FromRgb(30, 33, 46));
        session.DrawImage(ballImage, new RectangleF(40, 146, Cell * BallFrames, Cell));

        var floor = Color.FromRgb(30, 33, 46);
        for (int i = 0; i < 4; i++) session.DrawRectangle(30 + (i * 215), 320, 200, 200, filled: true, color: floor, cornerRadius: 12);

        looping = session.DrawSprite(balls, new RectangleF(30, 320, 200, 200), 12, SpritePlayback.Loop);
        session.DrawSprite(balls, new RectangleF(245, 320, 200, 200), 12, SpritePlayback.PingPong);
        session.DrawShape(new SpriteShape(balls, new RectangleF(460, 320, 200, 200), 6, SpritePlayback.Hold) { PlaybackStartTime = 2 });
        SpriteShape stepped = session.DrawSprite(balls, new RectangleF(675, 320, 200, 200));
        var steps = new FloatTrack { { 0, 0f }, { 4, 7.99f, EasingKind.SineInOut }, { 8, 0f, EasingKind.SineInOut } };
        session.Animations.Bind(stepped, AnimatedProperty.FrameIndex, steps);

        session.DrawShape(new SpriteShape(flames, new RectangleF(915, 340, FlameWidth * 7, FlameHeight * 7), 10, SpritePlayback.Loop, ImageSampling.Nearest));
        session.DrawShape(new SpriteShape(flames, new RectangleF(1030, 340, FlameWidth * 7, FlameHeight * 7), 10, SpritePlayback.Loop, ImageSampling.Linear));
        var tinted = new SpriteShape(flames, new RectangleF(1145, 340, FlameWidth * 7, FlameHeight * 7), 10, SpritePlayback.Loop, ImageSampling.Nearest)
        {
            Tint = Color.FromRgb(255, 120, 210),
            FrameIndex = 5,
        };
        session.DrawShape(tinted);

        now = session.AddLayer("Now", Accent);
    }

    /// <inheritdoc />
    public override string Name => "sprites";

    /// <inheritdoc />
    public override string Title => "Sprites from generated sheets";

    /// <inheritdoc />
    public override string Description => "ImageSource.FromRgba, SpriteSheet.FromGrid, SpriteShape (fps, Loop/PingPong/Hold, FrameIndex, PlaybackStartTime, Tint, sampling)";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        now.Clear();
        int cell = looping.GetFrameAt(seconds);
        now.AddShape(new RectangleShape(40 + (cell * Cell), 146, Cell, Cell, 3, Accent));
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        session.Dispose();
        balls.Dispose();
        flames.Dispose();
        ballImage.Dispose();
        flameImage.Dispose();
    }

    //Eight cells of a ball bouncing on a floor (squashed at the bottom), drawn over transparency
    private static byte[] BuildBallSheet()
    {
        var sheet = new byte[Cell * BallFrames * Cell * 4];
        var frame = new byte[Cell * Cell * 4];
        for (int i = 0; i < BallFrames; i++)
        {
            double phase = i / (double)BallFrames;
            float height = (float)Math.Abs(Math.Sin(Math.PI * phase));
            float squash = 1f + (0.35f * (float)Math.Pow(1 - height, 6));
            float radius = 22f;
            float cy = 82f - (radius / squash) - (34f * height);

            using var cellSession = CreateSession(Cell, Cell, Color.Transparent);
            cellSession.AddLayer("Ball", Color.White);
            cellSession.DrawEllipse(48, 86, 24 * (1.2f - (0.5f * height)), 5, filled: true, color: Color.FromRgba(0, 0, 0, (byte)(150 - (90 * height))));
            cellSession.DrawShape(new EllipseShape(48, cy, radius * squash, radius / squash,
                new RadialGradientFill(new PointF(40, cy - 8), 30, Color.FromRgb(255, 236, 170), Color.FromRgb(240, 110, 60))));
            cellSession.ExportPixels(frame, new Size(Cell, Cell), includeBackground: false);
            for (int y = 0; y < Cell; y++)
            {
                Array.Copy(frame, y * Cell * 4, sheet, ((y * Cell * BallFrames) + (i * Cell)) * 4, Cell * 4);
            }
        }

        return sheet;
    }

    //Twelve cells of a flickering flame: fractal noise rising with time, shaped into a flame and coloured
    private static byte[] BuildFlameSheet()
    {
        var ramp = new ColorRamp(Color.FromRgba(80, 0, 0, 0), Color.FromRgb(200, 30, 10), Color.FromRgb(255, 140, 20), Color.FromRgb(255, 230, 120), Color.White);
        var sheet = new DrawingPlaneImage(FlameWidth * FlameFrames, FlameHeight);
        sheet.Fill((x, y) =>
        {
            int frameIndex = x / FlameWidth;
            float fx = (x % FlameWidth) + 0.5f, fy = y + 0.5f;
            double t = frameIndex / (double)FlameFrames;
            double n = DrawingNoise.Fractal(DrawingNoiseKind.Gradient, fx * 0.28, (fy * 0.22) + (t * 6.0), 7, 3);
            double across = Math.Abs((fx - (FlameWidth / 2.0)) / (FlameWidth / 2.0));
            double up = 1 - (fy / FlameHeight);
            double heat = (1.1 - (across * (0.6 + up)) - up) + (n * 0.5);
            Rgba32 c = ramp.At(Math.Clamp(heat, 0, 1)).ToRgba32();
            return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        });
        return sheet.ToRgba();
    }
}
