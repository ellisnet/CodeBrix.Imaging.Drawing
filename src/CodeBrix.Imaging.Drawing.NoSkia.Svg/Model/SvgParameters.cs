// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Model; //Was previously: namespace Svg.Model;

// <summary>
// Represents parameters for SVG document parsing, including entity definitions and CSS styles.
// </summary>
// <param name="Entities">A dictionary of entity name-value pairs to substitute during parsing.</param>
// <param name="Css">Additional CSS styles to apply to the SVG document.</param>
internal readonly record struct SvgParameters(Dictionary<string, string> Entities, string Css);
