using System.IO.Compression;
using LbxRender;
using LbxRender.Models;
using LbxRender.Rendering;
using Xunit;

namespace LbxRender.Tests;

public class RenderingTests
{
    [Fact]
    public void RenderToBytes_WithEmptyLabel_ReturnsValidPng()
    {
        var label = new LbxLabel();
        label.Properties.LabelWidthMm = 89;
        label.Properties.LabelHeightMm = 36;

        var bytes = LbxRenderer.RenderToBytes(label);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void RenderToBytes_WithTextElement_ProducesImage()
    {
        var label = new LbxLabel();
        label.Properties.LabelWidthMm = 89;
        label.Properties.LabelHeightMm = 36;
        label.Elements.Add(new TextElement
        {
            X = 5, Y = 5,
            Width = 70, Height = 10,
            Text = "Test Label",
            FontSize = 14
        });

        var bytes = LbxRenderer.RenderToBytes(label);

        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void RenderToBitmap_RespectsDpi()
    {
        var label = new LbxLabel();
        label.Properties.LabelWidthMm = 100;
        label.Properties.LabelHeightMm = 50;

        using var lowDpi = LbxRenderer.RenderToBitmap(label, new RenderOptions { Dpi = 72 });
        using var highDpi = LbxRenderer.RenderToBitmap(label, new RenderOptions { Dpi = 300 });

        Assert.True(highDpi.Width > lowDpi.Width);
        Assert.True(highDpi.Height > lowDpi.Height);
    }

    [Fact]
    public void RenderToBytes_AsJpeg_ReturnsValidJpeg()
    {
        var label = new LbxLabel();
        label.Properties.LabelWidthMm = 50;
        label.Properties.LabelHeightMm = 25;

        var bytes = LbxRenderer.RenderToBytes(label, new RenderOptions { Format = Rendering.ImageFormat.Jpeg });

        Assert.NotNull(bytes);
        // JPEG magic bytes: FF D8 FF
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void RenderToFile_CreatesOutputFile()
    {
        var label = new LbxLabel();
        label.Properties.LabelWidthMm = 50;
        label.Properties.LabelHeightMm = 25;

        var tempPath = Path.Combine(Path.GetTempPath(), $"lbxtest_{Guid.NewGuid()}.png");
        try
        {
            LbxRenderer.RenderToFile(label, tempPath);
            Assert.True(File.Exists(tempPath));
            Assert.True(new FileInfo(tempPath).Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
