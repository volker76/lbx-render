namespace LbxRender.Models;

/// <summary>
/// Base class for all label elements. Coordinates and sizes are in points.
/// </summary>
public abstract class LbxElement
{
    public string? ObjectName { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float Rotation { get; set; }
    public string? BackColor { get; set; }

    // Pen properties
    public string PenStyle { get; set; } = "NULL";
    public string PenColor { get; set; } = "#000000";
    public float PenWidthX { get; set; } = 0.5f;
    public float PenWidthY { get; set; } = 0.5f;

    // Brush properties
    public string BrushStyle { get; set; } = "NULL";
    public string BrushColor { get; set; } = "#000000";
    public int BrushPatternId { get; set; }
}
