using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// Paints the underpainting: the scene itself, before any brushwork, at half the frame's size and in open-ended
/// floating-point colour (lights add up past 1 and are brought back by the painter's tone curve). The scene is
/// the night river with the far temple and shore, the foreground quay with its two torches, the Boat of Heaven
/// crossing toward the quay, the Octaquetra blossoming in the sky, the temple's light, and the dawn.
/// <para>
/// The scene and its lights are CodeBrix.Imaging.Drawing <see cref="DrawingPlaneImage" /> values filled per
/// pixel from formulas. Shapes (the temple tower, the shore, the boat, the torch stands, the sun's disc) are
/// drawn by a drawing session and exported as hard-edged masks at the frame's size, then averaged down to the
/// underpainting's size - so every edge is computed by the library's one pixel rule, the same in both packages.
/// </para>
/// </summary>
internal sealed class SceneRenderer : IDisposable
{
    private const int HW = Timeline.HalfWidth;
    private const int HH = Timeline.HalfHeight;
    private const double YH = Timeline.Horizon;
    private const double H = Timeline.Height;

    /// <summary>The Octaquetra's centre, frame coordinates.</summary>
    public const double StarX = 655.0;

    /// <summary>The Octaquetra's centre, frame coordinates.</summary>
    public const double StarY = 390.0;

    /// <summary>The Octaquetra's full radius.</summary>
    public const double StarRadius = 215.0;

    /// <summary>How deep the valleys between the star's eight points are, as a fraction of its radius.</summary>
    public const double StarValley = 0.426;

    // The five torches: base x, base y, flame y, size (1 = the tall ones on the quay; the rest are far off).
    private static readonly (double X, double Base, double Flame, double Size)[] Torches =
    {
        (115, 1900, 1150, 1.0), (975, 1900, 1185, 1.0), (118, YH - 52, YH - 66, 0.07), (205, YH - 82, YH - 96, 0.07), (300, YH - 30, YH - 42, 0.06),
    };

    // The rows of the underpainting above the horizon.
    private static readonly int SkyRows = (int)Math.Ceiling((YH - 1) / 2);

    private readonly DrawingSession maskSession;
    private readonly DrawingLayer maskLayer;
    private readonly DrawingPlane frameMask = new DrawingPlane(Timeline.Width, Timeline.Height);
    private readonly DrawingPlane cloud;      // slow, large-scale unevenness
    private readonly DrawingPlane ripple;     // finer unevenness
    private readonly DrawingPlane tower;      // the stepped temple tower
    private readonly DrawingPlane shore;      // the far shore
    private readonly DrawingPlane doorway;    // the temple's doorway
    private readonly DrawingPlane[] stands = new DrawingPlane[2];
    private readonly DrawingPlane[] bowls = new DrawingPlane[2];
    private readonly double[] quay = new double[HW];

