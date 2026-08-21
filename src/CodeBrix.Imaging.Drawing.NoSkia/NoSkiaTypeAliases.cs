//This file is the single, deliberate exception to the "no global usings" convention.
//
//The CodeBrix.Imaging.Drawing.NoSkia package compiles the SAME linked source files as
//CodeBrix.Imaging.Drawing (DrawingSession, DrawingRenderer, models, shapes, extensions).
//Those sources spell the SkiaSharp type names (SKColor, SKCanvas, SKBitmap, ...). In this
//assembly there is no SkiaSharp: the global using aliases below map every SK name onto
//the managed workalike type (DrawingColor, DrawingCanvas, DrawingBitmap, ...) in the
//CodeBrix.Imaging.Drawing.NoSkia namespace. Keeping all of the aliases in this one file -
//instead of repeating ~28 alias directives at the top of every shared source file - is
//what keeps the shared sources byte-identical between the two packages.

global using SKAlphaType = CodeBrix.Imaging.Drawing.NoSkia.DrawingAlphaType;
global using SKBitmap = CodeBrix.Imaging.Drawing.NoSkia.DrawingBitmap;
global using SKCanvas = CodeBrix.Imaging.Drawing.NoSkia.DrawingCanvas;
global using SKColor = CodeBrix.Imaging.Drawing.NoSkia.DrawingColor;
global using SKColors = CodeBrix.Imaging.Drawing.NoSkia.DrawingColors;
global using SKColorType = CodeBrix.Imaging.Drawing.NoSkia.DrawingColorType;
global using SKCubicResampler = CodeBrix.Imaging.Drawing.NoSkia.DrawingCubicResampler;
global using SKData = CodeBrix.Imaging.Drawing.NoSkia.DrawingData;
global using SKEncodedImageFormat = CodeBrix.Imaging.Drawing.NoSkia.DrawingEncodedImageFormat;
global using SKFilterMode = CodeBrix.Imaging.Drawing.NoSkia.DrawingFilterMode;
global using SKImage = CodeBrix.Imaging.Drawing.NoSkia.DrawingImage;
global using SKImageInfo = CodeBrix.Imaging.Drawing.NoSkia.DrawingImageInfo;
global using SKMipmapMode = CodeBrix.Imaging.Drawing.NoSkia.DrawingMipmapMode;
global using SKPaint = CodeBrix.Imaging.Drawing.NoSkia.DrawingPaint;
global using SKPaintStyle = CodeBrix.Imaging.Drawing.NoSkia.DrawingPaintStyle;
global using SKPath = CodeBrix.Imaging.Drawing.NoSkia.DrawingPath;
global using SKPathBuilder = CodeBrix.Imaging.Drawing.NoSkia.DrawingPathBuilder;
global using SKPathFillType = CodeBrix.Imaging.Drawing.NoSkia.DrawingPathFillType;
global using SKPathVerb = CodeBrix.Imaging.Drawing.NoSkia.DrawingPathVerb;
global using SKPoint = CodeBrix.Imaging.Drawing.NoSkia.DrawingPoint;
global using SKPointI = CodeBrix.Imaging.Drawing.NoSkia.DrawingPointI;
global using SKRect = CodeBrix.Imaging.Drawing.NoSkia.DrawingRect;
global using SKSamplingOptions = CodeBrix.Imaging.Drawing.NoSkia.DrawingSamplingOptions;
global using SKSize = CodeBrix.Imaging.Drawing.NoSkia.DrawingSize;
global using SKSizeI = CodeBrix.Imaging.Drawing.NoSkia.DrawingSizeI;
global using SKStrokeCap = CodeBrix.Imaging.Drawing.NoSkia.DrawingStrokeCap;
global using SKStrokeJoin = CodeBrix.Imaging.Drawing.NoSkia.DrawingStrokeJoin;
global using SKSurface = CodeBrix.Imaging.Drawing.NoSkia.DrawingSurface;
