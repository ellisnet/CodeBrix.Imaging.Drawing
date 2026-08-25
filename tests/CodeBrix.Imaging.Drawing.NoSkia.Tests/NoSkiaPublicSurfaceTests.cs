using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Guards the promise the package name makes: a developer who chose the NoSkia package must
/// never meet the word "Skia" and be left wondering whether the package really is Skia-free.
/// That covers everything a developer can actually see - the exported type and member NAMES
/// of both assemblies in the package, and the XML documentation that ships beside them and
/// drives every tooltip their editor shows.
/// <para>
/// The one legitimate occurrence is the substring inside "NoSkia" itself (the assembly name,
/// the namespace, and the types named for them), so every check here ignores a "Skia" that
/// is preceded by "No".
/// </para>
/// <para>
/// The SVG assembly is vendored from a Skia-named lineage, so ITS guard is the exported-type
/// list below: everything except the facade is internal, and a re-vendor that re-publicises a
/// type fails here rather than in a consumer's editor.
/// </para>
/// </summary>
public class NoSkiaPublicSurfaceTests
{
    //Matches a "Skia" that is NOT the tail of "NoSkia" - the assembly name and namespace are
    //  the only place the substring is allowed to appear
    private static readonly Regex SkiaMention = new Regex(@"(?<!No)Skia", RegexOptions.Compiled);

    private const string CoreDocumentationFile = "CodeBrix.Imaging.Drawing.NoSkia.xml";

    private const string SvgDocumentationFile = "CodeBrix.Imaging.Drawing.NoSkia.Svg.xml";

    //The whole consumer story of the SVG assembly: the facade, its scene and warning types,
    //  and the font registry. Nothing else is exported.
    private static readonly string[] ExportedSvgTypes =
    {
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvg",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvgNode",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvgNodeKind",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvgScene",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvgTextEmission",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvgWarning",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvgWarningKind",
        "CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering.NoSkiaFontRegistry",
    };

    private static Assembly CoreAssembly => typeof(DrawingCanvas).Assembly;

    private static Assembly SvgAssembly => typeof(DrawingSvg).Assembly;

    /// <summary>The XML documentation files the package ships.</summary>
    public static IEnumerable<object[]> DocumentationFiles()
    {
        yield return new object[] { CoreDocumentationFile };
        yield return new object[] { SvgDocumentationFile };
    }

    /// <summary>Both of the package's assemblies, as xUnit theory data.</summary>
    public static IEnumerable<object[]> PackagedAssemblies()
    {
        yield return new object[] { CoreAssembly.GetName().Name };
        yield return new object[] { SvgAssembly.GetName().Name };
    }

    private static Assembly Resolve(string assemblyName)
        => assemblyName == CoreAssembly.GetName().Name ? CoreAssembly : SvgAssembly;

    [Theory]
    [MemberData(nameof(DocumentationFiles))]
    public void Both_assemblies_ship_an_xml_documentation_file(string fileName)
    {
        //Arrange
        string path = Path.Combine(AppContext.BaseDirectory, fileName);

        //Act
        bool exists = File.Exists(path);

        //Assert - without this the documentation scan below could pass by finding nothing
        exists.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(DocumentationFiles))]
    public void No_packaged_documentation_mentions_skia(string fileName)
    {
        //Arrange
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, fileName));

        //Act
        List<string> offenders = lines
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => SkiaMention.IsMatch(entry.Line))
            .Select(entry => $"{fileName}({entry.Number}): {entry.Line.Trim()}")
            .ToList();

        //Assert
        offenders.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(PackagedAssemblies))]
    public void No_exported_type_is_skia_named(string assemblyName)
    {
        //Arrange
        Assembly assembly = Resolve(assemblyName);

        //Act
        List<string> offenders = assembly.GetExportedTypes()
            .Where(type => type.Name.StartsWith("SK", StringComparison.Ordinal)
                || SkiaMention.IsMatch(type.Name))
            .Select(type => type.FullName)
            .ToList();

        //Assert
        offenders.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(PackagedAssemblies))]
    public void No_exported_member_is_skia_named(string assemblyName)
    {
        //Arrange
        Assembly assembly = Resolve(assemblyName);
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        //Act
        List<string> offenders = assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(Flags).Select(member => (Type: type, Member: member)))
            .Where(entry => entry.Member.Name.Contains("SK", StringComparison.Ordinal)
                || SkiaMention.IsMatch(entry.Member.Name))
            .Select(entry => $"{entry.Type.Name}.{entry.Member.Name}")
            .Distinct()
            .ToList();

        //Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void The_svg_assembly_exports_the_facade_and_nothing_else()
    {
        //Arrange + Act
        List<string> exported = SvgAssembly.GetExportedTypes()
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        //Assert - the vendored scene compiler and shim stay behind the facade
        exported.Should().Equal(ExportedSvgTypes.OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Every_command_visitor_method_has_a_default_implementation()
    {
        //Arrange + Act - a visitor that implements nothing must compile and run, so that a
        //  command kind added later never breaks a consumer's visitor
        List<string> abstractMethods = typeof(IDrawingCommandVisitor)
            .GetMethods()
            .Where(method => method.IsAbstract)
            .Select(method => method.Name)
            .ToList();

        //Assert
        abstractMethods.Should().BeEmpty();
    }
}
