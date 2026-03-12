namespace LbxRender.Models;

public class ShapeElement : LbxElement
{
    public ShapeType ShapeType { get; set; } = ShapeType.Rectangle;
    public string StrokeColor { get; set; } = "#000000";
    public float StrokeWidth { get; set; } = 1f;
    public string? FillColor { get; set; }
}

public enum ShapeType
{
    Rectangle,
    Line,
    Ellipse
}
