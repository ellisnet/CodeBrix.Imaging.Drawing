using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Covers the scene facade - the answer to "what is this part of the picture?". The case
/// that matters most is an anchor: a hyperlink wrapping several shapes has to report the
/// area of everything it wraps, in the same coordinate space the picture is drawn in, or a
/// consumer cannot turn it into a link region.
/// </summary>
public class DrawingSvgSceneTests
{
    private const string AnchorMarkup =
@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink""
     width=""300"" height=""200"" viewBox=""0 0 300 200"">
  <a id=""link"" xlink:href=""textedit://file.tex:12"" xlink:title=""Go to source"" target=""_blank"">
    <path id=""tri"" d=""M10 10 L60 10 L60 40 Z"" fill=""green""/>
    <rect id=""box"" x=""100"" y=""60"" width=""80"" height=""30"" fill=""orange""
          transform=""rotate(30 140 75)""/>
  </a>
</svg>";

    private static DrawingSvg LoadAnchorDocument()
    {
        var svg = new DrawingSvg();
        svg.FromSvg(AnchorMarkup).Should().BeTrue();
        return svg;
    }

    [Fact]
    public void An_anchor_reports_the_union_of_everything_it_wraps()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act
        svg.Scene.TryGetNodeById("link", out DrawingSvgNode anchor).Should().BeTrue();
        svg.Scene.TryGetNodeById("tri", out DrawingSvgNode triangle).Should().BeTrue();
        svg.Scene.TryGetNodeById("box", out DrawingSvgNode box).Should().BeTrue();

        //Assert - the union of the children's own document bounds, the rotated one included
        anchor.Kind.Should().Be(DrawingSvgNodeKind.Anchor);
        anchor.DocumentBounds.Left.Should().Be(
            System.MathF.Min(triangle.DocumentBounds.Left, box.DocumentBounds.Left));
        anchor.DocumentBounds.Top.Should().Be(
            System.MathF.Min(triangle.DocumentBounds.Top, box.DocumentBounds.Top));
        anchor.DocumentBounds.Right.Should().Be(
            System.MathF.Max(triangle.DocumentBounds.Right, box.DocumentBounds.Right));
        anchor.DocumentBounds.Bottom.Should().Be(
            System.MathF.Max(triangle.DocumentBounds.Bottom, box.DocumentBounds.Bottom));

