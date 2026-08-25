// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Model; //Was previously: namespace Svg.Model;

// <summary>Specifies SVG drawing attributes that can be selectively ignored during rendering.</summary>
[Flags]
internal enum DrawAttributes
{
    // <summary>No attributes ignored.</summary>
    None = 0,
    // <summary>Ignore display attribute.</summary>
    Display = 1,
    // <summary>Ignore visibility attribute.</summary>
    Visibility = 2,
    // <summary>Ignore opacity attribute.</summary>
    Opacity = 4,
    // <summary>Ignore filter attribute.</summary>
    Filter = 8,
    // <summary>Ignore clip-path attribute.</summary>
    ClipPath = 16,
    // <summary>Ignore mask attribute.</summary>
    Mask = 32,
    // <summary>Ignore requiredFeatures attribute.</summary>
    RequiredFeatures = 64,
    // <summary>Ignore requiredExtensions attribute.</summary>
    RequiredExtensions = 128,
    // <summary>Ignore systemLanguage attribute.</summary>
    SystemLanguage = 256
}
