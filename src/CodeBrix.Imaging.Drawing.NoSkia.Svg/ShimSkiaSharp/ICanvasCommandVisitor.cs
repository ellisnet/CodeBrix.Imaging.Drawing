// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>Defines a visitor for processing canvas commands.</summary>
public interface ICanvasCommandVisitor
{
    /// <summary>Visits a clip path command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(ClipPathCanvasCommand cmd);
    /// <summary>Visits a clip rect command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(ClipRectCanvasCommand cmd);
    /// <summary>Visits a draw image command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(DrawImageCanvasCommand cmd);
    /// <summary>Visits a draw picture command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(DrawPictureCanvasCommand cmd);
    /// <summary>Visits a draw path command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(DrawPathCanvasCommand cmd);
    /// <summary>Visits a draw text blob command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(DrawTextBlobCanvasCommand cmd);
    /// <summary>Visits a draw text command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(DrawTextCanvasCommand cmd);
    /// <summary>Visits a draw text on path command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(DrawTextOnPathCanvasCommand cmd);
    /// <summary>Visits a restore command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(RestoreCanvasCommand cmd);
    /// <summary>Visits a save command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(SaveCanvasCommand cmd);
    /// <summary>Visits a save layer command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(SaveLayerCanvasCommand cmd);
    /// <summary>Visits a set matrix command.</summary>
    /// <param name="cmd">The command to visit.</param>
    void Visit(SetMatrixCanvasCommand cmd);
}
