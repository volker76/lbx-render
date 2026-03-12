# LbxRender

A .NET library to parse `.lbx` label files (ZIP archives produced by Brother P-touch label software) and render them as PNG, JPEG, BMP, or TIFF images.

## Features

- Parse `.lbx` files into a structured object model
- Render labels to PNG, JPEG, BMP, or TIFF at configurable DPI
- **Text**: font family, size, weight, italic, color, alignment, multi-line
- **Text spans**: per-segment font/size/weight/color within a single text element
- **Text effects**: outline, shadow, shadow light, surround
- **Text decorations**: underline, strikeout
- **Shapes**: rectangle, rounded rectangle, ellipse, line, polygon, polyline
- **Brush patterns**: GDI-style hatched fills (PATTERN) and stipple/dot patterns (DIBPATTERN)
- **Pen styles**: solid, dash, dot, dash-dot
- **Barcodes**: CODE39, CODE128, QR, DataMatrix, EAN-13, EAN-8, UPC-A, UPC-E, ITF, Codabar, PDF417 (via ZXing.Net)
- **Images**: embedded bitmaps rendered at element dimensions
- **Frames**: border elements with configurable pen styles
- Landscape/portrait orientation support

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

// Render to stream
using var stream = File.OpenWrite("output.png");
LbxRenderer.RenderToStream(label, stream);
```

## Supported Element Types

| Element | Rendering |
|---------|-----------|
| Text | Full font support, alignment, effects, spans, decorations |
| Shape | Rectangle, rounded rect, ellipse, polygon, polyline with pen/brush |
| Barcode | Real barcode images via ZXing.Net (1D and 2D codes) |
| Image | Embedded bitmaps; clipart/picture show as placeholders |
| Frame | Border rectangles with configurable pen styles |

## LBX File Format

LBX files are standard ZIP archives containing:
- `label.xml` — label layout (text, barcodes, images, shapes)
- `prop.xml` — label properties (dimensions, orientation)
- Optional embedded images (`.bmp`, `.tif`)

## Dependencies

- [SkiaSharp](https://github.com/mono/SkiaSharp) — 2D graphics rendering
- [ZXing.Net](https://github.com/micjahn/ZXing.Net) — barcode generation

## License

Apache-2.0
