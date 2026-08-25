// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Model; //Was previously: namespace Svg.Model;

// <summary>
// Represents a simple gradient mesh consisting of colored points.
// </summary>
internal sealed class GradientMesh
{
    // <summary>
    // List of mesh points.
    // </summary>
    public List<GradientMeshPoint> Points { get; } = new();
}

// <summary>
// Defines a single mesh point with position and color.
// </summary>
internal sealed record GradientMeshPoint(SKPoint Position, SKColor Color);
