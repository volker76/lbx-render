namespace LbxRender.Models;

public class TextElement : LbxElement
{
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 12f;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string HorizontalAlignment { get; set; } = "left";
    public string VerticalAlignment { get; set; } = "top";
    public string Color { get; set; } = "#000000";
}
