namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// The explicit choice of how a <c>DrawingSession.CreateForImage</c> factory method sets
/// the session's calibrated drawing space. There is no default - callers state which
/// behavior they want, so a session's calibration size is never a surprise.
/// </summary>
public enum CalibrationSizing
{
    /// <summary>
    /// Use the <see cref="DrawingSessionOptions.CalibrationSize"/> from the provided
    /// options exactly as given (or the documented 1000 x 1000 default when no options are
    /// provided). Choose this when the calibration space is decided by the application;
    /// note that a background image whose aspect ratio differs from the calibration
    /// space's is stretched to fill the drawing rectangle.
    /// </summary>
    FromOptions = 0,

    /// <summary>
    /// Derive the calibration size from the background image's aspect ratio, with the
    /// longest side set to <see cref="DrawingSession.CalibrationLongSide"/> units - so the
    /// image displays and exports without distortion. Any
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> in the provided options is not
    /// used.
    /// </summary>
    DeriveFromBackgroundImage = 1,
}
