using System;

namespace CodeBrix.Imaging.Drawing.Models;

//The public surface states every angle in DEGREES, clockwise on screen from +x (y points down) -
//  the unit of DrawingTransform.Rotation, DrawingCamera.Rotation and PathPoint.Angle. The trig
//  inside works in radians; these two conversions are the only places the units meet, so every
//  type converts with the same arithmetic.
internal static class AngleUnits
{
    internal const double RadiansPerDegree = Math.PI / 180.0;

    internal const double DegreesPerRadian = 180.0 / Math.PI;

    internal static double ToRadians(double degrees) => degrees * RadiansPerDegree;

    internal static float ToDegrees(double radians) => (float)(radians * DegreesPerRadian);
}
