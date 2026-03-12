using System.Xml.Linq;
using LbxRender.Models;

namespace LbxRender.Parsing;

/// <summary>
/// Reads prop.xml which contains metadata (meta:properties namespace),
/// NOT paper dimensions (those are in label.xml).
/// </summary>
internal static class PropXmlReader
{
    private static readonly XNamespace MetaNs = "http://schemas.brother.info/ptouch/2007/lbx/meta";
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";

    public static LbxProperties Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var props = new LbxProperties();

        var title = doc.Descendants(DcNs + "title").FirstOrDefault();
        if (!string.IsNullOrEmpty(title?.Value))
            props.Title = title!.Value;

        var creator = doc.Descendants(DcNs + "creator").FirstOrDefault();
        if (!string.IsNullOrEmpty(creator?.Value))
            props.Creator = creator!.Value;

        return props;
    }
}
