namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The standard named colors. Values follow the HTML/.NET named-color palette (for example
/// <see cref="Green"/> is <c>#FF008000</c>, not <c>#FF00FF00</c> - that is <see cref="Lime"/>).
/// </summary>
public static class DrawingColors
{
    /// <summary>Fully transparent (#00000000).</summary>
    public static readonly DrawingColor Transparent = new DrawingColor(0x00000000);

    /// <summary>Opaque black (#FF000000).</summary>
    public static readonly DrawingColor Black = new DrawingColor(0xFF000000);

    /// <summary>Opaque white (#FFFFFFFF).</summary>
    public static readonly DrawingColor White = new DrawingColor(0xFFFFFFFF);

    /// <summary>Opaque red (#FFFF0000).</summary>
    public static readonly DrawingColor Red = new DrawingColor(0xFFFF0000);

    /// <summary>Opaque green (#FF008000).</summary>
    public static readonly DrawingColor Green = new DrawingColor(0xFF008000);

    /// <summary>Opaque blue (#FF0000FF).</summary>
    public static readonly DrawingColor Blue = new DrawingColor(0xFF0000FF);

    /// <summary>Opaque yellow (#FFFFFF00).</summary>
    public static readonly DrawingColor Yellow = new DrawingColor(0xFFFFFF00);

    /// <summary>Opaque cyan (#FF00FFFF).</summary>
    public static readonly DrawingColor Cyan = new DrawingColor(0xFF00FFFF);

    /// <summary>Opaque magenta (#FFFF00FF).</summary>
    public static readonly DrawingColor Magenta = new DrawingColor(0xFFFF00FF);

    /// <summary>Opaque lime (#FF00FF00).</summary>
    public static readonly DrawingColor Lime = new DrawingColor(0xFF00FF00);

    /// <summary>Opaque gray (#FF808080).</summary>
    public static readonly DrawingColor Gray = new DrawingColor(0xFF808080);

    /// <summary>Opaque light gray (#FFD3D3D3).</summary>
    public static readonly DrawingColor LightGray = new DrawingColor(0xFFD3D3D3);

    /// <summary>Opaque dark gray (#FFA9A9A9).</summary>
    public static readonly DrawingColor DarkGray = new DrawingColor(0xFFA9A9A9);

    /// <summary>Opaque silver (#FFC0C0C0).</summary>
    public static readonly DrawingColor Silver = new DrawingColor(0xFFC0C0C0);

    /// <summary>Opaque orange (#FFFFA500).</summary>
    public static readonly DrawingColor Orange = new DrawingColor(0xFFFFA500);

    /// <summary>Opaque purple (#FF800080).</summary>
    public static readonly DrawingColor Purple = new DrawingColor(0xFF800080);

    /// <summary>Opaque brown (#FFA52A2A).</summary>
    public static readonly DrawingColor Brown = new DrawingColor(0xFFA52A2A);

    /// <summary>Opaque pink (#FFFFC0CB).</summary>
    public static readonly DrawingColor Pink = new DrawingColor(0xFFFFC0CB);

    /// <summary>Opaque navy (#FF000080).</summary>
    public static readonly DrawingColor Navy = new DrawingColor(0xFF000080);

    /// <summary>Opaque teal (#FF008080).</summary>
    public static readonly DrawingColor Teal = new DrawingColor(0xFF008080);

    /// <summary>Opaque olive (#FF808000).</summary>
    public static readonly DrawingColor Olive = new DrawingColor(0xFF808000);

    /// <summary>Opaque maroon (#FF800000).</summary>
    public static readonly DrawingColor Maroon = new DrawingColor(0xFF800000);
}
