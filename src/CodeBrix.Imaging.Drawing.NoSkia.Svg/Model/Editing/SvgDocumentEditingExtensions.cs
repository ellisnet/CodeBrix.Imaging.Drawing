// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Editing; //Was previously: namespace Svg.Model.Editing;

/// <summary>
/// Provides extension methods for editing <see cref="SvgDocument"/> instances.
/// </summary>
public static class SvgDocumentEditingExtensions
{
    /// <summary>
    /// Traverses all elements in the document tree using depth-first order.
    /// </summary>
    /// <param name="document">The SVG document to traverse.</param>
    /// <returns>An enumerable of all elements in the document tree.</returns>
    public static IEnumerable<SvgElement> TraverseElements(this SvgDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var stack = new Stack<SvgElement>();
        stack.Push(document);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.Children is null || current.Children.Count == 0)
            {
                continue;
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    /// <summary>
    /// Updates style attributes on visual elements in the document that match the specified predicate.
    /// </summary>
    /// <param name="document">The SVG document to update.</param>
    /// <param name="predicate">A function that determines which visual elements to update.</param>
    /// <param name="update">An action that modifies the matching visual elements.</param>
    /// <returns>The number of elements that were updated.</returns>
    public static int UpdateStyleAttributes(
        this SvgDocument document,
        Func<SvgVisualElement, bool> predicate,
        Action<SvgVisualElement> update)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var count = 0;
        foreach (var element in document.TraverseElements())
        {
            if (element is SvgVisualElement visual && predicate(visual))
            {
                update(visual);
                count++;
            }
        }

        return count;
    }
}
