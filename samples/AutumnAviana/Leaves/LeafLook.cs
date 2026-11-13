using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.PixelFormats;

namespace AutumnAviana.Leaves;

/// <summary>
/// How one leaf looks: its kind and silhouette variant, its length, its curl and its colours. Built from a seeded
/// random stream, so every leaf looks the same in every run. <see cref="CreateShape" /> turns it into drawing
/// elements centred on (0, 0): the blade (a gradient fill with a darker rim) and its veins, as a group that the
/// caller turns, foreshortens and places with the group's transform.
/// </summary>
internal sealed class LeafLook
{
    private LeafLook()
    {
    }

    /// <summary>The kind of leaf.</summary>
    public LeafKind Kind { get; private set; }

    /// <summary>The silhouette variant.</summary>
    public int Variant { get; private set; }

    /// <summary>The leaf's length, tip to the end of the stalk, pixels.</summary>
    public float Length { get; private set; }

    /// <summary>How far the midrib bends to one side, as a fraction of the length.</summary>
    public float Bend { get; private set; }

    /// <summary>How much one half of the blade is curled under (0 = flat, 0.3 = a third narrower).</summary>
    public float Curl { get; private set; }

    /// <summary>The colour near the stalk.</summary>
    public Color Inner { get; private set; }

    /// <summary>The colour towards the edges and the tip.</summary>
    public Color Outer { get; private set; }

    /// <summary>The colour of a dried rim, or the outer colour again for a leaf without one.</summary>
    public Color Rim { get; private set; }

    /// <summary>Creates a leaf's look from its own random stream.</summary>
    /// <param name="random">The leaf's random stream.</param>
    /// <param name="minLength">The shortest length, pixels.</param>
    /// <param name="maxLength">The longest length, pixels.</param>
    /// <param name="shade">A brightness factor applied to every colour (below 1 = deeper in a pile).</param>
    /// <returns>The look.</returns>
    public static LeafLook Create(DrawingRandom random, float minLength, float maxLength, float shade = 1f)
    {
        LeafLook look = new LeafLook();
        double k = random.NextDouble();
        look.Kind = k < 0.47 ? LeafKind.Maple : k < 0.63 ? LeafKind.Oak : k < 0.81 ? LeafKind.Elm : LeafKind.Birch;
        look.Variant = random.NextInt(LeafTemplates.VariantsPerKind);
        double u = random.NextDouble();
        float kindScale = look.Kind switch
        {
            LeafKind.Maple => 1.0f,
            LeafKind.Oak => 1.08f,
            LeafKind.Elm => 0.88f,
            _ => 0.72f,
        };
        look.Length = (minLength + ((maxLength - minLength) * (float)u)) * kindScale;
        look.Bend = (float)random.Uniform(-0.09, 0.09);
        look.Curl = (float)Math.Max(0, random.Uniform(-0.1, 0.32));
        Palette(look, random, shade);
        return look;
    }

    /// <summary>
    /// Builds the leaf as drawing elements centred on (0, 0) (the blade's middle), tip pointing up before the
    /// caller's transform: the gradient-filled blade with a thin darker rim, then the veins over it.
    /// </summary>
    /// <returns>The leaf, as a group.</returns>
    public DrawingGroup CreateShape()
    {
        LeafTemplate template = LeafTemplates.Get(Kind, Variant);
        PathGeometry blade = Blade(template);
        float length = Length;
        PathGeometryBuilder lines = new PathGeometryBuilder();
        foreach (PointF[] ring in template.Linework)
        {
            List<PointF> points = new List<PointF>(ring.Length);
            foreach (PointF p in ring) points.Add(Warp(p));
            lines.AddPolygon(points, closed: true);
        }

        PointF hub = Warp(template.Hub);
        RadialGradientFill fill = new RadialGradientFill(hub, length * 0.62f, new[]
        {
            new ColorStop(0f, Inner),
            new ColorStop(0.55f, Outer),
            new ColorStop(1f, Rim),
        });

        DrawingGroup leaf = new DrawingGroup();
        leaf.Add(new PathShape(blade, fill));
        leaf.Add(new PathShape(lines.Build(), Darker(Rim, 0.66, 168)));
        return leaf;
    }