        //  a rotated child's document bounds are the axis-aligned box AROUND the rotated
        //  shape, so they cover more than its own geometry does
        box.GeometryBounds.Should().Be(DrawingRect.Create(100f, 60f, 80f, 30f));
        box.DocumentBounds.Width.Should().BeGreaterThan(box.GeometryBounds.Width);
        box.DocumentBounds.Height.Should().BeGreaterThan(box.GeometryBounds.Height);
    }

    [Fact]
    public void An_anchor_carries_its_link()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act
        svg.Scene.TryGetNodeById("link", out DrawingSvgNode anchor).Should().BeTrue();

        //Assert
        anchor.Href.Should().Be("textedit://file.tex:12");
        anchor.Title.Should().Be("Go to source");
        anchor.Target.Should().Be("_blank");
        anchor.ElementName.Should().Be("a");
    }

    [Fact]
    public void A_node_that_is_not_an_anchor_carries_no_link()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act
        svg.Scene.TryGetNodeById("tri", out DrawingSvgNode triangle).Should().BeTrue();

        //Assert
        triangle.Href.Should().BeNull();
        triangle.Title.Should().BeNull();
        triangle.Target.Should().BeNull();
    }

    [Fact]
    public void The_scene_walks_the_document_parents_before_children()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act
        List<DrawingSvgNode> nodes = svg.Scene.Traverse().ToList();

        //Assert
        nodes.Select(node => node.Kind).Should().Equal(new[]
        {
            DrawingSvgNodeKind.Fragment,
            DrawingSvgNodeKind.Anchor,
            DrawingSvgNodeKind.Path,
            DrawingSvgNodeKind.Shape,
        });
        nodes.Select(node => node.ElementName).Should().Equal(new[] { "svg", "a", "path", "rect" });
        nodes[0].Should().BeSameAs(svg.Scene.Root);
        nodes[0].Parent.Should().BeNull();
        nodes[1].Parent.Should().BeSameAs(nodes[0]);
        nodes[0].Children.Should().Equal(new[] { nodes[1] });
        nodes[1].Children.Should().Equal(new[] { nodes[2], nodes[3] });
    }

    [Fact]
    public void One_node_is_handed_out_per_compiled_element()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act
        svg.Scene.TryGetNodeById("tri", out DrawingSvgNode fromId).Should().BeTrue();
        DrawingSvgNode fromTraversal = svg.Scene.Traverse().First(node => node.Id == "tri");
        DrawingSvgNode fromChildren = svg.Scene.Root.Children[0].Children[0];

        //Assert - so a consumer can key its own state on the node it was handed
        fromTraversal.Should().BeSameAs(fromId);
        fromChildren.Should().BeSameAs(fromId);
    }

    [Fact]
    public void An_unknown_id_finds_nothing()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act + Assert
        svg.Scene.TryGetNodeById("nope", out DrawingSvgNode node).Should().BeFalse();
        node.Should().BeNull();
    }

    [Fact]
    public void A_point_inside_a_shape_hits_it()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act - well inside the triangle's filled corner
        DrawingSvgNode topmost = svg.Scene.HitTestTopmost(new DrawingPoint(55f, 20f));
        List<DrawingSvgNode> hits = svg.Scene.HitTest(new DrawingPoint(55f, 20f)).ToList();

        //Assert
        topmost.Should().NotBeNull();
        topmost.Id.Should().Be("tri");
        hits.Should().Contain(topmost);
    }

    [Fact]
    public void A_point_outside_every_shape_hits_nothing()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act + Assert
        svg.Scene.HitTestTopmost(new DrawingPoint(290f, 190f)).Should().BeNull();
    }

    [Fact]
    public void A_rectangle_finds_every_shape_it_touches()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act - a rectangle spanning the whole document
        List<string> hits = svg.Scene.HitTest(new DrawingRect(0f, 0f, 300f, 200f))
            .Select(node => node.Id).ToList();

        //Assert
        hits.Should().Contain("tri");
        hits.Should().Contain("box");
    }

    [Fact]
    public void Every_drawn_node_is_visible_and_renderable()
    {
        //Arrange
        using DrawingSvg svg = LoadAnchorDocument();

        //Act
        List<DrawingSvgNode> shapes = svg.Scene.Traverse()
            .Where(node => node.Kind is DrawingSvgNodeKind.Path or DrawingSvgNodeKind.Shape).ToList();

        //Assert
        shapes.Should().HaveCount(2);
        shapes.Should().OnlyContain(node => node.IsVisible);
        shapes.Should().OnlyContain(node => node.IsRenderable);
    }

    [Fact]
    public void A_hidden_element_is_compiled_but_not_visible()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.FromSvg(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""100"" height=""100"" viewBox=""0 0 100 100"">
  <rect id=""shown"" x=""10"" y=""10"" width=""20"" height=""20"" fill=""red""/>
  <rect id=""hidden"" x=""40"" y=""10"" width=""20"" height=""20"" fill=""blue"" visibility=""hidden""/>
</svg>").Should().BeTrue();

        //Act
        svg.Scene.TryGetNodeById("shown", out DrawingSvgNode shown).Should().BeTrue();
        svg.Scene.TryGetNodeById("hidden", out DrawingSvgNode hidden).Should().BeTrue();

        //Assert
        shown.IsVisible.Should().BeTrue();
        hidden.IsVisible.Should().BeFalse();
    }
}
