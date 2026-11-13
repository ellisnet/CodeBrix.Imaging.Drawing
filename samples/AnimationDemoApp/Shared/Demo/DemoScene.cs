using System;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo;

/// <summary>
/// One named scene of the demo: a few seconds of animation that shows one group of library features. A scene is
/// written against the drawing-session API and CodeBrix.Imaging value types only - never against SkiaSharp or the
/// NoSkia engine types - so the same code draws through either package. To add a scene, derive from this class
/// and add it to <see cref="SceneCatalog" />.
/// </summary>
internal abstract class DemoScene : IDisposable
{
    /// <summary>The frame's background.</summary>
    protected static readonly Color Backdrop = Color.FromRgb(18, 20, 30);

    /// <summary>Bright text and lines.</summary>
    protected static readonly Color Ink = Color.FromRgb(236, 236, 244);

    /// <summary>Quiet text and lines.</summary>
    protected static readonly Color Dim = Color.FromRgb(132, 138, 164);

    /// <summary>The accent used for moving parts.</summary>
    protected static readonly Color Accent = Color.FromRgb(255, 176, 72);

    /// <summary>The top of the area below the header, frame pixels.</summary>
    protected const float ContentTop = 96;

    /// <summary>The scene's short name (for --scene and file names).</summary>
    public abstract string Name { get; }

    /// <summary>The scene's title, shown in its header.</summary>
    public abstract string Title { get; }

    /// <summary>A one-line description, shown under the title.</summary>
    public abstract string Description { get; }

    /// <summary>How long the scene plays, seconds.</summary>
    public virtual double DurationSeconds => 8;

    /// <summary>Which drawing package can draw the scene.</summary>
    public virtual SceneAvailability Availability => SceneAvailability.BothPackages;

    /// <summary>Draws the scene at a time.</summary>
    /// <param name="seconds">The time within the scene, 0..<see cref="DurationSeconds" />.</param>
    /// <param name="rgba">Receives the frame: <see cref="DemoFrame.Width" /> x <see cref="DemoFrame.Height" /> straight-alpha RGBA pixels.</param>
    public abstract void Render(double seconds, Span<byte> rgba);

    /// <summary>Releases the scene's sessions.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Creates a session for part of a frame, sharing the demo's fonts, with opaque layers.</summary>
    /// <param name="width">The calibrated width (the frame pixels it will be exported at, normally).</param>
    /// <param name="height">The calibrated height.</param>
    /// <param name="background">The background fill.</param>
    /// <returns>The session.</returns>
    protected static DrawingSession CreateSession(int width, int height, Color background)
        => new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(width, height),
            LayerOpacity = 255,
            ActiveStrokeOpacity = 255,
            BackgroundFillColor = background,
            Fonts = DemoFonts.Registry,
        });

    /// <summary>Creates a whole-frame session with the scene's header drawn on a first "Header" layer.</summary>
    /// <returns>The session; its active layer is the header layer, so add a layer of your own next.</returns>
    protected DrawingSession CreateFrameSession()
    {
        DrawingSession session = CreateSession(DemoFrame.Width, DemoFrame.Height, Backdrop);
        AddHeader(session, false);
        return session;
    }

    /// <summary>Adds the scene's header (title, description and rule) as a new "Header" layer.</summary>
    /// <param name="session">A whole-frame session.</param>
    /// <param name="opaqueBand">
    /// <c>true</c> to put an opaque backdrop band behind the header, for a header added ABOVE layers that may
    /// draw into the header area.
    /// </param>
    /// <returns>The header layer.</returns>
    protected DrawingLayer AddHeader(DrawingSession session, bool opaqueBand)
    {
        DrawingLayer header = session.AddLayer("Header", Ink);
        if (opaqueBand) header.AddShape(new RectangleShape(0, 0, DemoFrame.Width, ContentTop, 1, Backdrop, isFilled: true));
        header.AddShape(new TextShape(Title, 32, 46, 30, session.Fonts, Ink));
        header.AddShape(new TextShape(Description, 33, 74, 15, session.Fonts, Dim));
        header.AddShape(new LineShape(32, 86, DemoFrame.Width - 32, 86, 1, Color.FromRgba(132, 138, 164, 90)));
        return header;
    }

    /// <summary>Adds a single-line label.</summary>
    /// <param name="layer">The layer to add it to.</param>
    /// <param name="session">The session whose fonts it uses.</param>
    /// <param name="text">The text.</param>
    /// <param name="x">The anchor x.</param>
    /// <param name="y">The baseline.</param>
    /// <param name="size">The em size.</param>
    /// <param name="color">The color.</param>
    /// <param name="align">The alignment about x.</param>
    protected static void Label(DrawingLayer layer, DrawingSession session, string text, float x, float y, float size, Color color,
        TextAlign align = TextAlign.Left)
        => layer.AddShape(new TextShape(text, x, y, size, session.Fonts, color, align));

    /// <summary>Places a float image into the frame: converted to straight-alpha RGBA, then copied or blended.</summary>
    /// <param name="image">The image to place.</param>
    /// <param name="scratch">A buffer of at least the image's width x height x 4 bytes (reused frame to frame).</param>
    /// <param name="rgba">The frame buffer.</param>
    /// <param name="x">Where the image's left edge goes.</param>
    /// <param name="y">Where the image's top edge goes.</param>
    /// <param name="blend"><c>true</c> to draw it over the frame by its alpha; <c>false</c> (the default) to copy it.</param>
    protected static void PlaceImage(DrawingPlaneImage image, byte[] scratch, Span<byte> rgba, int x, int y, bool blend = false)
    {
        image.ToRgba(scratch);
        if (blend) FrameCompositor.Over(scratch, image.Width, image.Height, rgba, x, y);
        else FrameCompositor.Copy(scratch, image.Width, image.Height, rgba, x, y);
    }

    /// <summary>Creates an opaque grey checkerboard - the usual way to show what is transparent.</summary>
    /// <param name="width">The width, pixels.</param>
    /// <param name="height">The height, pixels.</param>
    /// <param name="square">The size of one square, pixels.</param>
    /// <returns>The checkerboard.</returns>
    protected static DrawingPlaneImage CreateCheckerboard(int width, int height, int square = 16)
    {
        var board = new DrawingPlaneImage(width, height);
        board.FillParallel((px, py) => ((px / square) + (py / square)) % 2 == 0
            ? new Vector4(0.27f, 0.28f, 0.33f, 1f)
            : new Vector4(0.43f, 0.44f, 0.49f, 1f));
        return board;
    }

    /// <summary>A value that goes from 0 up to 1 and back down to 0 every period, eased at both ends.</summary>
    /// <param name="seconds">The time.</param>
    /// <param name="period">The length of one up-and-down cycle, seconds.</param>
    /// <returns>The eased value, 0..1.</returns>
    protected static double PingPong(double seconds, double period)
    {
        double phase = (seconds % period) / period;
        return Easing.SineInOut(phase < 0.5 ? 2 * phase : 2 - (2 * phase));
    }

    /// <summary>Exports a whole-frame session into the frame buffer.</summary>
    /// <param name="session">The session.</param>
    /// <param name="rgba">The frame buffer.</param>
    protected static void ExportFrame(DrawingSession session, Span<byte> rgba)
        => session.ExportPixels(rgba, new Size(DemoFrame.Width, DemoFrame.Height));
}
