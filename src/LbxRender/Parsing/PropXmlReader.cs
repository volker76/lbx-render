using System.Xml.Linq;
using LbxRender.Models;

namespace LbxRender.Parsing;

internal static class PropXmlReader
{
    private static readonly XNamespace PtNs = "http://schemas.brother.info/ptouch/2007/lbx/main";
    private static readonly XNamespace StyleNs = "http://schemas.brother.info/ptouch/2007/lbx/style";

    public static LbxProperties Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var props = new LbxProperties();

        var paper = doc.Descendants(StyleNs + "paper").FirstOrDefault();
        if (paper is not null)
        {
            if (float.TryParse(paper.Attribute("width")?.Value, out var w))
                props.LabelWidthMm = PtToMm(w);
            if (float.TryParse(paper.Attribute("height")?.Value, out var h))
                props.LabelHeightMm = PtToMm(h);
        }

        var orientation = doc.Descendants(StyleNs + "orientation").FirstOrDefault();
        if (orientation?.Value is { } orient)
            props.Orientation = orient;

        var media = doc.Descendants(PtNs + "media").FirstOrDefault();
        props.MediaType = media?.Attribute("type")?.Value;

        return props;
    }

    private static float PtToMm(float pt) => pt * 25.4f / 72f;
}
