namespace LbxRender.Models;

public class LbxProperties
{
    public float LabelWidthMm { get; set; }
    public float LabelHeightMm { get; set; }
    public string Orientation { get; set; } = "portrait";
    public string? MediaType { get; set; }
    public string? PrinterModel { get; set; }
}
