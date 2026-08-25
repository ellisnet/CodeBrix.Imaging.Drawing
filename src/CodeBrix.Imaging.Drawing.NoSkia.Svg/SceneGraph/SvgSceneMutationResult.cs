using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

// <summary>
// Represents the result of a scene graph mutation operation.
// </summary>
internal sealed class SvgSceneMutationResult
{
    internal SvgSceneMutationResult(bool succeeded, int compilationRootCount, int resourceCount)
    {
        Succeeded = succeeded;
        CompilationRootCount = compilationRootCount;
        ResourceCount = resourceCount;
    }

    // <summary>
    // Gets a value indicating whether the mutation succeeded.
    // </summary>
    public bool Succeeded { get; }

    // <summary>
    // Gets the number of compilation roots in the mutated scene.
    // </summary>
    public int CompilationRootCount { get; }

    // <summary>
    // Gets the number of resources in the mutated scene.
    // </summary>
    public int ResourceCount { get; }
}
