using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

// VENDORING SHIM: The CodeBrix.SvgParse package grants InternalsVisibleTo only to the
// original "CodeBrix.SkiaSvg" assembly. This vendored assembly has a different name, so
// the three internal members the vendored code relied on are re-provided here:
//   1. SvgElementAddress - an internal SvgParse class built entirely on public API; a
//      verbatim local copy lives below (it shadows the inaccessible SvgParse one because
//      types in the containing namespace take precedence over using-imported types).
//   2. SvgElement._parent (internal field) - accessed via cached reflection, used both to
//      temporarily re-parent an element during compilation (WithTemporaryParent) and to
//      scope anchor CSS inheritance in SvgSceneTextCompiler.
internal static class SvgParseInternals
{
    private static readonly FieldInfo ParentField =
        typeof(SvgElement).GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(SvgElement).FullName, "_parent");

    internal static void SetParent(SvgElement element, SvgElement parent)
    {
        ParentField.SetValue(element, parent);
    }

    private static SvgElement GetParent(SvgElement element)
    {
        return (SvgElement)ParentField.GetValue(element);
    }

    // Mirrors the internal SvgElement.WithTemporaryParent helper from CodeBrix.SvgParse.
    internal static TResult WithTemporaryParent<TResult>(SvgElement element, SvgElement temporaryParent, Func<TResult> factory)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (temporaryParent is null)
        {
            throw new ArgumentNullException(nameof(temporaryParent));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        var originalParent = GetParent(element);
        try
        {
            SetParent(element, temporaryParent);
            return factory();
        }
        finally
        {
            SetParent(element, originalParent);
        }
    }
}

// Verbatim vendored copy of the internal CodeBrix.SvgParse.SvgElementAddress class
// (it only uses public SvgParse API: Parent, Children, IndexOf).
internal sealed class SvgElementAddress
{
    public SvgElementAddress(int[] childIndexes)
    {
        ChildIndexes = childIndexes;
    }

    public int[] ChildIndexes { get; }

    public string Key => string.Join("/", ChildIndexes.Select(static index => index.ToString(CultureInfo.InvariantCulture)));

    public static SvgElementAddress Create(SvgElement element)
    {
        var indexes = new Stack<int>();
        var current = element;

        while (current.Parent is { } parent)
        {
            indexes.Push(parent.Children.IndexOf(current));
            current = parent;
        }

        return new SvgElementAddress(indexes.ToArray());
    }

    public SvgElement Resolve(SvgDocument document)
    {
        SvgElement current = document;

        foreach (var childIndex in ChildIndexes)
        {
            if (childIndex < 0 || childIndex >= current.Children.Count)
            {
                return null;
            }

            current = current.Children[childIndex];
        }

        return current;
    }
}
