using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Painting;

public class DrawingOwnershipMapTests
{
    private const int N = DrawingOwnershipMap.Nobody;

    //A session whose calibrated units are output pixels
    private static DrawingSession CreateSession(int width, int height)
        => new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(width, height),
            BackgroundFillColor = Color.Transparent,
            LayerOpacity = 255,
        });

    //A map whose owned pixels are given as (x, y, owner); one element per distinct owner
    //  value, each a 1 x 1 square at its pixel, on one layer
    private static DrawingOwnershipMap MapOf(int width, int height, params (int X, int Y)[] pixels)
    {
        using DrawingSession session = CreateSession(width, height);
        session.AddLayer("Ink", Color.Red);
        foreach ((int x, int y) in pixels) { session.DrawRectangle(x, y, 1, 1, filled: true); }
        return session.ExportOwnershipMap(new Size(width, height));
    }

    //The nearest owned pixel by brute force, ties to the smallest x then the smallest y
    private static int[] BruteForceFill(DrawingOwnershipMap map)
    {
        int w = map.Width, h = map.Height;
        ReadOnlySpan<int> owners = map.Owners;
        var result = new int[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (owners[(y * w) + x] >= 0)
                {
                    result[(y * w) + x] = owners[(y * w) + x];
                    continue;
                }
                long best = long.MaxValue;
                int bestOwner = N;
                for (int qx = 0; qx < w; qx++)
                {
                    for (int qy = 0; qy < h; qy++)
                    {
                        int owner = owners[(qy * w) + qx];
                        if (owner < 0) { continue; }
                        long d = ((long)(qx - x) * (qx - x)) + ((long)(qy - y) * (qy - y));
                        if (d < best)
                        {
                            best = d;
                            bestOwner = owner;
                        }
                    }
                }
                result[(y * w) + x] = bestOwner;
            }
        }
        return result;
    }

    [Fact]
    public void ExportOwnershipMap_gives_each_pixel_to_the_last_element_covering_its_centre()
    {
        //Arrange - two overlapping squares; the second one is painted later
        using DrawingSession session = CreateSession(5, 3);
        session.AddLayer("Ink", Color.Red);
        DrawingShape first = session.DrawRectangle(0, 0, 3, 2, filled: true);
        DrawingShape second = session.DrawRectangle(2, 1, 2, 2, filled: true);

        //Act
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(5, 3));

        //Assert
        map.Owners.ToArray().Should().Equal(
            0, 0, 0, N, N,
            0, 0, 1, 1, N,
            N, N, 1, 1, N);
        map.ElementCount.Should().Be(2);
        map.UnownedCount.Should().Be(6);
        map.GetElement(0).Should().BeSameAs(first);
        map.IndexOf(second).Should().Be(1);
        map.GetPixelCount(0).Should().Be(5);
        map.GetPixelCount(1).Should().Be(4);
        map.GetBounds(0).Should().Be(new Rectangle(0, 0, 3, 2));
        map.GetBounds(1).Should().Be(new Rectangle(2, 1, 2, 2));
        map[3, 1].Should().Be(1);
    }

    [Fact]
    public void ExportOwnershipMap_numbers_every_element_across_layers_including_hidden_ones()
    {
        //Arrange
        using DrawingSession session = CreateSession(3, 1);
        DrawingLayer bottom = session.AddLayer("Bottom", Color.Red);
        session.DrawRectangle(0, 0, 1, 1, filled: true);
        DrawingShape hidden = session.DrawShape(new RectangleShape(1, 0, 1, 1, isFilled: true) { EndTime = 0.0 });
        DrawingLayer top = session.AddLayer("Top", Color.Blue);
        session.ActiveLayer = top;
        DrawingShape upper = session.DrawRectangle(2, 0, 1, 1, filled: true);

        //Act
        DrawingOwnershipMap all = session.ExportOwnershipMap(new Size(3, 1));
        DrawingOwnershipMap onlyTop = session.ExportOwnershipMap(top, new Size(3, 1));

        //Assert
        all.Owners.ToArray().Should().Equal(0, N, 2);
        all.IndexOf(hidden).Should().Be(1);
        all.GetPixelCount(1).Should().Be(0);
        all.GetBounds(1).Should().Be(Rectangle.Empty);
        all.GetLayer(0).Should().BeSameAs(bottom);
        all.GetLayer(2).Should().BeSameAs(top);
        onlyTop.Owners.ToArray().Should().Equal(N, N, 0);
        onlyTop.GetElement(0).Should().BeSameAs(upper);
        onlyTop.IndexOf(hidden).Should().Be(-1);
    }

    [Fact]
    public void ExportOwnershipMap_shares_an_edge_between_neighbours_without_gaps_or_overlaps()
    {
        //Arrange - two triangles splitting a square along its diagonal
        using DrawingSession session = CreateSession(8, 8);
        session.AddLayer("Ink", Color.Red);
        session.DrawPolyline(new[] { (0f, 0f), (8f, 0f), (8f, 8f) }, closed: true, filled: true);
        session.DrawPolyline(new[] { (0f, 0f), (8f, 8f), (0f, 8f) }, closed: true, filled: true);

        //Act
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(8, 8));

        //Assert - every pixel owned exactly once; a centre ON the diagonal lies on the upper
        //  triangle's left edge (inside) and on the lower triangle's right edge (outside)
        map.UnownedCount.Should().Be(0);
        (map.GetPixelCount(0) + map.GetPixelCount(1)).Should().Be(64);
        map.GetPixelCount(0).Should().Be(36);
        map[3, 3].Should().Be(0);
        map[3, 4].Should().Be(1);
    }

    [Fact]
    public void FillUnowned_takes_the_nearest_owner_with_ties_to_the_smallest_x()
    {
        //Arrange - owners at x 0 and x 4 of a 5 x 1 strip
        DrawingOwnershipMap map = MapOf(5, 1, (0, 0), (4, 0));

        //Act
        DrawingOwnershipMap filled = map.FillUnowned();

        //Assert - x 2 is 2 away from both: the smaller x wins
        filled.Owners.ToArray().Should().Equal(0, 0, 0, 1, 1);
        filled.UnownedCount.Should().Be(0);
        filled.GetPixelCount(0).Should().Be(3);
        map.Owners.ToArray().Should().Equal(0, N, N, N, 1);
    }

    [Fact]
    public void FillUnowned_breaks_equal_x_ties_by_the_smallest_y()
    {
        //Arrange - owners straight above and below (1, 1), and one off to the side
        DrawingOwnershipMap map = MapOf(3, 3, (1, 0), (1, 2));

        //Act
        DrawingOwnershipMap filled = map.FillUnowned();

        //Assert
        filled.Owners.ToArray().Should().Equal(
            0, 0, 0,
            0, 0, 0,
            1, 1, 1);
    }

    [Fact]
    public void FillUnowned_matches_a_brute_force_search_on_a_scattered_map()
    {
        //Arrange - scattered single pixels, many of them equidistant from their neighbours
        var pixels = new List<(int, int)>();
        var random = new DrawingRandom(42);
        for (int i = 0; i < 40; i++) { pixels.Add((random.NextInt(37), random.NextInt(23))); }
        pixels.Add((0, 0));
        pixels.Add((36, 22));
        DrawingOwnershipMap map = MapOf(37, 23, pixels.ToArray());

        //Act
        DrawingOwnershipMap filled = map.FillUnowned();

        //Assert
        filled.Owners.ToArray().Should().Equal(BruteForceFill(map));
    }

    [Fact]
    public void FillUnowned_of_a_map_with_nothing_owned_returns_it_unchanged()
    {
        //Arrange
        using DrawingSession session = CreateSession(4, 4);
        session.AddLayer("Ink", Color.Red);
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(4, 4));

        //Act
        DrawingOwnershipMap filled = map.FillUnowned();

        //Assert
        filled.Should().BeSameAs(map);
        filled.UnownedCount.Should().Be(16);
    }

    [Fact]
    public void FillUnowned_is_the_same_for_any_thread_count()
    {
        //Arrange - large enough for the row-parallel path
        var pixels = new List<(int, int)>();
        var random = new DrawingRandom(7);
        for (int i = 0; i < 300; i++) { pixels.Add((random.NextInt(240), random.NextInt(160))); }
        DrawingOwnershipMap map = MapOf(240, 160, pixels.ToArray());

        //Act
        int[] parallel = map.FillUnowned().ToArray();
        int previous = PlaneKernels.MaxDegreeOfParallelism;
        int[] single;
        try
        {
            PlaneKernels.MaxDegreeOfParallelism = 1;
            single = map.FillUnowned().ToArray();
        }
        finally
        {
            PlaneKernels.MaxDegreeOfParallelism = previous;
        }

        //Assert
        parallel.Should().Equal(single);
    }

    [Fact]
    public void CreateBoundaryPlane_marks_pixels_whose_left_or_upper_neighbour_differs()
    {
        //Arrange
        using DrawingSession session = CreateSession(4, 3);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(0, 0, 2, 3, filled: true);
        session.DrawRectangle(2, 0, 2, 3, filled: true);
        session.DrawRectangle(1, 1, 1, 1, filled: true);

        //Act
        DrawingPlane seams = session.ExportOwnershipMap(new Size(4, 3)).CreateBoundaryPlane();

        //Assert - owners  0 0 1 1 / 0 2 1 1 / 0 0 1 1
        seams.Data.Should().Equal(
            0f, 0f, 1f, 0f,
            0f, 1f, 1f, 0f,
            0f, 1f, 1f, 0f);
    }

    [Fact]
    public void CreateBoundaryPlane_with_a_blur_is_the_hard_seams_blurred()
    {
        //Arrange
        using DrawingSession session = CreateSession(30, 20);
        session.AddLayer("Ink", Color.Red);
        session.DrawCircle(12, 9, 7, filled: true);
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(30, 20));

        //Act
        DrawingPlane soft = map.CreateBoundaryPlane(1.3f);

        //Assert
        soft.Data.Should().Equal(map.CreateBoundaryPlane().GaussianBlur(1.3f).Data);
    }

    [Fact]
    public void GetMeanColors_averages_the_source_under_each_element()
    {
        //Arrange - element 0 owns x 0..1, element 1 owns x 2; the source is a red ramp
        var source = new DrawingPlaneImage(4, 1);
        source.Fill((x, y) => new Vector4(x * 0.25f, 0.5f, 0f, 1f));
        using DrawingSession session = CreateSession(4, 1);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(0, 0, 2, 1, filled: true);
        session.DrawRectangle(2, 0, 1, 1, filled: true);
        session.DrawRectangle(3, 0, 0.4f, 1, filled: true);
        DrawingOwnershipMap two = session.ExportOwnershipMap(new Size(4, 1));

        //Act
        Vector4[] means = two.GetMeanColors(source);

        //Assert - (0 + 0.25) / 2; 0.5; nothing for the sliver (its centre is outside)
        means.Should().Equal(new Vector4(0.125f, 0.5f, 0f, 1f), new Vector4(0.5f, 0.5f, 0f, 1f), Vector4.Zero);
        two.ElementCount.Should().Be(3);
    }

    [Fact]
    public void GetMeanColors_reads_a_half_size_source_at_half_coordinates()
    {
        //Arrange - one element over a 4 x 2 map; the source is 2 x 1
        using DrawingSession session = CreateSession(4, 2);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(0, 0, 1, 2, filled: true);
        session.DrawRectangle(1, 0, 3, 2, filled: true);
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(4, 2));
        var source = new DrawingPlaneImage(2, 1);
        source.SetPixel(0, 0, new Vector4(0.2f, 0f, 0f, 1f));
        source.SetPixel(1, 0, new Vector4(0.8f, 0f, 0f, 1f));

        //Act
        Vector4[] means = map.GetMeanColors(source);

        //Assert - element 1 reads x 1 -> 0, x 2, 3 -> 1 on both rows: (0.2 + 0.8 + 0.8) / 3
        means[0].X.Should().Be(0.2f);
        double sum = 0;
        foreach (float v in new[] { 0.2f, 0.8f, 0.8f, 0.2f, 0.8f, 0.8f }) { sum += v; }
        means[1].X.Should().Be((float)(sum / 6));
    }

    [Fact]
    public void GetMeanColors_weights_by_alpha_so_transparent_pixels_lend_no_colour()
    {
        //Arrange - one element over two pixels: opaque blue and fully transparent red
        using DrawingSession session = CreateSession(2, 1);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(0, 0, 2, 1, filled: true);
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(2, 1));
        var source = new DrawingPlaneImage(2, 1);
        source.SetPixel(0, 0, new Vector4(0f, 0f, 1f, 1f));
        source.SetPixel(1, 0, new Vector4(1f, 0f, 0f, 0f));
        byte[] rgba = { 0, 0, 255, 255, 255, 0, 0, 0 };

        //Act
        Vector4 mean = map.GetMeanColors(source)[0];
        Vector4 meanOfBytes = map.GetMeanColors(rgba, 2, 1)[0];

        //Assert - straight colour of the covered part, half the alpha
        mean.Should().Be(new Vector4(0f, 0f, 1f, 0.5f));
        meanOfBytes.Should().Be(new Vector4(0f, 0f, 1f, 0.5f));
    }

    [Fact]
    public void GetMeanColors_of_bytes_uses_exact_integer_sums()
    {
        //Arrange
        using DrawingSession session = CreateSession(3, 1);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(0, 0, 3, 1, filled: true);
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(3, 1));
        byte[] rgba = { 10, 20, 30, 255, 11, 21, 31, 255, 12, 22, 32, 255 };

        //Act
        Vector4 mean = map.GetMeanColors(rgba, 3, 1)[0];

        //Assert
        mean.Should().Be(new Vector4((float)(33 * 255 / (255.0 * 765)), (float)(63 * 255 / (255.0 * 765)), (float)(93 * 255 / (255.0 * 765)), 1f));
    }

    [Fact]
    public void ApplyColors_paints_owned_pixels_and_leaves_the_rest()
    {
        //Arrange
        DrawingOwnershipMap map = MapOf(3, 1, (0, 0), (2, 0));
        var image = new DrawingPlaneImage(3, 1);
        image.Fill(0.1f, 0.2f, 0.3f, 0.4f);
        var rgba = new byte[12];
        Array.Fill(rgba, (byte)9);

        //Act
        map.ApplyColors(image, new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(0f, 0f, 1f, 0f) });
        map.ApplyColors(rgba, new[] { Color.FromRgba(1, 2, 3, 4), Color.FromRgba(5, 6, 7, 8) });

        //Assert - straight alpha kept as given, including a transparent colour
        image.GetPixel(0, 0).Should().Be(new Vector4(1f, 0f, 0f, 1f));
        image.GetPixel(1, 0).Should().Be(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        image.GetPixel(2, 0).Should().Be(new Vector4(0f, 0f, 1f, 0f));
        rgba.Should().Equal(1, 2, 3, 4, 9, 9, 9, 9, 5, 6, 7, 8);
    }

    [Fact]
    public void ApplyColors_checks_its_arguments()
    {
        //Arrange
        DrawingOwnershipMap map = MapOf(3, 1, (0, 0), (2, 0));

        //Act
        Action wrongSize = () => map.ApplyColors(new DrawingPlaneImage(2, 1), new[] { Vector4.One, Vector4.One });
        Action tooFew = () => map.ApplyColors(new DrawingPlaneImage(3, 1), new[] { Vector4.One });
        Action shortFrame = () => map.ApplyColors(new byte[8], new[] { Color.Red, Color.Red });

        //Assert
        wrongSize.Should().Throw<ArgumentException>();
        tooFew.Should().Throw<ArgumentException>();
        shortFrame.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyColors_with_float_colours_writes_bytes_by_the_planes_rounding_rule()
    {
        //Arrange - 0.5 x 255 + 0.5 = 128 (rounded half up), out-of-range and NaN values clamp
        DrawingOwnershipMap map = MapOf(3, 1, (0, 0), (2, 0));
        var rgba = new byte[12];
        Array.Fill(rgba, (byte)9);
        var colors = new[] { new Vector4(0.5f, 1.5f, -0.25f, float.NaN), new Vector4(1f, 0f, 0.2f, 0.25f) };
        Action tooFew = () => map.ApplyColors(new byte[12], new[] { Vector4.One });
        Action missing = () => map.ApplyColors(new byte[12], (IReadOnlyList<Vector4>)null);

        //Act
        map.ApplyColors(rgba, colors);

        //Assert - unowned middle pixel untouched; 0.2 -> 51, 0.25 -> 64
        rgba.Should().Equal(128, 255, 0, 0, 9, 9, 9, 9, 255, 0, 51, 64);
        tooFew.Should().Throw<ArgumentException>();
        missing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FillByElement_measures_each_pixel_centre_in_its_owners_axes()
    {
        //Arrange - a rectangle whose axes are its centre (4, 2) along +x; a second one moved by
        //  a transform (its axes are in its own coordinates, so the move does not show)
        using DrawingSession session = CreateSession(10, 4);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(1, 0, 6, 4, filled: true);
        session.DrawShape(new RectangleShape(0, 0, 2, 4, isFilled: true) { Transform = DrawingTransform.Translate(8, 0) });
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(10, 4));
        var along = new DrawingPlane(10, 4);
        var across = new DrawingPlane(10, 4);

        //Act
        map.FillByElement(along, p => (float)p.Along, -99f);
        map.FillByElement(across, p => (float)p.NormalizedAcross);

        //Assert
        along[0, 0].Should().Be(-99f);
        along[1, 0].Should().Be(-2.5f);
        along[6, 3].Should().Be(2.5f);
        along[8, 1].Should().Be(-0.5f);
        along[9, 1].Should().Be(0.5f);
        across[3, 0].Should().Be(-0.75f);
        across[3, 3].Should().Be(0.75f);
        map.GetAxes(1).Should().Be(new ElementAxes(new PointF(1, 2), 0f, 2, 4));
    }

    [Fact]
    public void FillByElement_uses_a_brushstrokes_own_axes_and_accepts_overrides()
    {
        //Arrange - a vertical stroke: along runs down the screen, across to the left
        using DrawingSession session = CreateSession(20, 40);
        session.AddLayer("Ink", Color.Red);
        BrushStrokeShape stroke = session.DrawBrushStroke(10, 20, 30, 10, 90f);
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(20, 40));
        var along = new DrawingPlane(20, 40);
        var overridden = new DrawingPlane(20, 40);

        //Act
        map.FillByElement(along, p => (float)p.Along);
        map.FillByElement(overridden, p => (float)p.Along, 0f, i => new ElementAxes(new PointF(0, 0), 0f, 1, 1));
        map.TryGetElementPixel(12, 30, out ElementPixel pixel).Should().BeTrue();

        //Assert
        map.GetAxes(0).Should().Be(stroke.GetAxes().Value);
        along[10, 30].Should().BeApproximately(10.5f, 1e-4f);
        along[10, 9].Should().BeApproximately(-10.5f, 1e-4f);
        overridden[10, 30].Should().Be(10.5f);
        pixel.Across.Should().BeApproximately(-2.5, 1e-4);
        pixel.NormalizedAlong.Should().BeApproximately(10.5 / 15.0, 1e-5);
        map.TryGetElementPixel(0, 0, out _).Should().BeFalse();
    }

    [Fact]
    public void FillByElement_is_the_same_for_any_thread_count()
    {
        //Arrange
        using DrawingSession session = CreateSession(200, 120);
        session.AddLayer("Ink", Color.Red);
        var random = new DrawingRandom(3);
        for (int i = 0; i < 60; i++)
        {
            session.DrawBrushStroke((float)random.Uniform(0, 200), (float)random.Uniform(0, 120), (float)random.Uniform(20, 60),
                (float)random.Uniform(5, 15), (float)random.Uniform(0, 360), (float)random.Normal(0, 0.1));
        }
        DrawingOwnershipMap map = session.ExportOwnershipMap(new Size(200, 120)).FillUnowned();
        Func<ElementPixel, float> ridges = p => (float)(1 + (0.1 * Math.Sin(p.Across * 0.9 + p.ElementIndex)) + (0.05 * p.NormalizedAlong));
        var parallel = new DrawingPlane(200, 120);
        var single = new DrawingPlane(200, 120);

        //Act
        map.FillByElement(parallel, ridges);
        int previous = PlaneKernels.MaxDegreeOfParallelism;
        try
        {
            PlaneKernels.MaxDegreeOfParallelism = 1;
            map.FillByElement(single, ridges);
        }
        finally
        {
            PlaneKernels.MaxDegreeOfParallelism = previous;
        }

        //Assert
        parallel.Data.Should().Equal(single.Data);
        map.UnownedCount.Should().Be(0);
    }

    [Fact]
    public void ExportOwnershipMap_ignores_the_background_and_paint()
    {
        //Arrange - the same shapes with an opaque background and transparent colours
        using DrawingSession plain = CreateSession(12, 8);
        plain.AddLayer("Ink", Color.Red);
        plain.DrawCircle(5, 4, 3, filled: true);
        using var painted = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(12, 8),
            BackgroundFillColor = Color.White,
            LayerOpacity = 30,
        });
        painted.AddLayer("Ink", Color.Red);
        painted.DrawCircle(5, 4, 3, filled: true, color: Color.FromRgba(9, 9, 9, 0));

        //Act
        DrawingOwnershipMap a = plain.ExportOwnershipMap(new Size(12, 8));
        DrawingOwnershipMap b = painted.ExportOwnershipMap(new Size(12, 8));

        //Assert
        a.Owners.ToArray().Should().Equal(b.Owners.ToArray());
        a.GetPixelCount(0).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExportOwnershipMap_counts_a_group_as_one_element()
    {
        //Arrange
        using DrawingSession session = CreateSession(4, 1);
        session.AddLayer("Ink", Color.Red);
        var group = new DrawingGroup();
        group.Add(new RectangleShape(0, 0, 1, 1, isFilled: true));
        group.Add(new RectangleShape(2, 0, 1, 1, isFilled: true) { StartTime = 1.0 });
        session.DrawShape(group);

        //Act
        DrawingOwnershipMap early = session.ExportOwnershipMap(new Size(4, 1));
        session.Time = 2.0;
        DrawingOwnershipMap late = session.ExportOwnershipMap(new Size(4, 1));

        //Assert - the child's time window applies inside the group
        early.Owners.ToArray().Should().Equal(0, N, N, N);
        late.Owners.ToArray().Should().Equal(0, N, 0, N);
        late.ElementCount.Should().Be(1);
    }
}
