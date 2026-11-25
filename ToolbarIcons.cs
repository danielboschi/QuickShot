using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QuickShot;

/// <summary>Ícones vetoriais da barra, desenhados em uma grade de 24 × 24 com o mesmo traço.</summary>
internal static class ToolbarIcons
{
    public static readonly Geometry Move = Parse("M12,3 V21 M3,12 H21 M9,6 L12,3 L15,6 M9,18 L12,21 L15,18 M6,9 L3,12 L6,15 M18,9 L21,12 L18,15");
    public static readonly Geometry Redact = Parse("M3,9 H21 V15 H3 Z M4,4.5 H15 M4,19.5 H18");
    public static readonly Geometry Copy = Parse("M10,9 H18 A2,2 0 0 1 20,11 V18 A2,2 0 0 1 18,20 H10 A2,2 0 0 1 8,18 V11 A2,2 0 0 1 10,9 Z M16,9 V6 A2,2 0 0 0 14,4 H6 A2,2 0 0 0 4,6 V14 A2,2 0 0 0 6,16 H8");
    public static readonly Geometry Close = Parse("M6,6 L18,18 M18,6 L6,18");
    public static readonly Geometry Pen = Parse("M4,20 L5.1,15.6 L15.9,4.8 A2.1,2.1 0 0 1 18.9,4.8 L19.2,5.1 A2.1,2.1 0 0 1 19.2,8.1 L8.4,18.9 Z M13.8,6.9 L17.1,10.2");
    public static readonly Geometry Line = Parse("M5,19 L19,5");
    public static readonly Geometry Arrow = Parse("M5,19 L19,5 M10,5 H19 V14");
    public static readonly Geometry Rectangle = Parse("M6,5 H18 A2,2 0 0 1 20,7 V17 A2,2 0 0 1 18,19 H6 A2,2 0 0 1 4,17 V7 A2,2 0 0 1 6,5 Z");
    public static readonly Geometry Ellipse = Parse("M12,5 A9,7 0 1 1 12,19 A9,7 0 1 1 12,5 Z");
    public static readonly Geometry Text = Parse("M5,8 V5 H19 V8 M12,5 V19 M9,19 H15");
    public static readonly Geometry Highlight = Parse("M9,12 L16.2,4.8 A1.6,1.6 0 0 1 18.5,4.8 L20.2,6.5 A1.6,1.6 0 0 1 20.2,8.8 L13,16 Z M9,12 L7.5,15.5 L9.5,17.5 L13,16 M7.5,15.5 L4,20 H7.8 L9.5,17.5 M14,20 H20");
    public static readonly Geometry Erase = Parse("M9.5,19.5 L4.6,14.6 A1.8,1.8 0 0 1 4.6,12 L12.6,4.1 A1.8,1.8 0 0 1 15.2,4.1 L19.9,8.8 A1.8,1.8 0 0 1 19.9,11.4 L11.8,19.5 Z M8.6,8.1 L15.9,15.4 M9.5,19.5 H20");
    public static readonly Geometry Undo = Parse("M9,14 L4,9 L9,4 M4,9 H14.5 A5.5,5.5 0 0 1 14.5,20 H11");
    public static readonly Geometry Redo = Parse("M15,14 L20,9 L15,4 M20,9 H9.5 A5.5,5.5 0 0 0 9.5,20 H13");
    public static readonly Geometry Save = Parse("M6,4 H16 L20,8 V18 A2,2 0 0 1 18,20 H6 A2,2 0 0 1 4,18 V6 A2,2 0 0 1 6,4 Z M8,4 V9 H15 V4 M7.5,20 V14 H16.5 V20");

    /// <summary>O traço acompanha a cor de texto do botão, inclusive quando ele muda de estado.</summary>
    public static FrameworkElement Create(Geometry geometry)
    {
        var path = new Path
        {
            Data = geometry, Width = 24, Height = 24, StrokeThickness = 1.9,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
        };
        var foreground = new Binding(nameof(Control.Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) };
        path.SetBinding(Shape.StrokeProperty, foreground);
        if (ReferenceEquals(geometry, Redact)) path.SetBinding(Shape.FillProperty, foreground);
        return new Viewbox { Width = 19, Height = 19, Child = path };
    }

    private static Geometry Parse(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
