using System.IO.Compression;
using LbxRender;
using LbxRender.Models;
using Xunit;

namespace LbxRender.Tests;

public class ParsingTests
{
    [Fact]
    public void Open_WithValidLbxStream_ReturnsLabel()
    {
        using var stream = CreateMinimalLbx();
        var label = LbxFile.Open(stream);

        Assert.NotNull(label);
        Assert.NotNull(label.Properties);
    }

    [Fact]
    public void Open_WithLabelXml_ParsesTextElement()
    {
        using var stream = CreateLbxWithText("Hello World");
        var label = LbxFile.Open(stream);

        var textElement = Assert.Single(label.Elements.OfType<TextElement>());
        Assert.Equal("Hello World", textElement.Text);
    }

    [Fact]
    public void Open_WithPropXml_ParsesProperties()
    {
        using var stream = CreateLbxWithProperties(width: 252, height: 144);
        var label = LbxFile.Open(stream);

        Assert.True(label.Properties.LabelWidthMm > 0);
        Assert.True(label.Properties.LabelHeightMm > 0);
    }

    [Fact]
    public void Open_EmptyLbx_ReturnsEmptyLabel()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // empty archive
        }
        ms.Position = 0;

        var label = LbxFile.Open(ms);

        Assert.Empty(label.Elements);
    }

    private static MemoryStream CreateMinimalLbx()
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "label.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <pt:body xmlns:pt="http://schemas.brother.info/ptouch/2007/lbx/main" />
                """);
            AddEntry(archive, "prop.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <pt:property xmlns:pt="http://schemas.brother.info/ptouch/2007/lbx/main" />
                """);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateLbxWithText(string text)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "label.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <pt:body xmlns:pt="http://schemas.brother.info/ptouch/2007/lbx/main"
                         xmlns:text="http://schemas.brother.info/ptouch/2007/lbx/text"
                         xmlns:style="http://schemas.brother.info/ptouch/2007/lbx/style">
                  <pt:object>
                    <pt:objectStyle x="10" y="5" width="50" height="10" />
                    <text:text>
                      <text:span>{text}</text:span>
                    </text:text>
                  </pt:object>
                </pt:body>
                """);
            AddEntry(archive, "prop.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <pt:property xmlns:pt="http://schemas.brother.info/ptouch/2007/lbx/main" />
                """);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateLbxWithProperties(float width, float height)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "label.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <pt:body xmlns:pt="http://schemas.brother.info/ptouch/2007/lbx/main" />
                """);
            AddEntry(archive, "prop.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <pt:property xmlns:pt="http://schemas.brother.info/ptouch/2007/lbx/main"
                             xmlns:style="http://schemas.brother.info/ptouch/2007/lbx/style">
                  <style:paper width="{width}" height="{height}" />
                </pt:property>
                """);
        }
        ms.Position = 0;
        return ms;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
