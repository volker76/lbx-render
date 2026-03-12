namespace LbxRender.Models;

public class ImageElement : LbxElement
{
    public ImageType ImageType { get; set; } = ImageType.EmbeddedBitmap;
    public string FileName { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }

    // Clipart properties
    public string? ClipartOriginalName { get; set; }
    public string? ClipartCategory { get; set; }

    // Picture properties
    public string? PictureCategory { get; set; }
    public string? PictureValue { get; set; }
}

public enum ImageType
{
    EmbeddedBitmap,
    Clipart,
    Picture
}
