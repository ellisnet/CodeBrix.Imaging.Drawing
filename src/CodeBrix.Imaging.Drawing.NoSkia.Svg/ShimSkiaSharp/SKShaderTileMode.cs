// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies how a shader tiles when coordinates fall outside its bounds.
// </summary>
internal enum SKShaderTileMode
{
    // <summary>Clamp to the edge color.</summary>
    Clamp = 0,
    // <summary>Repeat the shader pattern.</summary>
    Repeat = 1,
    // <summary>Mirror the shader pattern at boundaries.</summary>
    Mirror = 2,
    // <summary>Render transparent outside the bounds.</summary>
    Decal = 3
}
