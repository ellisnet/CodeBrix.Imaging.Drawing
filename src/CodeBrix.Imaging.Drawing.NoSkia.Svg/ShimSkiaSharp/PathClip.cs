// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents a single path clip with an optional transform and nested clip.
/// </summary>
public class PathClip : ICloneable, IDeepCloneable<PathClip>
{
    /// <summary>Gets or sets the path used for clipping.</summary>
    public SKPath Path { get; set; }

    /// <summary>Gets or sets an optional transformation matrix applied to the clip.</summary>
    public SKMatrix? Transform { get; set; }

    /// <summary>Gets or sets an optional nested clip path for further clipping.</summary>
    public ClipPath Clip { get; set; }

    /// <summary>Creates a deep clone of this path clip.</summary>
    /// <returns>A new <see cref="PathClip"/> that is a deep copy.</returns>
    public PathClip Clone() => DeepClone(new CloneContext());

    /// <inheritdoc />
    public PathClip DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal PathClip DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out PathClip existing))
        {
            return existing;
        }

        var clone = new PathClip();
        context.Add(this, clone);

        clone.Path = Path?.DeepClone(context);
        clone.Transform = Transform;
        clone.Clip = Clip?.DeepClone(context);

        return clone;
    }
}
