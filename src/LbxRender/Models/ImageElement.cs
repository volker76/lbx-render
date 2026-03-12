namespace LbxRender.Models;

public class ImageElement : LbxElement
{
    public string FileName { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
}
