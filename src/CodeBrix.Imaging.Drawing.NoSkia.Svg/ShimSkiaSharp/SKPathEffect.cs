// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Base class for path effects that modify the geometry of stroked paths.
// </summary>
internal abstract record SKPathEffect : IDeepCloneable<SKPathEffect>
{
    // <summary>
    // Creates a dash path effect.
    // </summary>
    // <param name="intervals">An array of on/off dash lengths.</param>
    // <param name="phase">The offset into the dash pattern.</param>
    // <returns>A new <see cref="DashPathEffect"/>.</returns>
    public static SKPathEffect CreateDash(float[] intervals, float phase)
        => new DashPathEffect(intervals, phase);

    // <inheritdoc />
    public SKPathEffect DeepClone() => DeepClone(new CloneContext());

    internal SKPathEffect DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPathEffect existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            var clone = this switch
            {
                DashPathEffect dashPathEffect => new DashPathEffect(CloneHelpers.CloneArray(dashPathEffect.Intervals, context), dashPathEffect.Phase),
                _ => throw new NotSupportedException($"Unsupported {nameof(SKPathEffect)} type: {GetType().Name}.")
            };

            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}

// <summary>
// A path effect that draws dashed lines with alternating on/off intervals.
// </summary>
// <param name="Intervals">An array of on/off dash lengths.</param>
// <param name="Phase">The offset into the dash pattern.</param>
internal record DashPathEffect(float[] Intervals, float Phase) : SKPathEffect;
