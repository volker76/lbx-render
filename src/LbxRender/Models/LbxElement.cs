namespace LbxRender.Models;

/// <summary>
/// Base class for all label elements.
/// </summary>
public abstract class LbxElement
{
    public string? ObjectName { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float Rotation { get; set; }
}
