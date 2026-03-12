namespace LbxRender.Models;

public class BarcodeElement : LbxElement
{
    public string Protocol { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public int MarginTop { get; set; }
    public int MarginBottom { get; set; }
}
