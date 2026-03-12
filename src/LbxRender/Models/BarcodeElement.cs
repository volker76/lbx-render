namespace LbxRender.Models;

public class BarcodeElement : LbxElement
{
    public string Protocol { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public float BarWidth { get; set; }
    public string BarRatio { get; set; } = "1:3";
    public bool HumanReadable { get; set; }
    public string HumanReadableAlignment { get; set; } = "LEFT";
    public bool CheckDigit { get; set; }

    // QR-specific
    public string? QrModel { get; set; }
    public string? QrEccLevel { get; set; }
    public float QrCellSize { get; set; }

    // DataMatrix-specific
    public string? DmModel { get; set; }
    public float DmCellSize { get; set; }
}
