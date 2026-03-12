namespace LbxRender.Models;

public class LbxProperties
{
    /// <summary>Paper width in points.</summary>
    public float LabelWidthPt { get; set; }

    /// <summary>Paper height in points.</summary>
    public float LabelHeightPt { get; set; }

    public float MarginLeft { get; set; }
    public float MarginTop { get; set; }
    public float MarginRight { get; set; }
    public float MarginBottom { get; set; }

    public string Orientation { get; set; } = "portrait";
    public string? MediaType { get; set; }
    public string? PrinterModel { get; set; }
    public string? PaperColor { get; set; }
    public string? PaperInk { get; set; }

    // Metadata from prop.xml
    public string? Title { get; set; }
    public string? Creator { get; set; }

    /// <summary>Backward-compatible width in mm (computed from pt).</summary>
    public float LabelWidthMm
    {
        get => LabelWidthPt * 25.4f / 72f;
        set => LabelWidthPt = value * 72f / 25.4f;
    }

    /// <summary>Backward-compatible height in mm (computed from pt).</summary>
    public float LabelHeightMm
    {
        get => LabelHeightPt * 25.4f / 72f;
        set => LabelHeightPt = value * 72f / 25.4f;
    }
}
