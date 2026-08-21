// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Diagnostics;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>Represents an abstract canvas drawing command.</summary>
public abstract record CanvasCommand : IDeepCloneable<CanvasCommand>
{
    /// <inheritdoc />
    public CanvasCommand DeepClone() => DeepClone(new CloneContext());

    internal CanvasCommand DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out CanvasCommand existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            CanvasCommand clone = this switch
            {
                ClipPathCanvasCommand clipPathCanvasCommand => new ClipPathCanvasCommand(clipPathCanvasCommand.ClipPath?.DeepClone(context), clipPathCanvasCommand.Operation, clipPathCanvasCommand.Antialias),
                ClipRectCanvasCommand clipRectCanvasCommand => new ClipRectCanvasCommand(clipRectCanvasCommand.Rect, clipRectCanvasCommand.Operation, clipRectCanvasCommand.Antialias),
                DrawImageCanvasCommand drawImageCanvasCommand => new DrawImageCanvasCommand(drawImageCanvasCommand.Image?.DeepClone(context), drawImageCanvasCommand.Source, drawImageCanvasCommand.Dest, drawImageCanvasCommand.Paint?.DeepClone(context)),
                DrawPictureCanvasCommand drawPictureCanvasCommand => new DrawPictureCanvasCommand(drawPictureCanvasCommand.Picture?.DeepClone(context)),
                DrawPathCanvasCommand drawPathCanvasCommand => new DrawPathCanvasCommand(drawPathCanvasCommand.Path?.DeepClone(context), drawPathCanvasCommand.Paint?.DeepClone(context)),
                DrawTextBlobCanvasCommand drawTextBlobCanvasCommand => new DrawTextBlobCanvasCommand(drawTextBlobCanvasCommand.TextBlob?.DeepClone(context), drawTextBlobCanvasCommand.X, drawTextBlobCanvasCommand.Y, drawTextBlobCanvasCommand.Paint?.DeepClone(context)),
                DrawTextCanvasCommand drawTextCanvasCommand => new DrawTextCanvasCommand(drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, drawTextCanvasCommand.Paint?.DeepClone(context)),
                DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand => new DrawTextOnPathCanvasCommand(drawTextOnPathCanvasCommand.Text, drawTextOnPathCanvasCommand.Path?.DeepClone(context), drawTextOnPathCanvasCommand.HOffset, drawTextOnPathCanvasCommand.VOffset, drawTextOnPathCanvasCommand.Paint?.DeepClone(context)),
                RestoreCanvasCommand restoreCanvasCommand => new RestoreCanvasCommand(restoreCanvasCommand.Count),
                SaveCanvasCommand saveCanvasCommand => new SaveCanvasCommand(saveCanvasCommand.Count),
                SaveLayerCanvasCommand saveLayerCanvasCommand => new SaveLayerCanvasCommand(saveLayerCanvasCommand.Count, saveLayerCanvasCommand.Paint?.DeepClone(context)),
                SetMatrixCanvasCommand setMatrixCanvasCommand => new SetMatrixCanvasCommand(setMatrixCanvasCommand.DeltaMatrix, setMatrixCanvasCommand.TotalMatrix),
                _ => throw new NotSupportedException($"Unsupported {nameof(CanvasCommand)} type: {GetType().Name}.")
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

/// <summary>A canvas command that clips drawing to a path.</summary>
/// <param name="ClipPath">The clip path.</param>
/// <param name="Operation">The clip operation.</param>
/// <param name="Antialias">Whether to apply antialiasing to the clip.</param>
public record ClipPathCanvasCommand(ClipPath ClipPath, SKClipOperation Operation, bool Antialias) : CanvasCommand;

/// <summary>A canvas command that clips drawing to a rectangle.</summary>
/// <param name="Rect">The clip rectangle.</param>
/// <param name="Operation">The clip operation.</param>
/// <param name="Antialias">Whether to apply antialiasing to the clip.</param>
public record ClipRectCanvasCommand(SKRect Rect, SKClipOperation Operation, bool Antialias) : CanvasCommand;

/// <summary>A canvas command that draws an image.</summary>
/// <param name="Image">The image to draw.</param>
/// <param name="Source">The source rectangle within the image.</param>
/// <param name="Dest">The destination rectangle on the canvas.</param>
/// <param name="Paint">The optional paint to apply.</param>
public record DrawImageCanvasCommand(SKImage Image, SKRect Source, SKRect Dest, SKPaint Paint = null) : CanvasCommand;

/// <summary>A canvas command that draws a recorded picture.</summary>
/// <param name="Picture">The picture to draw.</param>
public record DrawPictureCanvasCommand(SKPicture Picture) : CanvasCommand;

/// <summary>A canvas command that draws a path with paint.</summary>
/// <param name="Path">The path to draw.</param>
/// <param name="Paint">The paint to use for drawing.</param>
public record DrawPathCanvasCommand(SKPath Path, SKPaint Paint) : CanvasCommand;

/// <summary>A canvas command that draws a text blob.</summary>
/// <param name="TextBlob">The text blob to draw.</param>
/// <param name="X">The X coordinate for the text origin.</param>
/// <param name="Y">The Y coordinate for the text origin.</param>
/// <param name="Paint">The paint to use for drawing.</param>
public record DrawTextBlobCanvasCommand(SKTextBlob TextBlob, float X, float Y, SKPaint Paint) : CanvasCommand;

/// <summary>A canvas command that draws text at a position.</summary>
/// <param name="Text">The text to draw.</param>
/// <param name="X">The X coordinate for the text origin.</param>
/// <param name="Y">The Y coordinate for the text origin.</param>
/// <param name="Paint">The paint to use for drawing.</param>
public record DrawTextCanvasCommand(string Text, float X, float Y, SKPaint Paint) : CanvasCommand;

/// <summary>A canvas command that draws text along a path.</summary>
/// <param name="Text">The text to draw.</param>
/// <param name="Path">The path along which to draw the text.</param>
/// <param name="HOffset">The horizontal offset along the path.</param>
/// <param name="VOffset">The vertical offset from the path.</param>
/// <param name="Paint">The paint to use for drawing.</param>
public record DrawTextOnPathCanvasCommand(string Text, SKPath Path, float HOffset, float VOffset, SKPaint Paint) : CanvasCommand;

/// <summary>A canvas command that restores the canvas state.</summary>
/// <param name="Count">The save count after the restore.</param>
public record RestoreCanvasCommand(int Count) : CanvasCommand;

/// <summary>A canvas command that saves the canvas state.</summary>
/// <param name="Count">The save count at the time of saving.</param>
public record SaveCanvasCommand(int Count) : CanvasCommand;

/// <summary>A canvas command that saves the canvas state with an optional paint layer.</summary>
/// <param name="Count">The save count at the time of saving.</param>
/// <param name="Paint">The optional paint for the layer.</param>
public record SaveLayerCanvasCommand(int Count, SKPaint Paint = null) : CanvasCommand;

/// <summary>A canvas command that sets the transformation matrix.</summary>
/// <param name="DeltaMatrix">The delta matrix applied.</param>
/// <param name="TotalMatrix">The resulting total matrix.</param>
public record SetMatrixCanvasCommand(SKMatrix DeltaMatrix, SKMatrix TotalMatrix) : CanvasCommand;

/// <summary>Represents a recording canvas that captures drawing commands.</summary>
public class SKCanvas : ICloneable, IDeepCloneable<SKCanvas>
{
    private int _saveCount;
    private readonly Stack<SKMatrix> _totalMatrices = new();

    /// <summary>Gets the list of recorded canvas commands.</summary>
    public IList<CanvasCommand> Commands { get; }

    /// <summary>Gets the current total transformation matrix.</summary>
    public SKMatrix TotalMatrix { get; private set; }

    internal SKCanvas(IList<CanvasCommand> commands, SKMatrix totalMatrix)
    {
        Commands = commands;
        TotalMatrix = totalMatrix;
    }

    /// <summary>Creates a deep clone of this canvas.</summary>
    /// <returns>A new <see cref="SKCanvas"/> instance.</returns>
    public SKCanvas Clone() => DeepClone(new CloneContext());

    /// <inheritdoc />
    public SKCanvas DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKCanvas DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKCanvas existing))
        {
            return existing;
        }

