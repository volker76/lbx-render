namespace LbxRender.Models;

public class FrameElement : LbxElement
{
    public string Category { get; set; } = "SIMPLE";
    public string Style { get; set; } = "0";
    public bool StretchCenter { get; set; }
}
