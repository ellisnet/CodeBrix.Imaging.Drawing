using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// A formation turned into session elements by
/// <see cref="DrawingAnimations.BindFormation"/>: for each member, a RESTING element (on the
/// resting layer, existing from its arrival time on) and, for a member that travels, the
/// TRAVELLING elements (on the travelling layer, existing only while it travels, posed every apply
/// from its path). Members are added in arrival order, so a layer's live cache only draws the new
/// arrivals as time plays forward, and seeking anywhere needs no rebuilding.
/// </summary>
public sealed class FormationElements
{
    private readonly FormationMember[] _members;
    private readonly DrawingElement[] _resting;
    private readonly DrawingElement[][] _travelling;
    private readonly int[] _order;

    internal FormationElements(FormationMember[] members, DrawingElement[] resting, DrawingElement[][] travelling, int[] order)
    {
        _members = members;
        _resting = resting;
        _travelling = travelling;
        _order = order;
    }

    /// <summary>The number of members.</summary>
    public int Count => _members.Length;

    /// <summary>The members, in the order they were given.</summary>
    public IReadOnlyList<FormationMember> Members => _members;

    /// <summary>The members' indices (in <see cref="Members"/>) in arrival order - the order their elements were added.</summary>
    public IReadOnlyList<int> ArrivalOrder => _order;

    /// <summary>The binding that poses the travelling elements (remove it to stop them moving).</summary>
    public AnimationBinding Binding { get; internal set; }

    /// <summary>A member's resting element.</summary>
    /// <param name="index">The member's index (in <see cref="Members"/>).</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public DrawingElement GetResting(int index)
    {
        if (index < 0 || index >= _members.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
        return _resting[index];
    }

    /// <summary>A member's travelling elements (none for a member that just appears).</summary>
    /// <param name="index">The member's index (in <see cref="Members"/>).</param>
    /// <returns>The elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public IReadOnlyList<DrawingElement> GetTravelling(int index)
    {
        if (index < 0 || index >= _members.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
        return _travelling[index];
    }

    /// <summary>How many members are on their way at a time.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The count.</returns>
    public int GetTravellingCount(double seconds)
    {
        int count = 0;
        foreach (FormationMember member in _members)
        {
            if (member.IsTravellingAt(seconds)) { count++; }
        }
        return count;
    }

    internal void Apply(double seconds)
    {
        for (int i = 0; i < _members.Length; i++)
        {
            FormationMember member = _members[i];
            DrawingElement[] parts = _travelling[i];
            if (parts.Length == 0 || !member.IsTravellingAt(seconds)) { continue; }
            DrawingTransform pose = member.Path.GetPoseAt(seconds).ToTransform();
            foreach (DrawingElement part in parts) { part.Transform = pose; }
        }
    }
}