    /// <summary>
    /// The leaf's blade alone in a deep, shaded version of its colour: drawn behind a resting leaf (turned and a
    /// little larger) it reads as another leaf further back in the heap, in the heap's own shade.
    /// </summary>
    /// <returns>The shaded blade, centred on (0, 0).</returns>
    public PathShape CreateShadeShape()
    {
        // Half the leaf's own colour, half a deep russet: shaded, but still warm.
        Rgba32 own = Outer.ToPixel<Rgba32>();
        Color shade = Color.FromRgb((byte)((own.R * 0.3) + 50), (byte)((own.G * 0.26) + 18), (byte)((own.B * 0.22) + 8));
        return new PathShape(Blade(LeafTemplates.Get(Kind, Variant)), shade);
    }

    /// <summary>The blade outline, curled, bent and sized.</summary>
    private PathGeometry Blade(LeafTemplate template)
    {
        List<PointF> outline = new List<PointF>(template.Outline.Count);
        foreach (PointF p in template.Outline) outline.Add(Warp(p));
        return PathGeometry.FromPoints(outline, closed: true);
    }

    /// <summary>A template point curled, bent and sized for this leaf.</summary>
    private PointF Warp(PointF p)
    {
        float t = p.Y + 0.5f;                                       // 0 at the tip .. 1 at the end of the stalk
        float x = p.X > 0 ? p.X * (1 - Curl) : p.X;                 // one half curled under, so it looks narrower
        x += Bend * ((1 - t) * (1 - t) - 0.25f);                    // the midrib bends towards the tip
        return new PointF(x * Length, p.Y * Length);
    }

    /// <summary>A colour scaled towards black, with an alpha.</summary>
    private static Color Darker(Color color, double factor, byte alpha)
    {
        Rgba32 c = color.ToPixel<Rgba32>();
        return Color.FromRgba((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor), alpha);
    }

    /// <summary>
    /// Fall colours by kind: maples run scarlet, crimson, orange and gold; oaks russet and brown-red; elms
    /// yellow-ochre with browning edges; birches clear yellow. Each leaf mixes its kind's colours with its own
    /// random amounts, and a few get a dried, brown rim.
    /// </summary>
    private static void Palette(LeafLook look, DrawingRandom random, float shade)
    {
        (Rgba32 Inner, Rgba32 Outer)[] choices = look.Kind switch
        {
            LeafKind.Maple => new[]
            {
                (new Rgba32(246, 176, 40), new Rgba32(214, 44, 26)),     // gold centre, scarlet lobes
                (new Rgba32(236, 102, 30), new Rgba32(186, 26, 30)),     // orange to crimson
                (new Rgba32(250, 204, 60), new Rgba32(240, 128, 28)),    // yellow to orange
                (new Rgba32(222, 58, 30), new Rgba32(168, 20, 32)),      // all red
                (new Rgba32(248, 214, 82), new Rgba32(232, 160, 36)),    // sugar-maple gold
            },
            LeafKind.Oak => new[]
            {
                (new Rgba32(222, 128, 52), new Rgba32(168, 64, 32)),     // russet
                (new Rgba32(200, 116, 54), new Rgba32(140, 76, 38)),     // brown
                (new Rgba32(230, 92, 44), new Rgba32(160, 40, 32)),      // oak red
                (new Rgba32(238, 150, 56), new Rgba32(196, 84, 34)),     // copper
            },
            LeafKind.Elm => new[]
            {
                (new Rgba32(232, 196, 70), new Rgba32(204, 146, 44)),    // ochre
                (new Rgba32(236, 178, 54), new Rgba32(186, 112, 40)),    // amber
            },
            _ => new[]
            {
                (new Rgba32(250, 222, 84), new Rgba32(240, 182, 40)),    // birch yellow
                (new Rgba32(246, 206, 66), new Rgba32(228, 150, 34)),
            },
        };

        (Rgba32 inner, Rgba32 outer) = choices[random.NextInt(choices.Length)];
        double mix = random.Uniform(-0.25, 0.25);               // shift the blend towards one end or the other
        double light = shade * random.Uniform(0.95, 1.08);
        look.Inner = Mixed(inner, outer, Math.Max(0, mix), light);
        look.Outer = Mixed(outer, inner, Math.Max(0, -mix), light);
        bool dried = random.NextDouble() < 0.16;
        look.Rim = dried ? Mixed(new Rgba32(140, 84, 40), outer, 0.3, light) : look.Outer;
    }

    private static Color Mixed(Rgba32 a, Rgba32 b, double t, double light)
    {
        byte Channel(byte x, byte y) => (byte)Math.Clamp(((x * (1 - t)) + (y * t)) * light, 0, 255);
        return Color.FromRgb(Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
    }
}
