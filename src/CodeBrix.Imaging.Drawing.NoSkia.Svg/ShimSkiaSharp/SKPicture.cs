// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents a recorded sequence of drawing commands with a bounding rectangle.
/// </summary>
/// <param name="CullRect">The bounding rectangle of the picture.</param>
/// <param name="Commands">The list of canvas commands in the picture.</param>
public record SKPicture(SKRect CullRect, IList<CanvasCommand> Commands) : IDeepCloneable<SKPicture>
{
    /// <inheritdoc />
    public SKPicture DeepClone() => DeepClone(new CloneContext());

    internal SKPicture DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPicture existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            var clone = new SKPicture(CullRect, CloneHelpers.CloneList(Commands, context, command => command.DeepClone(context)));
            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}
