namespace LbxRender.Models;

public class LbxLabel
{
    public LbxProperties Properties { get; set; } = new();
    public List<LbxElement> Elements { get; } = [];
    public Dictionary<string, byte[]> EmbeddedImages { get; } = [];
}
