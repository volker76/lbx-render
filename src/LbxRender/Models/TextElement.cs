namespace LbxRender.Models;

public class TextElement : LbxElement
{
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 12f;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikeout { get; set; }
    public string HorizontalAlignment { get; set; } = "LEFT";
    public string VerticalAlignment { get; set; } = "TOP";
    public string Color { get; set; } = "#000000";
    public string TextEffect { get; set; } = "NOEFFECT";
    public string TextControl { get; set; } = "FREE";
    public bool Shrink { get; set; }

    public List<TextSpan> Spans { get; } = [];
}

public class TextSpan
{
    public int CharLength { get; set; }
    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 12f;
    public int Weight { get; set; } = 400;
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikeout { get; set; }
    public string Color { get; set; } = "#000000";
    public string Effect { get; set; } = "NOEFFECT";
}
