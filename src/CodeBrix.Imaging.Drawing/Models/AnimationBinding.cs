using System;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// One entry of a <see cref="DrawingAnimations"/> list: something that, given a time, sets a
/// value - a keyframe track bound to a property, or a callback of the caller's own. Returned
/// by every <c>Bind</c> call so it can be removed again.
/// </summary>
public sealed class AnimationBinding
{
    private readonly Action<double> _apply;

    internal AnimationBinding(object target, AnimatedProperty? property, Action<double> apply)
        : this(target, property, apply, BindingMode.Replace)
    {
    }

    internal AnimationBinding(object target, AnimatedProperty? property, Action<double> apply, BindingMode mode)
    {
        Target = target;
        Property = property;
        _apply = apply;
        Mode = mode;
    }

    /// <summary>What the binding sets - an element, a layer, the camera, the track of a callback binding - or <c>null</c> for a plain time callback.</summary>
    public object Target { get; }

    /// <summary>The property it drives; or <c>null</c> for a binding made with a callback.</summary>
    public AnimatedProperty? Property { get; }

    /// <summary>
    /// How the binding combines with other bindings of the same property:
    /// <see cref="BindingMode.Replace"/> (it sets the value) for every binding made without a
    /// mode, or <see cref="BindingMode.Add"/> / <see cref="BindingMode.Multiply"/>.
    /// </summary>
    public BindingMode Mode { get; }

    //For an Add or Multiply binding: the components of its target it combines into, restored to
    //  their resting values at the start of every DrawingAnimations.Apply
    internal object[] RestKeys { get; set; }

    /// <summary>Sets the bound value for a time (what <see cref="DrawingAnimations.Apply(double)"/> does for every binding).</summary>
    /// <param name="seconds">The time.</param>
    public void Apply(double seconds) => _apply(seconds);
}
