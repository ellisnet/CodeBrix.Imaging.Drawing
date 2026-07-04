namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// The base type of everything that can be placed on a <see cref="DrawingLayer"/>: a
/// freehand <see cref="Stroke"/>, or a geometric shape derived from
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.DrawingShape"/>. Elements on a layer render
/// in the order they were added.
/// </summary>
public abstract class DrawingElement
{
    private protected DrawingElement()
    {
        //Only Stroke and DrawingShape derive directly from this type; custom element
        //  kinds derive from DrawingShape, whose Draw method renderers know how to call
    }
}
