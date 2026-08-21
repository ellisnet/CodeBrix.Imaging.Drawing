// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Defines a mechanism for creating a deep copy of an object.
/// </summary>
/// <typeparam name="T">The type of object being cloned.</typeparam>
public interface IDeepCloneable<out T>
{
    /// <summary>
    /// Creates a deep clone of the current instance.
    /// </summary>
    /// <returns>A new instance that is a deep copy of this object.</returns>
    T DeepClone();
}
