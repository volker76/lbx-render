using System.Xml.Linq;
using LbxRender.Models;

namespace LbxRender.Parsing;

internal static class LabelXmlReader
{
    private static readonly XNamespace PtNs = "http://schemas.brother.info/ptouch/2007/lbx/main";
    private static readonly XNamespace StyleNs = "http://schemas.brother.info/ptouch/2007/lbx/style";
    private static readonly XNamespace TextNs = "http://schemas.brother.info/ptouch/2007/lbx/text";
    private static readonly XNamespace DrawNs = "http://schemas.brother.info/ptouch/2007/lbx/draw";
    private static readonly XNamespace ImageNs = "http://schemas.brother.info/ptouch/2007/lbx/image";
    private static readonly XNamespace BarcodeNs = "http://schemas.brother.info/ptouch/2007/lbx/barcode";

    public static List<LbxElement> Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var elements = new List<LbxElement>();

        foreach (var obj in doc.Descendants(PtNs + "objectStyle").Select(os => os.Parent).Where(p => p is not null))
        {
            var element = ParseElement(obj!);
            if (element is not null)
                elements.Add(element);
        }

        return elements;
    }

    private static LbxElement? ParseElement(XElement obj)
    {
        var objectStyle = obj.Element(PtNs + "objectStyle");
        LbxElement? element = null;

        if (obj.Element(TextNs + "text") is not null || obj.Name.LocalName == "text")
            element = ParseTextElement(obj);
        else if (obj.Element(ImageNs + "image") is not null || obj.Name.LocalName == "image")
            element = ParseImageElement(obj);
        else if (obj.Element(BarcodeNs + "barcode") is not null || obj.Name.LocalName == "barcode")
            element = ParseBarcodeElement(obj);
        else if (obj.Element(DrawNs + "rectangle") is not null || obj.Element(DrawNs + "line") is not null || obj.Element(DrawNs + "ellipse") is not null)
            element = ParseShapeElement(obj);

        if (element is not null && objectStyle is not null)
            ApplyObjectStyle(element, objectStyle);

        return element;
    }

    private static TextElement ParseTextElement(XElement obj)
    {
        var te = new TextElement();

        var textNode = obj.Element(TextNs + "text") ?? obj;
        var spans = textNode.Descendants(TextNs + "span");
        te.Text = string.Join("", spans.Select(s => s.Value));
        if (string.IsNullOrEmpty(te.Text))
            te.Text = textNode.Value;

        var font = obj.Descendants(TextNs + "font").FirstOrDefault()
                   ?? obj.Descendants(StyleNs + "font").FirstOrDefault();
        if (font is not null)
        {
            te.FontFamily = font.Attribute("name")?.Value ?? te.FontFamily;
            if (float.TryParse(font.Attribute("size")?.Value, out var size))
                te.FontSize = size;
        }

        return te;
    }

    private static ImageElement ParseImageElement(XElement obj)
    {
        var ie = new ImageElement();
        var img = obj.Element(ImageNs + "image") ?? obj;
        ie.FileName = img.Attribute("src")?.Value
                      ?? img.Attribute("fileName")?.Value
                      ?? string.Empty;
        return ie;
    }

    private static BarcodeElement ParseBarcodeElement(XElement obj)
    {
        var be = new BarcodeElement();
        var bc = obj.Element(BarcodeNs + "barcode") ?? obj;
        be.Protocol = bc.Attribute("protocol")?.Value ?? string.Empty;
        be.Data = bc.Attribute("data")?.Value ?? bc.Value;
        return be;
    }

    private static ShapeElement ParseShapeElement(XElement obj)
    {
        var se = new ShapeElement();
        if (obj.Element(DrawNs + "line") is not null)
            se.ShapeType = ShapeType.Line;
        else if (obj.Element(DrawNs + "ellipse") is not null)
            se.ShapeType = ShapeType.Ellipse;
        return se;
    }

    private static void ApplyObjectStyle(LbxElement element, XElement objectStyle)
    {
        element.ObjectName = objectStyle.Attribute("name")?.Value;
        if (float.TryParse(objectStyle.Attribute("x")?.Value, out var x))
            element.X = x;
        if (float.TryParse(objectStyle.Attribute("y")?.Value, out var y))
            element.Y = y;
        if (float.TryParse(objectStyle.Attribute("width")?.Value, out var w))
            element.Width = w;
        if (float.TryParse(objectStyle.Attribute("height")?.Value, out var h))
            element.Height = h;
        if (float.TryParse(objectStyle.Attribute("rotation")?.Value, out var r))
            element.Rotation = r;
    }
}
