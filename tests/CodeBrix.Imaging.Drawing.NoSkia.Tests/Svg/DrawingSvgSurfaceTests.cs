using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Guards the promise the facade makes: a consumer of the loaded document never has to
/// name - or even be able to see - the intermediate types the SVG compiler works in. Every
/// type reachable from the facade's own signatures is a Drawing type, a parsed-SVG type, or
/// a framework type; none of them comes from the compiler's shim namespace.
/// </summary>
public class DrawingSvgSurfaceTests
{
    private const string ShimNamespace = "CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp";

    /// <summary>The facade types, as xUnit theory data.</summary>
    public static IEnumerable<object[]> FacadeTypes()
    {
        yield return new object[] { typeof(DrawingSvg) };
        yield return new object[] { typeof(DrawingSvgScene) };
        yield return new object[] { typeof(DrawingSvgNode) };
        yield return new object[] { typeof(DrawingSvgNodeKind) };
        yield return new object[] { typeof(DrawingSvgWarning) };
        yield return new object[] { typeof(DrawingSvgWarningKind) };
        yield return new object[] { typeof(DrawingSvgTextEmission) };
    }

    private static IEnumerable<(string Member, Type Type)> SignatureTypes(Type type)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (PropertyInfo property in type.GetProperties(Flags))
        {
            yield return (property.Name, property.PropertyType);
        }

        foreach (FieldInfo field in type.GetFields(Flags))
        {
            yield return (field.Name, field.FieldType);
        }

        foreach (MethodInfo method in type.GetMethods(Flags))
        {
            yield return (method.Name, method.ReturnType);
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                yield return ($"{method.Name}({parameter.Name})", parameter.ParameterType);
            }
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(Flags))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                yield return ($".ctor({parameter.Name})", parameter.ParameterType);
            }
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        Type element = type.IsByRef || type.IsArray || type.IsPointer ? type.GetElementType() : type;
        if (element == null) { yield break; }

        yield return element;
        if (element.IsGenericType)
        {
            foreach (Type argument in element.GetGenericArguments())
            {
                foreach (Type nested in Unwrap(argument)) { yield return nested; }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FacadeTypes))]
    public void No_facade_signature_names_a_compiler_shim_type(Type facadeType)
    {
        //Arrange + Act
        List<string> offenders = SignatureTypes(facadeType)
            .SelectMany(entry => Unwrap(entry.Type).Select(type => (entry.Member, Type: type)))
            .Where(entry => entry.Type.Namespace == ShimNamespace)
            .Select(entry => $"{facadeType.Name}.{entry.Member} : {entry.Type.Name}")
            .Distinct()
            .ToList();

        //Assert
        offenders.Should().BeEmpty();
    }
}
