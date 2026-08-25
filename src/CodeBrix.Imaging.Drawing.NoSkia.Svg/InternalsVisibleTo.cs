using System.Runtime.CompilerServices;

//Two test files drive the compiler's own display list directly - to prove that the
//Drawing-named picture a document loads into IS the compiled document, and to raise the
//warning kinds no markup can produce - so they reach the intermediate types this assembly
//no longer exports.
[assembly: InternalsVisibleTo("CodeBrix.Imaging.Drawing.NoSkia.Tests")]
