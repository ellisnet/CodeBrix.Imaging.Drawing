// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents a composite clip path built from one or more path clips.
/// </summary>
public class ClipPath : ICloneable, IDeepCloneable<ClipPath>
{
    /// <summary>Gets or sets the list of path clips that compose this clip path.</summary>
    public IList<PathClip> Clips { get; set; }

    /// <summary>Gets or sets an optional transformation matrix applied to the clip path.</summary>
    public SKMatrix? Transform { get; set; }

    /// <summary>Gets or sets an optional nested clip path for further clipping.</summary>
    public ClipPath Clip { get; set; }

    /// <summary>Gets a value indicating whether this clip path has no clips.</summary>
    public bool IsEmpty => Clips is null || Clips.Count == 0;

    /// <summary>Initializes a new empty <see cref="ClipPath"/>.</summary>
    public ClipPath()
    {
        Clips = new List<PathClip>();
    }

    /// <summary>Creates a deep clone of this clip path.</summary>
    /// <returns>A new <see cref="ClipPath"/> that is a deep copy.</returns>
    public ClipPath Clone() => DeepClone(new CloneContext());

    /// <inheritdoc />
    public ClipPath DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal ClipPath DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out ClipPath existing))
        {
            return existing;
        }

        var clone = new ClipPath();
        context.Add(this, clone);

        clone.Clips = CloneHelpers.CloneList(Clips, context, clip => clip.DeepClone(context));
        clone.Transform = Transform;
        clone.Clip = Clip?.DeepClone(context);

        return clone;
    }
}
