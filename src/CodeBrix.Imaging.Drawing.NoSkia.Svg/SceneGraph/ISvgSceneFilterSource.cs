using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

internal interface ISvgSceneFilterSource
{
    SKPicture SourceGraphic(SKRect? clip);
    SKPicture BackgroundImage(SKRect? clip);
    SKPicture FillPaint(SKRect? clip);
    SKPicture StrokePaint(SKRect? clip);
}
