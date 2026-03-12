using System.Globalization;
using System.Xml.Linq;
using LbxRender.Models;

namespace LbxRender.Parsing;

internal record LabelParseResult(LbxProperties Properties, List<LbxElement> Elements);

internal static class LabelXmlReader
{
    private static readonly XNamespace PtNs = "http://schemas.brother.info/ptouch/2007/lbx/main";
    private static readonly XNamespace StyleNs = "http://schemas.brother.info/ptouch/2007/lbx/style";
    private static readonly XNamespace TextNs = "http://schemas.brother.info/ptouch/2007/lbx/text";
    private static readonly XNamespace DrawNs = "http://schemas.brother.info/ptouch/2007/lbx/draw";
    private static readonly XNamespace ImageNs = "http://schemas.brother.info/ptouch/2007/lbx/image";
    private static readonly XNamespace BarcodeNs = "http://schemas.brother.info/ptouch/2007/lbx/barcode";

    public static LabelParseResult Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var props = new LbxProperties();
        var elements = new List<LbxElement>();

        // Extract paper dimensions from style:paper in style:sheet
        var paper = doc.Descendants(StyleNs + "paper").FirstOrDefault();
        if (paper is not null)
        {
            props.LabelWidthPt = Pt(paper.Attribute("width")?.Value);
            props.LabelHeightPt = Pt(paper.Attribute("height")?.Value);
            props.MarginLeft = Pt(paper.Attribute("marginLeft")?.Value);
            props.MarginTop = Pt(paper.Attribute("marginTop")?.Value);
            props.MarginRight = Pt(paper.Attribute("marginRight")?.Value);
            props.MarginBottom = Pt(paper.Attribute("marginBottom")?.Value);
            props.Orientation = paper.Attribute("orientation")?.Value ?? "portrait";
            props.MediaType = paper.Attribute("media")?.Value;
            props.PaperColor = paper.Attribute("paperColor")?.Value;
            props.PaperInk = paper.Attribute("paperInk")?.Value;
            props.PrinterModel = paper.Attribute("printerName")?.Value;
        }

        // Parse objects from pt:objects
        var objects = doc.Descendants(PtNs + "objects").FirstOrDefault();
        if (objects is not null)
        {
            foreach (var child in objects.Elements())
            {
                var element = ParseElement(child);
                if (element is not null)
                    elements.Add(element);
            }
        }

