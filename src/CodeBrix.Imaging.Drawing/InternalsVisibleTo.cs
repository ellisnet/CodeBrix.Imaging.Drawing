using System.Runtime.CompilerServices;

#if NOSKIA
[assembly: InternalsVisibleTo("CodeBrix.Imaging.Drawing.NoSkia.Tests")]
#else
[assembly: InternalsVisibleTo("CodeBrix.Imaging.Drawing.Tests")]
#endif
