namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// How a binding's value combines with the other bindings of the same property
/// (<see cref="DrawingAnimations"/>). Every apply first runs the <see cref="Replace"/> bindings
/// (and every callback binding) in the order they were bound, then every <see cref="Multiply"/>
/// binding, then every <see cref="Add"/> binding - so the result is
/// <c>(base x every factor) + every addition</c> whatever order the bindings were made in.
/// </summary>
public enum BindingMode
{
    /// <summary>The binding sets the property; a later replace binding of the same property wins (the default, and what every binding without a mode does).</summary>
    Replace = 0,

    /// <summary>
    /// The binding's value is ADDED to the property after every replace and multiply binding has
    /// run: a sway on top of a turn, a nod on top of a head's track, a wobble on top of a path.
    /// </summary>
    Add = 1,

    /// <summary>
    /// The property is MULTIPLIED by the binding's value after every replace binding has run (and
    /// before the additions): a pulse on top of a scale, a flicker on top of a fade.
    /// </summary>
    Multiply = 2,
}
