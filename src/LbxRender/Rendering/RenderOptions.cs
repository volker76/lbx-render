namespace LbxRender.Rendering;

public class RenderOptions
{
    public int Dpi { get; set; } = 300;
    public float Scale { get; set; } = 1.0f;
    public ImageFormat Format { get; set; } = ImageFormat.Png;
    public int JpegQuality { get; set; } = 90;
    public string BackgroundColor { get; set; } = "#FFFFFF";
}
