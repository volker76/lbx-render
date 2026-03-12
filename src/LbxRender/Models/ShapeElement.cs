namespace LbxRender.Models;

public class ShapeElement : LbxElement
{
    public ShapeType ShapeType { get; set; } = ShapeType.Rectangle;
    public float RoundnessX { get; set; }
    public float RoundnessY { get; set; }

    /// <summary>Points for polygon/polyline shapes (in absolute pt coordinates).</summary>
    public List<(float X, float Y)> Points { get; } = [];

    public string ArrowBegin { get; set; } = "SQUARE";
    public string ArrowEnd { get; set; } = "SQUARE";
}

public enum ShapeType
{
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Line,
    Polygon,
    Polyline
}
