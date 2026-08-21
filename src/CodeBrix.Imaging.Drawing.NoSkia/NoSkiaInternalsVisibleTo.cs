using System.Runtime.CompilerServices;

//The SVG rendering assembly (shipped inside this same NuGet package) reaches the raster
//internals for its filter-primitive evaluation.
[assembly: InternalsVisibleTo("CodeBrix.Imaging.Drawing.NoSkia.Svg")]
