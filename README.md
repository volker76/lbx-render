# LbxRender

A .NET library to parse `.lbx` label files (ZIP archives produced by B****** P-***** label software) and render them as PNG, JPEG, BMP, or TIFF images.

## Installation

```bash
dotnet add package LbxRender
```

## Usage

```csharp
using LbxRender;
using LbxRender.Rendering;

// Parse and render in one step
LbxFile.RenderToFile("label.lbx", "output.png");

// Or with options
LbxFile.RenderToFile("label.lbx", "output.jpg", new RenderOptions
{
    Format = ImageFormat.Jpeg,
    Dpi = 300,
    JpegQuality = 95
});

// Parse separately for inspection
var label = LbxFile.Open("label.lbx");
Console.WriteLine($"Label size: {label.Properties.LabelWidthMm} x {label.Properties.LabelHeightMm} mm");
Console.WriteLine($"Elements: {label.Elements.Count}");

// Render to byte array
byte[] pngBytes = LbxRenderer.RenderToBytes(label);
```

## LBX File Format

LBX files are standard ZIP archives containing:
- `label.xml` — label layout (text, barcodes, images, shapes)
- `prop.xml` — label properties (dimensions, orientation)
- Optional embedded images (`.bmp`, `.tif`)

## License

MIT
