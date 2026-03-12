using LbxRender.Models;
using LbxRender.Parsing;
using LbxRender.Rendering;

namespace LbxRender;

public static class LbxFile
{
    public static LbxLabel Open(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Open(stream);
    }

    public static LbxLabel Open(Stream stream)
    {
        return LbxParser.Parse(stream);
    }

    public static byte[] RenderToBytes(string path, RenderOptions? options = null)
    {
        var label = Open(path);
        return LbxRenderer.RenderToBytes(label, options);
    }

    public static void RenderToFile(string lbxPath, string outputPath, RenderOptions? options = null)
    {
        var label = Open(lbxPath);
        LbxRenderer.RenderToFile(label, outputPath, options);
    }
}