        var commands = Commands is null
            ? new List<CanvasCommand>()
            : CloneHelpers.CloneList(Commands, context, command => command.DeepClone(context)) ?? new List<CanvasCommand>();

        var clone = new SKCanvas(commands, TotalMatrix)
        {
            _saveCount = _saveCount
        };
        context.Add(this, clone);

        if (_totalMatrices.Count > 0)
        {
            var matrices = _totalMatrices.ToArray();
            for (var i = matrices.Length - 1; i >= 0; i--)
            {
                clone._totalMatrices.Push(matrices[i]);
            }
        }

        return clone;
    }

    /// <summary>Clips drawing to the specified path.</summary>
    /// <param name="clipPath">The clip path.</param>
    /// <param name="operation">The clip operation.</param>
    /// <param name="antialias">Whether to apply antialiasing.</param>
    public void ClipPath(ClipPath clipPath, SKClipOperation operation = SKClipOperation.Intersect, bool antialias = false)
    {
        Commands?.Add(new ClipPathCanvasCommand(clipPath, operation, antialias));
    }

    /// <summary>Clips drawing to the specified rectangle.</summary>
    /// <param name="rect">The clip rectangle.</param>
    /// <param name="operation">The clip operation.</param>
    /// <param name="antialias">Whether to apply antialiasing.</param>
    public void ClipRect(SKRect rect, SKClipOperation operation = SKClipOperation.Intersect, bool antialias = false)
    {
        Commands?.Add(new ClipRectCanvasCommand(rect, operation, antialias));
    }

    /// <summary>Draws an image within the specified source and destination rectangles.</summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="source">The source rectangle within the image.</param>
    /// <param name="dest">The destination rectangle on the canvas.</param>
    /// <param name="paint">The optional paint to apply.</param>
    public void DrawImage(SKImage image, SKRect source, SKRect dest, SKPaint paint = null)
    {
        Commands?.Add(new DrawImageCanvasCommand(image, source, dest, paint));
    }

    /// <summary>Draws a recorded picture.</summary>
    /// <param name="picture">The picture to draw.</param>
    public void DrawPicture(SKPicture picture)
    {
        Commands?.Add(new DrawPictureCanvasCommand(picture));
    }

    /// <summary>Draws a path with the specified paint.</summary>
    /// <param name="path">The path to draw.</param>
    /// <param name="paint">The paint to use for drawing.</param>
    public void DrawPath(SKPath path, SKPaint paint)
    {
        Commands?.Add(new DrawPathCanvasCommand(path, paint));
    }

    /// <summary>Draws a text blob at the specified position.</summary>
    /// <param name="textBlob">The text blob to draw.</param>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <param name="paint">The paint to use for drawing.</param>
    public void DrawText(SKTextBlob textBlob, float x, float y, SKPaint paint)
    {
        Commands?.Add(new DrawTextBlobCanvasCommand(textBlob, x, y, paint));
    }

    /// <summary>Draws text at the specified position.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <param name="paint">The paint to use for drawing.</param>
    public void DrawText(string text, float x, float y, SKPaint paint)
    {
        Commands?.Add(new DrawTextCanvasCommand(text, x, y, paint));
    }

    /// <summary>Draws text along the specified path.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="path">The path along which to draw the text.</param>
    /// <param name="hOffset">The horizontal offset along the path.</param>
    /// <param name="vOffset">The vertical offset from the path.</param>
    /// <param name="paint">The paint to use for drawing.</param>
    public void DrawTextOnPath(string text, SKPath path, float hOffset, float vOffset, SKPaint paint)
    {
        Commands?.Add(new DrawTextOnPathCanvasCommand(text, path, hOffset, vOffset, paint));
    }

    /// <summary>Sets the transformation matrix by concatenating with the current matrix.</summary>
    /// <param name="deltaMatrix">The delta matrix to apply.</param>
    public void SetMatrix(SKMatrix deltaMatrix)
    {
        TotalMatrix = TotalMatrix.PreConcat(deltaMatrix);
        Commands?.Add(new SetMatrixCanvasCommand(deltaMatrix, TotalMatrix));
    }

    /// <summary>Saves the current canvas state and returns the save count.</summary>
    /// <returns>The save count after the save operation.</returns>
    public int Save()
    {
        _totalMatrices.Push(TotalMatrix);
        Commands?.Add(new SaveCanvasCommand(_saveCount));
        _saveCount++;
        return _saveCount;
    }

    /// <summary>Saves the current canvas state with a layer defined by the specified paint.</summary>
    /// <param name="paint">The paint for the layer.</param>
    /// <returns>The save count after the save operation.</returns>
    public int SaveLayer(SKPaint paint)
    {
        _totalMatrices.Push(TotalMatrix);
        Commands?.Add(new SaveLayerCanvasCommand(_saveCount, paint));
        _saveCount++;
        return _saveCount;
    }

    /// <summary>Restores the canvas to the previously saved state.</summary>
    public void Restore()
    {
        if (_totalMatrices.Count == 0)
        {
            Debug.WriteLine($"Invalid Save and Restore balance.");
        }
        else
        {
            TotalMatrix = _totalMatrices.Pop();
            _saveCount--;
        }

        Commands?.Add(new RestoreCanvasCommand(_saveCount));
    }
}
