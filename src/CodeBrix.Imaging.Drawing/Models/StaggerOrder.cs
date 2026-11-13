namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>The order a staggered animation (<see cref="DrawingAnimations.BindStagger"/>) starts its elements in.</summary>
public enum StaggerOrder
{
    /// <summary>First element first (reading order for text pieces).</summary>
    Forward = 0,

    /// <summary>Last element first.</summary>
    Reverse = 1,

    /// <summary>The middle element(s) first, spreading out to both ends.</summary>
    CenterOut = 2,
}
