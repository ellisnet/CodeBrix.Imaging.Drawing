namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// One segment of a path as <see cref="DrawingPath.GetVerbs"/> reports it: the kind of
/// segment, and the points that kind carries - one for
/// <see cref="DrawingPathVerb.Move"/> and <see cref="DrawingPathVerb.Line"/>, two
/// (control, end) for <see cref="DrawingPathVerb.Quad"/>, three (control, control, end)
/// for <see cref="DrawingPathVerb.Cubic"/>, and none for
/// <see cref="DrawingPathVerb.Close"/>.
/// </summary>
/// <param name="Verb">The kind of segment.</param>
/// <param name="Points">The segment's points, in order; never <c>null</c>.</param>
public readonly record struct DrawingPathSegment(DrawingPathVerb Verb, DrawingPoint[] Points);
