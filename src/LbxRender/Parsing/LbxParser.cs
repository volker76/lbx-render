using System.IO.Compression;
using LbxRender.Models;

namespace LbxRender.Parsing;

internal static class LbxParser
{
    public static LbxLabel Parse(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var label = new LbxLabel();

        // Parse prop.xml
        var propEntry = archive.GetEntry("prop.xml");
        if (propEntry is not null)
        {
            using var propStream = propEntry.Open();
            label.Properties = PropXmlReader.Parse(propStream);
        }

        // Parse label.xml
        var labelEntry = archive.GetEntry("label.xml");
        if (labelEntry is not null)
        {
            using var labelStream = labelEntry.Open();
            var elements = LabelXmlReader.Parse(labelStream);
            foreach (var el in elements)
                label.Elements.Add(el);
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
