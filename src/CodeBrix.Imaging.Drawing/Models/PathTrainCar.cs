using System;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// One element of a <see cref="PathTrain"/> and its own place in the train. Its gap, delay and
/// rolling radius start from the train's options and may be changed afterwards - each frame reads
/// them - for trains whose elements are spaced or roll differently (letters of different widths).
/// Change them from the thread that renders.
/// </summary>
public sealed class PathTrainCar
{
    private float _gap;
    private double _delay;
    private float _rollRadius;

    internal PathTrainCar(DrawingElement element, int index, int rank, float gap, double delay, float rollRadius, PointF? reference)
    {
        Element = element;
        Index = index;
        Rank = rank;
        _gap = gap;
        _delay = delay;
        _rollRadius = rollRadius;
        Reference = reference;
    }

    /// <summary>The element.</summary>
    public DrawingElement Element { get; }

    /// <summary>Its position in the list the train was made from.</summary>
    public int Index { get; }

    /// <summary>Its rank in the train: 0 leads, 1 follows it, and so on.</summary>
    public int Rank { get; }

    /// <summary>How far behind the lead it travels along the path, calibrated units (before the spacing scale); 0 or more.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative or non-finite value.</exception>
    public float Gap
    {
        get => _gap;
        set
        {
            if (!Single.IsFinite(value) || value < 0f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _gap = value;
        }
    }

    /// <summary>How much later, in seconds, it follows the progress track than the lead; 0 or more.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative or non-finite value.</exception>
    public double Delay
    {
        get => _delay;
        set
        {
            if (!Double.IsFinite(value) || value < 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _delay = value;
        }
    }

    /// <summary>Its rolling radius, calibrated units (negative rolls the other way; 0 = no roll).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite.</exception>
    public float RollRadius
    {
        get => _rollRadius;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _rollRadius = value;
        }
    }

    //The point of the path that maps to the element's anchor: null = the anchor itself (the anchor
    //  rides the path), or the path's end (the element travels a copy of the path ending at its anchor)
    internal PointF? Reference { get; }
}
