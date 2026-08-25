// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.Editing; //Was previously: namespace ShimSkiaSharp.Editing;

// <summary>
// Provides extension methods for dispatching <see cref="CanvasCommand"/> instances to an <see cref="ICanvasCommandVisitor"/>.
// </summary>
internal static class CanvasCommandVisitorExtensions
{
    // <summary>
    // Dispatches the canvas command to the appropriate visitor method.
    // </summary>
    // <param name="command">The canvas command to dispatch.</param>
    // <param name="visitor">The visitor to receive the command.</param>
    public static void Accept(this CanvasCommand command, ICanvasCommandVisitor visitor)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (visitor is null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        switch (command)
        {
            case ClipPathCanvasCommand clipPath:
                visitor.Visit(clipPath);
                break;
            case ClipRectCanvasCommand clipRect:
                visitor.Visit(clipRect);
                break;
            case DrawImageCanvasCommand drawImage:
                visitor.Visit(drawImage);
                break;
            case DrawPictureCanvasCommand drawPicture:
                visitor.Visit(drawPicture);
                break;
            case DrawPathCanvasCommand drawPath:
                visitor.Visit(drawPath);
                break;
            case DrawTextBlobCanvasCommand drawTextBlob:
                visitor.Visit(drawTextBlob);
                break;
            case DrawTextCanvasCommand drawText:
                visitor.Visit(drawText);
                break;
            case DrawTextOnPathCanvasCommand drawTextOnPath:
                visitor.Visit(drawTextOnPath);
                break;
            case RestoreCanvasCommand restore:
                visitor.Visit(restore);
                break;
            case SaveCanvasCommand save:
                visitor.Visit(save);
                break;
            case SaveLayerCanvasCommand saveLayer:
                visitor.Visit(saveLayer);
                break;
            case SetMatrixCanvasCommand setMatrix:
                visitor.Visit(setMatrix);
                break;
            default:
                throw new NotSupportedException($"Unsupported {nameof(CanvasCommand)} type: {command.GetType().Name}.");
        }
    }
}
