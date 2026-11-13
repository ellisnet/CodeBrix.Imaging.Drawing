using CodeBrix.Imaging.Drawing.Animation;

namespace AnimationDemoApp.Demo;

/// <summary>Small helpers for building the demo's tracks.</summary>
internal static class TrackExtensions
{
    /// <summary>Makes a track loop and returns it.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The same track.</returns>
    public static FloatTrack Loop(this FloatTrack track)
    {
        track.LoopMode = TrackLoopMode.Loop;
        return track;
    }

    /// <summary>Makes a track loop and returns it.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The same track.</returns>
    public static PointTrack Loop(this PointTrack track)
    {
        track.LoopMode = TrackLoopMode.Loop;
        return track;
    }

    /// <summary>Makes a track loop and returns it.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The same track.</returns>
    public static ColorTrack Loop(this ColorTrack track)
    {
        track.LoopMode = TrackLoopMode.Loop;
        return track;
    }
}