    /// <summary>Creates the renderer and everything about the scene that never changes.</summary>
    public SceneRenderer()
    {
        maskSession = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(Timeline.Width, Timeline.Height) });
        maskLayer = maskSession.AddLayer("Mask", Color.White);

        DrawingRandom random = new DrawingRandom(7);
        cloud = Noise(random, 18, 8);
        ripple = Noise(random, 5, 3);
        tower = Mask(TowerOutline());
        shore = Mask(ShoreOutline());
        doorway = Mask(Points((196, YH - 84), (214, YH - 84), (214, YH - 108), (205, YH - 114), (196, YH - 108)));
        for (int i = 0; i < 2; i++)
        {
            (double x, double foot, double flame, _) = Torches[i];
            stands[i] = Mask(Points((x - 11, foot), (x + 11, foot), (x + 7, flame + 30), (x - 7, flame + 30)));
            bowls[i] = Mask(Points((x - 42, flame), (x + 42, flame), (x + 24, flame + 52), (x - 24, flame + 52)));
        }

        for (int x = 0; x < HW; x++) quay[x] = Timeline.QuayY((2 * x) + 1);
    }

    /// <summary>True where a background star would be hidden behind the temple tower or the far shore.</summary>
    /// <param name="frameX">The frame x.</param>
    /// <param name="frameY">The frame y.</param>
    /// <returns>Whether the point is covered.</returns>
    public bool IsSilhouette(double frameX, double frameY)
    {
        int col = (int)(frameX / 2), row = (int)(frameY / 2);
        return shore[col, row] > 0 || tower[col, row] > 0;
    }

    /// <summary>Paints the scene at a time.</summary>
    /// <param name="t">The animation time, seconds.</param>
    /// <returns>The scene (lights included), the lights alone - both opaque, half the frame's size - and the
    /// fade-out factor already applied to them (1 normally, 0 once faded to black).</returns>
    public (DrawingPlaneImage Scene, DrawingPlaneImage Light, double Fade) Render(double t)
    {
        double dawn = Timeline.Dawn.Progress(t);
        double exposure = (Timeline.FadeIn.Progress(t) * 0.85) + 0.15;
        DrawingPlaneImage scene = new DrawingPlaneImage(HW, HH);
        DrawingPlaneImage glow = new DrawingPlaneImage(HW, HH);
        glow.A.Fill(1f);
        List<(double X, double Y, double Strength, double Radius)> lights = new List<(double, double, double, double)>();

        // Torches: two tall ones on the foreground quay, three small ones at the far temple.
        double[] torch = new double[Torches.Length];
        for (int i = 0; i < Torches.Length; i++)
        {
            double lit = Timeline.TorchesLit[i];
            double flare = t > lit ? 0.6 * Math.Exp(-Math.Max(0, t - lit - 0.4) * 2.5) : 0;
            torch[i] = Timeline.TorchCaught(i, t) * (1 + flare) * Flicker(t, i * 1.7);
            (double x, _, double flame, double size) = Torches[i];
            if (torch[i] > 0) lights.Add((x, flame - (60 * size), (size > 0.5 ? 1.2 : 0.5) * torch[i], (760 * size) + 25));
        }

        BoatState boat = Boat.At(t);
        double sc = boat.Scale;
        double vis = boat.Visibility;
        (double X, double Y, double Height, double Strength)[] boatFlames = new (double, double, double, double)[Boat.Flames.Length];
        for (int j = 0; j < boatFlames.Length; j++)
        {
            (double fx, double fy) = boat.ToFrame(Boat.Flames[j]);
            double strength = vis * Flicker(t, 4 + (j * 2.1));
            boatFlames[j] = (fx, fy, 90 * sc, strength);
            lights.Add((fx, fy - (40 * sc), 1.25 * strength, (620 * sc) + 15));
        }

        (double glowX, double glowY) = boat.ToFrame(Boat.Glow);
        lights.Add((glowX, glowY, 0.45 * vis, (260 * sc) + 8));
        double doors = Timeline.Doors.Progress(t);
        double star = Timeline.StarGrows.Progress(t);

        // Broken reflections on the water: the boat's torches and shrine, the far torches, the temple door, the
        // rising sun and the star.
        DrawingPlane reflection = new DrawingPlane(HW, HH);
        for (int j = 0; j < boatFlames.Length; j++)
        {
            Reflect(reflection, t, boatFlames[j].X, boat.Waterline + (4 * sc), (560 * sc) + 30, (16 * sc) + 3, 0.95 * boatFlames[j].Strength, j * 2.0);
        }

        Reflect(reflection, t, glowX, boat.Waterline + (4 * sc), (300 * sc) + 10, (20 * sc) + 2, 0.35 * vis, 5.0);
        for (int i = 2; i < Torches.Length; i++) Reflect(reflection, t, Torches[i].X, YH + 3, 160, 4, 0.5 * torch[i], Torches[i].X);
        Reflect(reflection, t, 205, YH + 3, 600, 9, 0.5 * doors, 21.0);
        for (int k = 0; k < 7; k++)
        {
            Reflect(reflection, t, 780 + (30 * Math.Sin(k * 1.7)), YH + 2, 1100, 46 - (k * 3), 0.55 * dawn * dawn * (k == 0 ? 1 : 0.35), 9 + k);
        }

        if (star > 0) Reflect(reflection, t, StarX, YH + 4, 420, 10, 0.22 * star * (1 - (0.5 * dawn)), 13.0);

        // The light every lamp throws, kept for the later passes.
        DrawingPlaneImage lamp = new DrawingPlaneImage(HW, HH);
        lamp.FillParallel((col, row) =>
        {
            double x = (2 * col) + 1, y = (2 * row) + 1;
            double field = 0;
            foreach ((double lx, double ly, double strength, double radius) in lights)
            {
                double dx = x - lx;
                double dy = (y - ly) * 1.15;
                field += strength * Math.Exp(-((dx * dx) + (dy * dy)) / (radius * radius));
            }

            double doorDy = (y - YH) * 2.5;
            double doorLight = doors * Math.Exp(-(((x - 205) * (x - 205)) + (doorDy * doorDy)) / (380.0 * 380.0)) * 0.5;
            return new Vector4((float)(field + doorLight), (float)((field * 0.52) + (doorLight * 0.72)), (float)((field * 0.2) + (doorLight * 0.38)), 1f);
        });

        double silhouetteR = ((0.012 * (1 - dawn)) + (0.20 * dawn)) * exposure;
        double silhouetteG = ((0.012 * (1 - dawn)) + (0.10 * dawn)) * exposure;
        double silhouetteB = ((0.028 * (1 - dawn)) + (0.12 * dawn)) * exposure;
        double horizonR = ((0.045 * (1 - dawn)) + (0.85 * dawn)) * exposure;
        double horizonG = ((0.065 * (1 - dawn)) + (0.52 * dawn)) * exposure;
        double horizonB = ((0.165 * (1 - dawn)) + (0.34 * dawn)) * exposure;
        double deepR = ((0.012 * (1 - dawn)) + (0.10 * dawn)) * exposure;
        double deepG = ((0.022 * (1 - dawn)) + (0.09 * dawn)) * exposure;
        double deepB = ((0.055 * (1 - dawn)) + (0.16 * dawn)) * exposure;

        scene.FillParallel((col, row) =>
        {
            double x = (2 * col) + 1, y = (2 * row) + 1;
            Vector4 light = lamp.GetPixel(col, row);
            double lr = light.X, lg = light.Y, lb = light.Z;
            double r, g, b;
            if (y < YH)
            {
                // Sky: night blue deepening upward, turning to a dawn gradient; a glow where the sun rises.
                double v = Math.Clamp((YH - y) / YH, 0, 1);
                double high = Math.Pow(v, 0.55);
                double nightR = 0.055 + ((0.006 - 0.055) * high);
                double nightG = 0.075 + ((0.010 - 0.075) * high);
                double nightB = 0.19 + ((0.035 - 0.19) * high);
                double dawnR, dawnG, dawnB;
                if (v < 0.25)
                {
                    double f = v / 0.25;
                    dawnR = 1.0 + ((0.36 - 1.0) * f);
                    dawnG = 0.60 + ((0.28 - 0.60) * f);
                    dawnB = 0.33 + ((0.46 - 0.33) * f);
                }
                else
                {
                    double f = Math.Pow(Math.Clamp((v - 0.25) / 0.75, 0, 1), 0.8);
                    dawnR = 0.36 + ((0.07 - 0.36) * f);
                    dawnG = 0.28 + ((0.10 - 0.28) * f);
                    dawnB = 0.46 + ((0.26 - 0.46) * f);
                }

                double uneven = (1 + (0.10 * cloud[col, row] * (0.5 + v))) * exposure;
                double sunDy = (y - YH) * 1.6;
                double sun = dawn * dawn * Math.Exp(-(((x - 780) * (x - 780)) + (sunDy * sunDy)) / (240.0 * 240.0)) * 0.8;
                r = (((nightR * (1 - dawn)) + (dawnR * dawn)) * uneven) + (lr * 0.05) + sun;
                g = (((nightG * (1 - dawn)) + (dawnG * dawn)) * uneven) + (lg * 0.05) + (sun * 0.55);
                b = (((nightB * (1 - dawn)) + (dawnB * dawn)) * uneven) + (lb * 0.05) + (sun * 0.25);

                // The temple tower and the far shore, dark against the sky.
                double m = tower[col, row];
                if (m > 0)
                {
                    r = (r * (1 - m)) + (m * (silhouetteR + (lr * 0.02)));
                    g = (g * (1 - m)) + (m * (silhouetteG + (lg * 0.02)));
                    b = (b * (1 - m)) + (m * (silhouetteB + (lb * 0.02)));
                }

                m = shore[col, row];
                if (m > 0)
                {
                    r = (r * (1 - m)) + (m * silhouetteR * 1.1);
                    g = (g * (1 - m)) + (m * silhouetteG * 1.1);
                    b = (b * (1 - m)) + (m * silhouetteB * 1.1);
                }
            }
            else if (y <= quay[col])
            {
                // Water: horizon colour fading to deep water, moving wave glints, reflections and foam.
                double p = Math.Clamp((y - YH) / (H - YH), 0, 1);
                double far = (1 - p) * (1 - p) * (1 - p);
                double period = 3.0 + (26 * p);
                double wave = Math.Sin((y / period) + (2.4 * Math.Sin((x / (60 + (260 * p))) + (t * 0.55)))
                    + (1.3 * Math.Sin((x / (23 + (90 * p))) - (t * 0.8))) + (t * 1.3) + (ripple[col, row] * 0.9));
                double swell = Math.Sin((y / (period * 1.7)) - (x / (240 + (500 * p))) - (t * 0.9));
                double patch = Math.Clamp(0.55 + (0.6 * Math.Sin((x / (35 + (160 * p))) + (1.7 * Math.Sin(y / (9 + (40 * p)))) + (cloud[col, row] * 0.8) - (t * 0.35))), 0, 1);
                double glint = Math.Pow(Math.Clamp((wave * 0.7) + (swell * 0.5) - 0.55, 0, 1), 1.4) * patch;
                double body = 0.85 + (0.25 * swell);
                double shine = reflection[col, row];
                r = ((((horizonR * far * 0.75) + (deepR * (1 - far))) * body) + (glint * ((horizonR * 0.55) + (lr * 0.55))) + (lr * 0.06)) + shine;
                g = ((((horizonG * far * 0.75) + (deepG * (1 - far))) * body) + (glint * ((horizonG * 0.55) + (lg * 0.55))) + (lg * 0.06)) + (shine * 0.55);
                b = ((((horizonB * far * 0.75) + (deepB * (1 - far))) * body) + (glint * ((horizonB * 0.55) + (lb * 0.55))) + (lb * 0.06)) + (shine * 0.22);

                double hullDy = (y - (boat.Waterline + (5 * sc))) / ((6 * sc) + 1.2);
                double hullFoam = x > boat.X && x < boat.X + (1450 * sc)
                    ? Math.Exp(-(hullDy * hullDy)) * Math.Clamp(Math.Sin((x / ((16 * sc) + 2)) - (t * 2.6)) + 0.3, 0, 1)
                    : 0;
                double quayDy = (y - quay[col] + 6 + (4 * Math.Sin((t * 1.4) + (x / 90)))) / 9;
                double quayFoam = Math.Exp(-(quayDy * quayDy)) * Math.Clamp(Math.Sin((x / 21) - (t * 1.9)) + (0.4 * Math.Sin((x / 7) + (t * 3))) + 0.2, 0, 1);
                double foam = (hullFoam * vis) + quayFoam;
                r += foam * ((horizonR * 0.6) + (lr * 0.9) + 0.012);
                g += foam * ((horizonG * 0.6) + (lg * 0.9) + 0.012);
                b += foam * ((horizonB * 0.6) + (lb * 0.9) + 0.012);
            }
            else
            {
                // The foreground quay: paving stones, lit by the torches.
                double q = Math.Clamp((y - Timeline.QuayEdge) / (H - Timeline.QuayEdge), 0, 1);
                double course = Math.Sqrt(q) * 7.0;
                double rowIndex = Math.Floor(course);
                double cell = x / (150 + (160 * q));
                double along = (cell + (rowIndex * 0.5)) % 1.0;
                bool joint = course - rowIndex < 0.09 || along < 0.05;
                double tone = (1 + (0.3 * ripple[col, row] / 3)) * (1 + (0.15 * Math.Sin((rowIndex * 2.3) + (Math.Floor(cell) * 1.7))));
                double stoneR = (0.045 * tone * (1 - dawn)) + (0.16 * dawn);
                double stoneG = (0.040 * tone * (1 - dawn)) + (0.12 * dawn);
                double stoneB = (0.037 * tone * (1 - dawn)) + (0.11 * dawn);
                r = (stoneR * exposure) + (stoneR * lr * 3.0) + (lr * 0.01);
                g = (stoneG * exposure) + (stoneG * lg * 3.0) + (lg * 0.01);
                b = (stoneB * exposure) + (stoneB * lb * 3.0) + (lb * 0.01);
                if (joint)
                {
                    r *= 0.45;
                    g *= 0.45;
                    b *= 0.45;
                }

                double lipDy = (y - quay[col] - 8) / 7;
                double lip = Math.Exp(-(lipDy * lipDy));
                r += lip * ((lr * 0.25) + (horizonR * 0.3));
                g += lip * ((lg * 0.25) + (horizonG * 0.3));
                b += lip * ((lb * 0.25) + (horizonB * 0.3));
            }

            return new Vector4((float)r, (float)g, (float)b, 1f);
        });

        // The sun's disc, rising behind the horizon.
        double disc = Timeline.SunRises.Progress(t);
        if (disc > 0)
        {
            DrawingPlane sun = Mask(new CircleShape(780, (float)(YH + 30 - (55 * disc)), 34, 1f, null, true));
            sun.Fill(new Rectangle(0, SkyRows, HW, HH - SkyRows), 0f);
            glow.R.Add(sun, (float)(0.9 * disc));
            glow.G.Add(sun, (float)(0.45 * 0.9 * disc));
            glow.B.Add(sun, (float)(0.12 * 0.9 * disc));
        }

        if (vis > 0.001) PaintBoat(scene, glow, lamp, boat, t, exposure, boatFlames);

        // The torches: the two tall stands on the quay, and the small far flames.
        for (int n = 0; n < Torches.Length; n++)
        {
            (double x, _, double flame, double size) = Torches[n];
            if (size > 0.5)
            {
                DrawingPlane stand = stands[n];
                DrawingPlane bowl = bowls[n];
                scene.FillParallel((col, row) =>
                {
                    Vector4 c = scene.GetPixel(col, row);
                    Vector4 l = lamp.GetPixel(col, row);
                    double m = stand[col, row];
                    if (m > 0)
                    {
                        double shade = Math.Clamp(0.55 + ((((2 * col) + 1) - x) / 22), 0.15, 1) * 5;
                        c.X = (float)((c.X * (1 - m)) + (m * ((0.03 * exposure) + (0.05 * l.X * shade))));
                        c.Y = (float)((c.Y * (1 - m)) + (m * ((0.019 * exposure) + (0.03 * l.Y * shade))));
                        c.Z = (float)((c.Z * (1 - m)) + (m * ((0.011 * exposure) + (0.015 * l.Z * shade))));
                    }

                    m = bowl[col, row];
                    if (m > 0)
                    {
                        c.X = (float)((c.X * (1 - m)) + (m * ((0.03 * exposure) + (0.06 * l.X * 6))));
                        c.Y = (float)((c.Y * (1 - m)) + (m * ((0.02 * exposure) + (0.035 * l.Y * 6))));
                        c.Z = (float)((c.Z * (1 - m)) + (m * ((0.012 * exposure) + (0.018 * l.Z * 6))));
                    }

                    return c;
                });

                AddFlame(glow, x, flame + 4, 165, t, n * 1.7, torch[n]);
            }
            else
            {
                AddFlame(glow, x, flame, 15, t, n * 1.7, torch[n] * 0.9);
            }
        }

        // The temple doors open: the doorway glows and a pillar of light rises to the sky.
        if (doors > 0)
        {
            glow.FillParallel(new Rectangle(0, 0, HW, SkyRows), (col, row) =>
            {
                Vector4 c = glow.GetPixel(col, row);
                double x = (2 * col) + 1, y = (2 * row) + 1;
                double m = doorway[col, row] * doors * 1.2;
                if (y < YH - 100)
                {
                    double rise = YH - 110 - y;
                    double spread = (x - 205) / 3.0 / (22 + (50 * Math.Clamp(rise / 700, 0, 1)));
                    double pillar = Math.Exp(-(spread * spread)) * Math.Pow(Math.Clamp(1 - (rise / 820), 0, 1), 1.5) * 0.30 * doors;
                    c.X += (float)pillar;
                    c.Y += (float)(pillar * 0.78);
                    c.Z += (float)(pillar * 0.45);
                }

                if (m > 0)
                {
                    c.X += (float)m;
                    c.Y += (float)(m * 0.8);
                    c.Z += (float)(m * 0.45);
                }

                return c;
            });
        }

        if (star > 0) PaintStar(scene, star, t);

        // Dawn washes the scene out a little; the fade out takes everything to black.
        double fade = 1 - Timeline.FadeOut.Progress(t);
        glow.Multiply((float)(1 - (0.2 * dawn)));
        scene.Multiply((float)(1 - (0.38 * dawn))).Add(glow).Multiply((float)fade);
        glow.Multiply((float)fade);
        return (scene, glow, fade);
    }

    /// <summary>Releases the mask session.</summary>
    public void Dispose() => maskSession.Dispose();

    /// <summary>The distance from the centre of an eight-pointed star to its edge, in a direction.</summary>
    /// <param name="angle">The direction, radians from straight up.</param>
    /// <param name="radius">The star's radius (to its points).</param>
    /// <returns>The edge's distance from the centre.</returns>
    public static double StarEdge(double angle, double radius)
    {
        const double Segment = Math.PI / 8;
        double a = angle % (2 * Segment);
        if (a < 0) a += 2 * Segment;
        if (a > Segment) a = (2 * Segment) - a;
        double valleyX = radius * StarValley * Math.Cos(Segment);
        double valleyY = radius * StarValley * Math.Sin(Segment);
        double edgeX = valleyX - radius;
        double edgeY = valleyY;
        return (radius * edgeY) / ((Math.Cos(a) * edgeY) - (Math.Sin(a) * edgeX));
    }

    private static double Flicker(double t, double phase) => 0.86 + (0.09 * Math.Sin((t * 13) + phase)) + (0.05 * Math.Sin((t * 29) + (phase * 2.3)));

    private static PointF[] Points(params (double X, double Y)[] points)
    {
        PointF[] result = new PointF[points.Length];
        for (int i = 0; i < points.Length; i++) result[i] = new PointF((float)points[i].X, (float)points[i].Y);
        return result;
    }

    // Seeded white noise, smoothed by a Gaussian blur and scaled.
    private static DrawingPlane Noise(DrawingRandom random, float sigma, float gain)
    {
        DrawingPlane noise = new DrawingPlane(HW, HH);
        float[] values = noise.Data;
        for (int i = 0; i < values.Length; i++) values[i] = (float)random.Normal(0, 1);
        return noise.GaussianBlur(sigma).Multiply(gain);
    }

    // The mask of one or more filled polygons (frame coordinates), at the underpainting's size.
    private DrawingPlane Mask(params PointF[][] polygons)
    {
        DrawingShape[] shapes = new DrawingShape[polygons.Length];
        for (int i = 0; i < polygons.Length; i++) shapes[i] = new PolylineShape(polygons[i], 1f, null, isClosed: true, isFilled: true);
        return Mask(shapes);
    }

    // The shapes' hard-edged mask at the frame's size, averaged in 2 x 2 blocks down to the underpainting's.
    private DrawingPlane Mask(params DrawingShape[] shapes)
    {
        maskLayer.Clear();
        foreach (DrawingShape shape in shapes) maskLayer.AddShape(shape);
        maskSession.ExportMask(maskLayer, frameMask, MaskMeaning.HardEdged);
        return frameMask.Downsampled(2);
    }

    // The stepped temple tower on the far shore.
    private static PointF[] TowerOutline()
    {
        const double X0 = 40;
        double foot = YH + 2;
        (double Left, double Right, double Rise)[] steps = { (0, 230, 0), (25, 205, 22), (52, 178, 40), (80, 150, 56), (100, 130, 68) };
        List<(double, double)> outline = new List<(double, double)> { (X0 - 40, foot) };
        foreach ((double left, _, double rise) in steps)
        {
            outline.Add((X0 + left, foot - rise));
            outline.Add((X0 + left, foot - rise - 16));
        }

        for (int i = steps.Length - 1; i >= 0; i--)
        {
            outline.Add((X0 + steps[i].Right, foot - steps[i].Rise - 16));
            outline.Add((X0 + steps[i].Right, foot - steps[i].Rise));
        }

        outline.Add((X0 + 320, foot));
        return Points(outline.ToArray());
    }

    // The low far shore, with the odd taller clump.
    private static PointF[] ShoreOutline()
    {
        DrawingRandom random = new DrawingRandom(11);
        List<(double, double)> outline = new List<(double, double)> { (300, YH + 3) };
        for (int i = 0; i < 60; i++)
        {
            double x = 300 + ((Timeline.Width + 10 - 300) * i / 59.0);
            double rise = 3 + (4 * random.NextDouble());
            double clump = 10 * random.NextDouble();
            if (random.NextDouble() < 0.15) rise += clump;
            outline.Add((x, YH - rise));
        }

        outline.Add((Timeline.Width + 10, YH + 3));
        return Points(outline.ToArray());
    }

    // A broken streak of light on the water below a lamp.
    private static void Reflect(DrawingPlane reflection, double t, double x, double top, double length, double width, double strength, double phase)
    {
        if (strength <= 0.01) return;
        float[] values = reflection.Data;
        int y0 = (int)Math.Max(0, top / 2), y1 = (int)Math.Min(HH, (top + length) / 2);
        int x0 = (int)Math.Max(0, (x - (4 * width)) / 2), x1 = (int)Math.Min(HW, ((x + (4 * width)) / 2) + 1);
        for (int row = y0; row < y1; row++)
        {
            double py = (2 * row) + 1;
            double p = Math.Clamp((py - YH) / (H - YH), 0, 1);
            double down = (py - top) / length;
            double fall = Math.Pow(Math.Clamp(1 - down, 0, 1), 1.2);
            double widen = width * (1 + (0.8 * down));
            for (int col = x0; col < x1; col++)
            {
                double px = (2 * col) + 1;
                double wave = Math.Sin((py / (2.2 + (14 * p))) + (1.2 * Math.Sin((px / 7) + (t * 1.7) + phase)) + (t * 2.2) + phase);
                double broken = Math.Clamp((wave * 1.4) - 0.1, 0, 1);
                double across = (px - x) / widen;
                values[(row * HW) + col] += (float)(strength * Math.Exp(-(across * across)) * broken * fall);
            }
        }
    }

    // One tongue of flame; base at (x, y), frame coordinates - added to the lights.
    private static void Flame(DrawingPlaneImage glow, double x, double y, double height, double t, double phase, double strength)
    {
        if (strength <= 0.01 || height < 1.5) return;
        float[] lightR = glow.R.Data, lightG = glow.G.Data, lightB = glow.B.Data;
        double h = Math.Max(height, 3.0);
        int x0 = (int)Math.Max(0, (x - h) / 2), x1 = (int)Math.Min(HW, ((x + h) / 2) + 2);
        int y0 = (int)Math.Max(0, (y - (h * 1.4)) / 2), y1 = (int)Math.Min(HH, ((y + (h * 0.3)) / 2) + 2);
        double tip = 1.0 + (0.15 * Math.Sin((t * 17) + phase)) + (0.08 * Math.Sin((t * 31) + (phase * 3)));
        double lean = h * 0.05 * Math.Sin((t * 3.1) + phase);
        for (int row = y0; row < y1; row++)
        {
            double v = (y - ((2 * row) + 1)) / h; // 0 at the base, 1 at the tip
            if (v <= -0.15) continue;
            double sway = (h * 0.12 * Math.Sin((v * 5) + (t * 11) + phase) * v) + lean;
            double width = (h * 0.30 * Math.Pow(Math.Clamp(1 - v, 0, 1), 0.7) * (0.2 + (0.8 * Math.Clamp(v * 4, 0, 1)))) + 0.6;
            double taper = Math.Pow(Math.Clamp(1 - (v / tip), 0, 1), 0.8) * Math.Exp(-Math.Max(-v, 0) * 12);
            for (int col = x0; col < x1; col++)
            {
                double dx = ((2 * col) + 1 - x - sway) / width;
                double inside = Math.Exp(-(dx * dx)) * taper;
                double core = Math.Pow(inside, 2.5);
                int i = (row * HW) + col;
                lightR[i] += (float)(strength * (inside + (0.4 * core)));
                lightG[i] += (float)(strength * ((0.42 * inside) + (0.55 * core)));
                lightB[i] += (float)(strength * ((0.08 * inside) + (0.35 * core)));
            }
        }
    }

    // A flame; a tall one gets two smaller tongues beside it.
    private static void AddFlame(DrawingPlaneImage glow, double x, double y, double height, double t, double phase, double strength)
    {
        Flame(glow, x, y, height, t, phase, strength);
        if (height <= 20) return;
        Flame(glow, x - (0.16 * height), y - (0.02 * height), height * (0.55 + (0.1 * Math.Sin((t * 7) + phase))), t * 1.3, phase + 2.0, strength * 0.6);
        Flame(glow, x + (0.15 * height), y - (0.03 * height), height * (0.48 + (0.12 * Math.Sin((t * 9) + (phase * 2)))), t * 1.2, phase + 4.1, strength * 0.55);
    }

    private void PaintBoat(DrawingPlaneImage scene, DrawingPlaneImage glow, DrawingPlaneImage lamp, BoatState boat, double t, double exposure,
        (double X, double Y, double Height, double Strength)[] flames)
    {
        double sc = boat.Scale;
        double vis = boat.Visibility;
        DrawingPlane hull = Mask(boat.ToFrame(Boat.Hull), boat.ToFrame(Boat.Horn));
        DrawingPlane shrine = Mask(boat.ToFrame(Boat.Shrine));
        DrawingPlane door = Mask(boat.ToFrame(Boat.Door));
        DrawingPlane poles = Mask(boat.ToFrame(Boat.Poles[0]), boat.ToFrame(Boat.Poles[1]));

        // Rim light along the boat's upper and right-hand edges.
        DrawingPlane body = hull.Clone().Add(shrine).Min(1f);
        DrawingPlane rim = new DrawingPlane(HW, HH);
        rim.FillParallel((col, row) =>
        {
            float here = body[col, row];
            float up = body[col, (row + HH - 2) % HH];
            float right = body[(col + 2) % HW, row];
            return Math.Max(here - up, 0f) + (0.5f * Math.Max(here - right, 0f));
        });
        rim.GaussianBlur(0.8f);

        double doorGlow = vis * 0.55 * (0.9 + (0.1 * Math.Sin(t * 2.3)));
        scene.FillParallel((col, row) =>
        {
            Vector4 c = scene.GetPixel(col, row);
            Vector4 light = lamp.GetPixel(col, row);
            double lr = light.X, lg = light.Y, lb = light.Z;
            double y = (2 * row) + 1;
            double m = hull[col, row] * vis;
            if (m > 0)
            {
                double reed = 0.7 + (0.3 * Math.Sin((((2 * col) + 1 - boat.X) / ((9 * sc) + 1)) + ((y - boat.Waterline) / ((30 * sc) + 1))));
                c.X = (float)((c.X * (1 - m)) + (m * Lit(0.055 * reed, exposure, lr)));
                c.Y = (float)((c.Y * (1 - m)) + (m * Lit(0.034 * reed, exposure, lg)));
                c.Z = (float)((c.Z * (1 - m)) + (m * Lit(0.020 * reed, exposure, lb)));
            }

            m = shrine[col, row] * vis;
            if (m > 0)
            {
                c.X = (float)((c.X * (1 - m)) + (m * Lit(0.075, exposure, lr)));
                c.Y = (float)((c.Y * (1 - m)) + (m * Lit(0.05, exposure, lg)));
                c.Z = (float)((c.Z * (1 - m)) + (m * Lit(0.03, exposure, lb)));
            }

            m = poles[col, row] * vis;
            if (m > 0)
            {
                c.X = (float)((c.X * (1 - m)) + (m * Lit(0.04, exposure, lr)));
                c.Y = (float)((c.Y * (1 - m)) + (m * Lit(0.025, exposure, lg)));
                c.Z = (float)((c.Z * (1 - m)) + (m * Lit(0.015, exposure, lb)));
            }

            m = rim[col, row] * vis;
            if (m > 0)
            {
                c.X += (float)(m * ((lr * 0.55) + 0.004));
                c.Y += (float)(m * ((lg * 0.55) + 0.004));
                c.Z += (float)(m * ((lb * 0.55) + 0.004));
            }

            return c;
        });

        // The shrine's doorway glows.
        glow.R.Add(door, (float)doorGlow);
        glow.G.Add(door, (float)(doorGlow * 0.70));
        glow.B.Add(door, (float)(doorGlow * 0.30));

        for (int j = 0; j < flames.Length; j++)
        {
            AddFlame(glow, flames[j].X, flames[j].Y, flames[j].Height, t, 4 + (j * 2.1), flames[j].Strength * 1.1);
        }
    }

    // A dark surface: a little of its own colour, far more where lamplight falls on it.
    private static double Lit(double surface, double exposure, double light) => (surface * exposure * 0.7) + (surface * light * 4.5) + (light * 0.008);

    // The Octaquetra: a halo and slow rings of light in the sky, and the star itself in flat paint colours (the
    // brushstrokes laid out along its rays do the rest).
    private static void PaintStar(DrawingPlaneImage scene, double growth, double t)
    {
        double radius = StarRadius * 1.08 * (0.35 + (0.65 * growth));
        double opacity = Math.Clamp(growth * 1.4, 0, 1);
        scene.FillParallel((col, row) =>
        {
            Vector4 c = scene.GetPixel(col, row);
            double dy = (2 * row) + 1 - StarY;
            double dx = (2 * col) + 1 - StarX;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (row < SkyRows)
            {
                double ring = 0.5 + (0.5 * Math.Cos((distance / 26) - (t * 0.8)));
                double halo = (growth * Math.Exp(-(distance * distance) / (170.0 * 170.0)) * 0.38)
                    + (growth * Math.Exp(-distance / 260) * 0.20 * ring * ring * ring);
                c.X += (float)(halo * 0.9);
                c.Y += (float)(halo * 0.7);
                c.Z += (float)(halo * 0.45);
            }

            if (distance > radius + 3) return c;
            double q = distance / StarEdge(Math.Atan2(dx, -dy), radius); // below 1 inside the star
            double m = Math.Clamp(((1.0 - q) * radius / 4) + 0.5, 0, 1) * opacity;
            if (m <= 0) return c;
            double u = distance / radius;
            double outward = Math.Clamp((u - 0.55) / 0.45, 0, 1);
            double cr = 0.50 + ((0.36 - 0.50) * outward), cg = 0.21 + ((0.09 - 0.21) * outward), cb = 0.0;
            if (q < 0.62) (cr, cg, cb) = (0.56, 0.29, 0.02);
            if (u < 0.235) (cr, cg, cb) = (0.62, 0.40, 0.10);
            if (u > 0.13 && u < 0.18) (cr, cg, cb) = (0.36, 0.09, 0.0);
            if (u < 0.05) (cr, cg, cb) = (0.03, 0.04, 0.15);
            c.X = (float)((c.X * (1 - m)) + (cr * m));
            c.Y = (float)((c.Y * (1 - m)) + (cg * m));
            c.Z = (float)((c.Z * (1 - m)) + (cb * m));
            return c;
        });
    }
}
