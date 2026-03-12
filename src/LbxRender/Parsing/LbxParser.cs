using System.IO.Compression;
using LbxRender.Models;

namespace LbxRender.Parsing;

internal static class LbxParser
{
    public static LbxLabel Parse(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var label = new LbxLabel();

        // Parse label.xml first — it contains both paper dimensions and elements
        var labelEntry = archive.GetEntry("label.xml");
        if (labelEntry is not null)
        {
            using var labelStream = labelEntry.Open();
            var result = LabelXmlReader.Parse(labelStream);

            // Apply paper dimensions from label.xml
            label.Properties.LabelWidthPt = result.Properties.LabelWidthPt;
            label.Properties.LabelHeightPt = result.Properties.LabelHeightPt;
            label.Properties.MarginLeft = result.Properties.MarginLeft;
            label.Properties.MarginTop = result.Properties.MarginTop;
            label.Properties.MarginRight = result.Properties.MarginRight;
            label.Properties.MarginBottom = result.Properties.MarginBottom;
            label.Properties.Orientation = result.Properties.Orientation;
            label.Properties.MediaType = result.Properties.MediaType;
            label.Properties.PaperColor = result.Properties.PaperColor;
            label.Properties.PaperInk = result.Properties.PaperInk;
            label.Properties.PrinterModel = result.Properties.PrinterModel;

            foreach (var el in result.Elements)
                label.Elements.Add(el);
        }

        // Parse prop.xml — metadata only (title, creator)
        var propEntry = archive.GetEntry("prop.xml");
        if (propEntry is not null)
        {
            using var propStream = propEntry.Open();
            var metaProps = PropXmlReader.Parse(propStream);
            label.Properties.Title = metaProps.Title;
            label.Properties.Creator = metaProps.Creator;
        }

        // Extract embedded images
        foreach (var entry in archive.Entries)
        {
            var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (ext is ".bmp" or ".tif" or ".tiff" or ".png" or ".jpg" or ".jpeg")
            {
                using var imgStream = entry.Open();
                using var ms = new MemoryStream();
                imgStream.CopyTo(ms);
                label.EmbeddedImages[entry.FullName] = ms.ToArray();
            }
        }

        // Link image elements to their data
        foreach (var el in label.Elements.OfType<ImageElement>())
        {
            if (!string.IsNullOrEmpty(el.FileName) && label.EmbeddedImages.TryGetValue(el.FileName, out var data))
                el.ImageData = data;
        }

        return label;
    }
}
