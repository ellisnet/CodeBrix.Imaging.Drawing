namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// One thing a loaded SVG document asked for that the managed renderer could not do
/// exactly. Warnings are values: two warnings with the same kind and message are equal, and
/// <see cref="DrawingSvg.Warnings"/> reports each distinct one once.
/// </summary>
/// <param name="Kind">Why the document degraded - switch on this rather than parsing the message.</param>
/// <param name="Message">
/// A short human-readable description naming the feature involved (the filter primitive,
/// the shader kind); intended for logs and diagnostics, not for matching on.
/// </param>
public sealed record DrawingSvgWarning(DrawingSvgWarningKind Kind, string Message);