        return new LabelParseResult(props, elements);
    }

    private static LbxElement? ParseElement(XElement obj)
    {
        var localName = obj.Name.LocalName;
        var ns = obj.Name.Namespace;

        LbxElement? element = null;

        if (ns == TextNs && localName == "text")
            element = ParseTextElement(obj);
        else if (ns == DrawNs && localName == "rect")
            element = ParseRectElement(obj);
        else if (ns == DrawNs && localName == "poly")
            element = ParsePolyElement(obj);
        else if (ns == DrawNs && localName == "frame")
            element = ParseFrameElement(obj);
        else if (ns == BarcodeNs && localName == "barcode")
            element = ParseBarcodeElement(obj);
        else if (ns == ImageNs && localName == "clipart")
            element = ParseClipartElement(obj);
        else if (ns == ImageNs && localName == "image")
            element = ParseImageElement(obj);
        else if (ns == ImageNs && localName == "picture")
            element = ParsePictureElement(obj);

        if (element is not null)
        {
            var objectStyle = obj.Element(PtNs + "objectStyle");
            if (objectStyle is not null)
                ApplyObjectStyle(element, objectStyle);
        }

        return element;
    }

    private static TextElement ParseTextElement(XElement obj)
    {
        var te = new TextElement();

        // Text content is in pt:data
        var dataEl = obj.Element(PtNs + "data");
        te.Text = dataEl?.Value ?? string.Empty;

        // Default font from text:ptFontInfo (top-level, before stringItems)
        var fontInfo = obj.Element(TextNs + "ptFontInfo");
        if (fontInfo is not null)
            ApplyFontInfo(te, fontInfo);

        // Text alignment
        var align = obj.Element(TextNs + "textAlign");
        if (align is not null)
        {
            te.HorizontalAlignment = align.Attribute("horizontalAlignment")?.Value ?? "LEFT";
            te.VerticalAlignment = align.Attribute("verticalAlignment")?.Value ?? "TOP";
        }

        // Text control
        var control = obj.Element(TextNs + "textControl");
        if (control is not null)
            te.TextControl = control.Attribute("control")?.Value ?? "FREE";

        // Parse spans (text:stringItem elements)
        foreach (var stringItem in obj.Elements(TextNs + "stringItem"))
        {
            var span = new TextSpan();
            var charLenAttr = stringItem.Attribute("charLen")?.Value;
            if (int.TryParse(charLenAttr, out var charLen))
                span.CharLength = charLen;

            var spanFontInfo = stringItem.Element(TextNs + "ptFontInfo");
            if (spanFontInfo is not null)
                ApplyFontInfoToSpan(span, spanFontInfo);

            te.Spans.Add(span);
        }

        return te;
    }

    private static void ApplyFontInfo(TextElement te, XElement fontInfo)
    {
        var logFont = fontInfo.Element(TextNs + "logFont");
        if (logFont is not null)
        {
            te.FontFamily = logFont.Attribute("name")?.Value ?? te.FontFamily;
            te.Italic = logFont.Attribute("italic")?.Value == "true";
            var weightStr = logFont.Attribute("weight")?.Value;
            if (int.TryParse(weightStr, out var weight))
                te.Bold = weight >= 700;
        }

        var fontExt = fontInfo.Element(TextNs + "fontExt");
        if (fontExt is not null)
        {
            te.FontSize = Pt(fontExt.Attribute("size")?.Value);
            if (te.FontSize <= 0)
                te.FontSize = 12f;
            te.Color = fontExt.Attribute("textColor")?.Value ?? te.Color;
            te.TextEffect = fontExt.Attribute("effect")?.Value ?? "NOEFFECT";
            te.Underline = fontExt.Attribute("underline")?.Value != "0"
                           && fontExt.Attribute("underline")?.Value is not null
                           && fontExt.Attribute("underline")?.Value != "0";
            te.Underline = fontExt.Attribute("underline")?.Value is not null and not "0";
            te.Strikeout = fontExt.Attribute("strikeout")?.Value is not null and not "0";
        }
    }

    private static void ApplyFontInfoToSpan(TextSpan span, XElement fontInfo)
    {
        var logFont = fontInfo.Element(TextNs + "logFont");
        if (logFont is not null)
        {
            span.FontFamily = logFont.Attribute("name")?.Value ?? span.FontFamily;
            span.Italic = logFont.Attribute("italic")?.Value == "true";
            var weightStr = logFont.Attribute("weight")?.Value;
            if (int.TryParse(weightStr, out var weight))
                span.Weight = weight;
        }

        var fontExt = fontInfo.Element(TextNs + "fontExt");
        if (fontExt is not null)
        {
            span.FontSize = Pt(fontExt.Attribute("size")?.Value);
            span.Color = fontExt.Attribute("textColor")?.Value ?? span.Color;
            span.Effect = fontExt.Attribute("effect")?.Value ?? "NOEFFECT";
            span.Underline = fontExt.Attribute("underline")?.Value is not null and not "0";
            span.Strikeout = fontExt.Attribute("strikeout")?.Value is not null and not "0";
        }
    }

    private static ShapeElement ParseRectElement(XElement obj)
    {
        var se = new ShapeElement();

        var rectStyle = obj.Element(DrawNs + "rectStyle");
        if (rectStyle is not null)
        {
            var shape = rectStyle.Attribute("shape")?.Value ?? "RECTANGLE";
            se.ShapeType = shape switch
            {
                "ROUNDRECTANGLE" => ShapeType.RoundedRectangle,
                "ELLIPSE" => ShapeType.Ellipse,
                _ => ShapeType.Rectangle
            };
            se.RoundnessX = Pt(rectStyle.Attribute("roundnessX")?.Value);
            se.RoundnessY = Pt(rectStyle.Attribute("roundnessY")?.Value);
        }

        return se;
    }

    private static ShapeElement ParsePolyElement(XElement obj)
    {
        var se = new ShapeElement();

        var polyStyle = obj.Element(DrawNs + "polyStyle");
        if (polyStyle is not null)
        {
            var shape = polyStyle.Attribute("shape")?.Value ?? "POLYGON";
            se.ShapeType = shape switch
            {
                "LINE" => ShapeType.Line,
                "POLYLINE" => ShapeType.Polyline,
                _ => ShapeType.Polygon
            };
            se.ArrowBegin = polyStyle.Attribute("arrowBegin")?.Value ?? "SQUARE";
            se.ArrowEnd = polyStyle.Attribute("arrowEnd")?.Value ?? "SQUARE";

            // Parse points
            var pointsEl = polyStyle.Element(DrawNs + "polyLinePoints");
            if (pointsEl is not null)
            {
                var pointsStr = pointsEl.Attribute("points")?.Value;
                if (!string.IsNullOrEmpty(pointsStr))
                    ParsePoints(se, pointsStr);
            }
        }

        return se;
    }

    private static void ParsePoints(ShapeElement se, string pointsStr)
    {
        // Format: "x1pt,y1pt x2pt,y2pt ..."
        var pairs = pointsStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var coords = pair.Split(',');
            if (coords.Length == 2)
            {
                var x = Pt(coords[0]);
                var y = Pt(coords[1]);
                se.Points.Add((x, y));
            }
        }
    }

    private static FrameElement ParseFrameElement(XElement obj)
    {
        var fe = new FrameElement();

        var frameStyle = obj.Element(DrawNs + "frameStyle");
        if (frameStyle is not null)
        {
            fe.Category = frameStyle.Attribute("category")?.Value ?? "SIMPLE";
            fe.Style = frameStyle.Attribute("style")?.Value ?? "0";
            fe.StretchCenter = frameStyle.Attribute("stretchCenter")?.Value == "true";
        }

        return fe;
    }

    private static BarcodeElement ParseBarcodeElement(XElement obj)
    {
        var be = new BarcodeElement();

        // Protocol and settings from barcode:barcodeStyle
        var bcStyle = obj.Element(BarcodeNs + "barcodeStyle");
        if (bcStyle is not null)
        {
            be.Protocol = bcStyle.Attribute("protocol")?.Value ?? string.Empty;
            be.BarWidth = Pt(bcStyle.Attribute("barWidth")?.Value);
            be.BarRatio = bcStyle.Attribute("barRatio")?.Value ?? "1:3";
            be.HumanReadable = bcStyle.Attribute("humanReadable")?.Value == "true";
            be.HumanReadableAlignment = bcStyle.Attribute("humanReadableAlignment")?.Value ?? "LEFT";
            be.CheckDigit = bcStyle.Attribute("checkDigit")?.Value == "true";
        }

        // QR-specific
        var qrStyle = obj.Element(BarcodeNs + "qrcodeStyle");
        if (qrStyle is not null)
        {
            be.QrModel = qrStyle.Attribute("model")?.Value;
            be.QrEccLevel = qrStyle.Attribute("eccLevel")?.Value;
            be.QrCellSize = Pt(qrStyle.Attribute("cellSize")?.Value);
        }

        // DataMatrix-specific
        var dmStyle = obj.Element(BarcodeNs + "datamatrixStyle");
        if (dmStyle is not null)
        {
            be.DmModel = dmStyle.Attribute("model")?.Value;
            be.DmCellSize = Pt(dmStyle.Attribute("cellSize")?.Value);
        }

        // Data from pt:data
        var dataEl = obj.Element(PtNs + "data");
        be.Data = dataEl?.Value ?? string.Empty;

        return be;
    }

    private static ImageElement ParseClipartElement(XElement obj)
    {
        var ie = new ImageElement { ImageType = ImageType.Clipart };

        var clipartStyle = obj.Element(ImageNs + "clipartStyle");
        if (clipartStyle is not null)
        {
            ie.ClipartOriginalName = clipartStyle.Attribute("originalName")?.Value;
            var cat = clipartStyle.Element(ImageNs + "category");
            ie.ClipartCategory = cat?.Attribute("categoryName")?.Value;
        }

        return ie;
    }

    private static ImageElement ParseImageElement(XElement obj)
    {
        var ie = new ImageElement { ImageType = ImageType.EmbeddedBitmap };

        var imageStyle = obj.Element(ImageNs + "imageStyle");
        if (imageStyle is not null)
            ie.FileName = imageStyle.Attribute("fileName")?.Value ?? string.Empty;

        return ie;
    }

    private static ImageElement ParsePictureElement(XElement obj)
    {
        var ie = new ImageElement { ImageType = ImageType.Picture };

        var pictureStyle = obj.Element(ImageNs + "pictureStyle");
        if (pictureStyle is not null)
        {
            ie.PictureCategory = pictureStyle.Attribute("category")?.Value;
            ie.PictureValue = pictureStyle.Attribute("value")?.Value;
        }

        return ie;
    }

    private static void ApplyObjectStyle(LbxElement element, XElement objectStyle)
    {
        element.X = Pt(objectStyle.Attribute("x")?.Value);
        element.Y = Pt(objectStyle.Attribute("y")?.Value);
        element.Width = Pt(objectStyle.Attribute("width")?.Value);
        element.Height = Pt(objectStyle.Attribute("height")?.Value);
        element.Rotation = Pt(objectStyle.Attribute("angle")?.Value);
        element.BackColor = objectStyle.Attribute("backColor")?.Value;

        // Pen
        var pen = objectStyle.Element(PtNs + "pen");
        if (pen is not null)
        {
            element.PenStyle = pen.Attribute("style")?.Value ?? "NULL";
            element.PenColor = pen.Attribute("color")?.Value ?? "#000000";
            element.PenWidthX = Pt(pen.Attribute("widthX")?.Value);
            element.PenWidthY = Pt(pen.Attribute("widthY")?.Value);
        }

        // Brush
        var brush = objectStyle.Element(PtNs + "brush");
        if (brush is not null)
        {
            element.BrushStyle = brush.Attribute("style")?.Value ?? "NULL";
            element.BrushColor = brush.Attribute("color")?.Value ?? "#000000";
            if (int.TryParse(brush.Attribute("id")?.Value, out var brushId))
                element.BrushPatternId = brushId;
        }

        // Object name from pt:expanded
        var expanded = objectStyle.Element(PtNs + "expanded");
        if (expanded is not null)
            element.ObjectName = expanded.Attribute("objectName")?.Value;
    }

    private static float Pt(string? value) => PtValueParser.Parse(value);
}
