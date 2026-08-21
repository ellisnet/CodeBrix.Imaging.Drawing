// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse.Primitives;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Model; //Was previously: namespace Svg.Model;

internal class SvgFilterResult
{
    public string Key { get; }

    public SKImageFilter Filter { get; }

    public SvgColorInterpolation ColorSpace { get; }

    public SvgFilterResult(string key, SKImageFilter filter, SvgColorInterpolation colorSpace)
    {
        Key = key;
        Filter = filter;
        ColorSpace = colorSpace;
    }
}
